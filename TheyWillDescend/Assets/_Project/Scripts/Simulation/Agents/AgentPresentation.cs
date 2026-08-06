using Unity.Entities;
using UnityEngine;

namespace _Project.Scripts.Simulation.Agents
{
    /// <summary>
    /// Managed link to the skinned/animated GameObject on the Game scene.
    /// Canon for this slice: ECS owns motion; Animator stays on the GO (hybrid).
    /// Why not bake the whole human into SubScene: SkinnedMesh+Animator don't bake cleanly yet.
    /// </summary>
    public sealed class AgentPresentation : IComponentData
    {
        public Transform Transform;
        public Animator Animator;
    }
}
