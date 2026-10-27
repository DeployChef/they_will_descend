namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Static entry for UI leaves (buttons, toggles). Forwards to the one <see cref="AudioService"/>;
    /// does nothing when there is none (Edit Mode, tests).
    /// </summary>
    public static class UiSound
    {
        public static void Play(UiState state) => AudioService.Current?.Ui.Play(state);
    }
}
