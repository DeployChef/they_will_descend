using _Project.Scripts.Simulation.Session;
using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.Simulation.Agents
{
    /// <summary>
    /// Moves agents that have <see cref="CircleWalk"/> by writing <see cref="AgentPosition"/>.
    /// </summary>
    public partial struct AdvanceCircleWalkSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<SimControl>(out var sim))
                return;

            var dt = sim.DeltaTime;
            if (dt <= 0f)
                return;

            foreach (var (walk, position) in SystemAPI.Query<RefRW<CircleWalk>, RefRW<AgentPosition>>())
            {
                ref var w = ref walk.ValueRW;
                w.AngleRadians += w.Speed * w.Direction * dt * math.PI * 2f;
                position.ValueRW = w.ToPosition();
            }
        }
    }
}
