using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Workplace is the player's staffing. Construction is a sim overlay on top:
    /// night (and idle by day) workers walk to a site without losing the house job.
    /// <see cref="Arrived"/> is for the current destination (site if claimed, else workplace).
    /// </summary>
    public struct AgentAssignment : IComponentData
    {
        public int WorkplaceBuildingId;
        public int ConstructionBuildingId;
        public byte Arrived;

        public readonly bool HasConstructionTask => ConstructionBuildingId != 0;

        /// <summary>
        /// Already on the site finishes the house even if the work shift starts.
        /// Still walking + has a workplace + on shift → released back to the job.
        /// </summary>
        public readonly bool CanKeepConstruction(bool onShift) =>
            !onShift || WorkplaceBuildingId == 0 || Arrived != 0;

        public readonly bool IsFreeForConstruction(bool onShift) =>
            ConstructionBuildingId == 0 && (!onShift || WorkplaceBuildingId == 0);
    }
}
