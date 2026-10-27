using System;
using FMOD.Studio;
using FMODUnity;
using STOP_MODE = FMOD.Studio.STOP_MODE;
using TheyWillDescend.Infrastructure.Logging;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Owns the one music instance. Scenes ask for a theme; transitions between states are FMOD's job.
    /// </summary>
    public sealed class MusicDirector : IDisposable
    {
        readonly AudioCatalog _catalog;
        EventInstance _instance;
        FMOD.GUID _event;
        bool _paused;

        public MusicDirector(AudioCatalog catalog)
        {
            _catalog = catalog;
        }

        public void Set(MusicTheme theme) => Set(_catalog.Music(theme));

        public void Set(MusicCue cue)
        {
            if (cue.Event.IsNull)
            {
                Stop(STOP_MODE.ALLOWFADEOUT);
                return;
            }

            if (!_instance.isValid() || !cue.Event.Guid.Equals(_event))
            {
                Stop(STOP_MODE.ALLOWFADEOUT);
                if (!Start(cue.Event))
                    return;
            }

            if (cue.HasState)
                ApplyState(cue);
        }

        /// <summary>Music pauses together with the pause snapshot — that is the current mix.</summary>
        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (_instance.isValid())
                _instance.setPaused(paused);
        }

        /// <summary>
        /// The music event is 3D. Each scene has its own listener, and the music outlives all of them,
        /// so it sits at whichever listener is current.
        /// </summary>
        public void FollowListener()
        {
            if (!_instance.isValid())
                return;
            if (RuntimeManager.StudioSystem.getListenerAttributes(0, out var attributes) == FMOD.RESULT.OK)
                _instance.set3DAttributes(attributes);
        }

        public void Dispose() => Stop(STOP_MODE.IMMEDIATE);

        bool Start(EventReference reference)
        {
            try
            {
                _instance = RuntimeManager.CreateInstance(reference);
            }
            catch (EventNotFoundException)
            {
                GameLog.Error($"MusicDirector: music event {reference} is not in the loaded banks (Core set).");
                return false;
            }

            if (!_instance.isValid())
            {
                GameLog.Error($"MusicDirector: music instance for {reference} is invalid.");
                return false;
            }

            _event = reference.Guid;
            FollowListener();
            _instance.setPaused(_paused);
            _instance.start();
            GameLog.Info($"MusicDirector: music started ({reference}).");
            return true;
        }

        void ApplyState(MusicCue cue)
        {
            var result = _instance.setParameterByNameWithLabel(cue.Parameter, cue.Label);
            if (result == FMOD.RESULT.ERR_EVENT_NOTFOUND)
                result = RuntimeManager.StudioSystem.setParameterByNameWithLabel(cue.Parameter, cue.Label);
            if (result != FMOD.RESULT.OK)
                GameLog.Warning($"MusicDirector: cannot set '{cue.Parameter}' = '{cue.Label}' ({result}).");
        }

        void Stop(STOP_MODE mode)
        {
            if (!_instance.isValid())
                return;
            _instance.stop(mode);
            _instance.release();
            _instance.clearHandle();
            _event = default;
        }
    }
}
