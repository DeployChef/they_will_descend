using System;
using TheyWillDescend.Presentation.Audio;
using TheyWillDescend.Simulation.Session;
using VContainer.Unity;

namespace TheyWillDescend.Shell
{
    /// <summary>
    /// Game-scene bridge: the player pause in ECS drives the audio pause. The game knows about sound,
    /// sound does not know about the game. Leaving Game lifts the pause.
    /// </summary>
    public sealed class SimPauseAudioBridge : ILateTickable, IDisposable
    {
        readonly AudioService _audio;
        bool _paused;

        public SimPauseAudioBridge(AudioService audio)
        {
            _audio = audio;
        }

        public void LateTick()
        {
            var paused = SimWorld.TryGet(out var em, out var session)
                         && em.HasComponent<SimControl>(session)
                         && em.GetComponentData<SimControl>(session).PlayerPaused != 0;
            if (paused == _paused)
                return;

            _paused = paused;
            _audio.SetPaused(paused);
        }

        public void Dispose()
        {
            if (!_paused)
                return;
            _paused = false;
            _audio.SetPaused(false);
        }
    }
}
