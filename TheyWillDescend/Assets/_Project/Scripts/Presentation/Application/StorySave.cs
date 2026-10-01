using TheyWillDescend.Infrastructure.Save;
using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Stories;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Shell
{
    static class StorySave
    {
        public static void Capture(EntityManager em, Entity session, RunSnapshot snapshot)
        {
            if (!em.HasComponent<StoryBoard>(session))
                return;
            var board = em.GetComponentData<StoryBoard>(session);
            snapshot.storyRunStarted = board.RunStarted;
            snapshot.storySeenEraValid = board.SeenEraValid;
            snapshot.storySeenEra = board.SeenEra;

            using var dialogs = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveDialog>());
            using var dialogEntities = dialogs.ToEntityArray(Allocator.Temp);
            var defs = em.HasBuffer<StoryDialogDef>(session)
                ? em.GetBuffer<StoryDialogDef>(session)
                : default;
            snapshot.storyDialogs = new StoryDialogSnapshot[dialogEntities.Length];
            for (var i = 0; i < dialogEntities.Length; i++)
            {
                var live = em.GetComponentData<StoryLiveDialog>(dialogEntities[i]);
                var id = defs.IsCreated && (uint)live.DefIndex < (uint)defs.Length
                    ? defs[live.DefIndex].Id.ToString()
                    : string.Empty;
                var row = new StoryDialogSnapshot
                {
                    dialogId = id,
                    opened = live.Opened,
                    anchorReady = live.AnchorReady,
                    hasDeadline = live.HasDeadline,
                    deadlineHour = live.DeadlineHour,
                    serial = live.Serial
                };
                if (live.AnchorReady != 0 && em.Exists(live.Anchor))
                {
                    if (em.HasComponent<AgentId>(live.Anchor))
                        row.anchorAgentId = em.GetComponentData<AgentId>(live.Anchor).Value;
                    else if (em.HasComponent<Building>(live.Anchor))
                        row.anchorBuildingId = em.GetComponentData<Building>(live.Anchor).Id;
                    else if (em.HasComponent<Headquarters>(live.Anchor))
                        row.bindHeadquarters = 1;
                }

                snapshot.storyDialogs[i] = row;
            }

            using var quests = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveQuest>());
            using var questEntities = quests.ToEntityArray(Allocator.Temp);
            var questDefs = em.HasBuffer<StoryQuestDef>(session)
                ? em.GetBuffer<StoryQuestDef>(session)
                : default;
            snapshot.storyQuests = new StoryQuestSnapshot[questEntities.Length];
            for (var i = 0; i < questEntities.Length; i++)
            {
                var live = em.GetComponentData<StoryLiveQuest>(questEntities[i]);
                snapshot.storyQuests[i] = new StoryQuestSnapshot
                {
                    questId = questDefs.IsCreated && (uint)live.DefIndex < (uint)questDefs.Length
                        ? questDefs[live.DefIndex].Id.ToString()
                        : string.Empty,
                    hasDeadline = live.HasDeadline,
                    deadlineHour = live.DeadlineHour
                };
            }

            if (!em.HasBuffer<StoryTriggerDef>(session) || !em.HasBuffer<StoryTriggerState>(session))
                return;
            var triggerDefs = em.GetBuffer<StoryTriggerDef>(session);
            var states = em.GetBuffer<StoryTriggerState>(session);
            var count = triggerDefs.Length < states.Length ? triggerDefs.Length : states.Length;
            snapshot.storyTriggers = new StoryTriggerSnapshot[count];
            for (var i = 0; i < count; i++)
            {
                snapshot.storyTriggers[i] = new StoryTriggerSnapshot
                {
                    triggerId = triggerDefs[i].Id.ToString(),
                    armed = states[i].Armed,
                    fired = states[i].Fired
                };
            }
        }

        public static void QueueRestore(EntityManager em, Entity session, RunSnapshot snapshot)
        {
            if (!em.HasComponent<StoryBoard>(session))
                return;
            var board = em.GetComponentData<StoryBoard>(session);
            board.RunStarted = snapshot.storyRunStarted;
            board.SeenEraValid = snapshot.storySeenEraValid;
            board.SeenEra = snapshot.storySeenEra;
            board.Booted = 1;
            em.SetComponentData(session, board);

            if (!em.HasBuffer<StoryRestoreDialog>(session))
                return;
            var dialogs = em.GetBuffer<StoryRestoreDialog>(session);
            dialogs.Clear();
            var rows = snapshot.storyDialogs;
            if (rows != null)
            {
                for (var i = 0; i < rows.Length; i++)
                {
                    var row = rows[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.dialogId))
                        continue;
                    dialogs.Add(new StoryRestoreDialog
                    {
                        DialogId = new Unity.Collections.FixedString64Bytes(row.dialogId),
                        AnchorAgentId = row.anchorAgentId,
                        AnchorBuildingId = row.anchorBuildingId,
                        BindHeadquarters = row.bindHeadquarters,
                        Opened = row.opened,
                        AnchorReady = row.anchorReady,
                        HasDeadline = row.hasDeadline,
                        DeadlineHour = row.deadlineHour,
                        Serial = row.serial
                    });
                }
            }

            var quests = em.GetBuffer<StoryRestoreQuest>(session);
            quests.Clear();
            if (snapshot.storyQuests != null)
            {
                for (var i = 0; i < snapshot.storyQuests.Length; i++)
                {
                    var row = snapshot.storyQuests[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.questId))
                        continue;
                    quests.Add(new StoryRestoreQuest
                    {
                        QuestId = new Unity.Collections.FixedString64Bytes(row.questId),
                        HasDeadline = row.hasDeadline,
                        DeadlineHour = row.deadlineHour
                    });
                }
            }

            var triggers = em.GetBuffer<StoryRestoreTrigger>(session);
            triggers.Clear();
            if (snapshot.storyTriggers == null)
                return;
            for (var i = 0; i < snapshot.storyTriggers.Length; i++)
            {
                var row = snapshot.storyTriggers[i];
                if (row == null || string.IsNullOrWhiteSpace(row.triggerId))
                    continue;
                triggers.Add(new StoryRestoreTrigger
                {
                    TriggerId = new Unity.Collections.FixedString64Bytes(row.triggerId),
                    Armed = row.armed,
                    Fired = row.fired
                });
            }
        }
    }
}
