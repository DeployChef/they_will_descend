using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Writes LocalTransform. Entities Graphics would draw from this; Animator views copy it.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Io.CommandSystemGroup))]
    public partial struct AdvanceCircleWalkSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<SimControl>(out var sim))
                return;

            var dt = sim.DeltaTime;
            if (dt <= 0f)
                return;

            foreach (var (walk, transform) in
                     SystemAPI.Query<RefRW<CircleWalk>, RefRW<LocalTransform>>())
            {
                ref var w = ref walk.ValueRW;
                w.AngleRadians += w.Speed * w.Direction * dt * math.PI * 2f;
                transform.ValueRW = w.ToLocalTransform();
            }
        }
    }
}
