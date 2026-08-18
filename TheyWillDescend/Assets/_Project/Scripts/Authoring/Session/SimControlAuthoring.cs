using _Project.Scripts.Simulation.Session;
using Unity.Entities;
using UnityEngine;

namespace _Project.Scripts.Authoring.Session
{
    /// <summary>
    /// Bakes the SimControl singleton. Default mode is Off (Shell opens the gate later).
    /// </summary>
    public sealed class SimControlAuthoring : MonoBehaviour
    {
        class Baker : Baker<SimControlAuthoring>
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
