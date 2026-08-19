using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Shell;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace TheyWillDescend.Presentation.Agents
{
    /// <summary>
    /// Animator is not an Entities Graphics companion. Until workers drop Animator,
    /// this board instantiates the Mixamo GO and copies LocalTransform.
    /// Existence is pulled from the query — no spawn events.
    /// </summary>
    public sealed class AgentViewBoard : MonoBehaviour
    {
        GameObject[] _prefabs;
        Transform _spawnParent;
        readonly Dictionary<int, AgentView> _views = new();
        readonly HashSet<int> _seen = new();

        public void BindCatalog(GameObject[] prefabs, Transform spawnParent)
        {
            _prefabs = prefabs;
            _spawnParent = spawnParent;
        }

        void LateUpdate() => Pump();

        public void Pump()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            using var query = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<AgentId>(),
                ComponentType.ReadOnly<AgentType>(),
                ComponentType.ReadOnly<LocalTransform>());
            Sync(query);
        }

        void OnDisable()
        {
            ClearViews();
        }

        public void ClearViews()
        {
            foreach (var view in _views.Values)
            {
                if (view == null)
                    continue;
                var go = view.gameObject;
                go.SetActive(false);
                Object.DestroyImmediate(go);
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
                ApplyPose(view.transform, transforms[i]);
                view.SetAnimSpeed(animSpeed);
            }

            if (_views.Count != _seen.Count)
            {
                var stale = new List<int>();
                foreach (var pair in _views)
                {
                    if (!_seen.Contains(pair.Key))
                        stale.Add(pair.Key);
                }

                for (var i = 0; i < stale.Count; i++)
                    DestroyView(stale[i]);
            }

            ids.Dispose();
            types.Dispose();
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

            var instance = Instantiate(
                prefab,
                (Vector3)transform.Position,
                (Quaternion)transform.Rotation);
            instance.name = $"{prefab.name}_{agentId}";
            if (_spawnParent != null)
                instance.transform.SetParent(_spawnParent, true);

            var view = instance.GetComponent<AgentView>();
            if (view == null)
                view = instance.AddComponent<AgentView>();
            view.Bind();
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
            var go = view.gameObject;
            go.SetActive(false);
            Object.DestroyImmediate(go);
        }

        GameObject ResolvePrefab(AgentKind kind, int agentId)
        {
            if (_prefabs == null || _prefabs.Length == 0)
                return null;
            if (kind != AgentKind.Worker)
                return _prefabs[0];

            var index = agentId % _prefabs.Length;
            if (index < 0)
                index += _prefabs.Length;
            return _prefabs[index] != null ? _prefabs[index] : _prefabs[0];
        }

        static float AnimSpeed()
        {
            var gate = SimGate.Active;
            if (gate == null || gate.EffectiveMode != SimRunMode.Running)
                return 0f;
            return gate.Speed;
        }

        static void ApplyPose(Transform transform, in LocalTransform local)
        {
            transform.SetPositionAndRotation((Vector3)local.Position, (Quaternion)local.Rotation);
        }
    }
}
