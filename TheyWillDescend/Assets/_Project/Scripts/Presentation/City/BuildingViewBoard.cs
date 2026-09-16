using System.Collections.Generic;
using TheyWillDescend.Content;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Presentation.Audio;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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
        [SerializeField] Color idleOutlineColor = new(0.62f, 0.62f, 0.62f, 1f);
        [SerializeField] Color hoverOutlineColor = new(0.88f, 0.88f, 0.88f, 1f);
        [SerializeField] Color selectedOutlineColor = Color.white;
        [SerializeField] float outlineWidth = 0.28f;
        [SerializeField] AudioZoneManager audioZoneManager;

        Transform _root;
        readonly Dictionary<int, PlacedView> _views = new();
        readonly HashSet<int> _seen = new();
        readonly List<int> _stale = new();
        EntityQuery _buildingQuery;
        Camera _cam;


        sealed class PlacedView
        {
            public GameObject Root;
            public GameObject Overlay;
            public BuildingOverlay Zone;
            public BuildingView View;
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

                var buildMode = gridGuide != null && gridGuide.IsBuildModeActive;
                var hovered = selection != null && building.Id == selection.HoveredBuildingId;
                var selected = selection != null && building.Id == selection.SelectedBuildingId;
                if (placed.Zone != null)
                {
                    var show = buildMode || hovered || selected;
                    placed.Zone.SetVisible(show);
                    if (show)
                    {
                        if (selected)
                            placed.Zone.SetTint(selectedOutlineColor);
                        else if (hovered)
                            placed.Zone.SetTint(hoverOutlineColor);
                        else
                            placed.Zone.SetTint(idleOutlineColor);
                    }
                }
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

            var house = Object.Instantiate(prefab, _root);
            house.name = $"Building_{building.Id}";
            house.transform.position = (Vector3)position;
            var houseTag = house.GetComponent<BuildingIdTag>();
            if (houseTag == null)
                houseTag = house.AddComponent<BuildingIdTag>();
            houseTag.Id = building.Id;
            EnsurePickColliders(house);
            var view = house.GetComponent<BuildingView>();
            if (view == null)
                GameLog.Error($"BuildingViewBoard: {prefab.name} has no BuildingView.");

            var overlay = SpawnOverlay(building, center);
            RegisterAudioSource(house, position);

            var placed = new PlacedView
            {
                Root = house,
                Overlay = overlay != null ? overlay.gameObject : null,
                Zone = overlay,
                View = view
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

            var n = config.GetClusterCount(building.AnchorRadial);
            var turns0 = n > 0 ? building.AnchorCluster / (float)n : 0f;
            overlay.ApplyFootprint(center, config, turns0, building.AnchorRadial, footprint, outlineWidth);
            overlay.SetTint(idleOutlineColor);
            overlay.SetVisible(false);
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

        void RegisterAudioSource(GameObject buildingGo, float3 worldPosition)
        {
            // Менеджер живёт в Bootstrap-сцене (грузится additive), сериализованная
            // ссылка из Game-сцены невозможна — ищем автоматически.
            if (audioZoneManager == null)
                audioZoneManager = FindFirstObjectByType<AudioZoneManager>();
            if (audioZoneManager == null)
                return;

            // Ищем аудио-источник на префабе или создаём.
            var audioSource = buildingGo.GetComponent<BuildingAudioSource>();
            if (audioSource == null)
            {
                audioSource = buildingGo.AddComponent<BuildingAudioSource>();
            }

            // Находим ближайшую зону.
            var zone = audioZoneManager.FindZoneNear((Vector3)worldPosition);
            if (zone != null)
            {
                audioSource.LinkedZone = zone;

                // Явная регистрация: OnEnable у BuildingAudioSource срабатывает
                // в момент AddComponent, когда LinkedZone ещё null — там
                // зарегистрироваться невозможно. Регистрируем здесь.
                zone.AddAudioSource(audioSource);

                // Мгновенная активация: если зона уже в поле зрения камеры,
                // звук появляется сразу после постройки, без ожидания тика.
                if (zone.IsVisible && !zone.IsActive)
                    zone.SetActive(true);
            }
        }

        static void EnsurePickColliders(GameObject house)
        {
            if (house.GetComponentInChildren<Collider>(true) != null)
                return;
            var filters = house.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter.sharedMesh == null)
                    continue;
                if (filter.GetComponentInParent<BuildingWidget>(true) != null)
                    continue;
                var col = filter.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = filter.sharedMesh;
            }
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
