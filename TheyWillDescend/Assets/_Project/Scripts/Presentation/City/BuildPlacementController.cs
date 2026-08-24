using System;
using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Io;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Play aiming: ghost zone + catalog mesh, polar snap, click → PlaceBuildingCommand.
    /// Does not own house types, live meshes, or BuildingViewBoard.
    /// </summary>
    public sealed class BuildPlacementController : MonoBehaviour
    {
        [SerializeField] RadialGridGuide gridGuide;
        [SerializeField] BuildingCatalogAsset catalog;
        [SerializeField] Color zoneValidColor = new(0.15f, 0.75f, 1f, 0.45f);
        [SerializeField] Color zoneInvalidColor = new(0.95f, 0.2f, 0.15f, 0.5f);

        readonly List<(int cluster, int radial)> _clusters = new(64);

        string _typeId;
        BuildingFootprint _footprint;
        float _meshSize = 1f;
        GameObject _ghostPrefab;

        bool _placing;
        bool _canPlace;
        int _anchorCluster;
        int _anchorRadial;
        float _anchorTurns0;
        bool _angularSnapped;

        Transform _ghostRoot;
        Transform _ghostBuilding;
        MeshFilter _ghostZoneFilter;
        MeshRenderer _ghostZoneRenderer;
        Mesh _ghostZoneMesh;
        Material _ghostZoneMaterial;

        public static bool IsPlacingActive { get; private set; }

        public bool IsPlacing => _placing;

        public event Action Placed;

        public void BeginPlacing(string typeId)
        {
            if (!SimIo.TryGetBuilding(typeId, out var entry) || !entry.Footprint.IsValid)
            {
                GameLog.Error($"Place mode: unknown building type {typeId}.");
                return;
            }

            if (gridGuide == null)
            {
                GameLog.Error("BuildPlacementController: RadialGridGuide is not assigned.");
                return;
            }

            _typeId = entry.TypeId;
            _footprint = entry.Footprint;
            _meshSize = entry.MeshSize > 0.001f ? entry.MeshSize : 1f;
            _ghostPrefab = ResolveGhostPrefab(typeId);
            _placing = true;
            IsPlacingActive = true;
            gridGuide.SetBuildModeActive(true);
            EnsureGhost();
            RecreateGhostBuilding();
            GameLog.Info($"Place mode: {entry.DisplayName}.");
        }

        public void CancelPlacing()
        {
            if (!_placing)
                return;
            _placing = false;
            IsPlacingActive = false;
            _canPlace = false;
            if (gridGuide != null)
                gridGuide.SetBuildModeActive(false);
            if (_ghostRoot != null)
                _ghostRoot.gameObject.SetActive(false);
            GameLog.Info("Place mode cancelled.");
        }

        void Update()
        {
            if (!_placing)
                return;

            if (gridGuide == null || !SimIo.TryGetCityCenter(out var center))
                return;

            var config = gridGuide.Config;
            if (!config.IsValid)
                return;

            if (!TryGetPointerOnBuildPlane(out var world))
            {
                SetGhostVisible(false);
                _canPlace = false;
                return;
            }

            if (!TryResolveGhost(center, config, (float3)world))
            {
                SetGhostVisible(false);
                _canPlace = false;
                return;
            }

            UpdateGhost(center, config);

            if (!_canPlace)
                return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            PlaceBuilding();
        }

        bool TryResolveGhost(float3 center, in RadialGridConfig config, float3 world)
        {
            if (!RadialFootprintMath.TrySnapRing(center, config, world, out var ring, out var turns))
                return false;

            var n = config.GetClusterCount(ring);
            var snappedCluster = RadialGridMath.TurnsToCluster(turns, n);

            var probeOk = RadialFootprintMath.TryExpandClusters(
                config, snappedCluster, ring, _footprint, _clusters);
            var probeFree = probeOk && !SimIo.OverlapsOccupied(_clusters);
            var affordable = SimIo.CanAfford(_typeId);

            if (probeFree && affordable)
            {
                _canPlace = true;
                _angularSnapped = true;
                _anchorCluster = snappedCluster;
                _anchorRadial = ring;
                _anchorTurns0 = snappedCluster / (float)n;
                return true;
            }

            _canPlace = false;
            _angularSnapped = false;
            _anchorRadial = ring;
            _anchorTurns0 = turns;
            _anchorCluster = snappedCluster;

            return RadialFootprintMath.TryExpandClustersFromTurns(
                config, turns, ring, _footprint, _clusters);
        }

        void PlaceBuilding()
        {
            if (!SimIo.TryEnqueuePlaceBuilding(new PlaceBuildingCommand
                {
                    TypeId = _typeId,
                    WidthClusters = _footprint.WidthClusters,
                    DepthRadialRings = _footprint.DepthRadialRings,
                    AnchorCluster = _anchorCluster,
                    AnchorRadial = _anchorRadial
                }))
            {
                GameLog.Error("PlaceBuilding: sim world not ready.");
                return;
            }

            GameLog.Info($"Place command type={_typeId} c={_anchorCluster} r={_anchorRadial}.");
            CancelPlacing();
            Placed?.Invoke();
        }

        void UpdateGhost(float3 center, RadialGridConfig config)
        {
            EnsureGhost();
            SetGhostVisible(true);
            SetGhostZoneColor(_canPlace ? zoneValidColor : zoneInvalidColor);

            RadialSectorMeshBuilder.RebuildClusterZoneMesh(
                _ghostZoneMesh, center, config, _clusters);
            _ghostZoneFilter.sharedMesh = _ghostZoneMesh;

            if (_ghostBuilding == null)
                RecreateGhostBuilding();
            if (_ghostBuilding == null)
                return;

            if (_angularSnapped)
            {
                RadialFootprintMath.FootprintMarkerPose(
                    center, config, _anchorCluster, _anchorRadial, _footprint,
                    out var pos, out var rot, out var targetSize);
                ApplyBuildingPose(_ghostBuilding, (Vector3)pos, (Quaternion)rot, targetSize);
            }
            else
            {
                RadialFootprintMath.FootprintMarkerPoseFromTurns(
                    center, config, _anchorTurns0, _anchorRadial, _footprint,
                    out var pos, out var rot, out var targetSize);
                ApplyBuildingPose(_ghostBuilding, (Vector3)pos, (Quaternion)rot, targetSize);
            }
        }

        void EnsureGhost()
        {
            EnsureMaterials();
            if (_ghostRoot != null)
                return;

            _ghostRoot = new GameObject("GhostPlacement").transform;
            _ghostRoot.SetParent(transform, false);

            var zoneGo = new GameObject("GhostZone");
            zoneGo.transform.SetParent(_ghostRoot, false);
            _ghostZoneFilter = zoneGo.AddComponent<MeshFilter>();
            _ghostZoneRenderer = zoneGo.AddComponent<MeshRenderer>();
            _ghostZoneMesh = new Mesh { name = "GhostFootprintZone" };
            _ghostZoneFilter.sharedMesh = _ghostZoneMesh;
            _ghostZoneRenderer.sharedMaterial = _ghostZoneMaterial;
            _ghostZoneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _ghostZoneRenderer.receiveShadows = false;

            _ghostRoot.gameObject.SetActive(false);
        }

        void RecreateGhostBuilding()
        {
            EnsureGhost();
            if (_ghostBuilding != null)
            {
                Destroy(_ghostBuilding.gameObject);
                _ghostBuilding = null;
            }

            if (_ghostPrefab == null)
                return;

            var instance = Instantiate(_ghostPrefab, _ghostRoot);
            instance.name = "GhostHouse";
            StripColliders(instance);
            _ghostBuilding = instance.transform;
            _ghostBuilding.localScale = Vector3.one;
        }

        void ApplyBuildingPose(Transform t, Vector3 pos, Quaternion rot, float targetSize)
        {
            t.localScale = Vector3.one;
            t.SetPositionAndRotation(pos, rot);

            var size = _meshSize > 0.001f ? _meshSize : MeasureHorizontalSize(t.gameObject);
            if (size > 0.001f)
                t.localScale = Vector3.one * (targetSize / size);

            t.position = pos;
            t.rotation = rot;
        }

        static void StripColliders(GameObject go)
        {
            var cols = go.GetComponentsInChildren<Collider>();
            for (var i = 0; i < cols.Length; i++)
                Destroy(cols[i]);
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

        void SetGhostVisible(bool visible)
        {
            if (_ghostRoot != null)
                _ghostRoot.gameObject.SetActive(visible);
        }

        void SetGhostZoneColor(Color color)
        {
            EnsureMaterials();
            ApplyColor(_ghostZoneMaterial, color);
        }

        GameObject ResolveGhostPrefab(string typeId)
        {
            return catalog != null ? catalog.FindPrefab(typeId) : null;
        }

        void EnsureMaterials()
        {
            if (_ghostZoneMaterial != null)
                return;
            _ghostZoneMaterial = CreateUnlitMaterial("FootprintZone_Ghost", zoneValidColor);
            _ghostZoneMaterial.renderQueue = (int)RenderQueue.Transparent + 60;
        }

        static void ApplyColor(Material mat, Color color)
        {
            if (mat == null)
                return;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            mat.color = color;
        }

        static Material CreateUnlitMaterial(string name, Color color)
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
            ApplyColor(mat, color);
            return mat;
        }

        static bool TryGetPointerOnBuildPlane(out Vector3 world)
        {
            world = default;
            var cam = Camera.main;
            if (cam == null || Mouse.current == null)
                return false;
            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            var y = SimIo.TryGetCityCenter(out var center) ? center.y : 0f;
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            if (!plane.Raycast(ray, out var enter))
                return false;
            world = ray.GetPoint(enter);
            return true;
        }

        void OnDisable()
        {
            if (_placing)
                CancelPlacing();
        }

        void OnDestroy()
        {
            if (_placing)
                IsPlacingActive = false;
            if (_ghostZoneMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_ghostZoneMesh);
                else
                    DestroyImmediate(_ghostZoneMesh);
            }

            DestroyMat(_ghostZoneMaterial);
        }

        static void DestroyMat(Material mat)
        {
            if (mat == null)
                return;
            if (Application.isPlaying)
                Destroy(mat);
            else
                DestroyImmediate(mat);
        }
    }
}
