using Unity.Entities;

namespace TheyWillDescend.Simulation.Research
{
    /// <summary>
    /// Session points at the research singleton. Catalog/progress cannot live on
    /// the session archetype — it is already at the chunk size limit.
    /// </summary>
    public struct ResearchLink : IComponentData
    {
        public Entity Entity;
    }
}
