using System;
using UnityEngine;

namespace TheyWillDescend.Simulation.Stories
{
    public enum StoryDelivery
    {
        MapPin = 0,
        Immediate = 1
    }

    public enum StoryIconKind
    {
        Exclamation = 0,
        Question = 1
    }

    public enum StoryAnchorRule
    {
        Headquarters = 0,
        BuildingType = 1,
        FactSubject = 2,
        Agent = 3
    }

    public enum StoryGoalKind
    {
        StockAtLeast = 0,
        BuildingsAtLeast = 1
    }

    [Serializable]
    public struct StoryEffectAuthoring
    {
        public StoryEffectKind kind;
        public float amount;
        public string resourceId;
        public StoryDialogAsset dialog;
        public StoryQuestAsset quest;
        public StoryTriggerAsset trigger;
    }

    [Serializable]
    public struct StoryChoiceAuthoring
    {
        public string choiceId;
        public string label;
        public bool fallback;
        public StoryEffectAuthoring[] effects;
    }

    [CreateAssetMenu(fileName = "StoryDialog", menuName = "They Will Descend/Story Dialog")]
    public sealed class StoryDialogAsset : ScriptableObject
    {
        public string dialogId;
        public string title;
        [TextArea(3, 8)] public string body;
        public StoryDelivery delivery;
        public StoryIconKind icon = StoryIconKind.Exclamation;
        public StoryAnchorRule anchor = StoryAnchorRule.Headquarters;
        public string anchorTypeId;
        [Min(0f)] public float openHours;
        public StoryChoiceAuthoring[] choices = Array.Empty<StoryChoiceAuthoring>();
    }

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

    [CreateAssetMenu(fileName = "StoryTrigger", menuName = "They Will Descend/Story Trigger")]
    public sealed class StoryTriggerAsset : ScriptableObject
    {
        public string triggerId;
        public bool armedAtStart = true;
        public bool repeat;
        public StoryFactKind fact = StoryFactKind.RunStarted;
        public string factId;
        public StoryDialogAsset dialog;
        public StoryQuestAsset quest;
    }

    /// <summary>
    /// Scenario pack. Runtime copy is session buffers. Strings stay here for the HUD.
    /// </summary>
    [CreateAssetMenu(fileName = "StoryPack", menuName = "They Will Descend/Story Pack")]
    public sealed class StoryPackAsset : ScriptableObject
    {
        public StoryDialogAsset[] dialogs = Array.Empty<StoryDialogAsset>();
        public StoryQuestAsset[] quests = Array.Empty<StoryQuestAsset>();
        public StoryTriggerAsset[] triggers = Array.Empty<StoryTriggerAsset>();

        public StoryDialogAsset FindDialog(string id)
        {
            return Find(dialogs, id, asset => asset != null ? asset.dialogId : null);
        }

        public StoryQuestAsset FindQuest(string id)
        {
            return Find(quests, id, asset => asset != null ? asset.questId : null);
        }

        static T Find<T>(T[] items, string id, Func<T, string> key) where T : class
        {
            if (items == null || string.IsNullOrWhiteSpace(id))
                return null;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item != null && string.Equals(key(item), id, StringComparison.OrdinalIgnoreCase))
                    return item;
            }

            return null;
        }
    }
}
