using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.Session
{
    /// <summary>
    /// Session singleton: clock, command buffers, agent stamp.
        /// Grid, building catalog, and resource catalog are sibling authorings on this GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimControlAuthoring : MonoBehaviour
    {
        class SimControlBaker : Baker<SimControlAuthoring>
        {
            public override void Bake(SimControlAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SimControl
                {
                    Mode = SimRunMode.Off,
                    Speed = 1,
                    DeltaTime = 0f
                });
                AddComponent(entity, new SimBridge());
                AddBuffer<SimClockCommand>(entity);
                AddBuffer<SpawnAgentCommand>(entity);
                AddBuffer<PlaceBuildingCommand>(entity);
                AddBuffer<AssignWorkerCommand>(entity);
                AddBuffer<UnassignWorkerCommand>(entity);
                AddBuffer<BuildingRejectedEvent>(entity);
                AddBuffer<PendingScenarioPlace>(entity);

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
