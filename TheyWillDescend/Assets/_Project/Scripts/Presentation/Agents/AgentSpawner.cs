using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.Io;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Presentation.Agents
{
    /// <summary>
    /// Intent source for agents: HUD/save enqueue commands. Does not instantiate the sim entity.
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
            var prefab = PickCatalogPrefab();
            if (prefab == null)
            {
                GameLog.Error("AgentSpawner: no character prefabs.");
                return;
            }

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
            var pose = walk.ToPosition();
            if (!SimIo.TryEnqueueSpawn(ToCommand(walk, pose, hasPose: true, prefab.name)))
            {
                GameLog.Error("AgentSpawner: sim world not ready.");
                return;
            }

            FlushAndPump();
            GameLog.Info($"Spawn command {prefab.name} r={walk.Radius:0.0} s={walk.Speed:0.00}");
        }

        public void WipeAgentsAndViews()
        {
            SimIo.TryRequestDespawnAllAgents();
            SimIo.Flush();
            var views = EnsureViews();
            views.Pump();
            views.ClearViews();
        }

        public void FlushAndPump()
        {
            SimIo.Flush();
            EnsureViews().Pump();
        }

        AgentViewBoard EnsureViews()
        {
            if (_views == null)
                Awake();
            return _views;
        }

        static SpawnAgentCommand ToCommand(in CircleWalk walk, in AgentPosition pose, byte hasPose, string visualId)
        {
            return new SpawnAgentCommand
            {
                Center = walk.Center,
                Radius = walk.Radius,
                Speed = walk.Speed,
                Direction = walk.Direction,
                AngleRadians = walk.AngleRadians,
                Position = pose.Value,
                Facing = pose.Facing,
                HasPose = hasPose,
                VisualId = string.IsNullOrEmpty(visualId)
                    ? default
                    : new FixedString64Bytes(visualId)
            };
        }

        static SpawnAgentCommand ToCommand(in CircleWalk walk, in AgentPosition pose, bool hasPose, string visualId)
            => ToCommand(walk, pose, hasPose ? (byte)1 : (byte)0, visualId);

        GameObject PickCatalogPrefab()
        {
            if (characterPrefabs == null || characterPrefabs.Length == 0)
                return null;
            var prefab = characterPrefabs[UnityEngine.Random.Range(0, characterPrefabs.Length)];
            return prefab != null ? prefab : characterPrefabs[0];
        }
    }
}
