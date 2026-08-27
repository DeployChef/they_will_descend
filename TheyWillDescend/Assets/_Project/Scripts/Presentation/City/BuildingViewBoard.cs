using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Footprint zone always. Progress bar while Construction exists.
    /// Selection opens the HUD inspect panel — not a world-space crew widget.
    /// Finished house mesh is Entities Graphics on the Building entity — not this board.
    /// </summary>
    public sealed class BuildingViewBoard : MonoBehaviour
    {
        [SerializeField] RadialGridGuide gridGuide;
        [SerializeField] Color zoneColor = new(0.15f, 0.75f, 1f, 0.45f);

        Transform _overlayRoot;
        readonly Dictionary<int, PlacedView> _views = new();
        readonly HashSet<int> _seen = new();
        Material _placedZoneMaterial;
        Material _selectedZoneMaterial;
        Color _selectedZoneColor = new(0.95f, 0.82f, 0.2f, 0.55f);
        int _selectedId;

        sealed class PlacedView
        {
            public GameObject Root;
            public MeshRenderer ZoneRenderer;
            public GameObject BarRoot;
            public Image Fill;
        }

        public int SelectedBuildingId => _selectedId;

        public void Deselect() => _selectedId = 0;

        public void RebuildViews()
        {
            ClearViews();
            Pump();
        }

        void Awake() => EnsureReady();

        void Update()
        {
            if (!TryConsumeClick(out var hitBuildingId))
                return;
            _selectedId = hitBuildingId;
        }

        void LateUpdate() => Pump();

        public void Pump()
        {
            EnsureReady();
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;
            using var sessionQuery = em.CreateEntityQuery(ComponentType.ReadOnly<SimBridge>());
            DrainRejected(em, sessionQuery);
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Headquarters>());
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
            _selectedId = 0;
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

        void DrainRejected(EntityManager em, EntityQuery sessionQuery)
        {
            if (sessionQuery.IsEmptyIgnoreFilter)
                return;

            var rejected = em.GetBuffer<BuildingRejectedEvent>(sessionQuery.GetSingletonEntity());
            for (var i = 0; i < rejected.Length; i++)
            {
                var row = rejected[i];
                GameLog.Warning(
                    $"Building rejected ({ReasonText(row.Reason)}) c={row.AnchorCluster} r={row.AnchorRadial}.");
            }
            rejected.Clear();
        }

        static string ReasonText(byte reason)
        {
            return reason switch
            {
                BuildingRejectedEvent.UnknownType => "unknown type",
                BuildingRejectedEvent.InvalidCell => "invalid cell",
                BuildingRejectedEvent.Overlap => "overlap",
                BuildingRejectedEvent.Unaffordable => "not enough resources",
                _ => "rejected"
            };
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
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            _seen.Clear();
            var cam = Camera.main;
            for (var i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                _seen.Add(building.Id);
                if (!_views.TryGetValue(building.Id, out var view) || view?.Root == null)
                    view = CreateView(building);

                if (view == null)
                    continue;

                var constructing = em.HasComponent<Construction>(entities[i]);
                if (view.BarRoot != null)
                    view.BarRoot.SetActive(constructing);
                if (constructing && view.Fill != null)
                {
                    var construction = em.GetComponentData<Construction>(entities[i]);
                    view.Fill.fillAmount = construction.Normalized;
                }

                if (constructing && view.BarRoot != null)
                {
                    var pos = (Vector3)transforms[i].Position + Vector3.up * 2.2f;
                    view.BarRoot.transform.position = pos;
                    if (cam != null)
                        view.BarRoot.transform.rotation = Quaternion.LookRotation(
                            view.BarRoot.transform.position - cam.transform.position);
                }

                var selected = building.Id == _selectedId;
                if (view.ZoneRenderer != null)
                    view.ZoneRenderer.sharedMaterial = selected ? _selectedZoneMaterial : _placedZoneMaterial;
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
            transforms.Dispose();
        }

        PlacedView CreateView(in Building building)
        {
            EnsureReady();
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

            var root = new GameObject(
                $"Building_{building.WidthClusters}x{building.DepthRadialRings}_{building.Id}");
            root.transform.SetParent(_overlayRoot, true);

            var zoneGo = new GameObject("FootprintZone");
            zoneGo.transform.SetParent(root.transform, false);
            var zoneFilter = zoneGo.AddComponent<MeshFilter>();
            var zoneRenderer = zoneGo.AddComponent<MeshRenderer>();
            var zoneMesh = RadialSectorMeshBuilder.BuildClusterZoneMesh(center, config, clusters);
            zoneFilter.sharedMesh = zoneMesh;
            zoneRenderer.sharedMaterial = _placedZoneMaterial;
            zoneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            zoneRenderer.receiveShadows = false;
            var collider = zoneGo.AddComponent<MeshCollider>();
            collider.sharedMesh = zoneMesh;

            var tag = root.AddComponent<BuildingIdTag>();
            tag.Id = building.Id;

            var view = new PlacedView { Root = root, ZoneRenderer = zoneRenderer };
            CreateProgressBar(view, root.transform);
            _views[building.Id] = view;
            return view;
        }

        static void CreateProgressBar(PlacedView view, Transform parent)
        {
            var bar = new GameObject("ConstructionBar");
            bar.transform.SetParent(parent, false);
            var canvas = bar.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20;
            var group = bar.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var rect = bar.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 22f);
            bar.transform.localScale = Vector3.one * 0.02f;

            var bg = CreateBarImage(bar.transform, "Bg", new Color(0.08f, 0.1f, 0.12f, 0.85f));
            Stretch(bg.rectTransform);

            var fill = CreateBarImage(bar.transform, "Fill", new Color(0.25f, 0.85f, 0.45f, 0.95f));
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            view.BarRoot = bar;
            view.Fill = fill;
            bar.SetActive(false);
        }

        bool TryConsumeClick(out int buildingId)
        {
            buildingId = 0;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            if (BuildPlacementController.IsPlacingActive)
                return false;

            var cam = Camera.main;
            if (cam == null)
                return false;
            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 500f))
                return true;

            var tag = hit.collider.GetComponentInParent<BuildingIdTag>();
            buildingId = tag != null ? tag.Id : 0;
            return true;
        }

        static Sprite _whiteSprite;

        static Image CreateBarImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = WhiteSprite();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Sprite WhiteSprite()
        {
            if (_whiteSprite != null)
                return _whiteSprite;
            var texture = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _whiteSprite.name = "ConstructionBarWhite";
            return _whiteSprite;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        void DestroyView(int buildingId)
        {
            if (buildingId == _selectedId)
                _selectedId = 0;
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
            var go = new GameObject("BuildingOverlays");
            go.transform.SetParent(transform, false);
            _overlayRoot = go.transform;
        }

        static bool TryGetCityCenter(out float3 center)
        {
            center = default;
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasComponent<CityGrid>(bag))
                return false;
            var grid = em.GetComponentData<CityGrid>(bag);
            if (grid.Ready == 0)
                return false;
            center = grid.Center;
            return true;
        }
    }
}
