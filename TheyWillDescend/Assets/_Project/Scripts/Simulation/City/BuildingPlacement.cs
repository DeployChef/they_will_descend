using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Research;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.City
{
    public enum BuildingPlaceMode : byte
    {
        Gameplay = 0,
        Setup = 1,
        Restore = 2
    }

    public struct BuildingPlaceSpec
    {
        public FixedString64Bytes TypeId;
        public int AnchorCluster;
        public int AnchorRadial;
        public int BuildingId;
        public float ConstructionElapsed;
        public float ConstructionDuration;
        public byte InstantComplete;
        public byte Dismantling;
        public int DesiredWorkers;
        public byte Paused;
        public BuildingPlaceMode Mode;
    }

    /// <summary>
    /// Shared house spawn for player place, scenario seed, and save restore.
    /// Validation depends on <see cref="BuildingPlaceMode"/>.
    /// </summary>
    public static class BuildingPlacement
    {
        public static void CopyCatalog(
            EntityManager em,
            Entity session,
            out BuildingPrototype[] catalog,
            out BuildingCatalogRecipe[] recipes)
        {
            em.CompleteAllTrackedJobs();
            catalog = CopyBuffer<BuildingPrototype>(em, session);
            recipes = CopyBuffer<BuildingCatalogRecipe>(em, session);
        }

        public static bool TryPlace(
            EntityManager em,
            Entity session,
            ref CityGrid grid,
            in BuildingPlaceSpec spec,
            BuildingPrototype[] catalog,
            BuildingCatalogRecipe[] recipes)
        {
            em.CompleteAllTrackedJobs();
            if (!BuildingCatalog.TryResolve(catalog, spec.TypeId, out var stamp))
            {
                Reject(em, session, spec, BuildingRejectedEvent.UnknownType);
                return false;
            }

            if (stamp.StrokePaint != 0)
            {
                Reject(em, session, spec, BuildingRejectedEvent.StrokePaint);
                return false;
            }

            if (stamp.RequiresUnlock != 0
                && spec.Mode == BuildingPlaceMode.Gameplay
                && !ResearchRules.IsBuildingUnlocked(em, stamp.TypeId))
            {
                Reject(em, session, spec, BuildingRejectedEvent.Locked);
                return false;
            }

            var footprint = stamp.Footprint;
            var clusters = new NativeList<OccupiedCell>(64, Allocator.Temp);
            if (grid.Ready == 0
                || !RadialFootprintMath.TryExpandClusters(
                    grid.Config, spec.AnchorCluster, spec.AnchorRadial, footprint, clusters))
            {
                Reject(em, session, spec, BuildingRejectedEvent.InvalidCell);
                clusters.Dispose();
                return false;
            }

            var occupiedCopy = em.GetBuffer<OccupiedCell>(session).ToNativeArray(Allocator.Temp);
            if (Overlaps(occupiedCopy, clusters))
            {
                Reject(em, session, spec, BuildingRejectedEvent.Overlap);
                occupiedCopy.Dispose();
                clusters.Dispose();
                return false;
            }

            NativeArray<RoadSegment> roadsCopy = default;
            if (em.HasBuffer<RoadSegment>(session))
                roadsCopy = em.GetBuffer<RoadSegment>(session).ToNativeArray(Allocator.Temp);
            if (roadsCopy.IsCreated
                && RoadMath.FootprintBlockedByRoads(grid.Config, roadsCopy, clusters.AsArray()))
            {
                Reject(em, session, spec, BuildingRejectedEvent.Overlap);
                if (roadsCopy.IsCreated)
                    roadsCopy.Dispose();
                occupiedCopy.Dispose();
                clusters.Dispose();
                return false;
            }

            if (roadsCopy.IsCreated)
                roadsCopy.Dispose();

            var recipeLines = CollectRecipes(recipes, stamp.TypeId);

            if (!TryPay(em, session, stamp.TypeId, spec))
            {
                Reject(em, session, spec, BuildingRejectedEvent.Unaffordable);
                occupiedCopy.Dispose();
                clusters.Dispose();
                recipeLines.Dispose();
                return false;
            }

            occupiedCopy.Dispose();

            var id = spec.BuildingId > 0 ? spec.BuildingId : grid.NextBuildingId + 1;
            if (grid.NextBuildingId < id)
                grid.NextBuildingId = id;

            em.CompleteAllTrackedJobs();
            var occupied = em.GetBuffer<OccupiedCell>(session);
            for (var i = 0; i < clusters.Length; i++)
            {
                var cell = clusters[i];
                cell.BuildingId = id;
                occupied.Add(cell);
            }

            clusters.Dispose();

            RadialFootprintMath.FootprintMarkerPose(
                grid.Center, grid.Config, spec.AnchorCluster, spec.AnchorRadial, footprint,
                out var position, out var rotation);

            var building = new Building
            {
                Id = id,
                TypeId = stamp.TypeId,
                WidthClusters = footprint.WidthClusters,
                DepthRadialRings = footprint.DepthRadialRings,
                AnchorCluster = spec.AnchorCluster,
                AnchorRadial = spec.AnchorRadial
            };
            var transform = LocalTransform.FromPositionRotationScale(position, rotation, 1f);
            var duration = spec.ConstructionDuration > 0.001f
                ? spec.ConstructionDuration
                : stamp.ConstructionDuration;

            Construction? construction = null;
            if (spec.InstantComplete == 0)
            {
                var site = new Construction
                {
                    Elapsed = math.max(0f, spec.ConstructionElapsed),
                    Duration = duration,
                    Dismantling = spec.Dismantling
                };
                if (site.IsDismantling && site.Elapsed <= 0.0001f && duration > 0.001f)
                    site.Elapsed = duration;
                if (!site.IsComplete)
                    construction = site;
            }

            var desired = spec.Mode == BuildingPlaceMode.Restore ? spec.DesiredWorkers : 0;
            var paused = spec.Mode == BuildingPlaceMode.Restore ? spec.Paused : (byte)0;
            SpawnHouse(em, stamp, building, transform, construction, recipeLines, desired, paused);
            recipeLines.Dispose();
            return true;
        }

        public static Entity SpawnHouse(
            EntityManager em,
            in BuildingPrototype stamp,
            in Building building,
            LocalTransform transform,
            Construction? construction,
            NativeList<BuildingRecipeLine> recipes,
            int desiredWorkers = 0,
            byte paused = 0)
        {
            em.CompleteAllTrackedJobs();
            var entity = em.CreateEntity();
            RunWorld.Own(em, entity);
            em.AddComponentData(entity, building);
            em.AddComponentData(entity, stamp.ToBuildingType());
            if (stamp.WorkplaceSlots > 0)
            {
                em.AddComponentData(entity, new Workplace
                {
                    DesiredWorkers = desiredWorkers,
                    Paused = paused
                });
            }

            if (stamp.ResearchWorkplace != 0)
                em.AddComponentData(entity, new ResearchWorkplace());
            if (recipes.IsCreated && recipes.Length > 0)
            {
                var dest = em.AddBuffer<BuildingRecipeLine>(entity);
                for (var i = 0; i < recipes.Length; i++)
                    dest.Add(recipes[i]);
            }

            SimEntityPose.Apply(em, entity, transform);
            if (construction.HasValue)
                em.AddComponentData(entity, construction.Value);
#if UNITY_EDITOR
            em.SetName(entity, construction.HasValue
                ? $"BuildingSite_{building.Id}"
                : $"Building_{building.Id}");
#endif
            return entity;
        }

        static T[] CopyBuffer<T>(EntityManager em, Entity session)
            where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(session))
                return System.Array.Empty<T>();
            var src = em.GetBuffer<T>(session);
            if (src.Length == 0)
                return System.Array.Empty<T>();
            var dest = new T[src.Length];
            for (var i = 0; i < src.Length; i++)
                dest[i] = src[i];
            return dest;
        }

        static NativeList<BuildingRecipeLine> CollectRecipes(
            BuildingCatalogRecipe[] src,
            in FixedString64Bytes typeId)
        {
            var lines = new NativeList<BuildingRecipeLine>(8, Allocator.Temp);
            if (src == null || src.Length == 0)
                return lines;

            for (var i = 0; i < src.Length; i++)
            {
                var row = src[i];
                if (row.TypeId != typeId || row.PerHour <= 0.0001f)
                    continue;
                lines.Add(new BuildingRecipeLine
                {
                    Kind = row.Kind,
                    ResourceId = row.ResourceId,
                    PerHour = row.PerHour
                });
            }

            return lines;
        }

        static bool TryPay(
            EntityManager em,
            Entity session,
            in FixedString64Bytes typeId,
            in BuildingPlaceSpec spec)
        {
            if (spec.Mode != BuildingPlaceMode.Gameplay)
                return true;
            if (spec.InstantComplete != 0 || spec.BuildingId > 0)
                return true;
            if (!em.HasBuffer<BuildingCatalogCost>(session))
                return true;

            var costs = em.GetBuffer<BuildingCatalogCost>(session);
            if (!em.HasBuffer<ResourceAmount>(session))
                return !BuildingCosts.HasCost(costs, typeId);

            var stock = em.GetBuffer<ResourceAmount>(session);
            if (!BuildingCosts.CanAfford(costs, typeId, stock))
                return false;

            BuildingCosts.Pay(costs, typeId, stock);
            return true;
        }

        static void Reject(
            EntityManager em,
            Entity session,
            in BuildingPlaceSpec spec,
            byte reason)
        {
            if (!em.HasBuffer<BuildingRejectedEvent>(session))
                return;
            em.GetBuffer<BuildingRejectedEvent>(session).Add(new BuildingRejectedEvent
            {
                AnchorCluster = spec.AnchorCluster,
                AnchorRadial = spec.AnchorRadial,
                Reason = reason
            });
        }

        static bool Overlaps(NativeArray<OccupiedCell> occupied, NativeList<OccupiedCell> clusters)
        {
            for (var i = 0; i < clusters.Length; i++)
            {
                var cell = clusters[i];
                for (var j = 0; j < occupied.Length; j++)
                {
                    if (occupied[j].Cluster == cell.Cluster && occupied[j].Radial == cell.Radial)
                        return true;
                }
            }

            return false;
        }
    }
}
