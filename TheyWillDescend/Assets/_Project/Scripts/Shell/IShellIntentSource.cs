namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Shell-level intents (not raw keys). States listen to these.
    /// Menu "Start" is UI-driven, not an intent from this source.
    /// </summary>
    public interface IShellIntentSource
    {
        /// <summary>Press-any-key / confirm to leave splash.</summary>
        bool ConsumeProceed();

        bool ConsumePauseToggle();
    }
}
