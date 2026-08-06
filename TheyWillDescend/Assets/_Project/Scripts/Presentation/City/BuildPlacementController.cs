using System.Collections.Generic;
using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Simulation.City;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace _Project.Scripts.Presentation.City
{
    /// <summary>
    /// Placement on cluster underlay. Ghost size ≈ const (TargetClusterWorldWidth).
    /// </summary>
    public sealed class BuildPlacementController : MonoBehaviour
    {
        [SerializeField] RadialGridGuide gridGuide;
        [SerializeField] BuildingFootprint footprint = BuildingFootprint.House6x2;
        [SerializeField] Color ghostValidColor = new(0.2f, 0.85f, 1f, 0.55f);
        [SerializeField] Transform placedRoot;

        readonly List<(int cluster, int radial)> _clusters = new(64);

        bool _placing;
        bool _anchorValid;
        int _anchorCluster;
        int _anchorRadial;
        Transform _ghost;
        Material _ghostMaterial;

        public bool IsPlacing => _placing;

        public void SetFootprint(BuildingFootprint value) => footprint = value;

        public void BeginPlacing()
        {
            EnsureDeps();
            _placing = true;
            EnsureGhost();
            GameLog.Info(
                LogChannel.Presentation,
                $"Place mode: {footprint.WidthClusters}x{footprint.DepthRadialRings} (const world width).");
        }

        public void CancelPlacing()
        {
            if (!_placing)
                return;
            _placing = false;
            _anchorValid = false;
            if (_ghost != null)
                _ghost.gameObject.SetActive(false);
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

            RadialFootprintMath.FootprintToWorldPose(
                center, config, _anchorCluster, _anchorRadial, footprint,
                out var pos, out var rot, out var scale);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name =
                $"Stub_{footprint.WidthClusters}x{footprint.DepthRadialRings}_c{_anchorCluster}_r{_anchorRadial}";
            cube.transform.SetParent(placedRoot, true);
            var y = ((Vector3)scale).y * 0.5f;
            cube.transform.SetPositionAndRotation((Vector3)pos + Vector3.up * y, (Quaternion)rot);
            cube.transform.localScale = (Vector3)scale;

            var col = cube.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            GameLog.Info(
                LogChannel.Presentation,
                $"Placed stub c={_anchorCluster} r={_anchorRadial}, clusterSlots={_clusters.Count}.");
        }

        void UpdateGhost(float3 center, RadialGridConfig config, bool valid)
        {
            EnsureGhost();
            if (!valid)
            {
                SetGhostVisible(false);
                return;
            }

            RadialFootprintMath.FootprintToWorldPose(
                center, config, _anchorCluster, _anchorRadial, footprint,
                out var pos, out var rot, out var scale);

            SetGhostVisible(true);
            ApplyColor(_ghostMaterial, ghostValidColor);
            var y = ((Vector3)scale).y * 0.5f;
            _ghost.SetPositionAndRotation((Vector3)pos + Vector3.up * y, (Quaternion)rot);
            _ghost.localScale = (Vector3)scale;
        }

        void EnsureGhost()
        {
            EnsureGhostMaterial();
            if (_ghost != null)
                return;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "GhostBuilding";
            cube.transform.SetParent(transform, true);
            var col = cube.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            cube.GetComponent<MeshRenderer>().sharedMaterial = _ghostMaterial;
            _ghost = cube.transform;
            _ghost.gameObject.SetActive(false);
        }

        void SetGhostVisible(bool visible)
        {
            if (_ghost != null)
                _ghost.gameObject.SetActive(visible);
        }

        void EnsureDeps()
        {
            if (gridGuide == null)
                gridGuide = FindFirstObjectByType<RadialGridGuide>();
        }

        void EnsureGhostMaterial()
        {
            if (_ghostMaterial != null)
                return;
            var shader =
                Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
            _ghostMaterial = new Material(shader)
            {
                name = "BuildGhost_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };
            ApplyColor(_ghostMaterial, ghostValidColor);
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
            if (_ghostMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(_ghostMaterial);
            else
                DestroyImmediate(_ghostMaterial);
        }
    }
}
