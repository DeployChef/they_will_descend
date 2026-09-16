using System;
using System.Collections.Generic;
using TheyWillDescend.Content;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
        [SerializeField] BuildingOverlay overlayPrefab;
        [SerializeField] Material ghostBuildingMaterial;
        [SerializeField] Color zoneValidColor = Color.white;
        [SerializeField] Color zoneInvalidColor = new(1f, 0.28f, 0.22f, 1f);
        [SerializeField] float outlineWidth = 0.28f;
        [SerializeField] float gridMaskMarginCells = 2f;
        [Tooltip("Clusters of open grid revealed to each side of the footprint (Frostpunk-like band).")]
        [SerializeField] int gridMaskSideCells = 6;
        [Tooltip("Extra safety pad on top of the ghost model bounds (in grid cells). Raise only if a sliver of grid still peeks out.")]
        [SerializeField] float gridMaskHolePadCells = 0.15f;

        const float MaskFeatherRatio = 0.35f;
        const float HoleFeatherRatio = 0.15f;

        readonly List<(int cluster, int radial)> _clusters = new(64);

        string _typeId;
        BuildingFootprint _footprint;
        GameObject _ghostPrefab;

        bool _placing;
        bool _canPlace;
        int _anchorCluster;
        int _anchorRadial;
        float _anchorTurns0;
        bool _angularSnapped;

        Transform _ghostRoot;
        Transform _ghostBuilding;
        Renderer[] _ghostRenderers;
        BuildingOverlay _ghostOverlay;

        public BuildingCatalogAsset Catalog => catalog;

        public bool IsPlacing => _placing;

        public event Action Finished;

        public void BeginPlacing(string typeId)
        {
            if (!TryResolvePrototype(typeId, out var spec) || spec.TypeId.IsEmpty)
            {
                GameLog.Error($"Place mode: unknown building type {typeId}.");
                return;
            }

            if (gridGuide == null)
            {
                GameLog.Error("BuildPlacementController: RadialGridGuide is not assigned.");
                return;
            }

            if (!spec.Footprint.IsValid)
            {
                GameLog.Error($"Place mode: invalid footprint for {typeId}.");
                return;
            }
            _typeId = spec.TypeId.ToString();
            _footprint = spec.Footprint;
            _ghostPrefab = ResolveGhostPrefab(_typeId);
            _placing = true;
            gridGuide.SetBuildModeActive(true);
            EnsureGhost();
            RecreateGhostBuilding();
            GameLog.Info($"Place mode: {BuildingView.NameOf(_ghostPrefab)}.");
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

            if (Mouse.current != null
                && Mouse.current.rightButton.wasPressedThisFrame
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                CancelPlacing();
                Finished?.Invoke();
                return;
            }

            if (gridGuide == null || !TryGetCityCenter(out var center))
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
            if (!RadialFootprintMath.TrySnapFootprintCenter(
                    center, config, world, _footprint,
                    out var snappedCluster, out var ring, out var centerTurns))
                return false;

            var n = config.GetClusterCount(ring);
            if (n <= 0)
                return false;

            var centeredTurns0 = centerTurns - _footprint.WidthClusters * 0.5f / n;

            var probeOk = RadialFootprintMath.TryExpandClusters(
                config, snappedCluster, ring, _footprint, _clusters);
            var probeFree = probeOk && !OverlapsOccupied(_clusters);
            var affordable = CanAfford(_typeId);

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
            _anchorTurns0 = centeredTurns0;
            _anchorCluster = snappedCluster;

            return RadialFootprintMath.TryExpandClustersFromTurns(
                config, centeredTurns0, ring, _footprint, _clusters);
        }

        void PlaceBuilding()
        {
            if (!SimCommands.Request(new PlaceBuildingRequest
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
            if (CanAfford(_typeId))
                return;

            CancelPlacing();
            Finished?.Invoke();
        }

        void UpdateGhost(float3 center, RadialGridConfig config)
        {
            EnsureGhost();
            SetGhostVisible(true);
            var n = config.GetClusterCount(_anchorRadial);
            var turns0 = _angularSnapped && n > 0
                ? _anchorCluster / (float)n
                : _anchorTurns0;
            if (_ghostOverlay != null)
            {
                _ghostOverlay.ApplyFootprint(
                    center, config, turns0, _anchorRadial, _footprint, outlineWidth);
                _ghostOverlay.SetTint(_canPlace ? zoneValidColor : zoneInvalidColor);
                _ghostOverlay.SetVisible(true);
            }

            ApplyGhostBuildingColor();

            if (_ghostBuilding == null)
                RecreateGhostBuilding();
            if (_ghostBuilding == null)
                return;

            if (_angularSnapped)
            {
                RadialFootprintMath.FootprintMarkerPose(
                    center, config, _anchorCluster, _anchorRadial, _footprint,
                    out var outPos, out var outRot);
                ApplyBuildingPose(_ghostBuilding, (Vector3)outPos, (Quaternion)outRot);
            }
            else
            {
                RadialFootprintMath.FootprintMarkerPoseFromTurns(
                    center, config, _anchorTurns0, _anchorRadial, _footprint,
                    out var outPos, out var outRot);
                ApplyBuildingPose(_ghostBuilding, (Vector3)outPos, (Quaternion)outRot);
            }

            AdjustBuildingYOffset(_ghostBuilding, center.y + 0.02f);

            if (gridGuide != null)
                UpdateGridMask(center, config);
        }

        /// <summary>
        /// Open band = ring sector wider than the footprint; hole covers the footprint
        /// plus a padding that swallows the model overhang beyond its grid footprint.
        /// Vectors: (rInner, rOuter, thetaCenter, halfAngle).
        /// </summary>
        void UpdateGridMask(float3 center, RadialGridConfig config)
        {
            var n = config.GetClusterCount(_anchorRadial);
            if (n <= 0)
                return;

            var turns0 = _angularSnapped ? _anchorCluster / (float)n : _anchorTurns0;
            turns0 -= Mathf.Floor(turns0);
            var width = _footprint.WidthClusters;
            var thetaCenter = (turns0 + width * 0.5f / n) * (2f * Mathf.PI);

            var radialMargin = gridMaskMarginCells * config.RadialStep;
            var rInner = config.RingLineRadius(_anchorRadial);
            var rOuter = config.RingLineRadius(_anchorRadial + _footprint.DepthRadialRings);
            var band = new Vector4(
                Mathf.Max(0f, rInner - radialMargin),
                rOuter + radialMargin,
                thetaCenter,
                (width * 0.5f + gridMaskSideCells) * (2f * Mathf.PI) / n);

            // The model is bigger than its grid footprint (legs, eaves, base slab).
            // Cut by the actual renderer bounds so no lit grid survives underneath,
            // and clamp to the band so the open window never collapses.
            var hole = new Vector4(
                rInner,
                rOuter,
                thetaCenter,
                width * 0.5f * (2f * Mathf.PI) / n);

            var padCells = Mathf.Max(0f, gridMaskHolePadCells);
            if (TryGetGhostModelPolar(center, thetaCenter, out var modelRMin, out var modelRMax, out var modelHalf))
            {
                var padRadial = padCells * config.RadialStep;
                hole.x = Mathf.Min(hole.x, modelRMin - padRadial);
                hole.y = Mathf.Max(hole.y, modelRMax + padRadial);
                hole.w = Mathf.Max(hole.w, modelHalf + padCells * (2f * Mathf.PI) / n);
            }
            else
            {
                var padRadial = padCells * config.RadialStep;
                hole.x -= padRadial;
                hole.y += padRadial;
                hole.w += padCells * (2f * Mathf.PI) / n;
            }

            hole.x = Mathf.Clamp(hole.x, 0f, band.x);
            hole.y = Mathf.Clamp(hole.y, hole.x + 1e-3f, band.y);
            hole.w = Mathf.Min(hole.w, band.w);

            var clusterWidth = config.ClusterWorldWidth(_anchorRadial);
            var feather = new Vector4(
                MaskFeatherRatio * config.RadialStep,
                MaskFeatherRatio * clusterWidth,
                HoleFeatherRatio * config.RadialStep,
                HoleFeatherRatio * clusterWidth);

            gridGuide.SetGhostSector(center, band, hole, feather);
        }

        /// <summary>
        /// Polar extent of the ghost model itself (oriented mesh bounds, not the
        /// axis-aligned world box), relative to <paramref name="thetaCenter"/>.
        /// </summary>
        bool TryGetGhostModelPolar(
            float3 center, float thetaCenter,
            out float rMin, out float rMax, out float halfAngle)
        {
            rMin = float.MaxValue;
            rMax = 0f;
            halfAngle = 0f;
            if (_ghostRenderers == null)
                return false;

            var found = false;
            for (var i = 0; i < _ghostRenderers.Length; i++)
            {
                var renderer = _ghostRenderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh != null)
                {
                    var local = mesh.bounds;
                    var toWorld = renderer.localToWorldMatrix;
                    for (var iy = 0; iy < 2; iy++)
                    for (var iz = 0; iz < 3; iz++)
                    for (var ix = 0; ix < 3; ix++)
                    {
                        var point = toWorld.MultiplyPoint(new Vector3(
                            Mathf.Lerp(local.min.x, local.max.x, ix * 0.5f),
                            iy == 0 ? local.min.y : local.max.y,
                            Mathf.Lerp(local.min.z, local.max.z, iz * 0.5f)));
                        AccumulatePolar(center, thetaCenter, point, ref rMin, ref rMax, ref halfAngle);
                        found = true;
                    }
                    continue;
                }

                var world = renderer.bounds;
                for (var iz = 0; iz < 3; iz++)
                for (var ix = 0; ix < 3; ix++)
                {
                    var point = new Vector3(
                        Mathf.Lerp(world.min.x, world.max.x, ix * 0.5f),
                        0f,
                        Mathf.Lerp(world.min.z, world.max.z, iz * 0.5f));
                    AccumulatePolar(center, thetaCenter, point, ref rMin, ref rMax, ref halfAngle);
                    found = true;
                }
            }

            return found && rMax > rMin;
        }

        static void AccumulatePolar(
            float3 center, float thetaCenter, Vector3 point,
            ref float rMin, ref float rMax, ref float halfAngle)
        {
            var dx = point.x - center.x;
            var dz = point.z - center.z;
            var radius = Mathf.Sqrt(dx * dx + dz * dz);
            if (radius < rMin) rMin = radius;
            if (radius > rMax) rMax = radius;

            var delta = Mathf.Atan2(dx, dz) - thetaCenter;
            delta -= Mathf.Round(delta / (2f * Mathf.PI)) * 2f * Mathf.PI;
            var abs = Mathf.Abs(delta);
            if (abs > halfAngle) halfAngle = abs;
        }

        void EnsureGhost()
        {
            if (_ghostRoot != null)
                return;
            if (overlayPrefab == null)
            {
                GameLog.Error("BuildPlacementController: assign BuildingOverlay prefab.");
                return;
            }

            _ghostOverlay = Instantiate(overlayPrefab, transform);
            _ghostOverlay.name = "GhostPlacement";
            _ghostRoot = _ghostOverlay.transform;
            if (_ghostOverlay.ZoneCollider != null)
                _ghostOverlay.ZoneCollider.enabled = false;
            _ghostOverlay.SetVisible(false);
            _ghostRoot.gameObject.SetActive(false);
        }

        void RecreateGhostBuilding()
        {
            EnsureGhost();
            if (_ghostRoot == null)
                return;
            if (_ghostBuilding != null)
            {
                Destroy(_ghostBuilding.gameObject);
                _ghostBuilding = null;
            }
            _ghostRenderers = null;

            if (_ghostPrefab == null)
                return;

            var instance = Instantiate(_ghostPrefab, _ghostRoot);
            instance.name = "GhostHouse";
            StripColliders(instance);
            HideWidget(instance);
            ReplaceMaterialsToGhostShader(instance, ghostBuildingMaterial);
            _ghostBuilding = instance.transform;
            _ghostRenderers = instance.GetComponentsInChildren<Renderer>(false);
        }

        static void ReplaceMaterialsToGhostShader(GameObject ghost, Material ghostMaterialTemplate)
        {
            if (ghostMaterialTemplate == null)
                return;

            var renderers = ghost.GetComponentsInChildren<MeshRenderer>();
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!renderer.enabled)
                    continue;

                var origMat = renderer.sharedMaterial;
                if (origMat == null)
                    continue;

                var ghostMat = new Material(ghostMaterialTemplate);
                ghostMat.name = origMat.name + "_Ghost";
                ghostMat.SetTexture("_MainTex", origMat.mainTexture);
                renderer.sharedMaterial = ghostMat;
            }
        }

        static void ApplyBuildingPose(Transform t, Vector3 pos, Quaternion rot)
        {
            t.SetPositionAndRotation(pos, rot);
        }

        static void AdjustBuildingYOffset(Transform building, float groundY)
        {
            var renderers = building.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            var min = float.MaxValue;
            var max = float.MinValue;
            for (var i = 0; i < renderers.Length; i++)
            {
                var b = renderers[i].bounds;
                if (b.min.y < min) min = b.min.y;
                if (b.max.y > max) max = b.max.y;
            }

            var offset = groundY - min;
            if (Mathf.Abs(offset) > 0.001f)
                building.Translate(0f, offset, 0f, Space.World);
        }

        static void HideWidget(GameObject go)
        {
            var widgets = go.GetComponentsInChildren<BuildingWidget>(true);
            for (var i = 0; i < widgets.Length; i++)
                widgets[i].gameObject.SetActive(false);
        }

        static void StripColliders(GameObject go)
        {
            var cols = go.GetComponentsInChildren<Collider>();
            for (var i = 0; i < cols.Length; i++)
                Destroy(cols[i]);
        }

        void SetGhostVisible(bool visible)
        {
            if (_ghostRoot != null)
                _ghostRoot.gameObject.SetActive(visible);
        }

        void ApplyGhostBuildingColor()
        {
            if (_ghostBuilding == null)
                return;

            var renderers = _ghostBuilding.GetComponentsInChildren<MeshRenderer>();
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!renderer.enabled)
                    continue;

                var mat = renderer.sharedMaterial;
                if (mat == null || !mat.HasProperty("_CanBuild"))
                    continue;

                mat.SetFloat("_CanBuild", _canPlace ? 1f : 0f);
            }
        }

        GameObject ResolveGhostPrefab(string typeId)
        {
            return catalog != null ? catalog.FindPrefab(typeId) : null;
        }

        static bool TryGetPointerOnBuildPlane(out Vector3 world)
        {
            world = default;
            var cam = Camera.main;
            if (cam == null || Mouse.current == null)
                return false;
            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            var y = TryGetCityCenter(out var center) ? center.y : 0f;
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            if (!plane.Raycast(ray, out var enter))
                return false;
            world = ray.GetPoint(enter);
            return true;
        }

        static bool TryGetCityCenter(out float3 center)
        {
            center = default;
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasComponent<CityGrid>(bag))
                return false;
            var grid = em.GetComponentData<CityGrid>(bag);
            if (grid.Ready == 0 || !grid.Config.IsValid)
                return false;
            center = grid.Center;
            return true;
        }

        static bool TryResolvePrototype(string typeId, out BuildingPrototype prototype)
        {
            prototype = default;
            var id = ContentId.EncodeOrEmpty(typeId);
            if (id.IsEmpty
                || !SimWorld.TryGet(out var em, out var bag)
                || !em.HasBuffer<BuildingPrototype>(bag))
                return false;
            return BuildingCatalog.TryResolve(em.GetBuffer<BuildingPrototype>(bag), id, out prototype);
        }

        static bool OverlapsOccupied(List<(int cluster, int radial)> clusters)
        {
            if (clusters == null || clusters.Count == 0)
                return false;
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasBuffer<OccupiedCell>(bag))
                return false;
            var occupied = em.GetBuffer<OccupiedCell>(bag);
            for (var i = 0; i < clusters.Count; i++)
            {
                var cell = clusters[i];
                for (var j = 0; j < occupied.Length; j++)
                {
                    if (occupied[j].Cluster == cell.cluster && occupied[j].Radial == cell.radial)
                        return true;
                }
            }

            return false;
        }

        static bool CanAfford(string typeId)
        {
            if (!TryResolvePrototype(typeId, out var spec))
                return true;
            if (!SimWorld.TryGet(out var em, out var bag))
                return true;
            if (!em.HasBuffer<BuildingCatalogCost>(bag))
                return true;
            var costs = em.GetBuffer<BuildingCatalogCost>(bag);
            if (!em.HasBuffer<ResourceAmount>(bag))
                return !BuildingCosts.HasCost(costs, spec.TypeId);
            return BuildingCosts.CanAfford(costs, spec.TypeId, em.GetBuffer<ResourceAmount>(bag));
        }

        void OnDisable()
        {
            if (_placing)
                CancelPlacing();
        }
    }
}
