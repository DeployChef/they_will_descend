using TheyWillDescend.Simulation.Content;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Simulation.Stories
{
    public static class StoryBake
    {
        public static void Apply(EntityManager em, Entity session, StoryPackAsset pack)
        {
            DestroyLives(em);
            var board = new StoryBoard();
            if (!em.HasComponent<StoryBoard>(session))
                em.AddComponentData(session, board);
            else
                em.SetComponentData(session, board);

            Reset<StoryDialogDef>(em, session);
            Reset<StoryChoiceDef>(em, session);
            Reset<StoryQuestDef>(em, session);
            Reset<StoryTriggerDef>(em, session);
            Reset<StoryTriggerState>(em, session);
            Reset<StoryEffectDef>(em, session);
            Reset<StoryFact>(em, session);
            Reset<StoryPendingFact>(em, session);
            Reset<StoryUiCommand>(em, session);
            Reset<StoryRestoreDialog>(em, session);
            Reset<StoryRestoreQuest>(em, session);
            Reset<StoryRestoreTrigger>(em, session);

            if (pack == null)
                return;

            var dialogs = em.GetBuffer<StoryDialogDef>(session);
            var choices = em.GetBuffer<StoryChoiceDef>(session);
            var quests = em.GetBuffer<StoryQuestDef>(session);
            var triggers = em.GetBuffer<StoryTriggerDef>(session);
            var states = em.GetBuffer<StoryTriggerState>(session);
            var effects = em.GetBuffer<StoryEffectDef>(session);

            var dialogList = pack.dialogs ?? System.Array.Empty<StoryDialogAsset>();
            var questList = pack.quests ?? System.Array.Empty<StoryQuestAsset>();
            var triggerList = pack.triggers ?? System.Array.Empty<StoryTriggerAsset>();

            for (var i = 0; i < dialogList.Length; i++)
            {
                var asset = dialogList[i];
                if (asset == null || !ContentId.TryEncode(ContentId.Normalize(asset.dialogId, asset.name), out var id))
                {
                    Debug.LogError("Story pack skipped a dialog with an empty id.");
                    continue;
                }

                var choiceStart = choices.Length;
                var authored = asset.choices ?? System.Array.Empty<StoryChoiceAuthoring>();
                for (var c = 0; c < authored.Length; c++)
                {
                    var choice = authored[c];
                    var effectStart = effects.Length;
                    AppendEffects(effects, choice.effects, dialogList, questList, triggerList);
                    choices.Add(new StoryChoiceDef
                    {
                        Id = ContentId.EncodeOrEmpty(choice.choiceId),
                        Fallback = (byte)(choice.fallback ? 1 : 0),
                        EffectStart = effectStart,
                        EffectCount = effects.Length - effectStart
                    });
                }

                dialogs.Add(new StoryDialogDef
                {
                    Id = id,
                    Immediate = (byte)(asset.delivery == StoryDelivery.Immediate ? 1 : 0),
                    IconQuestion = (byte)(asset.icon == StoryIconKind.Question ? 1 : 0),
                    Anchor = (byte)asset.anchor,
                    AnchorTypeId = ContentId.EncodeOrEmpty(asset.anchorTypeId),
                    OpenHours = asset.openHours < 0f ? 0f : asset.openHours,
                    ChoiceStart = choiceStart,
                    ChoiceCount = choices.Length - choiceStart
                });
            }

            for (var i = 0; i < questList.Length; i++)
            {
                var asset = questList[i];
                if (asset == null || !ContentId.TryEncode(ContentId.Normalize(asset.questId, asset.name), out var id))
                {
                    Debug.LogError("Story pack skipped a quest with an empty id.");
                    continue;
                }

                var successStart = effects.Length;
                AppendEffects(effects, asset.onSuccess, dialogList, questList, triggerList);
                var successCount = effects.Length - successStart;
                var failStart = effects.Length;
                AppendEffects(effects, asset.onFail, dialogList, questList, triggerList);
                quests.Add(new StoryQuestDef
                {
                    Id = id,
                    CountBuildings = (byte)(asset.goal == StoryGoalKind.BuildingsAtLeast ? 1 : 0),
                    GoalId = ContentId.EncodeOrEmpty(asset.goalId),
                    Required = asset.required < 1 ? 1 : asset.required,
                    DeadlineHours = asset.deadlineHours < 0f ? 0f : asset.deadlineHours,
                    SuccessStart = successStart,
                    SuccessCount = successCount,
                    FailStart = failStart,
                    FailCount = effects.Length - failStart
                });
            }

            for (var i = 0; i < triggerList.Length; i++)
            {
                var asset = triggerList[i];
                if (asset == null || !ContentId.TryEncode(ContentId.Normalize(asset.triggerId, asset.name), out var id))
                {
                    Debug.LogError("Story pack skipped a trigger with an empty id.");
                    continue;
                }

                var targetIsQuest = asset.quest != null && asset.dialog == null;
                var target = targetIsQuest
                    ? IndexOf(questList, asset.quest)
                    : IndexOf(dialogList, asset.dialog);
                if (target < 0)
                {
                    Debug.LogError($"Story trigger {id} has no dialog or quest in the pack.");
                    continue;
                }

                triggers.Add(new StoryTriggerDef
                {
                    Id = id,
                    Repeat = (byte)(asset.repeat ? 1 : 0),
                    Fact = asset.fact,
                    FactId = ContentId.EncodeOrEmpty(asset.factId),
                    TargetIsQuest = (byte)(targetIsQuest ? 1 : 0),
                    TargetIndex = target
                });
                states.Add(new StoryTriggerState
                {
                    Armed = (byte)(asset.armedAtStart ? 1 : 0),
                    Fired = 0
                });
            }
        }

        public static int DialogIndex(DynamicBuffer<StoryDialogDef> dialogs, in FixedString64Bytes id)
        {
            for (var i = 0; i < dialogs.Length; i++)
            {
                if (dialogs[i].Id == id)
                    return i;
            }

            return -1;
        }

        public static int QuestIndex(DynamicBuffer<StoryQuestDef> quests, in FixedString64Bytes id)
        {
            for (var i = 0; i < quests.Length; i++)
            {
                if (quests[i].Id == id)
                    return i;
            }

            return -1;
        }

        static void AppendEffects(
            DynamicBuffer<StoryEffectDef> effects,
            StoryEffectAuthoring[] authored,
            StoryDialogAsset[] dialogs,
            StoryQuestAsset[] quests,
            StoryTriggerAsset[] triggers)
        {
            if (authored == null)
                return;
            for (var i = 0; i < authored.Length; i++)
            {
                var row = authored[i];
                var target = -1;
                if (row.kind == StoryEffectKind.StartDialog)
                    target = IndexOf(dialogs, row.dialog);
                else if (row.kind == StoryEffectKind.StartQuest)
                    target = IndexOf(quests, row.quest);
                else if (row.kind == StoryEffectKind.ArmTrigger)
                    target = IndexOf(triggers, row.trigger);
                if ((row.kind == StoryEffectKind.StartDialog
                     || row.kind == StoryEffectKind.StartQuest
                     || row.kind == StoryEffectKind.ArmTrigger)
                    && target < 0)
                {
                    Debug.LogError($"Story effect {row.kind} points outside the pack.");
                    continue;
                }

                effects.Add(new StoryEffectDef
                {
                    Kind = row.kind,
                    Amount = row.amount,
                    ResourceId = ContentId.EncodeOrEmpty(row.resourceId),
                    TargetIndex = target
                });
            }
        }

        static int IndexOf<T>(T[] items, T target) where T : class
        {
            if (items == null || target == null)
                return -1;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == target)
                    return i;
            }

            return -1;
        }

        static void Reset<T>(EntityManager em, Entity session) where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(session))
                em.AddBuffer<T>(session);
            em.GetBuffer<T>(session).Clear();
        }

        static void DestroyLives(EntityManager em)
        {
            using var dialogs = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveDialog>());
            if (!dialogs.IsEmptyIgnoreFilter)
                em.DestroyEntity(dialogs);
            using var quests = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveQuest>());
            if (!quests.IsEmptyIgnoreFilter)
                em.DestroyEntity(quests);
        }
    }
}
