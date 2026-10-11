using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Gods;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Stories
{
    /// <summary>
    /// Facts in, triggers, live dialogs and quests. Consequences land here, not in the HUD.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AdvanceTimelineSystem))]
    [UpdateAfter(typeof(AdvanceConstructionSystem))]
    public partial struct AdvanceStorySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StoryBoard>();
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<GameTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var session = SystemAPI.GetSingletonEntity<StoryBoard>();
            ApplyRestore(em, session);
            PullPending(em, session);
            ConsumeUi(em, session);
            var control = SystemAPI.GetSingleton<SimControl>();
            if (control.IsRunning)
            {
                var time = SystemAPI.GetSingleton<GameTime>();
                var now = AbsoluteHours(time);
                EmitClockFacts(em, session);
                ResolveTimersAndGoals(em, session, now);
                MatchTriggers(em, session, now);
            }

            var nowHours = AbsoluteHours(SystemAPI.GetSingleton<GameTime>());
            PromoteImmediate(em, session, nowHours);
            ResolveAnchors(em, session, nowHours);
            if (control.IsRunning)
                em.GetBuffer<StoryFact>(session).Clear();
        }

        static void ApplyRestore(EntityManager em, Entity session)
        {
            var board = em.GetComponentData<StoryBoard>(session);
            if (board.Booted == 0)
                return;

            var now = AbsoluteHours(em, session);
            var dialogs = em.GetBuffer<StoryRestoreDialog>(session).ToNativeArray(Allocator.Temp);
            var quests = em.GetBuffer<StoryRestoreQuest>(session).ToNativeArray(Allocator.Temp);
            for (var i = 0; i < dialogs.Length; i++)
            {
                var row = dialogs[i];
                var index = StoryBake.DialogIndex(em.GetBuffer<StoryDialogDef>(session), row.DialogId);
                if (index < 0)
                    continue;
                SpawnDialog(em, session, index, Entity.Null, now);
                if (!TryLiveDialog(em, index, out var entity))
                    continue;
                var live = em.GetComponentData<StoryLiveDialog>(entity);
                live.Opened = row.Opened;
                live.Serial = row.Serial;
                live.HasDeadline = row.HasDeadline;
                live.DeadlineHour = row.DeadlineHour;
                live.Anchor = FindRestoredAnchor(em, row);
                live.AnchorReady = live.Anchor != Entity.Null ? (byte)1 : row.AnchorReady;
                if (live.Anchor == Entity.Null)
                    live.AnchorReady = 0;
                em.SetComponentData(entity, live);
            }

            for (var i = 0; i < quests.Length; i++)
            {
                var row = quests[i];
                var index = StoryBake.QuestIndex(em.GetBuffer<StoryQuestDef>(session), row.QuestId);
                if (index < 0)
                    continue;
                SpawnQuest(em, session, index, now);
                if (!TryLiveQuest(em, index, out var entity))
                    continue;
                var live = em.GetComponentData<StoryLiveQuest>(entity);
                live.HasDeadline = row.HasDeadline;
                live.DeadlineHour = row.DeadlineHour;
                em.SetComponentData(entity, live);
            }

            var triggers = em.GetBuffer<StoryRestoreTrigger>(session);
            var states = em.GetBuffer<StoryTriggerState>(session);
            var defs = em.GetBuffer<StoryTriggerDef>(session);
            for (var i = 0; i < triggers.Length; i++)
            {
                var row = triggers[i];
                for (var t = 0; t < defs.Length && t < states.Length; t++)
                {
                    if (defs[t].Id != row.TriggerId)
                        continue;
                    states[t] = new StoryTriggerState { Armed = row.Armed, Fired = row.Fired };
                    break;
                }
            }

            dialogs.Dispose();
            quests.Dispose();
            em.GetBuffer<StoryRestoreDialog>(session).Clear();
            em.GetBuffer<StoryRestoreQuest>(session).Clear();
            em.GetBuffer<StoryRestoreTrigger>(session).Clear();
            board = em.GetComponentData<StoryBoard>(session);
            board.Booted = 0;
            em.SetComponentData(session, board);
        }

        static bool TryLiveDialog(EntityManager em, int index, out Entity entity)
        {
            entity = Entity.Null;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveDialog>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<StoryLiveDialog>(entities[i]).DefIndex != index)
                    continue;
                entity = entities[i];
                return true;
            }

            return false;
        }

        static bool TryLiveQuest(EntityManager em, int index, out Entity entity)
        {
            entity = Entity.Null;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveQuest>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<StoryLiveQuest>(entities[i]).DefIndex != index)
                    continue;
                entity = entities[i];
                return true;
            }

            return false;
        }

        static Entity FindRestoredAnchor(EntityManager em, in StoryRestoreDialog row)
        {
            if (row.AnchorAgentId > 0)
            {
                using var agents = em.CreateEntityQuery(ComponentType.ReadOnly<AgentId>());
                using var entities = agents.ToEntityArray(Allocator.Temp);
                for (var i = 0; i < entities.Length; i++)
                {
                    if (em.GetComponentData<AgentId>(entities[i]).Value == row.AnchorAgentId)
                        return entities[i];
                }
            }

            if (row.AnchorBuildingId > 0)
            {
                using var buildings = em.CreateEntityQuery(ComponentType.ReadOnly<Building>());
                using var entities = buildings.ToEntityArray(Allocator.Temp);
                for (var i = 0; i < entities.Length; i++)
                {
                    if (em.GetComponentData<Building>(entities[i]).Id == row.AnchorBuildingId)
                        return entities[i];
                }
            }

            if (row.BindHeadquarters == 0)
                return Entity.Null;
            using var hq = em.CreateEntityQuery(ComponentType.ReadOnly<Headquarters>());
            return hq.IsEmptyIgnoreFilter ? Entity.Null : hq.GetSingletonEntity();
        }

        static void PullPending(EntityManager em, Entity session)
        {
            var pending = em.GetBuffer<StoryPendingFact>(session);
            if (pending.Length == 0)
                return;
            var facts = em.GetBuffer<StoryFact>(session);
            for (var i = 0; i < pending.Length; i++)
            {
                var row = pending[i];
                facts.Add(new StoryFact { Kind = row.Kind, Id = row.Id, Subject = row.Subject });
            }

            em.GetBuffer<StoryPendingFact>(session).Clear();
        }

        static void ConsumeUi(EntityManager em, Entity session)
        {
            var commands = em.GetBuffer<StoryUiCommand>(session);
            if (commands.Length == 0)
                return;
            var copy = new NativeArray<StoryUiCommand>(commands.Length, Allocator.Temp);
            for (var i = 0; i < commands.Length; i++)
                copy[i] = commands[i];
            commands.Clear();

            for (var i = 0; i < copy.Length; i++)
            {
                var command = copy[i];
                if (!em.Exists(command.Dialog) || !em.HasComponent<StoryLiveDialog>(command.Dialog))
                    continue;
                if (command.Open != 0)
                    TryOpen(em, command.Dialog);
                else
                    ResolveChoice(em, session, command.Dialog, command.Choice);
            }

            copy.Dispose();
        }

        static void TryOpen(EntityManager em, Entity dialog)
        {
            if (AnyOpen(em))
                return;
            var live = em.GetComponentData<StoryLiveDialog>(dialog);
            if (live.Opened != 0 || live.AnchorReady == 0)
                return;
            live.Opened = 1;
            live.HasDeadline = 0;
            em.SetComponentData(dialog, live);
        }

        static void ResolveChoice(EntityManager em, Entity session, Entity dialog, int choice)
        {
            var live = em.GetComponentData<StoryLiveDialog>(dialog);
            if (live.Opened == 0)
                return;
            var defs = em.GetBuffer<StoryDialogDef>(session);
            if (live.DefIndex < 0 || live.DefIndex >= defs.Length)
            {
                em.DestroyEntity(dialog);
                return;
            }

            var def = defs[live.DefIndex];
            var choices = em.GetBuffer<StoryChoiceDef>(session);
            if (def.ChoiceCount > 0)
            {
                if (choice < 0 || choice >= def.ChoiceCount)
                    choice = Fallback(choices, def);
                var row = choices[def.ChoiceStart + choice];
                ApplyEffects(em, session, row.EffectStart, row.EffectCount, live.Anchor);
            }

            NoteClosed(em, session, def.Id);
            em.DestroyEntity(dialog);
        }

        static void EmitClockFacts(EntityManager em, Entity session)
        {
            var board = em.GetComponentData<StoryBoard>(session);
            if (board.RunStarted == 0)
            {
                board.RunStarted = 1;
                em.GetBuffer<StoryFact>(session).Add(new StoryFact { Kind = StoryFactKind.RunStarted });
            }

            if (!em.HasComponent<Timeline>(session) || !em.HasBuffer<EraLine>(session))
            {
                em.SetComponentData(session, board);
                return;
            }

            var timeline = em.GetComponentData<Timeline>(session);
            var eras = em.GetBuffer<EraLine>(session);
            if (board.SeenEraValid == 0)
            {
                board.SeenEra = timeline.EraIndex;
                board.SeenEraValid = 1;
            }
            else if (timeline.EraIndex > board.SeenEra)
            {
                var facts = em.GetBuffer<StoryFact>(session);
                var from = board.SeenEra + 1;
                var to = math.min(timeline.EraIndex, eras.Length - 1);
                for (var i = from; i <= to; i++)
                    facts.Add(new StoryFact { Kind = StoryFactKind.EraReached, Id = eras[i].EraId });
                board.SeenEra = timeline.EraIndex;
            }

            em.SetComponentData(session, board);
        }

        static void ResolveTimersAndGoals(EntityManager em, Entity session, float now)
        {
            var closing = new NativeList<Entity>(8, Allocator.Temp);
            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveDialog>()))
            using (var entities = query.ToEntityArray(Allocator.Temp))
            {
                var defs = em.GetBuffer<StoryDialogDef>(session);
                for (var i = 0; i < entities.Length; i++)
                {
                    var row = em.GetComponentData<StoryLiveDialog>(entities[i]);
                    if (row.HasDeadline == 0 || now < row.DeadlineHour)
                        continue;
                    if ((uint)row.DefIndex >= (uint)defs.Length)
                        continue;
                    var immediate = defs[row.DefIndex].Immediate != 0;
                    if (!immediate && row.Opened != 0)
                        continue;
                    if (immediate && row.Opened == 0)
                        continue;
                    closing.Add(entities[i]);
                }
            }

            for (var i = 0; i < closing.Length; i++)
            {
                if (!em.Exists(closing[i]))
                    continue;
                var live = em.GetComponentData<StoryLiveDialog>(closing[i]);
                var defs = em.GetBuffer<StoryDialogDef>(session);
                if ((uint)live.DefIndex < (uint)defs.Length)
                {
                    var def = defs[live.DefIndex];
                    var choices = em.GetBuffer<StoryChoiceDef>(session);
                    if (def.ChoiceCount > 0)
                    {
                        var index = Fallback(choices, def);
                        var choice = choices[def.ChoiceStart + index];
                        ApplyEffects(em, session, choice.EffectStart, choice.EffectCount, live.Anchor);
                    }

                    NoteClosed(em, session, def.Id);
                }

                em.DestroyEntity(closing[i]);
            }

            var quests = new NativeList<Entity>(8, Allocator.Temp);
            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveQuest>()))
            using (var entities = query.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var row = em.GetComponentData<StoryLiveQuest>(entities[i]);
                    var defBuffer = em.GetBuffer<StoryQuestDef>(session);
                    if ((uint)row.DefIndex >= (uint)defBuffer.Length)
                    {
                        quests.Add(entities[i]);
                        continue;
                    }

                    var def = defBuffer[row.DefIndex];
                    var met = GoalMet(em, def);
                    var failed = row.HasDeadline != 0 && now >= row.DeadlineHour && !met;
                    if (!met && !failed)
                        continue;
                    ApplyEffects(
                        em,
                        session,
                        met ? def.SuccessStart : def.FailStart,
                        met ? def.SuccessCount : def.FailCount,
                        Entity.Null);
                    NoteClosed(em, session, def.Id);
                    quests.Add(entities[i]);
                }
            }

            for (var i = 0; i < quests.Length; i++)
            {
                if (em.Exists(quests[i]))
                    em.DestroyEntity(quests[i]);
            }

            closing.Dispose();
            quests.Dispose();
        }

        static void MatchTriggers(EntityManager em, Entity session, float now)
        {
            var fired = new NativeList<int>(4, Allocator.Temp);
            var defs = em.GetBuffer<StoryTriggerDef>(session);
            var states = em.GetBuffer<StoryTriggerState>(session);
            var facts = em.GetBuffer<StoryFact>(session);
            var count = math.min(defs.Length, states.Length);
            for (var i = 0; i < count; i++)
            {
                if (states[i].Armed == 0 || !Matches(facts, defs[i]))
                    continue;
                fired.Add(i);
            }

            for (var n = 0; n < fired.Length; n++)
            {
                var i = fired[n];
                var def = em.GetBuffer<StoryTriggerDef>(session)[i];
                if (def.TargetIsQuest != 0)
                    SpawnQuest(em, session, def.TargetIndex, now);
                else
                    SpawnDialog(em, session, def.TargetIndex, Entity.Null, now);
                if (def.Repeat != 0)
                    continue;
                var statesNow = em.GetBuffer<StoryTriggerState>(session);
                var state = statesNow[i];
                state.Armed = 0;
                state.Fired = 1;
                statesNow[i] = state;
            }

            fired.Dispose();
        }

        static bool Matches(DynamicBuffer<StoryFact> facts, in StoryTriggerDef def)
        {
            for (var i = 0; i < facts.Length; i++)
            {
                var fact = facts[i];
                if (fact.Kind != def.Fact)
                    continue;
                if (def.FactId.Length > 0 && fact.Id != def.FactId)
                    continue;
                return true;
            }

            return false;
        }

        static void PromoteImmediate(EntityManager em, Entity session, float now)
        {
            if (AnyOpen(em))
                return;
            Entity best = Entity.Null;
            var bestSerial = int.MaxValue;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveDialog>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            var defs = em.GetBuffer<StoryDialogDef>(session);
            for (var i = 0; i < entities.Length; i++)
            {
                var live = em.GetComponentData<StoryLiveDialog>(entities[i]);
                if (live.Opened != 0 || (uint)live.DefIndex >= (uint)defs.Length)
                    continue;
                if (defs[live.DefIndex].Immediate == 0)
                    continue;
                if (live.Serial >= bestSerial)
                    continue;
                bestSerial = live.Serial;
                best = entities[i];
            }

            if (best == Entity.Null)
                return;
            var chosen = em.GetComponentData<StoryLiveDialog>(best);
            var def = defs[chosen.DefIndex];
            chosen.Opened = 1;
            if (def.OpenHours > 0.0001f)
            {
                chosen.HasDeadline = 1;
                chosen.DeadlineHour = now + def.OpenHours;
            }

            em.SetComponentData(best, chosen);
        }

        static void ResolveAnchors(EntityManager em, Entity session, float now)
        {
            var prototype = em.HasComponent<SimPrototypes>(session)
                ? em.GetComponentData<SimPrototypes>(session).Agent
                : Entity.Null;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveDialog>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var live = em.GetComponentData<StoryLiveDialog>(entities[i]);
                if (live.AnchorReady != 0)
                    continue;
                var defs = em.GetBuffer<StoryDialogDef>(session);
                if ((uint)live.DefIndex >= (uint)defs.Length)
                    continue;
                var def = defs[live.DefIndex];
                if (!TryFindAnchor(em, def, prototype, out var anchor))
                    continue;
                live.Anchor = anchor;
                live.AnchorReady = 1;
                if (def.Immediate == 0 && def.OpenHours > 0.0001f && live.Opened == 0)
                {
                    live.HasDeadline = 1;
                    live.DeadlineHour = now + def.OpenHours;
                }

                em.SetComponentData(entities[i], live);
            }
        }

        static bool TryFindAnchor(
            EntityManager em,
            in StoryDialogDef def,
            Entity prototype,
            out Entity anchor)
        {
            anchor = Entity.Null;
            if (def.Anchor == StoryAnchorKind.Headquarters)
            {
                using var hq = em.CreateEntityQuery(ComponentType.ReadOnly<Headquarters>());
                if (hq.IsEmptyIgnoreFilter)
                    return false;
                anchor = hq.GetSingletonEntity();
                return true;
            }

            if (def.Anchor == StoryAnchorKind.BuildingType)
            {
                using var buildings = em.CreateEntityQuery(
                    ComponentType.ReadOnly<Building>(),
                    ComponentType.Exclude<Construction>());
                using var entities = buildings.ToEntityArray(Allocator.Temp);
                for (var i = 0; i < entities.Length; i++)
                {
                    if (em.GetComponentData<Building>(entities[i]).TypeId != def.AnchorTypeId)
                        continue;
                    anchor = entities[i];
                    return true;
                }

                return false;
            }

            if (def.Anchor != StoryAnchorKind.Agent)
                return false;

            using var agents = em.CreateEntityQuery(
                ComponentType.ReadOnly<AgentAssignment>(),
                ComponentType.ReadOnly<AgentId>(),
                ComponentType.ReadOnly<AgentPlazaIdle>());
            using var list = agents.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < list.Length; i++)
            {
                if (list[i] == prototype)
                    continue;
                if (em.GetComponentData<AgentAssignment>(list[i]).Arrived != 0)
                    continue;
                if (AnchorTaken(em, list[i]))
                    continue;
                anchor = list[i];
                return true;
            }

            return false;
        }

        static bool AnchorTaken(EntityManager em, Entity candidate)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveDialog>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var live = em.GetComponentData<StoryLiveDialog>(entities[i]);
                if (live.AnchorReady != 0 && live.Anchor == candidate)
                    return true;
            }

            return false;
        }

        static void ApplyEffects(EntityManager em, Entity session, int start, int count, Entity anchor)
        {
            var effects = em.GetBuffer<StoryEffectDef>(session);
            var end = math.min(start + count, effects.Length);
            if (end <= start)
                return;
            var copy = new NativeArray<StoryEffectDef>(end - start, Allocator.Temp);
            for (var i = start; i < end; i++)
                copy[i - start] = effects[i];
            for (var i = 0; i < copy.Length; i++)
            {
                var effect = copy[i];
                switch (effect.Kind)
                {
                    case StoryEffectKind.Loyalty:
                        if (em.HasComponent<GodLoyalty>(session))
                        {
                            var loyalty = em.GetComponentData<GodLoyalty>(session);
                            loyalty.Value += effect.Amount;
                            loyalty.ClampToEffectiveMax();
                            em.SetComponentData(session, loyalty);
                        }

                        break;
                    case StoryEffectKind.Resource:
                        if (em.HasBuffer<ResourceAmount>(session))
                        {
                            var stock = em.GetBuffer<ResourceAmount>(session);
                            var info = em.HasBuffer<ResourceInfo>(session)
                                ? em.GetBuffer<ResourceInfo>(session)
                                : default;
                            if (info.IsCreated)
                                ResourceLedger.AddClamped(stock, info, effect.ResourceId, effect.Amount);
                            else
                                ResourceLedger.Add(stock, effect.ResourceId, effect.Amount);
                        }

                        break;
                    case StoryEffectKind.StartDialog:
                        SpawnDialog(em, session, effect.TargetIndex, anchor, AbsoluteHours(em, session));
                        break;
                    case StoryEffectKind.StartQuest:
                        SpawnQuest(em, session, effect.TargetIndex, AbsoluteHours(em, session));
                        break;
                    case StoryEffectKind.ArmTrigger:
                        Arm(em, session, effect.TargetIndex);
                        break;
                    case StoryEffectKind.KillAnchor:
                        if (anchor != Entity.Null && em.Exists(anchor) && em.HasComponent<AgentId>(anchor))
                            em.DestroyEntity(anchor);
                        break;
                }
            }

            copy.Dispose();
        }

        static void SpawnDialog(EntityManager em, Entity session, int index, Entity subject, float now)
        {
            var defs = em.GetBuffer<StoryDialogDef>(session);
            if ((uint)index >= (uint)defs.Length || DialogLive(em, index))
                return;
            var def = defs[index];
            var board = em.GetComponentData<StoryBoard>(session);
            var serial = board.NextSerial++;
            em.SetComponentData(session, board);
            var entity = em.CreateEntity();
            var ready = def.Anchor == StoryAnchorKind.FactSubject && subject != Entity.Null;
            em.AddComponentData(entity, new StoryLiveDialog
            {
                DefIndex = index,
                Serial = serial,
                Anchor = ready ? subject : Entity.Null,
                AnchorReady = (byte)(ready ? 1 : 0),
                HasDeadline = 0,
                DeadlineHour = now
            });
        }

        static void SpawnQuest(EntityManager em, Entity session, int index, float now)
        {
            var defs = em.GetBuffer<StoryQuestDef>(session);
            if ((uint)index >= (uint)defs.Length || QuestLive(em, index))
                return;
            var deadlineHours = defs[index].DeadlineHours;
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new StoryLiveQuest
            {
                DefIndex = index,
                HasDeadline = (byte)(deadlineHours > 0.0001f ? 1 : 0),
                DeadlineHour = now + deadlineHours
            });
        }

        static void Arm(EntityManager em, Entity session, int index)
        {
            var states = em.GetBuffer<StoryTriggerState>(session);
            if ((uint)index >= (uint)states.Length)
                return;
            var state = states[index];
            state.Armed = 1;
            state.Fired = 0;
            states[index] = state;
        }

        static void NoteClosed(EntityManager em, Entity session, in FixedString64Bytes id)
        {
            em.GetBuffer<StoryPendingFact>(session).Add(new StoryPendingFact
            {
                Kind = StoryFactKind.ProcessClosed,
                Id = id
            });
        }

        static bool GoalMet(EntityManager em, in StoryQuestDef def)
        {
            if (def.CountBuildings != 0)
            {
                using var query = em.CreateEntityQuery(
                    ComponentType.ReadOnly<Building>(),
                    ComponentType.Exclude<Construction>());
                using var buildings = query.ToComponentDataArray<Building>(Allocator.Temp);
                var count = 0;
                for (var i = 0; i < buildings.Length; i++)
                {
                    if (buildings[i].TypeId == def.GoalId)
                        count++;
                }

                return count >= def.Required;
            }

            using var sessionQuery = em.CreateEntityQuery(ComponentType.ReadOnly<StoryBoard>());
            if (sessionQuery.IsEmptyIgnoreFilter)
                return false;
            var session = sessionQuery.GetSingletonEntity();
            if (!em.HasBuffer<ResourceAmount>(session))
                return false;
            return ResourceLedger.Get(em.GetBuffer<ResourceAmount>(session), def.GoalId) + 0.0001f >= def.Required;
        }

        static bool DialogLive(EntityManager em, int index)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveDialog>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<StoryLiveDialog>(entities[i]).DefIndex == index)
                    return true;
            }

            return false;
        }

        static bool QuestLive(EntityManager em, int index)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveQuest>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<StoryLiveQuest>(entities[i]).DefIndex == index)
                    return true;
            }

            return false;
        }

        static bool AnyOpen(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveDialog>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<StoryLiveDialog>(entities[i]).Opened != 0)
                    return true;
            }

            return false;
        }

        static int Fallback(DynamicBuffer<StoryChoiceDef> choices, in StoryDialogDef def)
        {
            for (var i = 0; i < def.ChoiceCount; i++)
            {
                if (choices[def.ChoiceStart + i].Fallback != 0)
                    return i;
            }

            return 0;
        }

        static float AbsoluteHours(in GameTime time)
        {
            return time.Day * 24f + time.HourOfDay;
        }

        static float AbsoluteHours(EntityManager em, Entity session)
        {
            return em.HasComponent<GameTime>(session) ? AbsoluteHours(em.GetComponentData<GameTime>(session)) : 0f;
        }
    }
}
