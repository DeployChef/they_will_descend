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
    /// FMOD host on Root. Lives with the Root camera and AudioListener.
    /// Один инстанс музыки на сцене: event:/SOUNDTRACK/MUSIC_STATES (банк Main_theme).
    /// Пауза следует за <see cref="SimControl.PlayerPaused"/> (меню паузы: Esc / Continue) —
    /// это единственный источник истины, toggle по клавише разъезжался с кнопкой Continue.
    /// Пауза → снэпшот ESC, выход из паузы → ESC снимается (DEAFULT удалён из FMOD-проекта).
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        // Банк музыки (MUSIC_STATES лежит в Main_theme вместе со саундтреком).
        public const string MusicBankName = "Main_theme";
        // Ивент лежит в папке SOUNDTRACK → путь event:/SOUNDTRACK/MUSIC_STATES.
        public const string MusicEventPath = "event:/SOUNDTRACK/MUSIC_STATES";

        // Снэпшот паузы (корень Snapshots). DEAFULT удалён из FMOD-проекта — не стартовать.
        public const string PauseSnapshotPath = "snapshot:/ESC";

        EventInstance _music;
        EventInstance _escSnapshot;
        bool _lastPaused;

        void Start()
        {
            // Банк грузим явно: RuntimeManager может не успеть загрузить банки до Start().
            FmodBankLoader.LoadBank(MusicBankName);
            StartCoroutine(CreateMusicNextFrame());
        }

        System.Collections.IEnumerator CreateMusicNextFrame()
        {
            // Ждём один кадр, чтобы банк точно прогрузился (паттерн GlobalAmbienceManager).
            yield return null;

            try
            {
                _music = RuntimeManager.CreateInstance(MusicEventPath);
            }
            catch (EventNotFoundException)
            {
                GameLog.Error(
                    "GameAudio: event:/SOUNDTRACK/MUSIC_STATES not in loaded banks. " +
                    "In FMOD Studio put it on bank Main_theme, Ctrl+B, copy .bank into StreamingAssets/Desktop/.");
                yield break;
            }
            catch (Exception e)
            {
                GameLog.Error($"GameAudio: failed to create music instance. {e.Message}");
                yield break;
            }

            if (!_music.isValid())
            {
                GameLog.Error("GameAudio: music instance is invalid.");
                yield break;
            }

            // MUSIC_STATES — 3D-ивент: без атрибутов RuntimeManager варнит каждый кадр.
            // Вешаем на слушателя — атрибуты обновляются сами, музыка звучит "вокруг" камеры.
            var listener = FindFirstObjectByType<StudioListener>();
            if (listener != null)
                RuntimeManager.AttachInstanceToGameObject(_music, listener.transform);

            _music.start();
            GameLog.Info("GameAudio: music started (event:/SOUNDTRACK/MUSIC_STATES).");
        }

        void LateUpdate()
        {
            // Источник истины — PlayerPaused (меню паузы). Esc открывает/закрывает меню,
            // Continue закрывает кнопкой — оба пути сходятся здесь, рассинхрона нет.
            var paused = false;
            if (SimWorld.TryGet(out var em, out var bag) && em.HasComponent<SimControl>(bag))
                paused = em.GetComponentData<SimControl>(bag).PlayerPaused != 0;

            if (paused == _lastPaused)
                return;

            _lastPaused = paused;
            ApplyPause(paused);

            if (_music.isValid())
                _music.setPaused(paused);
        }

        void ApplyPause(bool paused)
        {
            if (paused)
                StartSnapshot(PauseSnapshotPath, ref _escSnapshot);
            else
                StopSnapshot(ref _escSnapshot);
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
            if (_music.isValid())
            {
                _music.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                _music.release();
                _music.clearHandle();
            }
            StopSnapshot(ref _escSnapshot);
        }
    }
}
