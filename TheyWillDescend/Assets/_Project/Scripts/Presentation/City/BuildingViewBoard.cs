using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Presentation.GameHud;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Io;
using TMPro;
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
    /// Finished house mesh is Entities Graphics on the Building entity — not this board.
    /// </summary>
    public sealed class BuildingViewBoard : MonoBehaviour
    {
        Transform _placedRoot;
        RadialGridGuide _gridGuide;
        readonly Dictionary<int, PlacedView> _views = new();
        readonly HashSet<int> _seen = new();
        Material _placedZoneMaterial;
        Material _selectedZoneMaterial;
        Color _zoneColor = new(0.15f, 0.75f, 1f, 0.45f);
        Color _selectedZoneColor = new(0.95f, 0.82f, 0.2f, 0.55f);
        int _selectedId;

        sealed class PlacedView
        {
            public GameObject Root;
            public MeshRenderer ZoneRenderer;
            public GameObject BarRoot;
            public Image Fill;
            public GameObject CrewRoot;
            public TMP_Text CrewLabel;
        }

        public void Bind(
            Transform placedRoot,
            RadialGridGuide gridGuide,
            Color zoneColor)
        {
            _placedRoot = placedRoot;
            _gridGuide = gridGuide;
            _zoneColor = zoneColor;
        }

        void Update()
        {
            if (!TryConsumeClick(out var hitBuildingId))
                return;
            _selectedId = hitBuildingId;
        }

        void LateUpdate() => Pump();

        public void Pump()
        {
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
                GameLog.Warning($"Building rejected c={rejected[i].AnchorCluster} r={rejected[i].AnchorRadial}.");
            rejected.Clear();
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

                var selected = building.Id == _selectedId && !constructing;
                if (view.ZoneRenderer != null)
                    view.ZoneRenderer.sharedMaterial = selected ? _selectedZoneMaterial : _placedZoneMaterial;
                RefreshCrew(view, building.Id, selected, transforms[i].Position, cam);
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
            if (_gridGuide == null || !SimIo.TryGetCityCenter(out var center))
            {
                GameLog.Error("BuildingViewBoard: grid or CityGrid.Center missing.");
                return null;
            }

            if (_placedRoot == null)
            {
                var rootGo = new GameObject("PlacedBuildings");
                _placedRoot = rootGo.transform;
            }

            EnsureMaterial();
            var footprint = new BuildingFootprint
            {
                WidthClusters = building.WidthClusters,
                DepthRadialRings = building.DepthRadialRings
            };
            var clusters = new List<(int cluster, int radial)>(32);
            var config = _gridGuide.Config;
            if (!RadialFootprintMath.TryExpandClusters(
                    config, building.AnchorCluster, building.AnchorRadial, footprint, clusters))
            {
                GameLog.Warning($"Building view skip id={building.Id}: expand failed.");
                return null;
            }

            var root = new GameObject(
                $"Building_{building.WidthClusters}x{building.DepthRadialRings}_{building.Id}");
            root.transform.SetParent(_placedRoot, true);

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
            CreateCrewWidget(view, root.transform, building.Id);
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

        void CreateCrewWidget(PlacedView view, Transform parent, int buildingId)
        {
            var crew = new GameObject("CrewWidget");
            crew.transform.SetParent(parent, false);
            var canvas = crew.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 25;
            crew.AddComponent<GraphicRaycaster>();
            var rect = crew.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 56f);
            crew.transform.localScale = Vector3.one * 0.02f;

            var bg = CreateBarImage(crew.transform, "Bg", new Color(0.07f, 0.08f, 0.1f, 0.92f));
            Stretch(bg.rectTransform);

            var minus = CreateCrewButton(crew.transform, "-", new Vector2(18f, 0.5f), () =>
            {
                SimIo.TryEnqueueUnassignWorker(buildingId);
            });
            var plus = CreateCrewButton(crew.transform, "+", new Vector2(202f, 0.5f), () =>
            {
                SimIo.TryEnqueueAssignWorker(buildingId);
            });
            ((RectTransform)minus.transform).anchoredPosition = new Vector2(28f, 0f);
            ((RectTransform)plus.transform).anchoredPosition = new Vector2(-28f, 0f);

            var labelGo = new GameObject("CrewLabel");
            labelGo.transform.SetParent(crew.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 22;
            label.color = Color.white;
            label.text = "0/1";
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.2f, 0f);
            labelRect.anchorMax = new Vector2(0.8f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            view.CrewRoot = crew;
            view.CrewLabel = label;
            crew.SetActive(false);
        }

        static Button CreateCrewButton(Transform parent, string caption, Vector2 _, UnityEngine.Events.UnityAction click)
        {
            var go = new GameObject(caption == "+" ? "Plus" : "Minus");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = WhiteSprite();
            image.color = new Color(0.2f, 0.45f, 0.7f, 0.95f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(caption == "+" ? 1f : 0f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(40f, 40f);
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = caption;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 28;
            text.color = Color.white;
            Stretch(text.rectTransform);
            HudButtons.Bind(button, click);
            return button;
        }

        void RefreshCrew(PlacedView view, int buildingId, bool selected, float3 position, Camera cam)
        {
            if (view.CrewRoot == null)
                return;
            view.CrewRoot.SetActive(selected);
            if (!selected)
                return;

            view.CrewRoot.transform.position = (Vector3)position + Vector3.up * 2.6f;
            if (cam != null)
                view.CrewRoot.transform.rotation = Quaternion.LookRotation(
                    view.CrewRoot.transform.position - cam.transform.position);

            var occupied = 0;
            if (SimIo.TryGetWorkplace(buildingId, out var workplace, out _))
                occupied = workplace.WorkerAgentId != 0 ? 1 : 0;
            if (view.CrewLabel != null)
                view.CrewLabel.text = $"{occupied}/1";
        }

        bool TryConsumeClick(out int buildingId)
        {
            buildingId = 0;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            var placement = FindFirstObjectByType<BuildPlacementController>();
            if (placement != null && placement.IsPlacing)
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
                _placedZoneMaterial = CreateZoneMaterial("FootprintZone_Placed", _zoneColor);
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
    }
}
