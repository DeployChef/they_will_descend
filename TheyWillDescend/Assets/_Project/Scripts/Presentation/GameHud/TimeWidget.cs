using TheyWillDescend.Simulation.Io;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Clock buttons enqueue sim clock commands. Day label pulls GameTime.
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
            var hasControl = SimIo.TryGetSimControl(out var control);
            var buildLocked = hasControl && control.BuildLocked != 0;
            HudButtons.SetInteractable(speed1Button, !buildLocked);
            HudButtons.SetInteractable(speed2Button, !buildLocked);
            HudButtons.SetInteractable(speed3Button, !buildLocked);
            HudButtons.SetInteractable(pauseButton, !buildLocked);

            HudButtons.Tint(speed1Button, hasControl && control.Speed == 1);
            HudButtons.Tint(speed2Button, hasControl && control.Speed == 2);
            HudButtons.Tint(speed3Button, hasControl && control.Speed == 3);
            HudButtons.Tint(pauseButton, hasControl && control.PlayerPaused != 0);

            if (clockLabel == null)
                return;

            clockLabel.text = SimIo.TryGetGameTime(out var time)
                ? GameClockFormat.Format(time)
                : "Day --  --:--";
        }

        static void OnPauseClicked()
        {
            SimIo.TryEnqueueTogglePlayerPause();
        }

        static void OnSpeedClicked(int speed)
        {
            SimIo.TryEnqueueSimSpeed(speed);
        }
    }
}
