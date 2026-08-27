using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.Agents
{
    /// <summary>
    /// Worker command buffers and the agent stamp. Must sit on the same GO as SimControlAuthoring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AgentSessionAuthoring : MonoBehaviour
    {
        class Baker : Baker<AgentSessionAuthoring>
        {
            public override void Bake(AgentSessionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddBuffer<SpawnAgentCommand>(entity);
                AddBuffer<AssignWorkerCommand>(entity);
                AddBuffer<UnassignWorkerCommand>(entity);
                AddBuffer<SetWorkplacePausedCommand>(entity);

                var agentPrototype = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
                AddComponent<Prefab>(agentPrototype);
                AddComponent(agentPrototype, new AgentLocomotion { Speed = 2f });
                AddComponent<AgentAssignment>(agentPrototype);
                AddComponent<AgentPlazaIdle>(agentPrototype);
                AddComponent(agentPrototype, new AgentId());
                AddComponent(agentPrototype, new AgentType { Kind = AgentKind.Worker });
                AddComponent(entity, new SimPrototypes { Agent = agentPrototype });
            }
        }
    }
}
