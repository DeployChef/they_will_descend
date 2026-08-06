using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.Presentation.GameHud
{
    /// <summary>
    /// Game-scene overlay HUD. Not shell menu — lives with the session scene.
    /// </summary>
    public sealed class GameHudBinder : MonoBehaviour
    {
        [SerializeField] Button spawnAgentButton;
        [SerializeField] Agents.AgentSpawner agentSpawner;

        void Awake()
        {
            if (spawnAgentButton != null)
                spawnAgentButton.onClick.AddListener(OnSpawnClicked);
        }

        void OnDestroy()
        {
            if (spawnAgentButton != null)
                spawnAgentButton.onClick.RemoveListener(OnSpawnClicked);
        }

        void OnSpawnClicked()
        {
            if (agentSpawner == null)
                agentSpawner = FindFirstObjectByType<Agents.AgentSpawner>();

            agentSpawner?.SpawnRandom();
        }
    }
}
