using System.Collections.Generic;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Dev-панель чит-кнопок. Toggle по клавише + (numpad или =/+).
    /// Это отладка, не игровая логика: день/ночь пишут GameTime напрямую,
    /// ресурсы двигают ResourceAmount тем же clamp, что и ledger.
    /// Ряды +/- клонируются из authored-шаблона по ResourceInfo — новый ресурс
    /// в каталоге сам появляется в панели. Не кладём в GameInput.
    /// </summary>
    public sealed class CheatButtons : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField, Tooltip("Корень кнопок (Day / Night / Spawn / ресурсы). Стартует закрытым.")]
        GameObject panel;

        [Header("Day / Night")]
        [SerializeField, Tooltip("Кнопка-переключатель: день -> ночь -> день.")]
        Button toggleDayNightButton;
        [SerializeField, Tooltip("Кнопка 'сделать день'. Опционально.")]
        Button setDayButton;
        [SerializeField, Tooltip("Кнопка 'сделать ночь'. Опционально.")]
        Button setNightButton;

        [SerializeField, Range(0f, 24f), Tooltip("Час, в который прыгаем при переключении на день.")]
        float dayHour = 9f;
        [SerializeField, Range(0f, 24f), Tooltip("Час, в который прыгаем при переключении на ночь.")]
        float nightHour = 21f;

        [Header("Resources")]
        [SerializeField, Tooltip("Ряд +/- (сейчас EnergyCheats). Клонируется на каждый ресурс из каталога.")]
        GameObject resourceRowTemplate;
        [SerializeField, Tooltip("Плюс на шаблоне. Если шаблон пуст — берём родителя этой кнопки.")]
        Button addEnergyButton;
        [SerializeField, Tooltip("Минус на шаблоне.")]
        Button removeEnergyButton;
        [SerializeField, FormerlySerializedAs("energyStep"), Min(0.001f), Tooltip("Сколько единиц добавляем или снимаем одним нажатием.")]
        float resourceStep = 5f;

        UnityAction _setDay;
        UnityAction _setNight;
        readonly List<(Button button, UnityAction action)> _resourceBinds = new();
        bool _resourceRowsBuilt;

        void Awake()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            gameObject.SetActive(false);
            return;
