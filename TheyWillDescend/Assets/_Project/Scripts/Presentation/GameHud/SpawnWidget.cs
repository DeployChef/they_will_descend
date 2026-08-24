using TheyWillDescend.Infrastructure.Logging;
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
                GameLog.Error("SpawnWidget: spawn button is not assigned.");
            if (agentSpawner == null)
                GameLog.Error("SpawnWidget: AgentSpawner is not assigned.");
            HudButtons.Bind(spawnAgentButton, OnSpawnClicked);
        }

        void OnDestroy()
        {
            HudButtons.Unbind(spawnAgentButton, OnSpawnClicked);
        }

        public void PumpViews()
        {
            agentSpawner?.PumpViews();
        }

        void OnSpawnClicked()
        {
            agentSpawner?.SpawnRandom();
        }
    }
}
