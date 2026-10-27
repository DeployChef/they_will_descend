using FMOD.Studio;
using FMODUnity;
using TheyWillDescend.Presentation.Infrastructure;
using Unity.Mathematics;
using UnityEngine;
using VContainer;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Ветер над городом: один вечный 2D-ивент из <see cref="AudioCatalog.Wind"/>, громкость — от высоты камеры.
    /// Банк приходит с набором GameWorld. Живёт в сцене Game: инстанс освобождается вместе с ней.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WindAmbience : MonoBehaviour
    {
        private sealed class ManagedEvent
        {
            public EventReference reference;
            public string eventPath;
            public string distanceRtpc;
            public FMOD.Studio.PARAMETER_ID distanceRtpcId;
            public float deathDistance;
            public float rtpcRange;
            public float hysteresis;
            public EventInstance instance;
            public bool isDead;
        }

        private ManagedEvent[] _events;

        [Header("Camera")]
        [SerializeField] Camera mainCamera;

        [Header("Debug")]
        [SerializeField] bool logActivity = false;

        [Header("Debug: Mouse Wheel Distance")]
        [Tooltip("DEBUG: ручное управление Distance колесом мыши в обход камеры. По умолчанию ВЫКЛ — параметр привязан к позиции камеры.")]
        [SerializeField] bool mouseWheelControlsDistance = false;

        [Header("Distance Mapping")]
        [Tooltip("Дистанция камеры до центра города при МАКСИМАЛЬНОМ приближении → параметр = 0.")]
        [SerializeField] float minCameraDistance = 8f;
        [Tooltip("Дистанция камеры до центра города при МАКСИМАЛЬНОМ отдалении → параметр = 1.")]
        [SerializeField] float maxCameraDistance = 65f;
        [Tooltip("Скорость сглаживания при УМЕНЬШЕНИИ Distance (камера вниз). Больше = быстрее реагирует, меньше = плавнее.")]
        [SerializeField] float smoothSpeed = 4f;
        [Tooltip("Скорость сглаживания при НАРАСТАНИИ Distance (резкое поднятие камеры). Меньше = ветер нарастает плавнее. 0 = использовать Smooth Speed.")]
        [SerializeField] float smoothSpeedRise = 1.5f;

        /// <summary>Шаг изменения Distance за один тик скролла. Жёстко 0.1: 10 тиков от 0 до 1.</summary>
        const float WheelStep = 0.1f;

        /// <summary>Целевое значение Distance 0..1 (дискретные шаги от колеса / непрерывное от камеры).</summary>
        float _wheelTarget;

        /// <summary>Сглаженное значение Distance 0..1 — реально отправляется в FMOD.</summary>
        float _smoothedDistance;

        /// <summary>Центр города — начало координат (HQ стоит в нуле, см. вики 06).</summary>
        readonly float3 _cityCenter = float3.zero;

        string ambienceBusPath;

        // Studio-шина амбиента: все инстансы перематываются в её ChannelGroup,
        // mute/unmute — один вызов _ambienceBus.setMute(...).
        FMOD.Studio.Bus _ambienceBus;
        bool _muted;

        AudioService _audio;

        [Inject]
        public void Construct(AudioService audio)
        {
            _audio = audio;
        }

        void Start()
        {
            _events = System.Array.Empty<ManagedEvent>();
            if (_audio == null)
            {
                GameLog.Error("WindAmbience: AudioService was not injected. Register WindAmbience in GameLifetimeScope.");
                return;
            }

            var wind = _audio.Catalog.Wind;
            if (wind.IsNull)
            {
                GameLog.Warning("WindAmbience: wind event is not assigned in AudioCatalog.");
                return;
            }

            _events = new[]
            {
                new ManagedEvent
                {
                    reference = wind,
                    eventPath = wind.ToString(),
                    distanceRtpc = "Distance",
                    deathDistance = 0f, // 0 = звучит всегда
                    rtpcRange = 100f,
                    hysteresis = 5f,
                    isDead = true
                }
            };

            ambienceBusPath = _audio.Catalog.AmbienceBus;
            ResolveAmbienceBus();

            // Резолвим ID глобального параметра. Сравниваем имена с обрезкой
            // пробелов: в FMOD-проекте параметр может называться 'Distance '
            // (с хвостовым пробелом), и точный матч по имени не срабатывает.
            for (var i = 0; i < _events.Length; i++)
            {
                var e = _events[i];
                if (string.IsNullOrEmpty(e.distanceRtpc))
                    continue;

                var wanted = e.distanceRtpc.Trim();
                var listResult = RuntimeManager.StudioSystem.getParameterDescriptionList(out var descriptions);
                if (listResult != FMOD.RESULT.OK || descriptions == null)
                {
                    GameLog.Warning($"WindAmbience: getParameterDescriptionList FAILED: {listResult}.");
                    continue;
                }

                var found = false;
                for (var p = 0; p < descriptions.Length; p++)
                {
                    var actualName = ((string)descriptions[p].name).Trim();
                    if (!string.Equals(actualName, wanted, System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    e.distanceRtpcId = descriptions[p].id;
                    found = true;
                    if (logActivity)
                        GameLog.Info($"WindAmbience: resolved global param '{actualName}' (wanted '{e.distanceRtpc}') id=({descriptions[p].id.data1}, {descriptions[p].id.data2}) min={descriptions[p].minimum} max={descriptions[p].maximum}.");
                    break;
                }

                if (!found)
                    GameLog.Warning($"WindAmbience: global param '{e.distanceRtpc}' NOT FOUND in banks (дамп: см. DumpGlobalParameters).");
            }

            // Ждём один кадр, чтобы банки точно прогрузились.
            StartCoroutine(DelayedCreateEvents());
        }

        System.Collections.IEnumerator DelayedCreateEvents()
        {
            yield return null; // один кадр ожидания

            var cam = mainCamera ?? Camera.main;
            if (cam != null)
            {
                var camPos = cam.transform.position;
                for (var i = 0; i < _events.Length; i++)
                {
                    var e = _events[i];

                    // Только вечные ивенты (deathDistance=0)
                    if (e.deathDistance == 0f)
                    {
                        if (logActivity)
                            GameLog.Info($"WindAmbience: creating eternal event[{i}]: {e.eventPath}.");
                        CreateEvent(e, camPos);
                    }
                }
            }
        }

        void LateUpdate()
        {
            // 2D-ивенты: обновление Distance RTPC.
            // AttachInstanceToGameObject не нужен — 2D-ивент звучит у слушателя автоматически.
            if (mainCamera == null)
                mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            for (var i = 0; i < _events.Length; i++)
            {
                var e = _events[i];
                if (e == null || e.isDead || !e.instance.isValid())
                    continue;

                // Distance RTPC: либо колесо мыши (debug), либо реальная позиция
                // камеры. Параметр в FMOD: 0..1.
                // Направление: максимальное ПРИБЛИЖЕНИЕ камеры к центру = 0 (тише),
                // максимальное ОТДАЛЕНИЕ = 1 (громче). Значение сглаживается
                // экспоненциально — без ступенек.
                if (e.deathDistance == 0f && !string.IsNullOrEmpty(e.distanceRtpc))
                {
                    float target;

                    if (mouseWheelControlsDistance)
                    {
                        // Debug-режим: цель задаётся колесом мыши (шаг 0.1).
                        UpdateWheelDistance();
                        target = _wheelTarget;
                        if (logActivity && Time.frameCount % 60 == 0)
                            GameLog.Info($"WindAmbience: RTPC {e.distanceRtpc} target={target:F1} (mouse wheel, global).");
                    }
                    else
                    {
                        target = ComputeCameraDistanceNormalized();
                        if (logActivity && Time.frameCount % 60 == 0)
                            GameLog.Info($"WindAmbience: RTPC {e.distanceRtpc} target={target:F3} (camera vs city center, global).");
                    }

                    // Направление прямое: приближение к центру = 0, отдаление = 1.
                    // (Ранее был флаг invertDistance — убран, направление зафиксировано.)

                    // Плавная интерполяция к цели (экспоненциальное сглаживание,
                    // независимо от FPS). Без ступенек между шагами колеса.
                    // Асимметрия: нарастание (резкое поднятие камеры) идёт своей,
                    // более низкой скоростью — ветер разгоняется плавно, спад — быстро.
                    var rising = target > _smoothedDistance;
                    var speed = rising && smoothSpeedRise > 0f ? smoothSpeedRise : smoothSpeed;
                    var t = 1f - Mathf.Exp(-speed * Time.deltaTime);
                    _smoothedDistance = Mathf.Lerp(_smoothedDistance, target, t);
                    if (Mathf.Abs(_smoothedDistance - target) < 0.0005f)
                        _smoothedDistance = target;

                    var normalized = _smoothedDistance;

                    // Ставим глобальный параметр ПО ID (надёжнее имени) с
                    // ignoreseekspeed=true — обходим Seek Speed.
                    var result = RuntimeManager.StudioSystem.setParameterByID(e.distanceRtpcId, normalized, true);
                    if (result != FMOD.RESULT.OK)
                    {
                        if (logActivity && Time.frameCount % 60 == 0)
                            GameLog.Warning($"WindAmbience: setParameterByID('{e.distanceRtpc}') FAILED: {result}.");
                    }

                    // Читаем обратно по ID — доказательство, что FMOD получил значение.
                    if (logActivity && Time.frameCount % 60 == 0)
                    {
                        var readResult = RuntimeManager.StudioSystem.getParameterByID(e.distanceRtpcId, out var readBack);
                        GameLog.Info($"WindAmbience: FMOD readback '{e.distanceRtpc}'={readBack:F3} (sent {normalized:F3}, read result={readResult}).");

                        // Если значение не применилось — дампим ВСЕ глобальные
                        // параметры, чтобы увидеть, что реально есть в банках.
                        if (Mathf.Abs(readBack - normalized) > 0.001f && !_dumpedParams)
                        {
                            _dumpedParams = true;
                            DumpGlobalParameters();
                        }
                    }
                }
            }
        }

        void Update()
        {
            if (_events == null || _events.Length == 0)
                return;

            // Камера может появиться позже Bootstrap — ищем лениво.
            if (mainCamera == null)
                mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            var camPos = mainCamera.transform.position;

            for (var i = 0; i < _events.Length; i++)
            {
                var e = _events[i];
                if (e == null)
                    continue;

                var anchor = Vector3.zero; // eternal events use world center
                var dist = Vector3.Distance(camPos, anchor);
                var deathEnabled = e.deathDistance > 0f;

                if (e.isDead)
                {
                    if (!deathEnabled || dist < e.deathDistance - Mathf.Max(0f, e.hysteresis))
                    {
                        if (logActivity)
                            GameLog.Info($"WindAmbience: try create [{i}] (dist={dist:F1}, deathDist={e.deathDistance}).");
                        CreateEvent(e, camPos);
                    }
                }
                else
                {
                    if (deathEnabled && dist >= e.deathDistance)
                    {
                        KillEvent(e);
                        continue;
                    }

                    // Для eternal events (deathDistance=0) RTPC обновляется в LateUpdate.
                    if (e.deathDistance > 0f)
                    {
                        var rtpc = string.IsNullOrEmpty(e.distanceRtpc) ? "Distance" : e.distanceRtpc;
                        var range = e.rtpcRange > 0f ? e.rtpcRange : e.deathDistance;
                        if (!string.IsNullOrEmpty(rtpc) && range > 0f)
                        {
                            var normalized = dist / range * 100f;
                            var clamped = Mathf.Clamp(normalized, 0f, 100f);
                            e.instance.setParameterByName(rtpc, clamped);
                            if (logActivity && Time.frameCount % 60 == 0)
                                GameLog.Info($"WindAmbience: RTPC [{i}] {rtpc}={clamped:F1} (dist={dist:F1}).");
                        }
                    }

                    if (logActivity && Time.frameCount % 120 == 0)
                    {
                        FMOD.Studio.PLAYBACK_STATE playState = FMOD.Studio.PLAYBACK_STATE.STOPPED;
                        e.instance.getPlaybackState(out playState);

                        // Читаем параметр обратно: глобальный и с инстанса.
                        var globalVal = -1f;
                        var instVal = -1f;
                        RuntimeManager.StudioSystem.getParameterByName(e.distanceRtpc, out globalVal);
                        e.instance.getParameterByName(e.distanceRtpc, out instVal);

                        GameLog.Info($"WindAmbience: instance [{i}] state={playState}, Distance global={globalVal:F3}, instance={instVal:F3}.");
                    }
                }
            }
        }

        void CreateEvent(ManagedEvent e, Vector3 camPos)
        {
            try
            {
                if (e.instance.isValid())
                {
                    e.instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                    e.instance.release();
                }

                e.instance = RuntimeManager.CreateInstance(e.reference);
                if (!e.instance.isValid())
                {
                    if (logActivity)
                        GameLog.Warning($"WindAmbience: instance not valid for {e.eventPath}.");
                    return;
                }

                var preState = FMOD.Studio.PLAYBACK_STATE.STOPPED;
                e.instance.getPlaybackState(out preState);
                if (logActivity)
                    GameLog.Info($"WindAmbience: [{e.eventPath}] pre-start state={preState}.");

                var startResult = e.instance.start();
                if (logActivity)
                    GameLog.Info($"WindAmbience: [{e.eventPath}] start()={startResult}.");

                var postState = FMOD.Studio.PLAYBACK_STATE.STOPPED;
                e.instance.getPlaybackState(out postState);
                if (logActivity)
                    GameLog.Info($"WindAmbience: [{e.eventPath}] after start state={postState}.");

                // Маршрутизируем инстанс в шину амбиента (после start():
                // channel group инстанса гарантированно живёт только с ним).
                RouteInstanceToBus(e.instance, e.eventPath);

                // Шина может быть уже заглушена (mute задан до создания ивента) —
                // это работает автоматически: mute свойство шины, новые дочерние
                // группы наследуют её состояние. Ничего дополнительно делать не нужно.

                e.isDead = false;

                if (logActivity)
                    GameLog.Info($"WindAmbience: [{e.eventPath}] ACTIVATED.");
            }
            catch (System.Exception ex)
            {
                if (logActivity)
                    GameLog.Warning($"WindAmbience: failed to create {e.eventPath}: {ex.Message}");
            }
        }

        void OnDestroy()
        {
            if (_events == null)
                return;
            for (var i = 0; i < _events.Length; i++)
            {
                if (_events[i] != null)
                    KillEvent(_events[i]);
            }
        }

        void KillEvent(ManagedEvent e)
        {
            if (!e.instance.isValid())
            {
                e.isDead = true;
                return;
            }

            e.instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            e.instance.release();
            e.instance = default;
            e.isDead = true;

            if (logActivity)
                GameLog.Info($"WindAmbience: [{e.eventPath}] KILLED.");
        }

        /// <summary>
        /// Debug: обновляет _wheelTarget от колеса мыши (дискретные шаги 0.1).
        /// Скролл вверх = отдаление = цель растёт. Вниз = приближение = падает.
        /// Реальное значение сглаживается в LateUpdate (_smoothedDistance).
        /// ВАЖНО (доки Input System, Background Behavior): в редакторе мышь
        /// отключается при потере фокуса Game View — скролл читается только
        /// когда фокус на Game View.
        /// </summary>
        /// <summary>
        /// Нормализованная ВЫСОТА камеры над землёй под ней: 0..1.
        /// Только вертикаль — пан камеры по карте (WASD) не меняет параметр.
        /// высота = minCameraDistance (макс. приближение) → 0,
        /// высота = maxCameraDistance (макс. отдаление) → 1.
        /// </summary>
        float ComputeCameraDistanceNormalized()
        {
            var height = mainCamera.transform.position.y - _cityCenter.y;
            return Mathf.Clamp01((height - minCameraDistance) / Mathf.Max(0.01f, maxCameraDistance - minCameraDistance));
        }

        void UpdateWheelDistance()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null)
            {
                if (!_warnedNoMouse && logActivity)
                {
                    _warnedNoMouse = true;
                    GameLog.Warning("WindAmbience: Mouse.current is NULL — Input System не видит мышь. Проверь Active Input Handling.");
                }
                return;
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.001f)
                return;

            // Направление: скролл вверх (scroll > 0) → камера отдаляется → цель растёт.
            var direction = Mathf.Sign(scroll);
            _wheelTarget = Mathf.Clamp01(_wheelTarget + direction * WheelStep);

            if (logActivity)
                GameLog.Info($"WindAmbience: wheel scroll={scroll:F1}, target={_wheelTarget:F1}.");
        }

        bool _warnedNoMouse;
        bool _dumpedParams;

        /// <summary>
        /// Диагностика: дамп всех глобальных параметров из FMOD (имя, диапазон,
        /// тип, текущее значение). Показывает, что реально загружено из банков.
        /// </summary>
        void DumpGlobalParameters()
        {
            if (!logActivity)
                return;

            var countResult = RuntimeManager.StudioSystem.getParameterDescriptionCount(out var count);
            if (countResult != FMOD.RESULT.OK)
            {
                GameLog.Warning($"WindAmbience: getParameterDescriptionCount FAILED: {countResult}.");
                return;
            }

            GameLog.Info($"WindAmbience: FMOD has {count} global parameter(s):");

            var listResult = RuntimeManager.StudioSystem.getParameterDescriptionList(out var descriptions);
            if (listResult != FMOD.RESULT.OK || descriptions == null)
            {
                GameLog.Warning($"WindAmbience: getParameterDescriptionList FAILED: {listResult}.");
                return;
            }

            for (var i = 0; i < descriptions.Length; i++)
            {
                var d = descriptions[i];
                var name = (string)d.name; // StringWrapper → строка
                RuntimeManager.StudioSystem.getParameterByID(d.id, out var current);
                GameLog.Info($"WindAmbience:   [{i}] '{name}' min={d.minimum} max={d.maximum} type={d.type} current={current:F3}");
            }
        }

        /// <summary>
        /// Находит Studio-шину амбиента по пути из каталога.
        /// Шина должна быть создана в проекте FMOD Studio и собранна в банки —
        /// в этой версии обёртки нет createBus/getMasterBus, создать шину
        /// из кода нельзя.
        /// </summary>
        void ResolveAmbienceBus()
        {
            if (string.IsNullOrEmpty(ambienceBusPath))
            {
                GameLog.Warning("WindAmbience: ambienceBusPath пуст — шина не будет использована.");
                return;
            }

            var result = RuntimeManager.StudioSystem.getBus(ambienceBusPath, out _ambienceBus);
            if (result != FMOD.RESULT.OK || !_ambienceBus.hasHandle())
            {
                _ambienceBus = default;
                GameLog.Warning($"WindAmbience: bus '{ambienceBusPath}' NOT FOUND ({result}). Создай шину в FMOD Studio (Buses) и пересобери банки. Ambience зазвучит напрямую в мастер-шину.");
                return;
            }

            if (_muted)
                _ambienceBus.setMute(true);

            if (logActivity)
                GameLog.Info($"WindAmbience: ambience bus resolved: '{ambienceBusPath}' (muted={_muted}).");
        }

        /// <summary>
        /// Перематывает channel group инстанса под channel group шины амбиента.
        /// В этой версии FMOD-обёртки у EventInstance нет setBusChannelGroup и
        /// setParentGroup, поэтому используется core-API: Bus.getChannelGroup +
        /// ChannelGroup.addGroup (добавление группы в группу = реparentинг).
        /// ChannelGroup шины по умолчанию залочен Studio — на время операции
        /// разлочиваем, потом залочиваем обратно.
        /// </summary>
        void RouteInstanceToBus(FMOD.Studio.EventInstance instance, string eventPath)
        {
            if (!_ambienceBus.hasHandle())
                return;

            _ambienceBus.unlockChannelGroup();

            var busGroupResult = _ambienceBus.getChannelGroup(out var busGroup);
            var instGroupResult = instance.getChannelGroup(out var instGroup);

            FMOD.RESULT reparentResult = FMOD.RESULT.ERR_INVALID_HANDLE;
            if (busGroupResult == FMOD.RESULT.OK && instGroupResult == FMOD.RESULT.OK)
                reparentResult = busGroup.addGroup(instGroup, true);

            _ambienceBus.lockChannelGroup();

            if (logActivity)
            {
                if (reparentResult == FMOD.RESULT.OK)
                    GameLog.Info($"WindAmbience: [{eventPath}] routed to bus '{ambienceBusPath}'.");
                else
                    GameLog.Warning($"WindAmbience: [{eventPath}] route to bus FAILED: busGroup={busGroupResult}, instGroup={instGroupResult}, addGroup={reparentResult}.");
            }
        }

        /// <summary>
        /// Заглушить/снять заглушку всего амбиента одним вызовом по шине.
        /// Инстансы при этом продолжают играть и двигаться по таймлайну —
        /// глушится только их суммарный сигнал на шине.
        /// </summary>
        public void SetMuted(bool muted)
        {
            _muted = muted;

            if (!_ambienceBus.hasHandle())
            {
                // Фолбэк: шины нет — глушим каждый инстанс напрямую через volume.
                for (var i = 0; i < _events.Length; i++)
                {
                    var e = _events[i];
                    if (e == null || e.isDead || !e.instance.isValid())
                        continue;
                    e.instance.setVolume(muted ? 0f : 1f);
                }

                if (logActivity)
                    GameLog.Warning($"WindAmbience: bus недоступна — применён volume-fallback на инстансы (muted={muted}).");
                return;
            }

            _ambienceBus.setMute(muted);

            if (logActivity)
            {
                _ambienceBus.getMute(out var confirm);
                GameLog.Info($"WindAmbience: bus '{ambienceBusPath}' setMute({muted}) → подтверждение getMute={confirm}.");
            }
        }

        public bool IsMuted => _muted;
    }
}
