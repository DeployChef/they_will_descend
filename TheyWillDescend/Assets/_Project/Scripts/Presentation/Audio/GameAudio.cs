using FMOD.Studio;
using FMODUnity;
using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// FMOD host on Bootstrap. Lives with Root (camera + AudioListener).
    /// Simulation never calls this. Player pause follows <see cref="SimControl.PlayerPaused"/>.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        public const string MusicBankName = "Main_theme";
        public const string MusicEventPath = "event:/main_soundtrack";

        [SerializeField] EventReference musicEvent;

        EventInstance _music;
        bool _lastPaused;

        public void StartSessionMusic()
        {
            StopSessionMusic();
            TryLoadMusicBank();

            try
            {
                _music = musicEvent.IsNull
                    ? RuntimeManager.CreateInstance(MusicEventPath)
                    : RuntimeManager.CreateInstance(musicEvent);
            }
            catch (EventNotFoundException)
            {
                GameLog.Error(
                    "GameAudio: event:/main_soundtrack not in loaded banks. " +
                    "In FMOD Studio put it on a bank, Ctrl+B, copy .bank into StreamingAssets/Desktop/.");
                return;
            }

            if (!_music.isValid())
            {
                GameLog.Error("GameAudio: music instance is invalid.");
                return;
            }

            _music.start();
            _lastPaused = false;
            GameLog.Info("GameAudio: session music started.");
        }

        public void StopSessionMusic()
        {
            if (!_music.isValid())
                return;

            _music.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _music.release();
            _music.clearHandle();
            _lastPaused = false;
        }

        void LateUpdate()
        {
            if (!_music.isValid())
                return;

            var paused = SimWorld.TryGet(out var em, out var bag)
                && em.GetComponentData<SimControl>(bag).PlayerPaused != 0;
            if (paused == _lastPaused)
                return;

            _music.setPaused(paused);
            _lastPaused = paused;
        }

        void OnDestroy() => StopSessionMusic();

        static void TryLoadMusicBank()
        {
            if (RuntimeManager.HasBankLoaded(MusicBankName))
                return;

            RuntimeManager.LoadBank(MusicBankName, loadSamples: true);
        }
    }
}
