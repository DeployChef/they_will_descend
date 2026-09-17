using TheyWillDescend.Presentation.ShellUi;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Clock buttons post clock commands. Day label pulls GameTime.
    /// ⏸ pauses the city clock. Esc opens the game menu.
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
            var hasControl = SimWorld.TryGet(out var em, out var bag);
            var control = hasControl ? em.GetComponentData<SimControl>(bag) : default;
            var buildLocked = hasControl && control.BuildLocked != 0;
            var pauseMenuOpen = PauseMenuScreen.Current != null && PauseMenuScreen.Current.IsOpen;
            HudButtons.SetInteractable(speed1Button, !buildLocked && !pauseMenuOpen);
            HudButtons.SetInteractable(speed2Button, !buildLocked && !pauseMenuOpen);
            HudButtons.SetInteractable(speed3Button, !buildLocked && !pauseMenuOpen);
            HudButtons.SetInteractable(pauseButton, !buildLocked && !pauseMenuOpen);

            // Активная скорость держится активной, пока не переключат на другую.
            var paused = hasControl && control.TimePaused != 0;
            HudButtons.SetActive(pauseButton, paused);
            HudButtons.SetActive(speed1Button, hasControl && !paused && control.Speed == 1);
            HudButtons.SetActive(speed2Button, hasControl && !paused && control.Speed == 2);
            HudButtons.SetActive(speed3Button, hasControl && !paused && control.Speed == 3);


            if (clockLabel == null)
                return;

            clockLabel.text = hasControl && TryGetGameTime(em, out var time)
                ? GameClockFormat.Format(time)
                : "Day --  --:--";
        }

        static bool TryGetGameTime(EntityManager em, out GameTime time)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameTime>());
            if (query.IsEmptyIgnoreFilter)
            {
                time = default;
                return false;
            }

            time = query.GetSingleton<GameTime>();
            return true;
        }

        static void OnPauseClicked()
        {
            SimCommands.TryPost(SimClockCommand.TogglePause());
        }

        static void OnSpeedClicked(int speed)
        {
            SimCommands.TryPost(SimClockCommand.Speed(speed));
        }
    }
}
