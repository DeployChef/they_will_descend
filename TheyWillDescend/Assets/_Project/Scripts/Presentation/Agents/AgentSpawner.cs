using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Session;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Presentation.Agents
{
    /// <summary>
    /// Intent source. Enqueues a worker; does not Instantiate the sim entity
    /// and does not pick a Mixamo for ECS.
    /// </summary>
    public sealed class AgentSpawner : MonoBehaviour
    {
        [SerializeField] Vector2 spawnAreaCenter = Vector2.zero;
        [SerializeField] Vector2 spawnAreaSize = new(20f, 20f);
        [SerializeField] Vector2 walkSpeedRange = new(1.5f, 2.5f);

        public void SpawnRandom()
        {
            var position = new float3(
                spawnAreaCenter.x + UnityEngine.Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f),
                0f,
                spawnAreaCenter.y + UnityEngine.Random.Range(-spawnAreaSize.y * 0.5f, spawnAreaSize.y * 0.5f));
            var facing = new float3(0f, 0f, 1f);
            var speed = UnityEngine.Random.Range(walkSpeedRange.x, walkSpeedRange.y);
            if (!SimCommands.TryPost(new SpawnAgentCommand
                {
                    Position = position,
                    Facing = facing,
                    Speed = speed,
                    HasPose = 1,
                    Kind = AgentKind.Worker
                }))
            {
                GameLog.Error("AgentSpawner: sim world not ready.");
                return;
            }

            GameLog.Info($"Spawn command Worker speed={speed:0.00}");
        }
    }
}
