using Unity.Entities;

namespace TheyWillDescend.Authoring.Scenario
{
    /// <summary>
    /// Bake-only. Runtime never sees this; the bake system enqueues worker spawns.
    /// </summary>
    public struct ScenarioPopulation : IComponentData
    {
        public int StartingWorkers;
    }
}
