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
    /// Привязывается к ячейке через AudioZoneManager.AttachSource (его вызывает
    /// BuildingViewBoard при создании постройки), отвязывается сама при выключении
    /// или удалении объекта.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingAudioSource : MonoBehaviour
    {
        [Header("Building Type")]
        [SerializeField] BuildingAudioSourceType buildingType = BuildingAudioSourceType.House;

        [Header("Activity")]
        [Tooltip("Вес активности ячейки (Cell_Activity). Множитель суммируется по постройкам ячейки.")]
        [SerializeField] [Range(0f, 1f)] float activityWeight = 0.5f;

        /// <summary>Ячейка, к которой привязан этот источник.</summary>
        internal AudioCell LinkedCell { get; set; }

        /// <summary>Менеджер ячеек, который привязал этот источник.</summary>
        internal AudioZoneManager Manager { get; set; }

        /// <summary>
        /// Здание закончено (Construction снята) — учитывается ячейкой в амбиенсе.
        /// Строящееся/демонтируемое здание (CountsForAmbience = false) в амбиенсе
        /// ячейки не участвует: Ambience_Town играет только после COMPLETE.
        /// Обновляется BuildingViewBoard из ECS каждый кадр.
        /// </summary>
        internal bool CountsForAmbience { get; set; }

        public BuildingAudioSourceType BuildingType => buildingType;

        // Ранее здесь был множитель isWorking — флаг не выставлялся из кода
        // (SetWorking никто не вызывал), и на префабе Sawmill он был выключен,
        // из-за чего активность ячейки была 0 и Ambience_Town не будился.
        // Участие в амбиенсе полностью определяет CountsForAmbience.
        public float ActivityWeight => activityWeight;

        void OnEnable()
        {
            // Ячейка жива — возвращаемся в неё. Если она опустела и была
            // уничтожена, пока объект стоял выключенным, просим новую.
            if (LinkedCell != null && !LinkedCell.IsDisposed)
                LinkedCell.AddSource(this);
            else if (Manager != null)
                Manager.AttachSource(this);
        }

        void OnDisable()
        {
            if (Manager != null)
                Manager.DetachSource(this);
        }

        void OnDestroy()
        {
            if (Manager != null)
                Manager.DetachSource(this);
        }
    }
}
