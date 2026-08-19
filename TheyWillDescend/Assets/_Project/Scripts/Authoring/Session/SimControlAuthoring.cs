using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.Session
{
    /// <summary>
    /// Bakes the SimControl singleton. Default mode is Off (Shell opens the gate later).
    /// </summary>
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
            }
        }
    }
}
