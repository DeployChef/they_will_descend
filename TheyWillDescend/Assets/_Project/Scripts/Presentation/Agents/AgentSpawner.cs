using System.Collections.Generic;
using _Project.Scripts.Infrastructure.Logging;
using _Project.Scripts.Infrastructure.Save;
using _Project.Scripts.Simulation.Agents;
using Unity.Mathematics;
using UnityEngine;

namespace _Project.Scripts.Presentation.Agents
{
    /// <summary>
    /// Runtime spawn of hybrid agents (prefab GO + CircleWalkAgent → ECS entity).
    /// </summary>
    public sealed class AgentSpawner : MonoBehaviour
    {
        [SerializeField] GameObject[] characterPrefabs;
        [SerializeField] Transform spawnParent;
        [SerializeField] Vector2 spawnAreaCenter = Vector2.zero;
        [SerializeField] Vector2 spawnAreaSize = new(20f, 20f);
        [SerializeField] Vector2 radiusRange = new(1.5f, 5f);
        [SerializeField] Vector2 speedRange = new(0.08f, 0.2f);

        readonly List<GameObject> _spawned = new();

        public void SpawnRandom()
        {
            if (characterPrefabs == null || characterPrefabs.Length == 0)
            {
                GameLog.Error("AgentSpawner: no prefabs assigned.");
                return;
            }

            var prefab = characterPrefabs[UnityEngine.Random.Range(0, characterPrefabs.Length)];
            if (prefab == null)
            {
                GameLog.Error("AgentSpawner: null prefab in list.");
                return;
            }
            var pos = new Vector3(
                spawnAreaCenter.x + UnityEngine.Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f),
                0f,
                spawnAreaCenter.y + UnityEngine.Random.Range(-spawnAreaSize.y * 0.5f, spawnAreaSize.y * 0.5f));

            var radius = UnityEngine.Random.Range(radiusRange.x, radiusRange.y);
            var speed = UnityEngine.Random.Range(speedRange.x, speedRange.y);
            var direction = UnityEngine.Random.value < 0.5f ? -1f : 1f;

            SpawnAt(prefab, pos, radius, speed, direction, 0f, activate: true);
        }

        public void ClearSpawned()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                var go = _spawned[i];
                if (go == null)
                    continue;
                go.SetActive(false);
                Object.DestroyImmediate(go);
            }

            _spawned.Clear();
        }

        public void SpawnFromSnapshot(AgentSnapshot record, int version)
        {
            var prefab = FindPrefab(record.prefabId);
            if (prefab == null)
            {
                GameLog.Error($"No prefab for id '{record.prefabId}'.");
                return;
            }

            var walk = new CircleWalk
            {
                Center = new float3(record.centerX, record.centerY, record.centerZ),
                Radius = record.radius,
                Speed = record.speed,
                Direction = record.direction,
                AngleRadians = record.angleRadians
            };
            var position = version >= 2
                ? new AgentPosition
                {
                    Value = new float3(record.posX, record.posY, record.posZ),
                    Facing = new float3(record.fwdX, record.fwdY, record.fwdZ)
                }
                : walk.ToPosition();
            var p = position.Value;
            var agent = SpawnAt(
                prefab,
                new Vector3(p.x, p.y, p.z),
                record.radius,
                record.speed,
                record.direction,
                0f,
                activate: false);
            agent.ApplyWalk(walk, position);
            agent.gameObject.SetActive(true);
        }

        CircleWalkAgent SpawnAt(
            GameObject prefab,
            Vector3 pos,
            float radius,
            float speed,
            float direction,
            float heightOffset,
            bool activate)
        {
            var instance = Instantiate(prefab, pos, Quaternion.identity);
            instance.name = $"{prefab.name}_Spawned";
            if (spawnParent != null)
                instance.transform.SetParent(spawnParent, true);

            instance.SetActive(false);
            var agent = instance.GetComponent<CircleWalkAgent>();
            if (agent == null)
                agent = instance.AddComponent<CircleWalkAgent>();

            agent.ApplySettings(radius, speed, direction, heightOffset, prefab.name);
            if (activate)
                instance.SetActive(true);
            _spawned.Add(instance);
            GameLog.Info($"Spawned {instance.name} at {pos} r={radius:0.0} s={speed:0.00}");
            return agent;
        }

        GameObject FindPrefab(string id)
        {
            if (string.IsNullOrEmpty(id) || characterPrefabs == null)
                return null;

            for (var i = 0; i < characterPrefabs.Length; i++)
            {
                var prefab = characterPrefabs[i];
                if (prefab != null && prefab.name == id)
                    return prefab;
            }

            return characterPrefabs.Length > 0 ? characterPrefabs[0] : null;
        }
    }
}
