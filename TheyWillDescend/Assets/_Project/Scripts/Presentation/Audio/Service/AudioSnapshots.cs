using System;using FMOD.Studio; using FMODUnity; using TheyWillDescend.Presentation.Infrastructure;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>Mix snapshots owned by the process. Today only the pause (ESC) snapshot.</summary>
    public sealed class AudioSnapshots : IDisposable
    {
        readonly EventReference _pause;
        EventInstance _pauseInstance;

        public AudioSnapshots(EventReference pause)
        {
            _pause = pause;
        }

        public void SetPaused(bool paused)
        {
            if (paused)
                Start();
            else
                Stop();
        }

        public void Dispose() => Stop();

        void Start()
        {
            if (_pauseInstance.isValid() || _pause.IsNull)
                return;

            try
            {
                _pauseInstance = RuntimeManager.CreateInstance(_pause);
            }
            catch (EventNotFoundException)
            {
                GameLog.Warning($"AudioSnapshots: pause snapshot {_pause} is not in the loaded banks (Core set).");
                return;
            }

            _pauseInstance.start();
        }

        void Stop()
        {
            if (!_pauseInstance.isValid())
                return;
            _pauseInstance.stop(STOP_MODE.ALLOWFADEOUT);
            _pauseInstance.release();
            _pauseInstance.clearHandle();
        }
    }
}
