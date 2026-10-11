using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Stories
{
    public enum StoryFactKind : byte
    {
        None = 0,
        RunStarted = 1,
        EraReached = 2,
        BuildingCompleted = 3,
        ProcessClosed = 4
    }

    public enum StoryEffectKind : byte
    {
        None = 0,
        Loyalty = 1,
        Resource = 2,
        StartDialog = 3,
        StartQuest = 4,
        ArmTrigger = 5,
        KillAnchor = 6
    }

    /// <summary>One-tick signal. Cleared at the end of the story pass that matched it.</summary>
    public struct StoryFact : IBufferElementData
    {
        public StoryFactKind Kind;
        public FixedString64Bytes Id;
        public Entity Subject;
    }

    /// <summary>Process-closed facts the next pass will match. Not this one.</summary>
    public struct StoryPendingFact : IBufferElementData
    {
        public StoryFactKind Kind;
        public FixedString64Bytes Id;
        public Entity Subject;
    }

    public struct StoryDialogDef : IBufferElementData
    {
        public FixedString64Bytes Id;
        public byte Immediate;
        public byte IconQuestion;
        public byte Anchor;
        public FixedString64Bytes AnchorTypeId;
        public float OpenHours;
        public int ChoiceStart;
        public int ChoiceCount;
    }

    public struct StoryChoiceDef : IBufferElementData
    {
        public FixedString64Bytes Id;
        public byte Fallback;
        public int EffectStart;
        public int EffectCount;
    }

    public struct StoryQuestDef : IBufferElementData
    {
        public FixedString64Bytes Id;
        public byte CountBuildings;
        public FixedString64Bytes GoalId;
        public int Required;
        public float DeadlineHours;
        public int SuccessStart;
        public int SuccessCount;
        public int FailStart;
        public int FailCount;
    }

    public struct StoryTriggerDef : IBufferElementData
    {
        public FixedString64Bytes Id;
        public byte Repeat;
        public StoryFactKind Fact;
        public FixedString64Bytes FactId;
        public byte TargetIsQuest;
        public int TargetIndex;
    }

    public struct StoryTriggerState : IBufferElementData
    {
        public byte Armed;
        public byte Fired;
    }

    public struct StoryEffectDef : IBufferElementData
    {
        public StoryEffectKind Kind;
        public float Amount;
        public FixedString64Bytes ResourceId;
        public int TargetIndex;
    }

    public struct StoryBoard : IComponentData
    {
        public byte RunStarted;
        public byte SeenEraValid;
        public int SeenEra;
        public byte Booted;
        public int NextSerial;
    }

    public struct StoryLiveDialog : IComponentData
    {
        public int DefIndex;
        public int Serial;
        public Entity Anchor;
        public byte AnchorReady;
        public byte Opened;
        public byte HasDeadline;
        public float DeadlineHour;
    }

    public struct StoryLiveQuest : IComponentData
    {
        public int DefIndex;
        public byte HasDeadline;
        public float DeadlineHour;
    }

    public struct StoryUiCommand : IBufferElementData
    {
        public byte Open;
        public Entity Dialog;
        public int Choice;
    }

    public struct StoryRestoreDialog : IBufferElementData
    {
        public FixedString64Bytes DialogId;
        public int AnchorAgentId;
        public int AnchorBuildingId;
        public byte BindHeadquarters;
        public byte Opened;
        public byte AnchorReady;
        public byte HasDeadline;
        public float DeadlineHour;
        public int Serial;
    }

    public struct StoryRestoreQuest : IBufferElementData
    {
        public FixedString64Bytes QuestId;
        public byte HasDeadline;
        public float DeadlineHour;
    }

    public struct StoryRestoreTrigger : IBufferElementData
    {
        public FixedString64Bytes TriggerId;
        public byte Armed;
        public byte Fired;
    }

    public static class StoryAnchorKind
    {
        public const byte Headquarters = 0;
        public const byte BuildingType = 1;
        public const byte FactSubject = 2;
        public const byte Agent = 3;
    }
}
