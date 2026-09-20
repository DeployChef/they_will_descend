using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Тестовые чит-кнопки. Работают напрямую с GameTime в симуляции,
    /// минуя команды: это отладочный инструмент, не игровая логика.
    /// </summary>
    public sealed class CheatButtons : MonoBehaviour
    {
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

        void Awake()
        {
            HudButtons.Bind(toggleDayNightButton, ToggleDayNight);
            HudButtons.Bind(setDayButton, () => SetHour(dayHour));
            HudButtons.Bind(setNightButton, () => SetHour(nightHour));
        }

        void OnDestroy()
        {
            HudButtons.Unbind(toggleDayNightButton, ToggleDayNight);
            HudButtons.Unbind(setDayButton, () => SetHour(dayHour));
            HudButtons.Unbind(setNightButton, () => SetHour(nightHour));
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
