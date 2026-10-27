using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Session-owned sequence for <see cref="Building.Id"/> (save key and sort order).
    /// Gameplay never looks up a house by this number.
    /// </summary>
    public struct BuildingIdSequence : IComponentData
    {
        public int NextBuildingId;

        public int Take(int requested = 0)
        {
            var id = requested > 0 ? requested : NextBuildingId + 1;
            if (NextBuildingId < id)
                NextBuildingId = id;
            return id;
        }
    }
}
