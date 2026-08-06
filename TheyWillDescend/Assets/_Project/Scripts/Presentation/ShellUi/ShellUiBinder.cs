using System;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.Presentation.ShellUi
{
    /// <summary>
    /// Shows/hides splash + main menu panels. Wired in the Game/Bootstrap scene.
    /// Canon for this vertical slice: one place Presentation talks to AppFlow about Start click.
    /// </summary>
    public sealed class ShellUiBinder : MonoBehaviour
    {
        [SerializeField] GameObject pressAnyKeyPanel;
        [SerializeField] GameObject mainMenuPanel;
        [SerializeField] Button startGameButton;

        public event Action StartGameClicked;

        void Awake()
        {
            if (startGameButton != null)
                startGameButton.onClick.AddListener(() => StartGameClicked?.Invoke());

            HideAll();
        }

        public void ShowPressAnyKey()
        {
            SetPanel(pressAnyKeyPanel, true);
            SetPanel(mainMenuPanel, false);
        }

        public void ShowMainMenu()
        {
            SetPanel(pressAnyKeyPanel, false);
            SetPanel(mainMenuPanel, true);
        }

        public void ShowGameplayHud()
        {
            HideAll();
        }

        public void HideAll()
        {
            SetPanel(pressAnyKeyPanel, false);
            SetPanel(mainMenuPanel, false);
        }

        static void SetPanel(GameObject panel, bool visible)
        {
            if (panel != null)
                panel.SetActive(visible);
        }
    }
}
