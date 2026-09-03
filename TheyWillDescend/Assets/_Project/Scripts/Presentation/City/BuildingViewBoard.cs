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
        [SerializeField] HqOverlay hqOverlayPrefab;
        [SerializeField] GameObject hqVisualPrefab;
        [SerializeField] Color zoneColor = new(0.15f, 0.75f, 1f, 0.45f);


        Transform _root;
        readonly Dictionary<int, PlacedView> _views = new();
        readonly HashSet<int> _seen = new();
        Material _placedZoneMaterial;
        Material _selectedZoneMaterial;
        Color _selectedZoneColor = new(0.95f, 0.82f, 0.2f, 0.55f);
        PlacedView _hqView;

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
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
            Sync(em, query);
        }

        public void ClearViews()
        {
            foreach (var view in _views.Values)
                DestroyPlaced(view);
            _views.Clear();
            if (_hqView != null)
            {
                DestroyPlaced(_hqView);
                _hqView = null;
            }
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

        void Sync(EntityManager em, EntityQuery query)
        {
            try
            {
                SyncHqView(em);
            }
            catch (System.Exception ex)
            {
                GameLog.Error($"BuildingViewBoard: SyncHqView error: {ex.Message}");
            }


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
            var cam = Camera.main;
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
                    placed.View.Sync(em, entity, cam);
                else
                    placed.Root.transform.position = (Vector3)position;

                var selected = selection != null && building.Id == selection.SelectedBuildingId;
                if (placed.ZoneRenderer != null)
                    placed.ZoneRenderer.sharedMaterial = selected ? _selectedZoneMaterial : _placedZoneMaterial;
            }

            if (_views.Count != _seen.Count)
            {
                var stale = new List<int>();
                foreach (var pair in _views)
                {
                    if (!_seen.Contains(pair.Key))
                        stale.Add(pair.Key);
                }

                for (var i = 0; i < stale.Count; i++)
                    DestroyView(stale[i]);
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

        void SyncHqView(EntityManager em)
        {
            using var hqQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Headquarters>());
            if (hqQuery.IsEmptyIgnoreFilter)
            {
                if (_hqView != null)
                {
                    DestroyPlaced(_hqView);
                    _hqView = null;
                }
                return;
            }

            var hqEntity = hqQuery.GetSingletonEntity();
            var position = PositionOf(em, hqEntity);

            if (_hqView == null || _hqView.Root == null)
                _hqView = CreateHqView(position);

            if (_hqView != null)
            {
                _hqView.Root.transform.position = (Vector3)position;
                var selected = selection != null && selection.SelectedBuildingId == 1;
                if (_hqView.ZoneRenderer != null)
                    _hqView.ZoneRenderer.sharedMaterial = selected ? _selectedZoneMaterial : _placedZoneMaterial;
            }
        }

        PlacedView CreateHqView(float3 position)
        {
            EnsureReady();
            if (hqOverlayPrefab == null)
            {
                GameLog.Error("BuildingViewBoard: assign HqOverlay prefab.");
                return null;
            }

            EnsureMaterial();
            var overlay = Object.Instantiate(hqOverlayPrefab, _root);
            overlay.name = "Headquarters";
            overlay.transform.position = (Vector3)position;
            if (overlay.IdTag != null)
                overlay.IdTag.Id = 1;

            var visualPrefab = hqVisualPrefab;
#if UNITY_EDITOR
            if (visualPrefab == null)
            {
                visualPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Packages/RPGPP_LT/Prefabs/Buildings/Bld_closed/rpgpp_lt_building_03.prefab");
            }
#endif
            if (visualPrefab != null)
            {
                try
                {
                    var visual = Object.Instantiate(visualPrefab, overlay.transform);
                    visual.name = "Visual";
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one;

                    var meshFilter = visual.GetComponentInChildren<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        var collider = visual.GetComponent<MeshCollider>();
                        if (collider == null)
                            collider = visual.AddComponent<MeshCollider>();
                        collider.sharedMesh = meshFilter.sharedMesh;
                    }
                }
                catch (System.Exception ex)
                {
                    GameLog.Error($"BuildingViewBoard: failed to instantiate hq visual: {ex.Message}");
                }
            }




            var plaza = PlazaRadius();
            if (overlay.PlazaFilter != null)
                overlay.PlazaFilter.sharedMesh = BuildAnnulusMesh(plaza * 0.55f, plaza * 0.92f, 48, 0.06f);
            if (overlay.PlazaRenderer != null)
            {
                overlay.PlazaRenderer.sharedMaterial = _placedZoneMaterial;
                overlay.PlazaRenderer.shadowCastingMode = ShadowCastingMode.Off;
                overlay.PlazaRenderer.receiveShadows = false;
            }

            var clickRadius = plaza * 0.82f;
            var clickHeight = 18f;
            if (overlay.ClickProxy != null)
            {
                overlay.ClickProxy.transform.localPosition = Vector3.up * (clickHeight * 0.5f);
                overlay.ClickProxy.radius = clickRadius;
                overlay.ClickProxy.height = clickHeight;
                overlay.ClickProxy.direction = 1;
            }

            return new PlacedView
            {
                Root = overlay.gameObject,
                Overlay = overlay.gameObject,
                ZoneRenderer = overlay.PlazaRenderer
            };
        }


        static Mesh BuildAnnulusMesh(float inner, float outer, int segments, float y)
        {
            var verts = new Vector3[segments * 2];
            var tris = new int[segments * 12];
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var c = Mathf.Cos(angle);
                var s = Mathf.Sin(angle);
                verts[i * 2] = new Vector3(c * inner, y, s * inner);
                verts[i * 2 + 1] = new Vector3(c * outer, y, s * outer);
                var next = (i + 1) % segments;
                var i0 = i * 2;
                var i1 = i * 2 + 1;
                var i2 = next * 2;
                var i3 = next * 2 + 1;
                var t = i * 12;
                tris[t] = i0;
                tris[t + 1] = i1;
                tris[t + 2] = i2;
                tris[t + 3] = i1;
                tris[t + 4] = i3;
                tris[t + 5] = i2;
                tris[t + 6] = i0;
                tris[t + 7] = i2;
                tris[t + 8] = i1;
                tris[t + 9] = i1;
                tris[t + 10] = i2;
                tris[t + 11] = i3;
            }

            var mesh = new Mesh { name = "HqPlazaRing" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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

        static float PlazaRadius()
        {
            if (TryGetCityGrid(out var grid) && grid.Config.InnerRadius > 0.5f)
                return grid.Config.InnerRadius;
            return RadialGridConfig.Default.InnerRadius;
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
