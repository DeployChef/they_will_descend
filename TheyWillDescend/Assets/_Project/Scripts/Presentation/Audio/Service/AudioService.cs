using System;
using VContainer.Unity;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Process audio facade on AppScope. Speaks the game's language: bank sets, music themes, pause, UI states.
    /// Owners of sound get it through DI; leaves such as buttons go through <see cref="UiSound"/>.
    /// It never reads the simulation — scenes push state into it.
    /// </summary>
    public sealed class AudioService : ITickable, ILateTickable, IDisposable
    {
        readonly IDisposable _core;
        readonly AudioSnapshots _snapshots;

        public AudioService(AudioCatalog catalog)
        {
            Catalog = catalog;
            Banks = new AudioBanks();
            _core = Banks.Load(catalog.Core);
            Music = new MusicDirector(catalog);
            _snapshots = new AudioSnapshots(catalog.PauseSnapshot);
            Ui = new UiAudio(catalog.UiEvent, catalog.UiStateParameter);
            Current = this;
        }

        /// <summary>The live instance for <see cref="UiSound"/>. Null outside Play.</summary>
        internal static AudioService Current { get; private set; }

        public AudioCatalog Catalog { get; }
        public AudioBanks Banks { get; }
        public MusicDirector Music { get; }
        public UiAudio Ui { get; }

        public bool WorldBanksLoaded => Banks.IsLoaded(Catalog.GameWorld);

        /// <summary>Game pause: the ESC snapshot, and music holds with it.</summary>
        public void SetPaused(bool paused)
        {
            _snapshots.SetPaused(paused);
            Music.SetPaused(paused);
        }

        public void Tick() => Ui.Tick();

        public void LateTick() => Music.FollowListener();

        public void Dispose()
        {
            if (Current == this)
                Current = null;
            Ui.Dispose();
            _snapshots.Dispose();
            Music.Dispose();
            _core.Dispose();
        }
    }
}
