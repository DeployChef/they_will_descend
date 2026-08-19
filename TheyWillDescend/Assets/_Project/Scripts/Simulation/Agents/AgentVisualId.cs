using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Catalog key for the view (prefab name). Not a GameObject, not Transform.
    /// Save stores this; Presentation looks the prefab up.
    /// </summary>
    public struct AgentVisualId : IComponentData
    {
        public FixedString64Bytes Value;
    }
}
