using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.Session
{
    /// <summary>
    /// Session root: lifecycle state, clock, ID sequences, and command buffers.
    /// Feature-specific content buffers may live on sibling authorings.
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
                AddComponent(entity, new SimSession { Phase = SimSessionPhase.Unprepared });
                AddComponent(entity, new AgentIdSequence());
                AddComponent(entity, new PendingScenarioSpawns());
                AddBuffer<SimClockCommand>(entity);
                AddBuffer<DespawnAllAgentsCommand>(entity);
                AddBuffer<DespawnAllBuildingsCommand>(entity);
            }
        }
    }
}
