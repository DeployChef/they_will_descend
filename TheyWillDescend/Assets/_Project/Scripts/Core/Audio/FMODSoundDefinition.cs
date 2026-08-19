using UnityEngine;

namespace Futboloid.Core.Audio
{
    /// <summary>
    /// Определение одного FMOD-звука. Настраивается в Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "FMODSoundDefinition", menuName = "Futboloid/Audio/FMOD Sound Definition")]
    public class FMODSoundDefinition : ScriptableObject
    {
        /// <summary>
        /// FMOD Event Path, например "event:/SFX/Gameplay/BallHit"
        /// </summary>
        [field: SerializeField]
        public string EventPath { get; private set; }

        /// <summary>
        /// FMOD Bus для группировки (Music, SFX, UI)
        /// </summary>
        [field: SerializeField]
        public string Bus { get; private set; } = "SFX";

        /// <summary>
        /// Базовая громкость (0..1)
        /// </summary>
        [field: Range(0f, 1f)]
        [field: SerializeField]
        public float Volume { get; private set; } = 1f;

        /// <summary>
        /// Базовый питч
        /// </summary>
        [field: Range(0.1f, 4f)]
        [field: SerializeField]
        public float Pitch { get; private set; } = 1f;

        /// <summary>
        /// Случайное отклонение питча (±range)
        /// </summary>
        [field: Range(0f, 0.5f)]
        [field: SerializeField]
        public float PitchRandomRange { get; private set; } = 0f;

        /// <summary>
        /// Минимальный интервал между воспроизведениями одного и того же звука (сек)
        /// </summary>
        [field: Range(0f, 5f)]
        [field: SerializeField]
        public float Cooldown { get; private set; } = 0f;

        /// <summary>
        /// Зацикливание (для музыки и фоновых звуков)
        /// </summary>
        [field: SerializeField]
        public bool Loop { get; private set; } = false;

        /// <summary>
        /// Длительность fade in при воспроизведении (сек)
        /// </summary>
        [field: Range(0f, 2f)]
        [field: SerializeField]
        public float FadeInDuration { get; private set; } = 0f;

        /// <summary>
        /// Длительность fade out при остановке (сек)
        /// </summary>
        [field: Range(0f, 2f)]
        [field: SerializeField]
        public float FadeOutDuration { get; private set; } = 0f;
    }
}
