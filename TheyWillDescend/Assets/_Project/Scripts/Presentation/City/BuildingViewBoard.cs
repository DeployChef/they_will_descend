using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Io;
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
        Color _zoneColor = new(0.15f, 0.75f, 1f, 0.45f);

        sealed class PlacedView
        {
            public GameObject Root;
            public GameObject BarRoot;
            public Image Fill;
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
                ComponentType.ReadOnly<LocalTransform>());
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
        }

        void OnDisable()
        {
            ClearViews();
            if (_placedZoneMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(_placedZoneMaterial);
            else
                DestroyImmediate(_placedZoneMaterial);
            _placedZoneMaterial = null;
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
            if (_gridGuide == null || CityCenter.Active == null)
            {
                GameLog.Error("BuildingViewBoard: grid or CityCenter missing.");
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
            var center = (float3)CityCenter.Active.Position;
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
            zoneFilter.sharedMesh = RadialSectorMeshBuilder.BuildClusterZoneMesh(center, config, clusters);
            zoneRenderer.sharedMaterial = _placedZoneMaterial;
            zoneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            zoneRenderer.receiveShadows = false;

            var view = new PlacedView { Root = root };
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
            if (_placedZoneMaterial != null)
                return;
            var shader =
                Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
            _placedZoneMaterial = new Material(shader)
            {
                name = "FootprintZone_Placed",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (_placedZoneMaterial.HasProperty("_BaseColor"))
                _placedZoneMaterial.SetColor("_BaseColor", _zoneColor);
            if (_placedZoneMaterial.HasProperty("_Color"))
                _placedZoneMaterial.SetColor("_Color", _zoneColor);
            _placedZoneMaterial.color = _zoneColor;
            _placedZoneMaterial.renderQueue = (int)RenderQueue.Transparent + 60;
        }
    }
}