#endif
            _setDay = () => SetHour(dayHour);
            _setNight = () => SetHour(nightHour);
            HudButtons.Bind(toggleDayNightButton, ToggleDayNight);
            HudButtons.Bind(setDayButton, _setDay);
            HudButtons.Bind(setNightButton, _setNight);
            SetPanelOpen(false);
        }

        void OnDestroy()
        {
            if (_setDay == null)
                return;
            HudButtons.Unbind(toggleDayNightButton, ToggleDayNight);
            HudButtons.Unbind(setDayButton, _setDay);
            HudButtons.Unbind(setNightButton, _setNight);
            for (var i = 0; i < _resourceBinds.Count; i++)
                HudButtons.Unbind(_resourceBinds[i].button, _resourceBinds[i].action);
        }

        void Update()
        {
            EnsureResourceRows();

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.numpadPlusKey.wasPressedThisFrame || keyboard.equalsKey.wasPressedThisFrame)
                TogglePanel();
        }

        void EnsureResourceRows()
        {
            if (_resourceRowsBuilt)
                return;
            if (!TryGetTemplate(out var template))
                return;
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasBuffer<ResourceInfo>(bag))
                return;

            var info = em.GetBuffer<ResourceInfo>(bag);
            if (info.Length == 0)
                return;

            var parent = template.transform.parent;
            var rows = new GameObject[info.Length];
            rows[0] = template;
            for (var i = 1; i < info.Length; i++)
                rows[i] = Instantiate(template, parent);

            for (var i = 0; i < info.Length; i++)
            {
                rows[i].name = $"Cheat_{info[i].ResourceId}";
                rows[i].SetActive(true);
                BindResourceRow(rows[i], info[i], i == 0);
            }

            _resourceRowsBuilt = true;
        }

        bool TryGetTemplate(out GameObject template)
        {
            template = resourceRowTemplate;
            if (template == null && addEnergyButton != null)
                template = addEnergyButton.transform.parent != null
                    ? addEnergyButton.transform.parent.gameObject
                    : addEnergyButton.gameObject;
            return template != null;
        }

        void BindResourceRow(GameObject row, in ResourceInfo info, bool original)
        {
            if (!TryGetRowButtons(row, original, out var add, out var remove))
                return;

            var id = info.ResourceId;
            var name = info.DisplayName.IsEmpty ? id.ToString() : info.DisplayName.ToString();
            var step = resourceStep;
            BindResourceButton(add, $"+{step:0} {name}", id, step);
            BindResourceButton(remove, $"-{step:0} {name}", id, -step);
        }

        bool TryGetRowButtons(GameObject row, bool original, out Button add, out Button remove)
        {
            if (original && addEnergyButton != null && removeEnergyButton != null)
            {
                add = addEnergyButton;
                remove = removeEnergyButton;
                return true;
            }

            var buttons = row.GetComponentsInChildren<Button>(true);
            if (buttons.Length < 2)
            {
                add = null;
                remove = null;
                return false;
            }

            add = buttons[0];
            remove = buttons[1];
            return true;
        }

        void BindResourceButton(Button button, string label, FixedString64Bytes id, float delta)
        {
            button.onClick.RemoveAllListeners();
            HudButtons.SetLabel(button, label);
            UnityAction action = () => ApplyResource(id, delta);
            HudButtons.Bind(button, action);
            _resourceBinds.Add((button, action));
        }

        void TogglePanel()
        {
            SetPanelOpen(panel != null && !panel.activeSelf);
        }

        void SetPanelOpen(bool open)
        {
            if (panel != null)
                panel.SetActive(open);
        }

        /// <summary>
        /// Двигает склад напрямую. Значение зажимается тем же StockCap,
        /// что использует ResourceLedger, поэтому HUD не уедет за 100%.
        /// </summary>
        void ApplyResource(FixedString64Bytes id, float delta)
        {
            if (!SimWorld.TryGet(out var em, out var bag)
                || !em.HasBuffer<ResourceAmount>(bag))
                return;

            var stock = em.GetBuffer<ResourceAmount>(bag);

            if (em.HasBuffer<ResourceInfo>(bag))
            {
                ResourceLedger.AddClamped(stock, em.GetBuffer<ResourceInfo>(bag), id, delta);
                return;
            }

            ResourceLedger.Add(stock, id, delta);
        }

        void ToggleDayNight()
        {
            if (!TryGetGameTime(out var time))
                return;

            // День = рабочая смена (по умолчанию 6:00-18:00). Сейчас день — прыгаем в ночь, и наоборот.
            SetHour(time.IsWorkShift ? nightHour : dayHour);
        }

        void SetHour(float hour)
        {
            if (!TryGetGameTime(out var time))
                return;

            var duration = time.DayDuration > 0.0001f ? time.DayDuration : 1f;
            time.ElapsedInDay = Mathf.Clamp01(hour / 24f) * duration;
            WriteGameTime(time);
        }

        static bool TryGetGameTime(out GameTime time)
        {
            time = default;
            if (!SimWorld.TryGet(out var em, out _))
                return false;

            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<GameTime>());
            if (query.IsEmptyIgnoreFilter)
                return false;

            time = query.GetSingleton<GameTime>();
            return true;
        }

        static void WriteGameTime(GameTime time)
        {
            if (!SimWorld.TryGet(out var em, out _))
                return;

            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<GameTime>());
            if (query.IsEmptyIgnoreFilter)
                return;

            em.SetComponentData(query.GetSingletonEntity(), time);
        }
    }
}
