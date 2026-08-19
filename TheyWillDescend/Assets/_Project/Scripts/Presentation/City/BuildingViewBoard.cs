using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Io;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// House meshes for Building entities. Simulation does not know prefabs.
    /// </summary>
    public sealed class BuildingViewBoard : MonoBehaviour
    {
        GameObject _prefabHouse6x2;
        GameObject _prefabHouse2x2;
        Transform _placedRoot;
        RadialGridGuide _gridGuide;
        readonly Dictionary<int, GameObject> _views = new();
        Material _placedZoneMaterial;
        Color _zoneColor = new(0.15f, 0.75f, 1f, 0.45f);

        public void Bind(
            GameObject prefabHouse6x2,
            GameObject prefabHouse2x2,
            Transform placedRoot,
            RadialGridGuide gridGuide,
            Color zoneColor)
        {
            _prefabHouse6x2 = prefabHouse6x2;
            _prefabHouse2x2 = prefabHouse2x2;
            _placedRoot = placedRoot;
            _gridGuide = gridGuide;
            _zoneColor = zoneColor;
        }

        public void LateUpdate() => Pump();

        public void Pump()
        {
            DrainEvents();
        }

        public void ClearViews()
        {
            foreach (var go in _views.Values)
            {
                if (go == null)
                    continue;
                go.SetActive(false);
                Object.DestroyImmediate(go);
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

        void DrainEvents()
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;
            using var bridgeQuery = em.CreateEntityQuery(
                Unity.Entities.ComponentType.ReadOnly<SimBridge>());
            if (bridgeQuery.IsEmptyIgnoreFilter)
                return;

            var bridgeEntity = bridgeQuery.GetSingletonEntity();
            var placed = em.GetBuffer<BuildingPlacedEvent>(bridgeEntity);
            for (var i = 0; i < placed.Length; i++)
                CreateView(placed[i]);
            placed.Clear();

            var despawned = em.GetBuffer<BuildingDespawnedEvent>(bridgeEntity);
            for (var i = 0; i < despawned.Length; i++)
                DestroyView(despawned[i].BuildingId);
            despawned.Clear();

            var rejected = em.GetBuffer<BuildingRejectedEvent>(bridgeEntity);
            for (var i = 0; i < rejected.Length; i++)
                GameLog.Warning($"Building rejected c={rejected[i].AnchorCluster} r={rejected[i].AnchorRadial}.");
            rejected.Clear();
        }

        void CreateView(in BuildingPlacedEvent placed)
        {
            if (_views.ContainsKey(placed.BuildingId))
                return;
            if (_gridGuide == null || CityCenter.Active == null)
            {
                GameLog.Error("BuildingViewBoard: grid or CityCenter missing.");
                return;
            }

            if (_placedRoot == null)
            {
                var rootGo = new GameObject("PlacedBuildings");
                _placedRoot = rootGo.transform;
            }

            EnsureMaterial();
            var footprint = new BuildingFootprint
            {
                WidthClusters = placed.WidthClusters,
                DepthRadialRings = placed.DepthRadialRings
            };
            var clusters = new List<(int cluster, int radial)>(32);
            var config = _gridGuide.Config;
            var center = (float3)CityCenter.Active.Position;
            if (!RadialFootprintMath.TryExpandClusters(
                    config, placed.AnchorCluster, placed.AnchorRadial, footprint, clusters))
            {
                GameLog.Warning($"Building view skip id={placed.BuildingId}: expand failed.");
                return;
            }

            var root = new GameObject(
                $"Building_{placed.WidthClusters}x{placed.DepthRadialRings}_{placed.BuildingId}");
            root.transform.SetParent(_placedRoot, true);

            var zoneGo = new GameObject("FootprintZone");
            zoneGo.transform.SetParent(root.transform, false);
            var zoneFilter = zoneGo.AddComponent<MeshFilter>();
            var zoneRenderer = zoneGo.AddComponent<MeshRenderer>();
            zoneFilter.sharedMesh = RadialSectorMeshBuilder.BuildClusterZoneMesh(center, config, clusters);
            zoneRenderer.sharedMaterial = _placedZoneMaterial;
            zoneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            zoneRenderer.receiveShadows = false;

            RadialFootprintMath.FootprintMarkerPose(
                center, config, placed.AnchorCluster, placed.AnchorRadial, footprint,
                out var pos, out var rot, out var targetSize);
            SpawnVisual(root.transform, footprint, (Vector3)pos, (Quaternion)rot, targetSize);
            _views[placed.BuildingId] = root;
        }

        void DestroyView(int buildingId)
        {
            if (!_views.TryGetValue(buildingId, out var go))
                return;
            _views.Remove(buildingId);
            if (go == null)
                return;
            go.SetActive(false);
            Object.DestroyImmediate(go);
        }

        void SpawnVisual(
            Transform parent,
            in BuildingFootprint footprint,
            Vector3 pos,
            Quaternion rot,
            float targetSize)
        {
            var prefab = ResolvePrefab(footprint);
            GameObject instance;
            if (prefab == null)
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var col = instance.GetComponent<Collider>();
                if (col != null)
                    Destroy(col);
            }
            else
            {
                instance = Instantiate(prefab, parent);
                instance.name = "HouseVisual";
                var cols = instance.GetComponentsInChildren<Collider>();
                for (var i = 0; i < cols.Length; i++)
                    Destroy(cols[i]);
            }

            instance.transform.SetParent(parent, true);
            instance.transform.localScale = Vector3.one;
            instance.transform.SetPositionAndRotation(pos, rot);
            var size = MeasureHorizontalSize(instance);
            if (size > 0.001f)
                instance.transform.localScale = Vector3.one * (targetSize / size);
            instance.transform.SetPositionAndRotation(pos, rot);
        }

        GameObject ResolvePrefab(in BuildingFootprint footprint)
        {
            if (footprint.WidthClusters == 2 && footprint.DepthRadialRings == 2)
                return _prefabHouse2x2 != null ? _prefabHouse2x2 : _prefabHouse6x2;
            return _prefabHouse6x2 != null ? _prefabHouse6x2 : _prefabHouse2x2;
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

        static float MeasureHorizontalSize(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return 1f;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return Mathf.Max(bounds.size.x, bounds.size.z);
        }
    }
}
