using System.Collections.Generic;
using TheyWillDescend.Content;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Registry: spawn/destroy house views and grid overlays. Feature chrome
    /// lives on <see cref="BuildingView"/>, not here.
    /// </summary>
    public sealed class BuildingViewBoard : MonoBehaviour
    {
        [SerializeField] RadialGridGuide gridGuide;
        [SerializeField] BuildingSelection selection;
        [SerializeField] BuildingCatalogAsset catalog;
        [SerializeField] BuildingOverlay overlayPrefab;
        [SerializeField] Color zoneColor = new(0.15f, 0.75f, 1f, 0.45f);

        Transform _root;
        readonly Dictionary<int, PlacedView> _views = new();
        readonly HashSet<int> _seen = new();
        readonly List<int> _stale = new();
        Material _placedZoneMaterial;
        Material _selectedZoneMaterial;
        Color _selectedZoneColor = new(0.95f, 0.82f, 0.2f, 0.55f);
        EntityQuery _buildingQuery;
        Camera _cam;


        sealed class PlacedView
        {
            public GameObject Root;
            public GameObject Overlay;
            public BuildingView View;
            public MeshRenderer ZoneRenderer;
        }

        public void RebuildViews()
        {
            ClearViews();
            Pump();
        }

        void Awake() => EnsureReady();

        void LateUpdate() => Pump();

        public void Pump()
        {
            EnsureReady();
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;
            if (_buildingQuery == default)
                _buildingQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
            Sync(em, _buildingQuery);
        }


        public void ClearViews()
        {
            foreach (var view in _views.Values)
                DestroyPlaced(view);
            _views.Clear();
            selection?.Deselect();
        }




        void OnDisable()
        {
            ClearViews();
            if (_placedZoneMaterial == null && _selectedZoneMaterial == null)
                return;
            if (Application.isPlaying)
            {
                if (_placedZoneMaterial != null)
                    Destroy(_placedZoneMaterial);
                if (_selectedZoneMaterial != null)
                    Destroy(_selectedZoneMaterial);
            }
            else
            {
                if (_placedZoneMaterial != null)
                    DestroyImmediate(_placedZoneMaterial);
                if (_selectedZoneMaterial != null)
                    DestroyImmediate(_selectedZoneMaterial);
            }

            _placedZoneMaterial = null;
            _selectedZoneMaterial = null;
        }

        void OnDestroy()
        {
            _buildingQuery = default;
            _cam = null;
        }


        void Sync(EntityManager em, EntityQuery query)
        {
            if (query.IsEmptyIgnoreFilter)

            {
                if (_views.Count > 0)
                {
                    foreach (var view in _views.Values)
                        DestroyPlaced(view);
                    _views.Clear();
                }
                return;
            }

            var entities = query.ToEntityArray(Allocator.Temp);
            var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            _seen.Clear();
            if (_cam == null)
                _cam = Camera.main;
            for (var i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                var entity = entities[i];
                var position = PositionOf(em, entity);
                _seen.Add(building.Id);
                if (!_views.TryGetValue(building.Id, out var placed) || placed?.Root == null)
                {
                    placed = CreateHouseView(em, entity, building, position);
                }


                if (placed == null)
                    continue;

                if (placed.View != null)
                    placed.View.Sync(em, entity, _cam);
                else
                    placed.Root.transform.position = (Vector3)position;

                var selected = selection != null && building.Id == selection.SelectedBuildingId;
                if (placed.ZoneRenderer != null)
                    placed.ZoneRenderer.sharedMaterial = selected ? _selectedZoneMaterial : _placedZoneMaterial;
            }

            if (_views.Count != _seen.Count)
            {
                _stale.Clear();
                foreach (var pair in _views)
                {
                    if (!_seen.Contains(pair.Key))
                        _stale.Add(pair.Key);
                }

                for (var i = 0; i < _stale.Count; i++)
                    DestroyView(_stale[i]);
            }


            entities.Dispose();
            buildings.Dispose();
        }

        static float3 PositionOf(EntityManager em, Entity entity)
        {
            if (em.HasComponent<LocalToWorld>(entity))
                return em.GetComponentData<LocalToWorld>(entity).Position;
            if (em.HasComponent<LocalTransform>(entity))
                return em.GetComponentData<LocalTransform>(entity).Position;
            return default;
        }



        PlacedView CreateHouseView(EntityManager em, Entity entity, in Building building, float3 position)
        {
            EnsureReady();
            var prefab = ResolveStampPrefab(em, entity, building);
            if (prefab == null)
            {
                GameLog.Error($"BuildingViewBoard: no stamp prefab for {building.TypeId}.");
                return null;
            }

            if (overlayPrefab == null)
            {
                GameLog.Error("BuildingViewBoard: assign overlay prefab.");
                return null;
            }

            if (gridGuide == null || !TryGetCityCenter(out var center))
            {
                GameLog.Error("BuildingViewBoard: grid or CityGrid.Center missing.");
                return null;
            }

            EnsureMaterial();
            var house = Object.Instantiate(prefab, _root);
            house.name = $"Building_{building.Id}";
            house.transform.position = (Vector3)position;
            var view = house.GetComponent<BuildingView>();
            if (view == null)
                GameLog.Error($"BuildingViewBoard: {prefab.name} has no BuildingView.");

            var overlay = SpawnOverlay(building, center);
            var placed = new PlacedView
            {
                Root = house,
                Overlay = overlay != null ? overlay.gameObject : null,
                View = view,
                ZoneRenderer = overlay != null ? overlay.ZoneRenderer : null
            };
            _views[building.Id] = placed;
            return placed;
        }

        BuildingOverlay SpawnOverlay(in Building building, float3 center)
        {
            var footprint = new BuildingFootprint
            {
                WidthClusters = building.WidthClusters,
                DepthRadialRings = building.DepthRadialRings
            };
            var clusters = new List<(int cluster, int radial)>(32);
            var config = gridGuide.Config;
            if (!RadialFootprintMath.TryExpandClusters(
                    config, building.AnchorCluster, building.AnchorRadial, footprint, clusters))
            {
                GameLog.Warning($"Building overlay skip id={building.Id}: expand failed.");
                return null;
            }

            var overlay = Object.Instantiate(overlayPrefab, _root);
            overlay.name = $"Overlay_{building.Id}";
            if (overlay.IdTag != null)
                overlay.IdTag.Id = building.Id;

            var zoneMesh = RadialSectorMeshBuilder.BuildClusterZoneMesh(center, config, clusters);
            if (overlay.ZoneFilter != null)
                overlay.ZoneFilter.sharedMesh = zoneMesh;
            if (overlay.ZoneCollider != null)
                overlay.ZoneCollider.sharedMesh = zoneMesh;
            if (overlay.ZoneRenderer != null)
            {
                overlay.ZoneRenderer.sharedMaterial = _placedZoneMaterial;
                overlay.ZoneRenderer.shadowCastingMode = ShadowCastingMode.Off;
                overlay.ZoneRenderer.receiveShadows = false;
            }

            return overlay;
        }

        GameObject ResolveStampPrefab(EntityManager em, Entity entity, in Building building)
        {
            var source = catalog != null
                ? catalog
                : Object.FindFirstObjectByType<BuildPlacementController>()?.Catalog;
            if (source == null)
                return null;
            var typeId = em.HasComponent<Building>(entity)
                ? em.GetComponentData<Building>(entity).TypeId.ToString()
                : building.TypeId.ToString();
            return source.FindPrefab(typeId);
        }

        void DestroyView(int buildingId)
        {
            selection?.ClearIf(buildingId);
            if (!_views.TryGetValue(buildingId, out var view))
                return;
            _views.Remove(buildingId);
            DestroyPlaced(view);
        }

        static void DestroyPlaced(PlacedView view)
        {
            if (view == null)
                return;
            DestroyGo(view.Root);
            if (view.Overlay != null && view.Overlay != view.Root)
                DestroyGo(view.Overlay);
        }

        static void DestroyGo(GameObject go)
        {
            if (go == null)
                return;
            go.SetActive(false);
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }


        void EnsureMaterial()
        {
            if (_placedZoneMaterial == null)
                _placedZoneMaterial = CreateZoneMaterial("FootprintZone_Placed", zoneColor);
            if (_selectedZoneMaterial == null)
                _selectedZoneMaterial = CreateZoneMaterial("FootprintZone_Selected", _selectedZoneColor);
        }

        static Material CreateZoneMaterial(string name, Color color)
        {
            var shader =
                Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            mat.color = color;
            mat.renderQueue = (int)RenderQueue.Transparent + 60;
            return mat;
        }

        void EnsureReady()
        {
            if (_root != null)
                return;
            _root = transform;
        }



        static bool TryGetCityCenter(out float3 center)
        {
            center = default;
            if (!TryGetCityGrid(out var grid))
                return false;
            center = grid.Center;
            return true;
        }

        static bool TryGetCityGrid(out CityGrid grid)
        {
            grid = default;
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasComponent<CityGrid>(bag))
                return false;
            grid = em.GetComponentData<CityGrid>(bag);
            return grid.Ready != 0;
        }
    }
}
