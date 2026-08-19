using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Io;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Debug footprint zone only. The house mesh is Entities Graphics on the Building entity.
    /// </summary>
    public sealed class BuildingViewBoard : MonoBehaviour
    {
        Transform _placedRoot;
        RadialGridGuide _gridGuide;
        readonly Dictionary<int, GameObject> _views = new();
        readonly HashSet<int> _seen = new();
        Material _placedZoneMaterial;
        Color _zoneColor = new(0.15f, 0.75f, 1f, 0.45f);
        EntityQuery _query;
        World _queryWorld;

        public void Bind(
            Transform placedRoot,
            RadialGridGuide gridGuide,
            Color zoneColor)
        {
            _placedRoot = placedRoot;
            _gridGuide = gridGuide;
            _zoneColor = zoneColor;
        }

        public void LateUpdate() => Pump();

        public void Pump()
        {
            DrainRejected();
            SyncViews();
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
            DisposeQuery();
            ClearViews();
            if (_placedZoneMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(_placedZoneMaterial);
            else
                DestroyImmediate(_placedZoneMaterial);
            _placedZoneMaterial = null;
        }

        void DrainRejected()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;
            using var sessionQuery = em.CreateEntityQuery(ComponentType.ReadOnly<SimBridge>());
            if (sessionQuery.IsEmptyIgnoreFilter)
                return;

            var rejected = em.GetBuffer<BuildingRejectedEvent>(sessionQuery.GetSingletonEntity());
            for (var i = 0; i < rejected.Length; i++)
                GameLog.Warning($"Building rejected c={rejected[i].AnchorCluster} r={rejected[i].AnchorRadial}.");
            rejected.Clear();
        }

        void SyncViews()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            if (_query == default || _queryWorld != world)
            {
                DisposeQuery();
                _query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Building>());
                _queryWorld = world;
            }

            if (_query.IsEmptyIgnoreFilter)
            {
                if (_views.Count > 0)
                    ClearViews();
                return;
            }

            var buildings = _query.ToComponentDataArray<Building>(Allocator.Temp);
            _seen.Clear();
            for (var i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                _seen.Add(building.Id);
                if (!_views.ContainsKey(building.Id) || _views[building.Id] == null)
                    CreateView(building);
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

            buildings.Dispose();
        }

        void CreateView(in Building building)
        {
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
                return;
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

            _views[building.Id] = root;
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

        void DisposeQuery()
        {
            if (_query == default)
                return;
            _query.Dispose();
            _query = default;
            _queryWorld = null;
        }
    }
}
