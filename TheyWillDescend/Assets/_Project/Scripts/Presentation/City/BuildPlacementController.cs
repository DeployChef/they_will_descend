using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Io;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Place house prefab on polar section zone.
    /// Valid = cyan + full snap (ring + rays). Invalid = red + ring snap only (rays free).
    /// </summary>
    public sealed class BuildPlacementController : MonoBehaviour
    {
        [SerializeField] RadialGridGuide gridGuide;
        [SerializeField] BuildingFootprint footprint = BuildingFootprint.House6x2;
        [SerializeField] GameObject prefabHouse6x2;
        [SerializeField] GameObject prefabHouse2x2;
        [SerializeField] Color zoneValidColor = new(0.15f, 0.75f, 1f, 0.45f);
        [SerializeField] Color zoneInvalidColor = new(0.95f, 0.2f, 0.15f, 0.5f);
        [SerializeField] Transform placedRoot;

        readonly List<(int cluster, int radial)> _clusters = new(64);

        BuildingViewBoard _views;

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

        public bool IsPlacing => _placing;

        void Awake()
        {
            EnsureViews();
        }

        public void PumpViews()
        {
            EnsureViews().Pump();
        }

        public void WipeViews()
        {
            EnsureViews().Pump();
            EnsureViews().ClearViews();
        }

        public void SetFootprint(BuildingFootprint value)
        {
            footprint = value;
            if (_ghostRoot != null)
                RecreateGhostBuilding();
        }

        public void BeginPlacing()
        {
            EnsureDeps();
            _placing = true;
            if (gridGuide != null)
                gridGuide.SetBuildModeActive(true);
            EnsureGhost();
            RecreateGhostBuilding();
            GameLog.Info(
                $"Place mode: {footprint.WidthClusters}x{footprint.DepthRadialRings} — zone + house prefab.");
        }

        public void CancelPlacing()
        {
            if (!_placing)
                return;
            _placing = false;
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

            EnsureDeps();
            if (gridGuide == null || CityCenter.Active == null)
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

            var center = (float3)CityCenter.Active.Position;
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

            // Probe: full snap (ring + rays) — can we place here?
            var probeOk = RadialFootprintMath.TryExpandClusters(
                config, snappedCluster, ring, footprint, _clusters);
            var probeFree = probeOk && !SimIo.OverlapsOccupied(_clusters);

            if (probeFree)
            {
                _canPlace = true;
                _angularSnapped = true;
                _anchorCluster = snappedCluster;
                _anchorRadial = ring;
                _anchorTurns0 = snappedCluster / (float)n;
                return true;
            }

            // Invalid: ring snap stays, angular (ray) snap off — follow cursor angle.
            _canPlace = false;
            _angularSnapped = false;
            _anchorRadial = ring;
            _anchorTurns0 = turns;
            _anchorCluster = snappedCluster;

            if (!RadialFootprintMath.TryExpandClustersFromTurns(
                    config, turns, ring, footprint, _clusters))
                return false;

            return true;
        }

        void PlaceBuilding()
        {
            if (!SimIo.TryEnqueuePlaceBuilding(new PlaceBuildingCommand
                {
                    WidthClusters = footprint.WidthClusters,
                    DepthRadialRings = footprint.DepthRadialRings,
                    AnchorCluster = _anchorCluster,
                    AnchorRadial = _anchorRadial
                }))
            {
                GameLog.Error("PlaceBuilding: sim world not ready.");
                return;
            }

            SimIo.Flush();
            EnsureViews().Pump();
            GameLog.Info($"Place command c={_anchorCluster} r={_anchorRadial}.");
        }

        BuildingViewBoard EnsureViews()
        {
            if (_views != null)
                return _views;

            _views = GetComponent<BuildingViewBoard>();
            if (_views == null)
                _views = gameObject.AddComponent<BuildingViewBoard>();
            EnsureDeps();
            if (placedRoot == null)
                placedRoot = new GameObject("PlacedBuildings").transform;
            _views.Bind(prefabHouse6x2, prefabHouse2x2, placedRoot, gridGuide, zoneValidColor);
            return _views;
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

            if (_angularSnapped)
            {
                RadialFootprintMath.FootprintMarkerPose(
                    center, config, _anchorCluster, _anchorRadial, footprint,
                    out var pos, out var rot, out var targetSize);
                ApplyBuildingPose(_ghostBuilding, (Vector3)pos, (Quaternion)rot, targetSize);
            }
            else
            {
                RadialFootprintMath.FootprintMarkerPoseFromTurns(
                    center, config, _anchorTurns0, _anchorRadial, footprint,
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

            var prefab = ResolvePrefab();
            if (prefab == null)
            {
                GameLog.Warning("No house prefab assigned for current footprint.");
                return;
            }

            var instance = Instantiate(prefab, _ghostRoot);
            instance.name = "GhostHouse";
            StripColliders(instance);
            _ghostBuilding = instance.transform;
            _ghostBuilding.localScale = Vector3.one;
        }

        void ApplyBuildingPose(Transform t, Vector3 pos, Quaternion rot, float targetSize)
        {
            t.localScale = Vector3.one;
            t.SetPositionAndRotation(pos, rot);

            var size = MeasureHorizontalSize(t.gameObject);
            if (size > 0.001f)
            {
                var s = targetSize / size;
                t.localScale = Vector3.one * s;
            }

            t.position = pos;
            t.rotation = rot;
        }

        GameObject ResolvePrefab()
        {
            if (footprint.WidthClusters == 6 && footprint.DepthRadialRings == 2)
                return prefabHouse6x2 != null ? prefabHouse6x2 : prefabHouse2x2;
            if (footprint.WidthClusters == 2 && footprint.DepthRadialRings == 2)
                return prefabHouse2x2 != null ? prefabHouse2x2 : prefabHouse6x2;
            return prefabHouse6x2 != null ? prefabHouse6x2 : prefabHouse2x2;
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

        void EnsureDeps()
        {
            if (gridGuide == null)
                gridGuide = FindFirstObjectByType<RadialGridGuide>();
        }

        void EnsureMaterials()
        {
            if (_ghostZoneMaterial == null)
            {
                _ghostZoneMaterial = CreateUnlitMaterial("FootprintZone_Ghost", zoneValidColor);
                _ghostZoneMaterial.renderQueue = (int)RenderQueue.Transparent + 60;
            }
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
