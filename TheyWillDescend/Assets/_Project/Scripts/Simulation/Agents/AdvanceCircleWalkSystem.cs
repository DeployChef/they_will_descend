using _Project.Scripts.Simulation.Session;
using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.Simulation.Agents
{
    /// <summary>
    /// Advances circle walk only while <see cref="SimRunMode.Running"/>.
    /// </summary>
    public partial struct AdvanceCircleWalkSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<SimControl>(out var sim) || sim.Mode != SimRunMode.Running)
                return;

            var dt = SystemAPI.Time.DeltaTime;

            foreach (var walk in SystemAPI.Query<RefRW<CircleWalk>>())
            {
                ref var w = ref walk.ValueRW;
                w.AngleRadians += w.Speed * w.Direction * dt * math.PI * 2f;
            }
        }
    }
}
