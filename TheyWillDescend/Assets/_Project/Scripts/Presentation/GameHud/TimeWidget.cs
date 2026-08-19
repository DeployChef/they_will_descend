using TheyWillDescend.Shell;
using TheyWillDescend.Simulation.Time;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Clock buttons → SimGate. Day label pulls GameTime (read-only).
    /// </summary>
    public sealed class TimeWidget : MonoBehaviour
    {
        [SerializeField] Button pauseButton;
        [SerializeField] Button speed1Button;
        [SerializeField] Button speed2Button;
        [SerializeField] Button speed3Button;
        [SerializeField] TMP_Text clockLabel;

        void Awake()
        {
            HudButtons.Bind(pauseButton, OnPauseClicked);
            HudButtons.Bind(speed1Button, () => OnSpeedClicked(1));
            HudButtons.Bind(speed2Button, () => OnSpeedClicked(2));
            HudButtons.Bind(speed3Button, () => OnSpeedClicked(3));
        }

        void OnDestroy()
        {
            HudButtons.Unbind(pauseButton, OnPauseClicked);
        }

        void Update()
        {
            var gate = SimGate.Active;
            var buildLocked = gate != null && gate.BuildLocked;
            HudButtons.SetInteractable(speed1Button, !buildLocked);
            HudButtons.SetInteractable(speed2Button, !buildLocked);
            HudButtons.SetInteractable(speed3Button, !buildLocked);
            HudButtons.SetInteractable(pauseButton, !buildLocked);

            HudButtons.Tint(speed1Button, gate != null && gate.Speed == 1);
            HudButtons.Tint(speed2Button, gate != null && gate.Speed == 2);
            HudButtons.Tint(speed3Button, gate != null && gate.Speed == 3);
            HudButtons.Tint(pauseButton, gate != null && gate.PlayerPaused);

            if (clockLabel == null)
                return;

            clockLabel.text = TryGetGameTime(out var time)
                ? GameClockFormat.Format(time)
                : "Day --  --:--";
        }

        static void OnPauseClicked()
        {
            SimGate.Active?.TogglePlayerPause();
        }

        static void OnSpeedClicked(int speed)
        {
            SimGate.Active?.SetSpeed(speed);
        }

        static bool TryGetGameTime(out GameTime time)
        {
            time = default;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GameTime>());
            if (query.IsEmptyIgnoreFilter)
                return false;

            time = query.GetSingleton<GameTime>();
            return true;
        }
    }
}
