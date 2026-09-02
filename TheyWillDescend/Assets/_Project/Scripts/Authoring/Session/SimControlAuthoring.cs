using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.Session
{
    /// <summary>
    /// Session singleton: clock and the bag entity. Domain buffers live on sibling authorings.
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
                    DeltaTime = 0f,
                    RunPrepared = 0
                });
                AddComponent(entity, new SimBridge());
                AddBuffer<SimClockCommand>(entity);
            }
        }
    }
}
