using System.Collections.Generic;
using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Simulation.City;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace _Project.Scripts.Presentation.City
{
    /// <summary>
    /// Fixed-size cube marker + polar section zone (annular sectors) under it.
    /// </summary>
    public sealed class BuildPlacementController : MonoBehaviour
    {
        [SerializeField] RadialGridGuide gridGuide;
        [SerializeField] BuildingFootprint footprint = BuildingFootprint.House6x2;
        [SerializeField] Color zoneValidColor = new(0.15f, 0.75f, 1f, 0.45f);
        [SerializeField] Color stubColor = new(0.85f, 0.85f, 0.9f, 1f);
        [SerializeField] Transform placedRoot;

        readonly List<(int cluster, int radial)> _clusters = new(64);

        bool _placing;
        bool _anchorValid;
        int _anchorCluster;
        int _anchorRadial;

        Transform _ghostRoot;
        Transform _ghostCube;
        MeshFilter _ghostZoneFilter;
        Mesh _ghostZoneMesh;
        Material _zoneMaterial;
        Material _stubMaterial;

        public bool IsPlacing => _placing;

        public void SetFootprint(BuildingFootprint value) => footprint = value;

        public void BeginPlacing()
        {
            EnsureDeps();
            _placing = true;
            EnsureGhost();
            GameLog.Info(
                LogChannel.Presentation,
                $"Place mode: {footprint.WidthClusters}x{footprint.DepthRadialRings} — zone=full pad, cube=short-side stub.");
        }

        public void CancelPlacing()
        {
            if (!_placing)
                return;
            _placing = false;
            _anchorValid = false;
            if (_ghostRoot != null)
                _ghostRoot.gameObject.SetActive(false);
            GameLog.Info(LogChannel.Presentation, "Place mode cancelled.");
        }

        void Update()
        {
            if (!_placing)
                return;

            EnsureDeps();
            if (gridGuide == null || CityCenter.Active == null)
                return;

            var config = gridGuide.Config;
            if (!config.IsValid)
                return;

            if (!TryGetPointerOnBuildPlane(out var world))
            {
                SetGhostVisible(false);
                _anchorValid = false;
                return;
            }

            var center = (float3)CityCenter.Active.Position;
            _anchorValid = RadialFootprintMath.TrySnapAnchor(
                center, config, (float3)world, out _anchorCluster, out _anchorRadial);

            if (_anchorValid)
            {
                _anchorValid = RadialFootprintMath.TryExpandClusters(
                    config, _anchorCluster, _anchorRadial, footprint, _clusters);
            }

            UpdateGhost(center, config, _anchorValid);

            if (!_anchorValid)
                return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            PlaceStub(center, config);
        }

        void PlaceStub(float3 center, RadialGridConfig config)
        {
            if (placedRoot == null)
                placedRoot = new GameObject("PlacedBuildings").transform;

            var root = new GameObject(
                $"Building_{footprint.WidthClusters}x{footprint.DepthRadialRings}_c{_anchorCluster}_r{_anchorRadial}");
            root.transform.SetParent(placedRoot, true);

            // Zone under building — exact occupied sections.
            var zoneGo = new GameObject("FootprintZone");
            zoneGo.transform.SetParent(root.transform, false);
            var zoneFilter = zoneGo.AddComponent<MeshFilter>();
            var zoneRenderer = zoneGo.AddComponent<MeshRenderer>();
            zoneFilter.sharedMesh = RadialSectorMeshBuilder.BuildClusterZoneMesh(
                center, config, _clusters);
            zoneRenderer.sharedMaterial = _zoneMaterial;
            zoneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            zoneRenderer.receiveShadows = false;

            RadialFootprintMath.FootprintMarkerPose(
                center, config, _anchorCluster, _anchorRadial, footprint,
                out var pos, out var rot, out var stubSize);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "StubCube";
            cube.transform.SetParent(root.transform, true);
            cube.transform.SetPositionAndRotation(
                (Vector3)pos + Vector3.up * (stubSize * 0.5f),
                (Quaternion)rot);
            cube.transform.localScale = Vector3.one * stubSize;
            cube.GetComponent<MeshRenderer>().sharedMaterial = _stubMaterial;
            var col = cube.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            GameLog.Info(
                LogChannel.Presentation,
                $"Placed stub+zone c={_anchorCluster} r={_anchorRadial}, sections={_clusters.Count}.");
        }

        void UpdateGhost(float3 center, RadialGridConfig config, bool valid)
        {
            EnsureGhost();
            if (!valid)
            {
                SetGhostVisible(false);
                return;
            }

            SetGhostVisible(true);
            RadialSectorMeshBuilder.RebuildClusterZoneMesh(
                _ghostZoneMesh, center, config, _clusters);
            _ghostZoneFilter.sharedMesh = _ghostZoneMesh;

            RadialFootprintMath.FootprintMarkerPose(
                center, config, _anchorCluster, _anchorRadial, footprint,
                out var pos, out var rot, out var stubSize);

            _ghostCube.SetPositionAndRotation(
                (Vector3)pos + Vector3.up * (stubSize * 0.5f),
                (Quaternion)rot);
            _ghostCube.localScale = Vector3.one * stubSize;
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
            var zoneRenderer = zoneGo.AddComponent<MeshRenderer>();
            _ghostZoneMesh = new Mesh { name = "GhostFootprintZone" };
            _ghostZoneFilter.sharedMesh = _ghostZoneMesh;
            zoneRenderer.sharedMaterial = _zoneMaterial;
            zoneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            zoneRenderer.receiveShadows = false;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "GhostCube";
            cube.transform.SetParent(_ghostRoot, false);
            var col = cube.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            cube.GetComponent<MeshRenderer>().sharedMaterial = _stubMaterial;
            _ghostCube = cube.transform;

            _ghostRoot.gameObject.SetActive(false);
        }

        void SetGhostVisible(bool visible)
        {
            if (_ghostRoot != null)
                _ghostRoot.gameObject.SetActive(visible);
        }

        void EnsureDeps()
        {
            if (gridGuide == null)
                gridGuide = FindFirstObjectByType<RadialGridGuide>();
        }

        void EnsureMaterials()
        {
            if (_zoneMaterial == null)
            {
                _zoneMaterial = CreateUnlitMaterial("FootprintZone_Runtime", zoneValidColor);
                // Transparent-ish queue.
                _zoneMaterial.renderQueue = (int)RenderQueue.Transparent + 60;
            }

            if (_stubMaterial == null)
                _stubMaterial = CreateUnlitMaterial("StubCube_Runtime", stubColor);
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

        static void ApplyColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            mat.color = color;
        }

        static bool TryGetPointerOnBuildPlane(out Vector3 world)
        {
            world = default;
            var cam = Camera.main;
            if (cam == null || Mouse.current == null)
                return false;
            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            var y = CityCenter.Active != null ? CityCenter.Active.Position.y : 0f;
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            if (!plane.Raycast(ray, out var enter))
                return false;
            world = ray.GetPoint(enter);
            return true;
        }

        void OnDestroy()
        {
            if (_ghostZoneMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_ghostZoneMesh);
                else
                    DestroyImmediate(_ghostZoneMesh);
            }

            DestroyMat(_zoneMaterial);
            DestroyMat(_stubMaterial);
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
