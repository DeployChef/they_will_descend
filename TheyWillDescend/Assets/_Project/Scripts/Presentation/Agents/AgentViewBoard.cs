using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace TheyWillDescend.Presentation.Agents
{
    /// <summary>
    /// Mixamo GO copies LocalTransform and Moving. Animator stays on the GO.
    /// Existence is pulled from the query — no spawn events.
    /// </summary>
    public sealed class AgentViewBoard : MonoBehaviour
    {
        [SerializeField] GameObject[] characterPrefabs;
        [SerializeField] Transform spawnParent;

        readonly Dictionary<int, AgentView> _views = new();
        readonly HashSet<int> _seen = new();
        readonly List<int> _stale = new();
        readonly Dictionary<GameObject, Stack<AgentView>> _pool = new();
        EntityQuery _agentQuery;

        void LateUpdate() => Pump();

        public void Pump()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;
            if (_agentQuery == default)
            {
                _agentQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<AgentId>(),
                    ComponentType.ReadOnly<AgentType>(),
                    ComponentType.ReadOnly<AgentLocomotion>(),
                    ComponentType.ReadOnly<AgentAssignment>(),
                    ComponentType.ReadOnly<LocalTransform>());
            }
            Sync(_agentQuery);
        }

        void OnDisable()
        {
            ClearViews();
        }

        void OnDestroy()
        {
            _agentQuery = default;
            ClearPool();
        }

        void ClearPool()
        {
            foreach (var stack in _pool.Values)
            {
                while (stack.Count > 0)
                {
                    var view = stack.Pop();
                    if (view != null)
                        DestroyGo(view.gameObject);
                }
            }
            _pool.Clear();
        }

        public void ClearViews()
        {
            foreach (var pair in _views)
            {
                var view = pair.Value;
                if (view == null)
                    continue;

                view.gameObject.SetActive(false);
                var prefab = ResolvePrefab(AgentKind.Worker, pair.Key);
                if (prefab != null)
                {
                    if (!_pool.TryGetValue(prefab, out var stack))
                    {
                        stack = new Stack<AgentView>();
                        _pool[prefab] = stack;
                    }
                    stack.Push(view);
                }
                else
                {
                    DestroyGo(view.gameObject);
                }
            }

            _views.Clear();
        }


        void Sync(EntityQuery query)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                if (_views.Count > 0)
                    ClearViews();
                return;
            }

            var ids = query.ToComponentDataArray<AgentId>(Allocator.Temp);
            var types = query.ToComponentDataArray<AgentType>(Allocator.Temp);
            var motors = query.ToComponentDataArray<AgentLocomotion>(Allocator.Temp);
            var assignments = query.ToComponentDataArray<AgentAssignment>(Allocator.Temp);
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var animSpeed = AnimSpeed();
            _seen.Clear();
            for (var i = 0; i < ids.Length; i++)
            {
                var id = ids[i].Value;
                _seen.Add(id);
                if (!_views.TryGetValue(id, out var view) || view == null)
                    view = CreateView(id, types[i].Kind, transforms[i]);
                if (view == null)
                    continue;
                var onField = assignments[i].Arrived == 0;
                view.SetOnField(onField);
                if (!onField)
                    continue;
                ApplyPose(view.transform, transforms[i]);
                view.SetMoving(motors[i].Moving != 0);
                view.SetAnimSpeed(animSpeed);
            }

            if (_views.Count != _seen.Count)
            {
                _stale.Clear();
                foreach (var pair in _views)
                {
                    if (!_seen.Contains(pair.Key))
                        _stale.Add(pair.Key);
                }

                for (var i = 0; i < _stale.Count; i++)
                    DestroyView(_stale[i]);
            }

            ids.Dispose();
            types.Dispose();
            motors.Dispose();
            assignments.Dispose();
            transforms.Dispose();
        }

        AgentView CreateView(int agentId, AgentKind kind, LocalTransform transform)
        {
            var prefab = ResolvePrefab(kind, agentId);
            if (prefab == null)
            {
                GameLog.Error("AgentViewBoard: no character prefabs.");
                return null;
            }

            AgentView view = null;
            if (_pool.TryGetValue(prefab, out var stack) && stack.Count > 0)
            {
                while (stack.Count > 0 && view == null)
                {
                    view = stack.Pop();
                }

                if (view != null)
                {
                    view.gameObject.name = $"{prefab.name}_{agentId}";
                    view.transform.SetPositionAndRotation((Vector3)transform.Position, (Quaternion)transform.Rotation);
                    view.gameObject.SetActive(true);
                }
            }

            if (view == null)
            {
                var instance = Instantiate(
                    prefab,
                    (Vector3)transform.Position,
                    (Quaternion)transform.Rotation);
                instance.name = $"{prefab.name}_{agentId}";
                if (spawnParent != null)
                    instance.transform.SetParent(spawnParent, true);

                view = instance.GetComponent<AgentView>();
                if (view == null)
                    view = instance.AddComponent<AgentView>();
                view.Bind();
            }

            _views[agentId] = view;
            return view;
        }

        void DestroyView(int agentId)
        {
            if (!_views.TryGetValue(agentId, out var view))
                return;
            _views.Remove(agentId);
            if (view == null)
                return;

            view.gameObject.SetActive(false);
            var prefab = ResolvePrefab(AgentKind.Worker, agentId);
            if (prefab != null)
            {
                if (!_pool.TryGetValue(prefab, out var stack))
                {
                    stack = new Stack<AgentView>();
                    _pool[prefab] = stack;
                }
                stack.Push(view);
            }
            else
            {
                DestroyGo(view.gameObject);
            }
        }


        static void DestroyGo(GameObject go)
        {
            if (go == null)
                return;
            go.SetActive(false);
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }


        GameObject ResolvePrefab(AgentKind kind, int agentId)
        {
            if (characterPrefabs == null || characterPrefabs.Length == 0)
                return null;
            if (kind != AgentKind.Worker)
                return characterPrefabs[0];

            var index = agentId % characterPrefabs.Length;
            if (index < 0)
                index += characterPrefabs.Length;
            return characterPrefabs[index] != null ? characterPrefabs[index] : characterPrefabs[0];
        }

        static float AnimSpeed()
        {
            if (!SimWorld.TryGet(out var em, out var bag))
                return 0f;
            var control = em.GetComponentData<SimControl>(bag);
            return control.IsRunning ? control.Speed : 0f;
        }

        static void ApplyPose(Transform transform, in LocalTransform local)
        {
            transform.SetPositionAndRotation((Vector3)local.Position, (Quaternion)local.Rotation);
        }
    }
}
