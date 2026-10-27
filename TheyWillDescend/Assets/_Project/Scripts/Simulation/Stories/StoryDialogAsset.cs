using System;
using UnityEngine;

namespace TheyWillDescend.Simulation.Stories
{
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
}
