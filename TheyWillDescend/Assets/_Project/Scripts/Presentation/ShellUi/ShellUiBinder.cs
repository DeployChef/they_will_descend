using System;
using TheyWillDescend.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// MainMenu-scene implementation of <see cref="IShellUi"/>.
    /// </summary>
    public sealed class ShellUiBinder : MonoBehaviour, IShellUi
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

        void OnEnable() => ShellUiPort.Bind(this);

        void OnDisable() => ShellUiPort.Unbind(this);

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
