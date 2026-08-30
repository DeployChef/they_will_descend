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
    /// Footprint zone always. Progress bar while Construction exists.
    /// Finished house mesh is Entities Graphics on the Building entity — not this board.
    /// </summary>
    public sealed class BuildingViewBoard : MonoBehaviour
    {
        [SerializeField] RadialGridGuide gridGuide;
        [SerializeField] BuildingSelection selection;
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
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            _seen.Clear();
            var cam = Camera.main;
            for (var i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                _seen.Add(building.Id);
                if (!_views.TryGetValue(building.Id, out var view) || view?.Root == null)
                {
                    view = em.HasComponent<Headquarters>(entities[i])
                        ? CreateHqView(building, transforms[i].Position)
                        : CreateView(building);
                }

                if (view == null)
                    continue;

                var constructing = em.HasComponent<Construction>(entities[i]);
                var barOn = view.BarRoot != null;
                if (barOn)
                    view.BarRoot.SetActive(true);

                if (constructing && view.Fill != null)
                {
                    var construction = em.GetComponentData<Construction>(entities[i]);
                    view.Fill.color = _constructionFill;
                    view.Fill.fillAmount = construction.Normalized;
                }
                else if (view.Fill != null)
                {
                    var paused = em.HasComponent<Workplace>(entities[i])
                        && em.GetComponentData<Workplace>(entities[i]).IsPaused;
                    var slots = em.HasComponent<BuildingType>(entities[i])
                        ? em.GetComponentData<BuildingType>(entities[i]).WorkplaceSlots
                        : 0;
                    var assigned = em.HasComponent<Workplace>(entities[i])
                        ? em.GetComponentData<Workplace>(entities[i]).AssignedCount
                        : 0;
                    view.Fill.color = paused
                        ? new Color(0.45f, 0.45f, 0.48f, 0.9f)
                        : _loadFill;
                    view.Fill.fillAmount = Workplace.Load01(assigned, slots);
                    if (slots <= 0 && view.BarRoot != null)
                        view.BarRoot.SetActive(false);
                }

                if (view.BarRoot != null && view.BarRoot.activeSelf)
                {
                    var pos = (Vector3)transforms[i].Position + Vector3.up * 2.2f;
                    view.BarRoot.transform.position = pos;
                    if (cam != null)
                        view.BarRoot.transform.rotation = Quaternion.LookRotation(
                            view.BarRoot.transform.position - cam.transform.position);
                }

                var selected = selection != null && building.Id == selection.SelectedBuildingId;
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

        PlacedView CreateHqView(in Building building, float3 position)
        {
            EnsureReady();
            EnsureMaterial();
            var root = new GameObject($"Headquarters_{building.Id}");
            root.transform.SetParent(_overlayRoot, true);
            root.transform.position = (Vector3)position;

            var zoneGo = new GameObject("PlazaRing");
            zoneGo.transform.SetParent(root.transform, false);
            var zoneFilter = zoneGo.AddComponent<MeshFilter>();
            var zoneRenderer = zoneGo.AddComponent<MeshRenderer>();
            zoneFilter.sharedMesh = BuildAnnulusMesh(9f, 13.5f, 48, 0.06f);
            zoneRenderer.sharedMaterial = _placedZoneMaterial;
            zoneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            zoneRenderer.receiveShadows = false;

            var click = new GameObject("ClickProxy");
            click.transform.SetParent(root.transform, false);
            click.transform.localPosition = Vector3.up * 6f;
            var capsule = click.AddComponent<CapsuleCollider>();
            capsule.radius = 12f;
            capsule.height = 28f;
            capsule.direction = 1;

            var tag = root.AddComponent<BuildingIdTag>();
            tag.Id = building.Id;

            var view = new PlacedView { Root = root, ZoneRenderer = zoneRenderer };
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
