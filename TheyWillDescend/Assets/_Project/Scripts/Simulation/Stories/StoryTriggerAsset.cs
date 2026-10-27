using UnityEngine;

namespace TheyWillDescend.Simulation.Stories
{
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
}
