using System;
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
    /// main_soundtrack отключён флагом enableMusic (по умолчанию off).
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        public const string MusicBankName = "Main_theme";
        public const string MusicEventPath = "event:/main_soundtrack";

        [SerializeField] EventReference musicEvent;

        [Header("Music")]
        [Tooltip("Включить main_soundtrack. По умолчанию выключен — трек не играет.")]
        [SerializeField] bool enableMusic = false;

        EventInstance _music;
        bool _lastPaused;

        void Awake()
        {
            // Гарантия: трек не играет, даже если его запустили раньше.
            if (!enableMusic)
                StopSessionMusic();
        }

        public void StartSessionMusic()
        {
            // Музыка выключена флагом — ничего не запускаем.
            if (!enableMusic)
                return;

            StopSessionMusic();
            TryLoadBank("Master");
            TryLoadBank(MusicBankName);

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
                    "In FMOD Studio put it on bank Main_theme, Ctrl+B, copy .bank into StreamingAssets/Desktop/.");
                return;
            }
            catch (Exception e)
            {
                GameLog.Error($"GameAudio: failed to create music instance. {e.Message}");
                return;
            }

            if (!_music.isValid())
            {
                GameLog.Error("GameAudio: music instance is invalid.");
                return;
            }

            var listener = FindFirstObjectByType<StudioListener>();
            if (listener != null)
                RuntimeManager.AttachInstanceToGameObject(_music, listener.transform);

            _music.start();
            _lastPaused = false;
            GameLog.Info("GameAudio: session music started.");
        }

        public void StopSessionMusic()
        {
            if (!_music.isValid())
                return;

            RuntimeManager.DetachInstanceFromGameObject(_music);
            _music.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _music.release();
            _music.clearHandle();
            _lastPaused = false;
        }

        void LateUpdate()
        {
            if (!_music.isValid())
                return;

            var paused = false;
            if (SimWorld.TryGet(out var em, out var bag) && em.HasComponent<SimControl>(bag))
                paused = em.GetComponentData<SimControl>(bag).PlayerPaused != 0;
            if (paused == _lastPaused)
                return;

            _music.setPaused(paused);
            _lastPaused = paused;
        }

        void OnDestroy() => StopSessionMusic();

        static void TryLoadBank(string bankName)
        {
            try
            {
                if (RuntimeManager.HasBankLoaded(bankName))
                    return;
                RuntimeManager.LoadBank(bankName, loadSamples: true);
            }
            catch (BankLoadException e)
            {
                GameLog.Warning($"GameAudio: bank '{bankName}' missing. {e.Message}");
            }
        }
    }
}
