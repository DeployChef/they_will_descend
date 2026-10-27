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
