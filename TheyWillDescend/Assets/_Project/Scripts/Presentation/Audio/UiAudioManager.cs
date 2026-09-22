using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using TheyWillDescend.Infrastructure.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Состояния UI-ивента. Номера совпадают с лейблами параметра UI_States
    /// в FMOD Studio (NONE=0, HOVER=1, YES=2, NO=3, WARNING=4, ...).
    /// </summary>
    public enum UiState
    {
        None = 0,
        Hover = 1,
        Yes = 2,
        No = 3,
        Warning = 4,
    }

    /// <summary>
    /// Хост UI-звука. Один живой инстанс event:/UI на всю сессию: ивент вечный
    /// (Persistent On), а звук выбирается параметром UI_States — инструменты
    /// лежат на parameter sheet и триггерятся при входе значения в их колонку.
    /// Поэтому start() вызывается ровно один раз, дальше только SetState().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiAudioManager : MonoBehaviour
    {
        public const string BankName = "UI";
        // Ивент UI лежит в папке UI → путь event:/UI/UI (Master.strings.bank).
        public const string EventPath = "event:/UI/UI";
        const string ParamName = "UI_States";

        static UiAudioManager _instance;

        /// <summary>Создаёт себя сам на отдельном persistent-объекте — сцена не нужна.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Bootstrap()
        {
            if (_instance != null)
                return;

            var go = new GameObject(nameof(UiAudioManager));
            _instance = go.AddComponent<UiAudioManager>();
        }

        /// <summary>Может быть null, если менеджер ещё не создался или объект удалён.</summary>
        public static UiAudioManager Instance => _instance;

        EventInstance _evt;
        PARAMETER_ID _stateParamId;
        bool _paramResolved;
        UiState _current = UiState.None;

        [Header("Debug")]
        [SerializeField] bool logActivity = false;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            FmodBankLoader.LoadBank(BankName);
            StartCoroutine(DelayedCreate());
        }

        System.Collections.IEnumerator DelayedCreate()
        {
            // LoadBank асинхронный: ждём готовности банка, а не один кадр.
            // Поллим состояние загрузки до READY (с таймаутом).
            var waitUntil = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < waitUntil)
            {
                if (RuntimeManager.HasBankLoaded(BankName))
                    break;

                yield return null;
            }

            if (!RuntimeManager.HasBankLoaded(BankName))
            {
                GameLog.Error($"UiAudioManager: bank '{BankName}' не загрузился за 10с. " +
                              "Проверь, что UI.bank скопирован в Assets/StreamingAssets/Desktop/.");
                yield break;
            }

            try
            {
                _evt = RuntimeManager.CreateInstance(EventPath);
            }
            catch (EventNotFoundException)
            {
                GameLog.Error($"UiAudioManager: {EventPath} not in loaded banks. " +
                              "In FMOD Studio put it on bank UI, Ctrl+B, copy .bank into StreamingAssets/Desktop/.");
                yield break;
            }
            catch (Exception e)
            {
                GameLog.Error($"UiAudioManager: failed to create instance. {e.Message}");
                yield break;
            }

            if (!_evt.isValid())
            {
                GameLog.Warning($"UiAudioManager: instance is invalid for {EventPath}.");
                yield break;
            }

            ResolveStateParam();

            // Стартовое состояние — NONE: иначе ивент откроется на значении из
            // пресета параметра и один из звуков сыграет при запуске.
            if (_paramResolved)
                _evt.setParameterByID(_stateParamId, (float)UiState.None, true);

            _evt.start();
            _current = UiState.None;

            if (logActivity)
                GameLog.Info($"UiAudioManager: {EventPath} started, param resolved={_paramResolved}.");
        }

        /// <summary>
        /// Резолвит ID параметра UI_States у ивента. Имя сравнивается через
        /// getParameterDescriptionByName — FMOD сам матчит по имени параметра.
        /// </summary>
        void ResolveStateParam()
        {
            var descResult = _evt.getDescription(out var description);
            if (descResult != FMOD.RESULT.OK || !description.isValid())
            {
                GameLog.Warning($"UiAudioManager: getDescription FAILED: {descResult}.");
                return;
            }

            // У EventDescription в FMOD 2.03 нет getParameterDescriptionList —
            // берём описание сразу по имени.
            var result = description.getParameterDescriptionByName(ParamName, out var paramDesc);
            if (result != FMOD.RESULT.OK)
            {
                GameLog.Warning($"UiAudioManager: param '{ParamName}' NOT FOUND in {EventPath} ({result}). " +
                                "Проверь, что параметр добавлен в ивент и банки пересобраны.");
                return;
            }

            if ((paramDesc.flags & PARAMETER_FLAGS.READONLY) != 0)
            {
                GameLog.Warning($"UiAudioManager: параметр '{ParamName}' — READONLY (автоматический). " +
                                "Сделай его Game Parameter (User: Labeled) в FMOD Studio.");
                return;
            }

            _stateParamId = paramDesc.id;
            _paramResolved = true;

            if (logActivity)
                GameLog.Info($"UiAudioManager: resolved param '{ParamName}' min={paramDesc.minimum} max={paramDesc.maximum}.");
        }

        /// <summary>
        /// Переключает состояние UI-ивента. Одинаковое состояние подряд не
        /// переотправляется: FMOD триггерит инструмент только на смену значения.
        /// </summary>
        public void SetState(UiState state)
        {
            if (!_paramResolved || !_evt.isValid())
                return;

            if (state == _current)
                return;

            _current = state;

            // ignoreseekspeed=true — параметр дискретный, значение должно
            // примениться сразу, иначе Seek Speed растянет установку.
            var result = _evt.setParameterByID(_stateParamId, (float)state, true);
            if (result != FMOD.RESULT.OK && logActivity)
                GameLog.Warning($"UiAudioManager: setParameterByID('{ParamName}', {state}) FAILED: {result}.");

            if (logActivity)
                GameLog.Info($"UiAudioManager: state → {state}.");
        }

        /// <summary>Громкость всего UI одним вызовом.</summary>
        public void SetVolume(float volume)
        {
            if (!_evt.isValid())
                return;

            _evt.setVolume(Mathf.Clamp01(volume));
        }

        // === Глобальный перехват hover: рейкаст курсора каждый кадр ===
        // Покрывает ВСЕ кнопки всех канвас (Button, LayeredButton, Toggle, Slider)
        // без правки сцен и вешания компонентов на каждую кнопку.

        PointerEventData _pointerData;
        readonly List<RaycastResult> _raycastResults = new();
        GameObject _hoveredButton;

        void Update()
        {
            if (!_paramResolved)
                return;

            var es = EventSystem.current;
            if (es == null)
                return;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null)
                return;

            if (_pointerData == null)
                _pointerData = new PointerEventData(es);

            _pointerData.position = mouse.position.ReadValue();
            _raycastResults.Clear();
            es.RaycastAll(_pointerData, _raycastResults);

            // Первая интерактивная кнопка под курсором.
            GameObject found = null;
            for (var i = 0; i < _raycastResults.Count; i++)
            {
                var selectable = _raycastResults[i].gameObject.GetComponentInParent<Selectable>(true);
                if (selectable == null || !selectable.IsInteractable())
                    continue;

                found = selectable.gameObject;
                break;
            }

            if (found == _hoveredButton)
                return;

            _hoveredButton = found;
            SetState(found != null ? UiState.Hover : UiState.None);
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            if (!_evt.isValid())
                return;

            _evt.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _evt.release();
            _evt.clearHandle();
        }
    }
}
