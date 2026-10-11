using System.Collections.Generic;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Stories;
using TheyWillDescend.Simulation.Time;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Quest list on the Game HUD, bottom-left above the faith bar.
    /// Rows are instances of <see cref="rowPrefab"/>.
    /// </summary>
    public sealed class StoryQuestWidget : MonoBehaviour
    {
        [SerializeField] StoryPackAsset pack;
        [SerializeField] StoryQuestRowView rowPrefab;
        [SerializeField] RectTransform rowRoot;

        readonly Dictionary<Entity, StoryQuestRowView> _rows = new();
        readonly List<Entity> _drop = new();

        void Update()
        {
            if (rowPrefab == null || rowRoot == null || !SimWorld.TryGet(out var em, out var session))
                return;
            if (!em.HasBuffer<StoryQuestDef>(session))
                return;

            var defs = em.GetBuffer<StoryQuestDef>(session);
            var now = Now(em);
            var seen = new HashSet<Entity>();
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveQuest>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var live = em.GetComponentData<StoryLiveQuest>(entities[i]);
                if ((uint)live.DefIndex >= (uint)defs.Length)
                    continue;
                seen.Add(entities[i]);
                if (!_rows.TryGetValue(entities[i], out var view))
                {
                    view = Instantiate(rowPrefab, rowRoot);
                    _rows.Add(entities[i], view);
                }

                var def = defs[live.DefIndex];
                var asset = pack != null ? pack.FindQuest(def.Id.ToString()) : null;
                var current = Count(em, session, def);
                var timed = live.HasDeadline != 0 && def.DeadlineHours > 0.0001f;
                var fill = 0f;
                if (timed)
                {
                    var remain = live.DeadlineHour - now;
                    if (remain < 0f)
                        remain = 0f;
                    fill = remain / def.DeadlineHours;
                }

                view.Show(
                    asset != null ? asset.title : def.Id.ToString(),
                    asset != null ? asset.description : string.Empty,
                    $"{current} / {def.Required}",
                    timed,
                    fill);
            }

            _drop.Clear();
            foreach (var pair in _rows)
            {
                if (!seen.Contains(pair.Key))
                    _drop.Add(pair.Key);
            }

            for (var i = 0; i < _drop.Count; i++)
            {
                if (_rows.TryGetValue(_drop[i], out var view) && view != null)
                    Destroy(view.gameObject);
                _rows.Remove(_drop[i]);
            }
        }

        static int Count(EntityManager em, Entity session, in StoryQuestDef def)
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

                return count;
            }

            if (!em.HasBuffer<ResourceAmount>(session))
                return 0;
            return (int)ResourceLedger.Get(em.GetBuffer<ResourceAmount>(session), def.GoalId);
        }

        static float Now(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameTime>());
            if (query.IsEmptyIgnoreFilter)
                return 0f;
            var time = query.GetSingleton<GameTime>();
            return time.Day * 24f + time.HourOfDay;
        }
    }
}
