using _Project.Scripts.Simulation.Session;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Project.Scripts.Simulation.Agents
{
    /// <summary>
    /// Copies <see cref="AgentPosition"/> onto the hybrid GameObject Transform.
    /// Does not care how the agent moves — circle, path, or stand still.
    /// </summary>
    [UpdateAfter(typeof(AdvanceCircleWalkSystem))]
    public partial class ApplyAgentPositionPresentationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var animSpeed = 0f;
            if (SystemAPI.TryGetSingleton<SimControl>(out var sim))
                animSpeed = sim.Mode == SimRunMode.Running ? sim.Speed : 0f;

            foreach (var (position, presentation) in SystemAPI.Query<RefRO<AgentPosition>, AgentPresentation>())
            {
                var view = presentation;
                if (view.Transform == null)
                    continue;

                if (view.Animator != null)
                    view.Animator.speed = animSpeed;

                var p = position.ValueRO;
                view.Transform.position = new Vector3(p.Value.x, p.Value.y, p.Value.z);
                if (math.lengthsq(p.Facing) > 0.0001f)
                    view.Transform.rotation = Quaternion.LookRotation(
                        new Vector3(p.Facing.x, p.Facing.y, p.Facing.z));
            }
        }
    }
}
