using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Session catalog row: numbers for one house type. Not a pointer to a baked
    /// art prefab — Place copies this onto a new entity.
    /// </summary>
    public struct BuildingPrototype : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public int WidthClusters;
        public int DepthRadialRings;
        public float ConstructionDuration;
        public int WorkplaceSlots;
        public int ConstructionCrewSlots;
        public byte ResearchWorkplace;
        public byte RequiresUnlock;

        public BuildingFootprint Footprint => new()
        {
            WidthClusters = WidthClusters,
            DepthRadialRings = DepthRadialRings
        };

        public BuildingType ToBuildingType() => new()
        {
            TypeId = TypeId,
            WidthClusters = WidthClusters,
            DepthRadialRings = DepthRadialRings,
            ConstructionDuration = ConstructionDuration,
            WorkplaceSlots = WorkplaceSlots,
            ConstructionCrewSlots = ConstructionCrew.ResolveSlots(ConstructionCrewSlots)
        };
    }

    /// <summary>
    /// Immutable prefab defaults baked onto the session. Run setup copies these
    /// rows into <see cref="BuildingPrototype"/> before applying overlays.
    /// </summary>
    public struct BaseBuildingPrototype : IBufferElementData
    {
        public FixedString64Bytes TypeId;
        public int WidthClusters;
        public int DepthRadialRings;
        public float ConstructionDuration;
        public int WorkplaceSlots;
        public int ConstructionCrewSlots;
        public byte ResearchWorkplace;
        public byte RequiresUnlock;

        public BuildingPrototype ToResolved() => new()
        {
            TypeId = TypeId,
            WidthClusters = WidthClusters,
            DepthRadialRings = DepthRadialRings,
            ConstructionDuration = ConstructionDuration,
            WorkplaceSlots = WorkplaceSlots,
            ConstructionCrewSlots = ConstructionCrew.ResolveSlots(ConstructionCrewSlots),
            ResearchWorkplace = ResearchWorkplace,
            RequiresUnlock = RequiresUnlock
        };
    }

    public static class BuildingCatalog
    {
        public static bool TryResolve(
            DynamicBuffer<BuildingPrototype> catalog,
            in FixedString64Bytes typeId,
            out BuildingPrototype prototype)
        {
            prototype = default;
            if (typeId.IsEmpty || catalog.Length == 0)
                return false;

            for (var i = 0; i < catalog.Length; i++)
            {
                if (catalog[i].TypeId != typeId)
                    continue;
                prototype = catalog[i];
                return true;
            }

            return false;
        }
    }
}
