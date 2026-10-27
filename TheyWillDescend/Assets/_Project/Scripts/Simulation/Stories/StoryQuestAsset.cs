using System;
using UnityEngine;

namespace TheyWillDescend.Simulation.Stories
{
    [CreateAssetMenu(fileName = "StoryQuest", menuName = "They Will Descend/Story Quest")]
    public sealed class StoryQuestAsset : ScriptableObject
    {
        public string questId;
        public string title;
        [TextArea(2, 5)] public string description;
        public StoryGoalKind goal;
        public string goalId;
        [Min(1)] public int required = 1;
        [Min(0f)] public float deadlineHours;
        public StoryEffectAuthoring[] onSuccess = Array.Empty<StoryEffectAuthoring>();
        public StoryEffectAuthoring[] onFail = Array.Empty<StoryEffectAuthoring>();
    }
}
