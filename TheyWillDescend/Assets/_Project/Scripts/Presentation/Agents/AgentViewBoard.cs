using System.Collections.Generic;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Shell;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Io;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Presentation.Agents
{
    /// <summary>
    /// Views for agent entities. Reads events (spawn/despawn) and pulls AgentPosition.
    /// Owns GameObjects; never writes simulation.
    /// </summary>
    public sealed class AgentViewBoard : MonoBehaviour
    {
        GameObject[] _prefabs;
        Transform _spawnParent;
        readonly Dictionary<int, AgentView> _views = new();
        EntityQuery _poseQuery;
        World _queryWorld;

        public void BindCatalog(GameObject[] prefabs, Transform spawnParent)
        {
            _prefabs = prefabs;
            _spawnParent = spawnParent;
        }

        public void LateUpdate() => Pump();

        public void Pump()
        {
            DrainEvents();
            PullPoses();
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

        void OnDisable()
        {
            DisposeQuery();
            ClearViews();
        }

        void DrainEvents()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;
            using var bridgeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<SimBridge>());
            if (bridgeQuery.IsEmptyIgnoreFilter)
                return;

            var bridgeEntity = bridgeQuery.GetSingletonEntity();

            var spawned = em.GetBuffer<AgentSpawnedEvent>(bridgeEntity);
            for (var i = 0; i < spawned.Length; i++)
                CreateView(spawned[i].AgentId, spawned[i].Position, spawned[i].VisualId);
            spawned.Clear();

            var despawned = em.GetBuffer<AgentDespawnedEvent>(bridgeEntity);
            for (var i = 0; i < despawned.Length; i++)
                DestroyView(despawned[i].AgentId);
            despawned.Clear();

            var days = em.GetBuffer<DayChangedEvent>(bridgeEntity);
            for (var i = 0; i < days.Length; i++)
                GameLog.Info($"Day {days[i].Day}");
            days.Clear();
        }

        void PullPoses()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            if (_poseQuery == default || _queryWorld != world)
            {
                DisposeQuery();
                _poseQuery = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<AgentId>(),
                    ComponentType.ReadOnly<AgentPosition>());
                _queryWorld = world;
            }

            if (_poseQuery.IsEmptyIgnoreFilter)
                return;

            var ids = _poseQuery.ToComponentDataArray<AgentId>(Allocator.Temp);
            var poses = _poseQuery.ToComponentDataArray<AgentPosition>(Allocator.Temp);
            var animSpeed = AnimSpeed();
            for (var i = 0; i < ids.Length; i++)
            {
                if (!_views.TryGetValue(ids[i].Value, out var view) || view == null)
                    continue;
                ApplyPose(view.transform, poses[i]);
                view.SetAnimSpeed(animSpeed);
            }

            ids.Dispose();
            poses.Dispose();
        }

        void CreateView(int agentId, float3 position, FixedString64Bytes visualId)
        {
            if (_views.ContainsKey(agentId))
                return;

            var prefab = ResolvePrefab(visualId);
            if (prefab == null)
            {
                GameLog.Error("AgentViewBoard: no character prefabs.");
                return;
            }

            var instance = Instantiate(
                prefab,
                new Vector3(position.x, position.y, position.z),
                Quaternion.identity);
            instance.name = $"{prefab.name}_{agentId}";
            if (_spawnParent != null)
                instance.transform.SetParent(_spawnParent, true);

            StripLegacySim(instance);
            var view = instance.GetComponent<AgentView>();
            if (view == null)
                view = instance.AddComponent<AgentView>();
            view.Bind();
            _views[agentId] = view;
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

        GameObject ResolvePrefab(FixedString64Bytes visualId)
        {
            if (_prefabs == null || _prefabs.Length == 0)
                return null;

            var key = visualId.ToString();
            if (!string.IsNullOrEmpty(key))
            {
                for (var i = 0; i < _prefabs.Length; i++)
                {
                    var candidate = _prefabs[i];
                    if (candidate != null && candidate.name == key)
                        return candidate;
                }

                GameLog.Warning($"AgentViewBoard: unknown visual '{key}', picking from catalog.");
            }

            return PickPrefab();
        }

        GameObject PickPrefab()
        {
            if (_prefabs == null || _prefabs.Length == 0)
                return null;
            var prefab = _prefabs[UnityEngine.Random.Range(0, _prefabs.Length)];
            return prefab != null ? prefab : _prefabs[0];
        }

        static float AnimSpeed()
        {
            var gate = SimGate.Active;
            if (gate == null || gate.EffectiveMode != SimRunMode.Running)
                return 0f;
            return gate.Speed;
        }

        static void ApplyPose(Transform transform, in AgentPosition position)
        {
            transform.position = new Vector3(position.Value.x, position.Value.y, position.Value.z);
            if (math.lengthsq(position.Facing) > 0.0001f)
                transform.rotation = Quaternion.LookRotation(
                    new Vector3(position.Facing.x, position.Facing.y, position.Facing.z));
        }

        static void StripLegacySim(GameObject instance)
        {
            var legacy = instance.GetComponent("CircleWalkAgent");
            if (legacy != null)
                Object.DestroyImmediate(legacy);
        }

        void DisposeQuery()
        {
            if (_poseQuery == default)
                return;
            _poseQuery.Dispose();
            _poseQuery = default;
            _queryWorld = null;
        }
    }
}
