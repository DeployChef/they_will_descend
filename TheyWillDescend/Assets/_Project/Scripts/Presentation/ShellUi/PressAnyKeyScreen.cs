using UnityEngine;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// Splash panel on MainMenu. Bind in Awake / OnDestroy so Hide() can SetActive(false)
    /// without clearing the port.
    /// </summary>
    public sealed class PressAnyKeyScreen : MonoBehaviour
    {
        public static PressAnyKeyScreen Current { get; private set; }

        void Awake()
        {
            Current = this;
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
