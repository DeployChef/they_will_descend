using _Project.Scripts.Simulation.Session;
using Unity.Entities;
using UnityEngine;

namespace _Project.Scripts.Shell
{
    /// <summary>
    /// Bridge: copies Shell clock policy into ECS <see cref="SimControl"/> every frame (DeltaTime changes).
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SimControlSyncSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var gate = SimGate.Active;
            if (gate == null)
                return;

            if (!SystemAPI.TryGetSingletonRW<SimControl>(out var control))
                return;

            var mode = gate.EffectiveMode;
            var speed = gate.Speed;
            var dt = mode == SimRunMode.Running
                ? UnityEngine.Time.deltaTime * speed
                : 0f;

            ref var value = ref control.ValueRW;
            value.Mode = mode;
            value.Speed = speed;
            value.DeltaTime = dt;
        }
    }
}
