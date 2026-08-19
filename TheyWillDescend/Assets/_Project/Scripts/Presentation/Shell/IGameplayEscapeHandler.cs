namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Presentation overlays (build catalog, later dialogs) can consume Esc
    /// before <see cref="States.PlayingState"/> opens pause.
    /// </summary>
    public interface IGameplayEscapeHandler
    {
        /// <returns>True if Esc was handled and must not open pause.</returns>
        bool TryHandleEscape();
    }

    /// <summary>
    /// Temporary Active router (same pattern as <see cref="SimGate"/>.Active).
    /// Replace with composition-root injection when HUD/overlays grow.
    /// </summary>
    public static class GameplayEscapeRouter
    {
        public static IGameplayEscapeHandler Active { get; set; }
    }
}
