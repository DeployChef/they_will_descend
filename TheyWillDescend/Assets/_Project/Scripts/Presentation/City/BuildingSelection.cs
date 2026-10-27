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
        [SerializeField] RoadPaintController roadPaint;

        public Entity Selected { get; private set; }
        public Entity Hovered { get; private set; }
        public bool IsPyramidSelected { get; private set; }

        public void Deselect()
        {
            Selected = Entity.Null;
            IsPyramidSelected = false;
        }

        public void SelectBuilding(Entity building)
        {
            Selected = building;
            IsPyramidSelected = false;
        }

        public void SelectPyramid()
        {
            Selected = Entity.Null;
            IsPyramidSelected = true;
        }

        public void ClearIf(Entity building)
        {
            if (Selected == building)
                Selected = Entity.Null;
        }

        void Update()
        {
            UpdateHover();
            if (!TryConsumeClick(out var hitBuilding, out var hitPyramid))
                return;

            if (hitPyramid)
                SelectPyramid();
            else if (hitBuilding != Entity.Null)
                SelectBuilding(hitBuilding);
            else
                Deselect();
        }

        void UpdateHover()
        {
            Hovered = Entity.Null;
            if (Mouse.current == null)
                return;
            if (placement != null && placement.IsPlacing)
                return;
            if (roadPaint != null && roadPaint.IsPainting)
                return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            var cam = Camera.main;
            if (cam == null)
                return;
            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!TryPickMesh(ray, out var building, out var hitPyramid) || hitPyramid)
                return;
            Hovered = building;
        }

        bool TryConsumeClick(out Entity building, out bool hitPyramid)
        {
            building = Entity.Null;
            hitPyramid = false;

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            if (placement != null && placement.IsPlacing)
                return false;
            if (roadPaint != null && roadPaint.IsPainting)
                return false;

            TryPick(out building, out hitPyramid);
            return true;
        }

        bool TryPick(out Entity building, out bool hitPyramid)
        {
            building = Entity.Null;
            hitPyramid = false;
            var cam = Camera.main;
            if (cam == null || Mouse.current == null)
                return false;

            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (TryPickMesh(ray, out building, out hitPyramid))
                return true;

            if (!TryPlanePoint(cam, ray, out var world, out var center, out var config))
                return false;

            if (TryPickFootprint(center, config, world, out building))
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

        static bool TryPickMesh(Ray ray, out Entity building, out bool hitPyramid)
        {
            building = Entity.Null;
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

            if (best is BuildingIdTag bTag && bTag.Target != Entity.Null)
            {
                building = bTag.Target;
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
            float3 center, in RadialGridConfig config, float3 world, out Entity building)
        {
            building = Entity.Null;
            if (!SimWorld.TryGet(out var em, out _))
                return false;

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
            if (query.IsEmptyIgnoreFilter)
                return false;

            var houses = query.ToEntityArray(Allocator.Temp);
            var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
            var best = float.MaxValue;
            var picked = Entity.Null;
            for (var i = 0; i < buildings.Length; i++)
            {
                var row = buildings[i];
                var footprint = new BuildingFootprint
                {
                    WidthClusters = row.WidthClusters,
                    DepthRadialRings = row.DepthRadialRings
                };
                if (!RadialFootprintMath.ContainsWorldPoint(
                        center, config, world,
                        row.AnchorCluster, row.AnchorRadial, footprint))
                    continue;

                RadialFootprintMath.FootprintMarkerPose(
                    center, config,
                    row.AnchorCluster, row.AnchorRadial, footprint,
                    out var pose, out _);
                var d = math.lengthsq(new float2(world.x - pose.x, world.z - pose.z));
                if (d >= best)
                    continue;
                best = d;
                picked = houses[i];
            }

            houses.Dispose();
            buildings.Dispose();
            if (picked == Entity.Null)
                return false;
            building = picked;
            return true;
        }
    }
}
