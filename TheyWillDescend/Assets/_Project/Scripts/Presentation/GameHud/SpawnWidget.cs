using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Spawn-worker button. Intent only — sim spawn goes through AgentSpawner.
    /// </summary>
    public sealed class SpawnWidget : MonoBehaviour
    {
        [SerializeField] Button spawnAgentButton;
        [SerializeField] Agents.AgentSpawner agentSpawner;

        void Awake()
        {
            if (spawnAgentButton == null)
                spawnAgentButton = GetComponent<Button>();
            HudButtons.Bind(spawnAgentButton, OnSpawnClicked);
        }

        void OnDestroy()
        {
            HudButtons.Unbind(spawnAgentButton, OnSpawnClicked);
        }

        public void PumpViews()
        {
            EnsureSpawner();
            agentSpawner?.PumpViews();
        }

        void OnSpawnClicked()
        {
            EnsureSpawner();
            agentSpawner?.SpawnRandom();
        }

        void EnsureSpawner()
        {
            if (agentSpawner == null)
                agentSpawner = FindFirstObjectByType<Agents.AgentSpawner>();
        }
    }
}
