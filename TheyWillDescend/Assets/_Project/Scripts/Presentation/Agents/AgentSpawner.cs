using _Project.Scripts.Infrastructure.Logging;
using UnityEngine;

namespace _Project.Scripts.Presentation.Agents
{
    /// <summary>
    /// Runtime spawn of hybrid agents (prefab GO + CircleWalkAgent → ECS entity).
    /// Lives on Game scene. Prefabs are presentation; motion is still ECS via CircleWalkAgent.
    /// </summary>
    public sealed class AgentSpawner : MonoBehaviour
    {
        [SerializeField] GameObject[] characterPrefabs;
        [SerializeField] Transform spawnParent;
        [SerializeField] Vector2 spawnAreaCenter = Vector2.zero;
        [SerializeField] Vector2 spawnAreaSize = new(20f, 20f);
        [SerializeField] Vector2 radiusRange = new(1.5f, 5f);
        [SerializeField] Vector2 speedRange = new(0.08f, 0.2f);

        public void SpawnRandom()
        {
            if (characterPrefabs == null || characterPrefabs.Length == 0)
            {
                GameLog.Error(LogChannel.City, "AgentSpawner: no prefabs assigned.");
                return;
            }

            var prefab = characterPrefabs[Random.Range(0, characterPrefabs.Length)];
            if (prefab == null)
            {
                GameLog.Error(LogChannel.City, "AgentSpawner: null prefab in list.");
                return;
            }

            var pos = new Vector3(
                spawnAreaCenter.x + Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f),
                0f,
                spawnAreaCenter.y + Random.Range(-spawnAreaSize.y * 0.5f, spawnAreaSize.y * 0.5f));

            var instance = Instantiate(prefab, pos, Quaternion.identity);
            instance.name = $"{prefab.name}_Spawned";
            if (spawnParent != null)
                instance.transform.SetParent(spawnParent, true);

            instance.SetActive(false);
            var agent = instance.GetComponent<CircleWalkAgent>();
            if (agent == null)
                agent = instance.AddComponent<CircleWalkAgent>();

            var radius = Random.Range(radiusRange.x, radiusRange.y);
            var speed = Random.Range(speedRange.x, speedRange.y);
            var direction = Random.value < 0.5f ? -1f : 1f;
            agent.ApplySettings(radius, speed, direction);
            instance.SetActive(true);

            GameLog.Info(LogChannel.City, $"Spawned {instance.name} at {pos} r={radius:0.0} s={speed:0.00}");
        }
    }
}
