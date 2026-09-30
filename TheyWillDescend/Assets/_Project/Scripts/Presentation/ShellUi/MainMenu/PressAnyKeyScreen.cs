using UnityEngine;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// Splash panel on the MainMenu scene. <see cref="MainMenuFlow"/> shows and hides it.
    /// </summary>
    public sealed class PressAnyKeyScreen : MonoBehaviour
    {
        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);
    }
}
