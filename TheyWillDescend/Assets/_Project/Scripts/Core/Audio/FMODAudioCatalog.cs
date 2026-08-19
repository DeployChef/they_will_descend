using UnityEngine;
using System.Collections.Generic;

namespace Futboloid.Core.Audio
{
    /// <summary>
    /// Каталог FMOD-звуков проекта. Заполняется в Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "FMODAudioCatalog", menuName = "Futboloid/Audio/FMOD Audio Catalog")]
    public class FMODAudioCatalog : ScriptableObject
    {
        [SerializeField] private List<FMODSoundDefinition> _sounds = new();
        
        private readonly Dictionary<string, FMODSoundDefinition> _lookup = new();

        private void OnEnable()
        {
            _lookup.Clear();
            foreach (var def in _sounds)
            {
                if (def != null && !string.IsNullOrEmpty(def.EventPath))
                {
                    _lookup[def.EventPath] = def;
                }
            }
        }

        /// <summary>
        /// Получить определение звука по Event Path.
        /// </summary>
        public bool TryGetDefinition(string eventPath, out FMODSoundDefinition definition)
        {
            return _lookup.TryGetValue(eventPath, out definition);
        }

        /// <summary>
        /// Получить определение звука (выбрасывает исключение, если не найден).
        /// </summary>
        public FMODSoundDefinition GetDefinition(string eventPath)
        {
            if (!_lookup.TryGetValue(eventPath, out var def))
            {
                Debug.LogError($"[FMODAudioCatalog] Sound not found: {eventPath}");
                return null;
            }
            return def;
        }

        /// <summary>
        /// Все зарегистрированные определения.
        /// </summary>
        public IEnumerable<FMODSoundDefinition> AllDefinitions => _sounds;

        /// <summary>
        /// Константы Event Path для удобного обращения.
        /// </summary>
        public static class Paths
        {
            // --- SFX Gameplay ---
            public const string BallHit = "event:/SFX/Gameplay/BallHit";
            public const string BallHitMan = "event:/SFX/Gameplay/BallHitMan";
            public const string GoalScored = "event:/SFX/Gameplay/GoalScored";
            public const string GoalConceded = "event:/SFX/Gameplay/GoalConceded";
            public const string MatchStart = "event:/SFX/Gameplay/MatchStart";
            public const string MatchEnd = "event:/SFX/Gameplay/MatchEnd";
            public const string DefenderHit = "event:/SFX/Gameplay/DefenderHit";
            public const string DefenderDestroyed = "event:/SFX/Gameplay/DefenderDestroyed";
            public const string PromotionStarted = "event:/SFX/Gameplay/PromotionStarted";
            public const string PromotionCompleted = "event:/SFX/Gameplay/PromotionCompleted";
            public const string DefenderReturned = "event:/SFX/Gameplay/DefenderReturned";
            public const string DefenderRoleChanged = "event:/SFX/Gameplay/DefenderRoleChanged";
            public const string TimeBonus = "event:/SFX/Gameplay/TimeBonus";
            public const string TimePenalty = "event:/SFX/Gameplay/TimePenalty";
            public const string BuffApplied = "event:/SFX/Gameplay/BuffApplied";
            public const string DebuffApplied = "event:/SFX/Gameplay/DebuffApplied";
            public const string BuffConsumed = "event:/SFX/UI/BuffConsumed";

            // --- SFX UI ---
            public const string PerkPick = "event:/SFX/UI/PerkPick";
            public const string LevelUp = "event:/SFX/UI/LevelUp";
            public const string ReshuffleStart = "event:/SFX/UI/ReshuffleStart";
            public const string BonusPickOpen = "event:/SFX/UI/BonusPickOpen";
            public const string ComboMultiplierUp = "event:/SFX/UI/ComboMultiplierUp";
            public const string ComboMultiplierDown = "event:/SFX/UI/ComboMultiplierDown";
            public const string ScorePoints = "event:/SFX/UI/ScorePoints";

            // --- Music ---
            public const string MusicMatch = "event:/Music/Match";
            public const string MusicPause = "event:/Music/Pause";

            // --- UI Navigation ---
            public const string UiMenuOpen = "event:/UI/Menu/Open";
            public const string UiPauseOpen = "event:/UI/Pause/Open";
            public const string UiTournamentOpen = "event:/UI/Tournament/Open";
        }
    }
}
