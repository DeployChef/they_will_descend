using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Footprint overlay + world UI instances. House mesh is Entities Graphics
    /// on the Building entity. Does not assemble bars or texts with new GameObject.
    /// </summary>
    public sealed class BuildingViewBoard : MonoBehaviour
    {
        [SerializeField] RadialGridGuide gridGuide;
        [SerializeField] BuildingSelection selection;
        [SerializeField] TheyWillDescend.Simulation.Content.BuildingCatalogAsset catalog;
        [SerializeField] BuildingOverlay overlayPrefab;
        [SerializeField] HqOverlay hqOverlayPrefab;
        [SerializeField] BuildingWorldUi worldUiPrefab;
        [SerializeField] Color zoneColor = new(0.15f, 0.75f, 1f, 0.45f);
        readonly Color _constructionFill = new(0.25f, 0.85f, 0.45f, 0.95f);
        readonly Color _loadFill = new(0.95f, 0.72f, 0.18f, 0.95f);

        Transform _overlayRoot;
        readonly Dictionary<int, PlacedView> _views = new();
        readonly HashSet<int> _seen = new();
        Material _placedZoneMaterial;
        Material _selectedZoneMaterial;
        Color _selectedZoneColor = new(0.95f, 0.82f, 0.2f, 0.55f);

        sealed class PlacedView
        {
            public GameObject Root;
            public MeshRenderer ZoneRenderer;
            public BuildingWorldUi WorldUi;
            public GameObject BarRoot;
            public Image Fill;
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
            {
                if (view?.Root == null)
                    continue;
                view.Root.SetActive(false);
                Object.DestroyImmediate(view.Root);
            }

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

        void Sync(EntityManager em, EntityQuery query)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                if (_views.Count > 0)
                    ClearViews();
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
                if (!_views.TryGetValue(building.Id, out var view) || view?.Root == null)
                {
                    view = em.HasComponent<Headquarters>(entity)
                        ? CreateHqView(building, position)
                        : CreateView(em, entity, building);
                }

                if (view == null)
                    continue;

                var constructing = em.HasComponent<Construction>(entity);
                if (view.BarRoot != null)
                    view.BarRoot.SetActive(constructing || HasStaffBar(em, entity));

                if (constructing && view.Fill != null)
                {
                    var construction = em.GetComponentData<Construction>(entity);
                    view.Fill.color = _constructionFill;
                    view.Fill.fillAmount = construction.Normalized;
                }
                else if (view.Fill != null)
                {
                    var paused = em.HasComponent<Workplace>(entity)
                        && em.GetComponentData<Workplace>(entity).IsPaused;
                    var slots = em.HasComponent<BuildingType>(entity)
                        ? em.GetComponentData<BuildingType>(entity).WorkplaceSlots
                        : 0;
                    var assigned = em.HasComponent<Workplace>(entity)
                        ? em.GetComponentData<Workplace>(entity).AssignedCount
                        : 0;
                    view.Fill.color = paused
                        ? new Color(0.45f, 0.45f, 0.48f, 0.9f)
                        : _loadFill;
                    view.Fill.fillAmount = Workplace.Load01(assigned, slots);
                }

                if (view.WorldUi != null)
                {
                    var pose = view.WorldUi.transform;
                    pose.position = (Vector3)position + Vector3.up * 2.2f;
                    if (cam != null)
                        pose.rotation = Quaternion.LookRotation(pose.position - cam.transform.position);
                }

                var selected = selection != null && building.Id == selection.SelectedBuildingId;
                if (view.ZoneRenderer != null)
                    view.ZoneRenderer.sharedMaterial = selected ? _selectedZoneMaterial : _placedZoneMaterial;

                var tintCatalog = catalog != null
                    ? catalog
                    : Object.FindFirstObjectByType<BuildPlacementController>()?.Catalog;
                BuildingWorkTint.Apply(em, entity, tintCatalog);
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

        static bool HasStaffBar(EntityManager em, Entity entity)
        {
            return em.HasComponent<BuildingType>(entity)
                && em.GetComponentData<BuildingType>(entity).WorkplaceSlots > 0;
        }

        PlacedView CreateHqView(in Building building, float3 position)
        {
            EnsureReady();
            if (hqOverlayPrefab == null)
            {
                GameLog.Error("BuildingViewBoard: assign HqOverlay prefab.");
                return null;
            }

            EnsureMaterial();
            var overlay = Object.Instantiate(hqOverlayPrefab, _overlayRoot);
            overlay.name = $"Headquarters_{building.Id}";
            overlay.transform.position = (Vector3)position;
            if (overlay.IdTag != null)
                overlay.IdTag.Id = building.Id;

            var plaza = PlazaRadius();
            if (overlay.PlazaFilter != null)
                overlay.PlazaFilter.sharedMesh = BuildAnnulusMesh(plaza * 0.55f, plaza * 0.92f, 48, 0.06f);
            if (overlay.PlazaRenderer != null)
            {
                overlay.PlazaRenderer.sharedMaterial = _placedZoneMaterial;
                overlay.PlazaRenderer.shadowCastingMode = ShadowCastingMode.Off;
                overlay.PlazaRenderer.receiveShadows = false;
            }

            const float clickHeight = 18f;
            if (overlay.ClickProxy != null)
            {
                overlay.ClickProxy.transform.localPosition = Vector3.up * (clickHeight * 0.5f);
                overlay.ClickProxy.radius = plaza * 0.82f;
                overlay.ClickProxy.height = clickHeight;
                overlay.ClickProxy.direction = 1;
            }

            var view = new PlacedView
            {
                Root = overlay.gameObject,
                ZoneRenderer = overlay.PlazaRenderer
            };
            _views[building.Id] = view;
            return view;
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

        PlacedView CreateView(EntityManager em, Entity entity, in Building building)
        {
            EnsureReady();
            if (overlayPrefab == null)
            {
                GameLog.Error("BuildingViewBoard: assign BuildingOverlay prefab.");
                return null;
            }

            if (gridGuide == null || !TryGetCityCenter(out var center))
            {
                GameLog.Error("BuildingViewBoard: grid or CityGrid.Center missing.");
                return null;
            }

            EnsureMaterial();
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
                GameLog.Warning($"Building view skip id={building.Id}: expand failed.");
                return null;
            }

            var overlay = Object.Instantiate(overlayPrefab, _overlayRoot);
            overlay.name = $"Building_{building.WidthClusters}x{building.DepthRadialRings}_{building.Id}";
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

            var worldUi = SpawnWorldUi(em, entity, overlay.transform);
            var view = new PlacedView
            {
                Root = overlay.gameObject,
                ZoneRenderer = overlay.ZoneRenderer,
                WorldUi = worldUi,
                BarRoot = worldUi != null ? worldUi.BarRoot : null,
                Fill = worldUi != null ? worldUi.Fill : null
            };
            _views[building.Id] = view;
            return view;
        }

        BuildingWorldUi SpawnWorldUi(EntityManager em, Entity entity, Transform parent)
        {
            var prefab = ResolveWorldUiPrefab(em, entity);
            if (prefab == null)
            {
                GameLog.Error("BuildingViewBoard: assign BuildingWorldUi prefab.");
                return null;
            }

            var ui = Object.Instantiate(prefab, parent);
            ui.name = "WorldUi";
            return ui;
        }

        BuildingWorldUi ResolveWorldUiPrefab(EntityManager em, Entity entity)
        {
            var tintCatalog = catalog != null
                ? catalog
                : Object.FindFirstObjectByType<BuildPlacementController>()?.Catalog;
            if (tintCatalog == null || !em.HasComponent<Building>(entity))
                return worldUiPrefab;
            var typeId = em.GetComponentData<Building>(entity).TypeId.ToString();
            var stamp = tintCatalog.FindPrefab(typeId);
            var view = stamp != null ? stamp.GetComponent<BuildingView>() : null;
            if (view != null && view.WorldUiPrefab != null)
                return view.WorldUiPrefab;
            return worldUiPrefab;
        }

        void DestroyView(int buildingId)
        {
            selection?.ClearIf(buildingId);
            if (!_views.TryGetValue(buildingId, out var view))
                return;
            _views.Remove(buildingId);
            if (view?.Root == null)
                return;
            view.Root.SetActive(false);
            Object.DestroyImmediate(view.Root);
        }

        void EnsureMaterial()
        {
            if (_placedZoneMaterial == null)
            {
                _placedZoneMaterial = CreateZoneMaterial("FootprintZone_Placed", zoneColor);
            }

            if (_selectedZoneMaterial == null)
            {
                _selectedZoneMaterial = CreateZoneMaterial("FootprintZone_Selected", _selectedZoneColor);
            }
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
            if (_overlayRoot != null)
                return;
            _overlayRoot = transform;
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
