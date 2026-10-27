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
        public Entity Workplace;
        public Entity ConstructionSite;
        public byte Arrived;

        public readonly bool HasConstructionTask => ConstructionSite != Entity.Null;

        /// <summary>
        /// Already on the site finishes the house even if the work shift starts.
        /// Still walking + has a workplace + on shift → released back to the job.
        /// </summary>
        public readonly bool CanKeepConstruction(bool onShift) =>
            !onShift || Workplace == Entity.Null || Arrived != 0;

        public readonly bool IsFreeForConstruction(bool onShift) =>
            ConstructionSite == Entity.Null && (!onShift || Workplace == Entity.Null);
    }
}
