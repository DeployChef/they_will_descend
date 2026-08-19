using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Io;
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
        [SerializeField] GameObject[] characterPrefabs;
        [SerializeField] Transform spawnParent;
        [SerializeField] Vector2 spawnAreaCenter = Vector2.zero;
        [SerializeField] Vector2 spawnAreaSize = new(20f, 20f);
        [SerializeField] Vector2 radiusRange = new(1.5f, 5f);
        [SerializeField] Vector2 speedRange = new(0.08f, 0.2f);

        AgentViewBoard _views;

        void Awake()
        {
            _views = GetComponent<AgentViewBoard>();
            if (_views == null)
                _views = gameObject.AddComponent<AgentViewBoard>();
            _views.BindCatalog(characterPrefabs, spawnParent);
        }

        public void SpawnRandom()
        {
            var center = new float3(
                spawnAreaCenter.x + UnityEngine.Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f),
                0f,
                spawnAreaCenter.y + UnityEngine.Random.Range(-spawnAreaSize.y * 0.5f, spawnAreaSize.y * 0.5f));
            var walk = new CircleWalk
            {
                Center = center,
                Radius = UnityEngine.Random.Range(radiusRange.x, radiusRange.y),
                Speed = UnityEngine.Random.Range(speedRange.x, speedRange.y),
                Direction = UnityEngine.Random.value < 0.5f ? -1f : 1f,
                AngleRadians = 0f
            };
            walk.GetPose(out var position, out var facing);
            if (!SimIo.TryEnqueueSpawn(ToCommand(walk, position, facing)))
            {
                GameLog.Error("AgentSpawner: sim world not ready.");
                return;
            }

            GameLog.Info($"Spawn command Worker r={walk.Radius:0.0} s={walk.Speed:0.00}");
        }

        public void WipeAgentsAndViews()
        {
            SimIo.TryRequestDespawnAllAgents();
            EnsureViews().ClearViews();
        }

        public void PumpViews() => EnsureViews().Pump();

        AgentViewBoard EnsureViews()
        {
            if (_views == null)
                Awake();
            return _views;
        }

        static SpawnAgentCommand ToCommand(in CircleWalk walk, float3 position, float3 facing)
        {
            return new SpawnAgentCommand
            {
                Center = walk.Center,
                Radius = walk.Radius,
                Speed = walk.Speed,
                Direction = walk.Direction,
                AngleRadians = walk.AngleRadians,
                Position = position,
                Facing = facing,
                HasPose = 1,
                Kind = AgentKind.Worker
            };
        }
    }
}
