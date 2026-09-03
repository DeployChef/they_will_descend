using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// Cached workshop load for HUD and the research tick. Written each in-game
    /// tick from finished <see cref="ResearchWorkplace"/> houses.
    /// </summary>
    public struct ResearchCapacity : IComponentData
    {
        public float WorkshopLoad;
        public byte HasFinishedWorkshop;

        public bool HasWorkshop => HasFinishedWorkshop != 0;
    }
}
