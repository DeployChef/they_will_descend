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
        [SerializeField] string prefabId;

        Entity _entity;
        World _world;
        bool _registered;

        CircleWalk _pendingWalk;
        AgentPosition _pendingPosition;
        bool _hasPendingWalk;

        public string PrefabId => string.IsNullOrEmpty(prefabId) ? name : prefabId;

        /// <summary>Call while inactive (before OnEnable) so registration picks up values.</summary>
        public void ApplySettings(
            float walkRadius,
            float walkSpeed,
            float walkDirection,
            float heightOffset,
            string contentId)
        {
            radius = walkRadius;
            speed = walkSpeed;
            direction = walkDirection;
            circleHeightOffset = heightOffset;
            prefabId = contentId;
        }

        public void ApplyWalk(in CircleWalk walk, in AgentPosition position)
        {
            radius = walk.Radius;
            speed = walk.Speed;
            direction = walk.Direction;
            _pendingWalk = walk;
            _pendingPosition = position;
            _hasPendingWalk = true;
            ApplyPosition(position);
            WriteSimIfRegistered(walk, position);
        }

        public void ApplyWalk(in CircleWalk walk) => ApplyWalk(walk, walk.ToPosition());

        void ApplyPosition(in AgentPosition position)
        {
            transform.position = new Vector3(position.Value.x, position.Value.y, position.Value.z);
            if (math.lengthsq(position.Facing) > 0.0001f)
                transform.rotation = Quaternion.LookRotation(
                    new Vector3(position.Facing.x, position.Facing.y, position.Facing.z));
        }

        void WriteSimIfRegistered(in CircleWalk walk, in AgentPosition position)
        {
            if (!_registered || _world == null || !_world.IsCreated)
                return;

            var em = _world.EntityManager;
            if (!em.Exists(_entity))
                return;
            if (em.HasComponent<CircleWalk>(_entity))
                em.SetComponentData(_entity, walk);
            if (em.HasComponent<AgentPosition>(_entity))
                em.SetComponentData(_entity, position);
        }

        void OnEnable() => TryRegister();

        void Start()
        {
            TryRegister();
            EnsureWalkAnimation();
        }

        void Update()
        {
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
            CircleWalk walk;
            AgentPosition position;
            if (_hasPendingWalk)
            {
                walk = _pendingWalk;
                position = _pendingPosition;
            }
            else
            {
                var center = transform.position;
                center.y += circleHeightOffset;
                walk = new CircleWalk
                {
                    Center = (float3)center,
                    Radius = radius,
                    Speed = speed,
                    Direction = direction,
                    AngleRadians = 0f
                };
                position = walk.ToPosition();
            }

            _entity = em.CreateEntity();
            em.AddComponentData(_entity, walk);
            em.AddComponentData(_entity, position);
            em.AddComponentData(_entity, new AgentPrefabId
            {
                Value = PrefabId
            });
            em.AddComponentObject(_entity, new AgentPresentation
            {
                Transform = transform,
                Animator = GetComponent<Animator>()
            });
#if UNITY_EDITOR
            em.SetName(_entity, name);
#endif

            _registered = true;
            ApplyPosition(position);
            GameLog.Info($"CircleWalkAgent registered: {name} id={PrefabId}");
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
