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
        // Ивент лежит в папке SOUNDTRACK → путь event:/SOUNDTRACK/main_soundtrack.
        public const string MusicEventPath = "event:/SOUNDTRACK/main_soundtrack";

        // Снэпшоты FMOD (корень Snapshots). DEAFULT — имя в FMOD-проекте с опечаткой.
        public const string PauseSnapshotPath = "snapshot:/ESC";
        public const string DefaultSnapshotPath = "snapshot:/DEAFULT";

        [SerializeField] EventReference musicEvent;

        [Header("Music")]
        [Tooltip("Включить main_soundtrack. По умолчанию выключен — трек не играет.")]
        [SerializeField] bool enableMusic = false;

        EventInstance _music;
        // Снэпшоты паузы: Esc → ESC, повторный Esc (выход из паузы) → DEAFULT.
        EventInstance _escSnapshot;
        EventInstance _defaultSnapshot;
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
                    "GameAudio: event:/SOUNDTRACK/main_soundtrack not in loaded banks. " +
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
            // Пауза по Esc (PlayerPaused) — переключение снэпшотов работает всегда,
            // независимо от того, включена ли музыка.
            var paused = false;
            if (SimWorld.TryGet(out var em, out var bag) && em.HasComponent<SimControl>(bag))
                paused = em.GetComponentData<SimControl>(bag).PlayerPaused != 0;
            if (paused != _lastPaused)
            {
                ApplyPauseSnapshots(paused);
                _lastPaused = paused;
            }

            if (!_music.isValid())
                return;

            _music.setPaused(paused);
        }

        void ApplyPauseSnapshots(bool paused)
        {
            if (paused)
            {
                StopSnapshot(ref _defaultSnapshot);
                StartSnapshot(PauseSnapshotPath, ref _escSnapshot);
            }
            else
            {
                StopSnapshot(ref _escSnapshot);
                StartSnapshot(DefaultSnapshotPath, ref _defaultSnapshot);
            }
        }

        static void StartSnapshot(string path, ref EventInstance snapshot)
        {
            // В обёртке FMOD 2.03 нет getSnapshot — снэпшот резолвится как ивент (snapshot:/...).
            var result = RuntimeManager.StudioSystem.getEvent(path, out var description);
            if (result != FMOD.RESULT.OK || !description.isValid())
            {
                GameLog.Warning($"GameAudio: snapshot '{path}' not found ({result}). " +
                                "Build banks in FMOD Studio (Ctrl+B) and copy them to StreamingAssets/Desktop/.");
                return;
            }

            result = description.createInstance(out snapshot);
            if (result != FMOD.RESULT.OK || !snapshot.isValid())
            {
                GameLog.Warning($"GameAudio: failed to create snapshot '{path}' ({result}).");
                return;
            }

            snapshot.start();
        }

        static void StopSnapshot(ref EventInstance snapshot)
        {
            if (!snapshot.isValid())
                return;

            snapshot.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            snapshot.release();
            snapshot.clearHandle();
        }

        void OnDestroy()
        {
            StopSessionMusic();
            StopSnapshot(ref _escSnapshot);
            StopSnapshot(ref _defaultSnapshot);
        }

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
