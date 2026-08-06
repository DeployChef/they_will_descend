using _Project.Scripts.Simulation.Session;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Project.Scripts.Simulation.Agents
{
    /// <summary>
    /// Pushes CircleWalk pose onto GameObject transforms / animator speed.
    /// Managed system (Animator + Transform).
    /// </summary>
    [UpdateAfter(typeof(AdvanceCircleWalkSystem))]
    public partial class ApplyCircleWalkPresentationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var running = false;
            if (SystemAPI.TryGetSingleton<SimControl>(out var sim))
                running = sim.Mode == SimRunMode.Running;

            foreach (var (walk, presentation) in SystemAPI.Query<RefRO<CircleWalk>, AgentPresentation>())
            {
                var view = presentation;
                if (view.Transform == null)
                    continue;

                if (view.Animator != null)
                    view.Animator.speed = running ? 1f : 0f;

                if (!running)
                    continue;

                var w = walk.ValueRO;
                var x = w.Center.x + math.cos(w.AngleRadians) * w.Radius;
                var z = w.Center.z + math.sin(w.AngleRadians) * w.Radius;
                var pos = new Vector3(x, w.Center.y, z);
                view.Transform.position = pos;

                var moveDir = new Vector3(
                    -math.sin(w.AngleRadians) * w.Direction,
                    0f,
                    math.cos(w.AngleRadians) * w.Direction);
                if (moveDir.sqrMagnitude > 0.0001f)
                    view.Transform.rotation = Quaternion.LookRotation(moveDir);
            }
        }
    }
}
