using UnityEngine;
using TheyWillDescend.Presentation.Audio;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Тип здания для аудио-системы. Определяет RTPC-параметры.
    /// </summary>
    public enum BuildingAudioSourceType
    {
        None = 0,
        House = 1,
        Workshop = 2,
        Market = 3,
        Infrastructure = 4,
        Decoration = 5
    }

    /// <summary>
    /// Компонент на постройку. Сообщает AudioZoneManager о своём типе и активности.
    /// Автоматически регистрируется/отписывается при старте/удалении.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingAudioSource : MonoBehaviour
    {
        [Header("Building Type")]
        [SerializeField] BuildingAudioSourceType buildingType = BuildingAudioSourceType.House;

        [Header("Activity")]
        [Tooltip("Вес активности зоны (Cell_Activity). Множитель суммируется по постройкам зоны.")]
        [SerializeField] [Range(0f, 1f)] float activityWeight = 0.5f;

        /// <summary>Ссылка на зону, к которой привязан этот источник.</summary>
        internal AudioZone LinkedZone { get; set; }

        /// <summary>
        /// Здание закончено (Construction снята) — учитывается зоной в амбиенсе.
        /// Строящееся/демонтируемое здание (CountsForAmbience = false) в амбиенсе
        /// зоны не участвует: Ambience_Town играет только после COMPLETE.
        /// Обновляется BuildingViewBoard из ECS каждый кадр.
        /// </summary>
        internal bool CountsForAmbience { get; set; }

        public BuildingAudioSourceType BuildingType => buildingType;

        // Ранее здесь был множитель isWorking — флаг не выставлялся из кода
        // (SetWorking никто не вызывал), и на префабе Sawmill он был выключен,
        // из-за чего активность зоны была 0 и Ambience_Town не будился.
        // Участие в амбиенсе полностью определяет CountsForAmbience.
        public float ActivityWeight => activityWeight;

        void OnEnable()
        {
            if (LinkedZone != null)
                LinkedZone.AddAudioSource(this);
        }

        void OnDisable()
        {
            if (LinkedZone != null)
                LinkedZone.RemoveAudioSource(this);
        }

        void OnDestroy()
        {
            if (LinkedZone != null)
                LinkedZone.RemoveAudioSource(this);
        }
    }
}
