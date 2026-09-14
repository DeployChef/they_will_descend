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
        [SerializeField] [Range(0f, 1f)] float activityWeight = 0.5f;

        [Header("Audio")]
        [SerializeField] bool isWorking = true;

        /// <summary>Ссылка на зону, к которой привязан этот источник.</summary>
        internal AudioZone LinkedZone { get; set; }

        public BuildingAudioSourceType BuildingType => buildingType;
        public float ActivityWeight => isWorking ? activityWeight : 0f;

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

        /// <summary>
        /// Обновляет состояние работы (для RTPC).
        /// </summary>
        public void SetWorking(bool working) => isWorking = working;
    }
}
