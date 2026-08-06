using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Simulation.Agents;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Project.Scripts.Presentation.Agents
{
    /// <summary>
    /// Spawns a simulation entity for this GameObject and keeps Animator on the GO.
    /// Lives on Game scene humans (not SubScene bake) — hybrid by design for skinned characters.
    /// </summary>
    public sealed class CircleWalkAgent : MonoBehaviour
    {
        [SerializeField] float radius = 5f;
        [SerializeField] float speed = 0.5f;
        [SerializeField] float direction = 1f;
        [SerializeField] float circleHeightOffset;
        [SerializeField] string walkBoolParameter = "Walk 1";

        Entity _entity;
        World _world;
        bool _registered;

        /// <summary>Call while inactive (before OnEnable) so registration picks up values.</summary>
        public void ApplySettings(float walkRadius, float walkSpeed, float walkDirection, float heightOffset = 0f)
        {
            radius = walkRadius;
            speed = walkSpeed;
            direction = walkDirection;
            circleHeightOffset = heightOffset;
        }

        void OnEnable() => TryRegister();

        void Start()
        {
            TryRegister();
            EnsureWalkAnimation();
        }

        void Update()
        {
            // Game may load after Default World exists; register as soon as World is ready.
            if (!_registered)
                TryRegister();
        }

        void OnDisable() => Unregister();

        void TryRegister()
        {
            if (_registered)
                return;

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
                return;

            var em = _world.EntityManager;
            var center = transform.position;
            center.y += circleHeightOffset;

            _entity = em.CreateEntity();
            em.AddComponentData(_entity, new CircleWalk
            {
                Center = (float3)center,
                Radius = radius,
                Speed = speed,
                Direction = direction,
                AngleRadians = 0f
            });
            em.AddComponentObject(_entity, new AgentPresentation
            {
                Transform = transform,
                Animator = GetComponent<Animator>()
            });

            _registered = true;
            GameLog.Info(LogChannel.City, $"CircleWalkAgent registered: {name}");
        }

        void Unregister()
        {
            if (!_registered || _world == null || !_world.IsCreated)
            {
                _registered = false;
                return;
            }

            var em = _world.EntityManager;
            if (em.Exists(_entity))
                em.DestroyEntity(_entity);

            _registered = false;
            _entity = Entity.Null;
        }

        void EnsureWalkAnimation()
        {
            var animator = GetComponent<Animator>();
            if (animator == null || string.IsNullOrEmpty(walkBoolParameter))
                return;

            foreach (var p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool)
                    animator.SetBool(p.name, p.name == walkBoolParameter);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            var c = transform.position;
            c.y += circleHeightOffset;
            Gizmos.DrawWireSphere(c, radius);
        }
    }
}
