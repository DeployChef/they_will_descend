using FMOD.Studio;
using FMODUnity;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Presentation.Infrastructure;
using TheyWillDescend.Simulation.City;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Строительный звук постройки. Вешается на префаб здания; если компонента нет,
    /// BuildingViewBoard добавляет его и отдаёт событие стройки из AudioCatalog.
    /// Состояние читается из ECS каждый кадр (компонент Construction):
    ///   поставлено, рабочие не пришли (Elapsed = 0) → PLACEMENT (ваншот);
    ///   рабочие на площадке (Elapsed &gt; 0)          → BUILD (держится до конца стройки);
    ///   Construction снята (стройка закончена)       → COMPLETE (ваншот), потом NONE;
    ///   демонтаж                                     → тишина.
    /// Гейтится конусом камеры через ячейку постройки (BuildingAudioSource.LinkedCell):
    /// ячейка невидима — звука не существует (мёртвое состояние); BUILD возобновляется
    /// при возврате ячейки в конус. Ваншоты PLACEMENT/COMPLETE в невидимой ячейке не доигрываются.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingConstructionAudio : MonoBehaviour
    {
        // Значения = лейблы параметра BUILDING в FMOD (NONE/PLACEMENT/BUILD/COMPLETE).
        enum BuildState { None = 0, Placement = 1, Build = 2, Complete = 3 }

        // В FMOD у параметра хвостовой пробел ("BUILDING ") — резолвим по ID через Trim.
        const string ParamName = "BUILDING";

        [Header("FMOD")]
        [Tooltip("Ивент BUILD (drag-and-drop из FMOD Studio). Пусто — событие стройки из AudioCatalog. Банк — в наборе GameWorld.")]
        [SerializeField] EventReference buildEvent;

        [Header("Debug")]
        [SerializeField] bool logActivity;

        EventInstance _instance;
        PARAMETER_ID _paramId;
        bool _paramResolved;
        BuildState _state = BuildState.None;
        bool _synced;
        bool _logOnce;

        BuildingAudioSource _audioSource;

        /// <summary>Событие, если на префабе своё не задано. Отдаёт владелец вида.</summary>
        public void UseDefaultEvent(EventReference fallback)
        {
            if (buildEvent.IsNull)
                buildEvent = fallback;
        }

        /// <summary>
        /// Синхронизация состояния из ECS. Вызывается BuildingViewBoard каждый кадр.
        /// </summary>
        public void SyncState(EntityManager em, Entity entity)
        {
            var target = ResolveState(em, entity);
            var first = !_synced;
            _synced = true;

            // Первый синк уже законченного здания (загрузка снапшота) — COMPLETE не играем.
            if (first && target == BuildState.Complete)
            {
                _state = BuildState.None;
                return;
            }

            var visible = IsZoneVisible();

            // Демонтаж или снятие стройки — тишина.
            if (target == BuildState.None)
            {
                if (_state != BuildState.None)
                {
                    StopInstance();
                    _state = BuildState.None;
                    if (logActivity)
                        GameLog.Info($"BuildingConstructionAudio: {name} → NONE (dismantle/stop).");
                }
                return;
            }

            // Переход состояния: PLACEMENT → BUILD → COMPLETE.
            if (target != _state)
            {
                var previous = _state;
                _state = target;
                if (visible)
                    PlayState(target);
                if (logActivity)
                    GameLog.Info($"BuildingConstructionAudio: {name} {previous} → {target} (visible={visible}).");
                return;
            }

            // То же состояние: гейтинг конусом камеры.
            if (!visible)
            {
                // Зона невидима — мёртвое состояние, звука не существует.
                if (_instance.isValid())
                {
                    StopInstance();
                    if (logActivity)
                        GameLog.Info($"BuildingConstructionAudio: {name} silenced (zone invisible).");
                }
                return;
            }

            // Зона видима:
            // BUILD держится — инстанс заглушен конусом раньше → возобновляем.
            if (_state == BuildState.Build && !_instance.isValid())
            {
                PlayState(BuildState.Build);
                return;
            }

            // COMPLETE-ваншот доиграл → NONE, инстанс освобождаем.
            if (_state == BuildState.Complete && _instance.isValid() && IsStopped())
            {
                SetParam(BuildState.None);
                StopInstance();
                _state = BuildState.None;
            }
        }

        /// <summary>
        /// Состояние стройки из ECS: Construction есть — строится (Elapsed = 0 →
        /// рабочие ещё не пришли, &gt; 0 → стройка идёт); Construction нет — закончена.
        /// Демонтаж (IsDismantling) — тишина (None).
        /// </summary>
        static BuildState ResolveState(EntityManager em, Entity entity)
        {
            if (!em.HasComponent<Construction>(entity))
                return BuildState.Complete;

            var construction = em.GetComponentData<Construction>(entity);
            if (construction.IsDismantling)
                return BuildState.None;
            return construction.Elapsed > 0.0001f
                ? BuildState.Build
                : BuildState.Placement;
        }

        /// <summary>Ячейка постройки видима (конус камеры). Нет ячейки — не гейтим.</summary>
        bool IsZoneVisible()
        {
            _audioSource ??= GetComponent<BuildingAudioSource>();
            var cell = _audioSource != null ? _audioSource.LinkedCell : null;
            return cell == null || cell.IsVisible;
        }

        void PlayState(BuildState state)
        {
            if (!EnsureInstance())
                return;

            SetParam(state);
            _instance.start();
        }

        /// <summary>Создаёт инстанс (лениво), резолвит ID параметра BUILDING.</summary>
        bool EnsureInstance()
        {
            if (_instance.isValid())
                return true;

            if (buildEvent.IsNull)
            {
                LogOnce($"BuildingConstructionAudio: {name} has no build event (prefab or AudioCatalog).");
                return false;
            }

            try
            {
                _instance = RuntimeManager.CreateInstance(buildEvent);
            }
            catch (EventNotFoundException)
            {
                LogOnce($"BuildingConstructionAudio: event {buildEvent} not in loaded banks (GameWorld set).");
                return false;
            }

            if (!_instance.isValid())
            {
                LogOnce($"BuildingConstructionAudio: instance invalid for {buildEvent}.");
                return false;
            }

            ResolveParam();
            RuntimeManager.AttachInstanceToGameObject(_instance, gameObject);
            return true;
        }

        /// <summary>
        /// Резолвит ID параметра BUILDING. В FMOD-проекте у имени хвостовой пробел
        /// ("BUILDING ") — сравниваем через Trim, матч по имени строгий.
        /// </summary>
        void ResolveParam()
        {
            if (_instance.getDescription(out var description) != FMOD.RESULT.OK || !description.isValid())
            {
                LogOnce("BuildingConstructionAudio: getDescription failed.");
                return;
            }

            if (description.getParameterDescriptionCount(out var count) != FMOD.RESULT.OK)
            {
                LogOnce("BuildingConstructionAudio: getParameterDescriptionCount failed.");
                return;
            }

            for (var i = 0; i < count; i++)
            {
                if (description.getParameterDescriptionByIndex(i, out var desc) != FMOD.RESULT.OK)
                    continue;

                var name = ((string)desc.name)?.Trim();
                if (!string.Equals(name, ParamName, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if ((desc.flags & PARAMETER_FLAGS.READONLY) != 0)
                {
                    LogOnce("BuildingConstructionAudio: параметр BUILDING — READONLY (автоматический). " +
                            "Сделай его Game Parameter (User: Labeled) в FMOD Studio.");
                    return;
                }

                _paramId = desc.id;
                _paramResolved = true;
                return;
            }

            LogOnce($"BuildingConstructionAudio: параметр '{ParamName}' не найден в {buildEvent}. " +
                    "Добавь его в ивент и пересобери банки.");
        }

        void SetParam(BuildState state)
        {
            if (!_paramResolved || !_instance.isValid())
                return;

            // ignoreseekspeed=true — параметр labeled, значение ставится сразу.
            var result = _instance.setParameterByID(_paramId, (float)state, true);
            if (result != FMOD.RESULT.OK && logActivity)
                GameLog.Warning($"BuildingConstructionAudio: setParameterByID('{ParamName}', {state}) FAILED: {result}.");
        }

        bool IsStopped()
        {
            return _instance.getPlaybackState(out var playback) == FMOD.RESULT.OK
                   && playback == PLAYBACK_STATE.STOPPED;
        }

        void StopInstance()
        {
            if (!_instance.isValid())
                return;

            RuntimeManager.DetachInstanceFromGameObject(_instance);
            _instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _instance.release();
            _instance.clearHandle();
        }

        void OnDestroy() => StopInstance();

        void LogOnce(string message)
        {
            if (_logOnce)
                return;
            _logOnce = true;
            GameLog.Warning(message);
        }
    }
}
