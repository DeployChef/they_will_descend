using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Click-select a player-constructed building or the central Pyramid.
    /// Plot pick is polar (the white sector), not the overlay AABB quad.
    /// </summary>
    public sealed class BuildingSelection : MonoBehaviour
    {
        [SerializeField] BuildPlacementController placement;

        EntityQuery _buildingQuery;

        public int SelectedBuildingId { get; private set; }
        public int HoveredBuildingId { get; private set; }
        public bool IsPyramidSelected { get; private set; }

        public void Deselect()
        {
            SelectedBuildingId = 0;
            IsPyramidSelected = false;
        }

        public void SelectBuilding(int buildingId)
        {
            SelectedBuildingId = buildingId;
            IsPyramidSelected = false;
        }

        public void SelectPyramid()
        {
            SelectedBuildingId = 0;
            IsPyramidSelected = true;
        }

        public void ClearIf(int buildingId)
        {
            if (SelectedBuildingId == buildingId)
                SelectedBuildingId = 0;
        }

        void Update()
        {
            UpdateHover();
            if (!TryConsumeClick(out var hitBuildingId, out var hitPyramid))
                return;

            if (hitPyramid)
                SelectPyramid();
            else if (hitBuildingId > 0)
                SelectBuilding(hitBuildingId);
            else
                Deselect();
        }

        void OnDestroy()
        {
            _buildingQuery = default;
        }

        void UpdateHover()
        {
            HoveredBuildingId = 0;
            if (Mouse.current == null)
                return;
            if (placement != null && placement.IsPlacing)
                return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            if (!TryPick(out var buildingId, out _))
                return;
            HoveredBuildingId = buildingId;
        }

        bool TryConsumeClick(out int buildingId, out bool hitPyramid)
        {
            buildingId = 0;
            hitPyramid = false;

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            if (placement != null && placement.IsPlacing)
                return false;

            TryPick(out buildingId, out hitPyramid);
            return true;
        }

        bool TryPick(out int buildingId, out bool hitPyramid)
        {
            buildingId = 0;
            hitPyramid = false;
            var cam = Camera.main;
            if (cam == null || Mouse.current == null)
                return false;

            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (TryPickMesh(ray, out buildingId, out hitPyramid))
                return true;

            if (!TryPlanePoint(cam, ray, out var world, out var center, out var config))
                return false;

            if (TryPickFootprint(center, config, world, out buildingId))
                return true;

            var dx = world.x - center.x;
            var dz = world.z - center.z;
            if (math.length(new float2(dx, dz)) < config.InnerRadius)
            {
                hitPyramid = true;
                return true;
            }

            return false;
        }

        static bool TryPickMesh(Ray ray, out int buildingId, out bool hitPyramid)
        {
            buildingId = 0;
            hitPyramid = false;
            var hits = Physics.RaycastAll(ray, 500f);
            var bestDist = float.MaxValue;
            Component best = null;

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.distance >= bestDist)
                    continue;
                if (hit.collider.GetComponentInParent<BuildingOverlay>() != null)
                    continue;

                var py = hit.collider.GetComponentInParent<PyramidView>();
                if (py != null)
                {
                    bestDist = hit.distance;
                    best = py;
                    continue;
                }

                var tag = hit.collider.GetComponentInParent<BuildingIdTag>();
                if (tag != null)
                {
                    bestDist = hit.distance;
                    best = tag;
                }
            }

            if (best is PyramidView)
            {
                hitPyramid = true;
                return true;
            }

            if (best is BuildingIdTag bTag && bTag.Id > 0)
            {
                buildingId = bTag.Id;
                return true;
            }

            return false;
        }

        static bool TryPlanePoint(
            Camera cam, Ray ray,
            out float3 world, out float3 center, out RadialGridConfig config)
        {
            world = default;
            center = default;
            config = default;
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasComponent<CityGrid>(bag))
                return false;
            var grid = em.GetComponentData<CityGrid>(bag);
            if (grid.Ready == 0 || !grid.Config.IsValid)
                return false;
            center = grid.Center;
            config = grid.Config;
            var plane = new Plane(Vector3.up, new Vector3(0f, center.y, 0f));
            if (!plane.Raycast(ray, out var enter))
                return false;
            world = ray.GetPoint(enter);
            return cam != null;
        }

        bool TryPickFootprint(
            float3 center, in RadialGridConfig config, float3 world, out int buildingId)
        {
            buildingId = 0;
            if (!SimWorld.TryGet(out var em, out _))
                return false;

            if (_buildingQuery == default)
                _buildingQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
            if (_buildingQuery.IsEmptyIgnoreFilter)
                return false;

            var buildings = _buildingQuery.ToComponentDataArray<Building>(Allocator.Temp);
            var best = float.MaxValue;
            var picked = 0;
            for (var i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                var footprint = new BuildingFootprint
                {
                    WidthClusters = building.WidthClusters,
                    DepthRadialRings = building.DepthRadialRings
                };
                if (!RadialFootprintMath.ContainsWorldPoint(
                        center, config, world,
                        building.AnchorCluster, building.AnchorRadial, footprint))
                    continue;

                RadialFootprintMath.FootprintMarkerPose(
                    center, config,
                    building.AnchorCluster, building.AnchorRadial, footprint,
                    out var pose, out _);
                var d = math.lengthsq(new float2(world.x - pose.x, world.z - pose.z));
                if (d >= best)
                    continue;
                best = d;
                picked = building.Id;
            }

            buildings.Dispose();
            if (picked <= 0)
                return false;
            buildingId = picked;
            return true;
        }
    }
}
