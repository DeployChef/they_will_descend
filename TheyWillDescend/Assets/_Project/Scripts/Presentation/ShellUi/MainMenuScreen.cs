using System;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// Start-game panel on MainMenu. Does not know about the splash.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] Button startGameButton;

        public static MainMenuScreen Current { get; private set; }

        public event Action StartClicked;

        void Awake()
        {
            Current = this;
            if (startGameButton != null)
                startGameButton.onClick.AddListener(() => StartClicked?.Invoke());
            Hide();
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);
    }
}
