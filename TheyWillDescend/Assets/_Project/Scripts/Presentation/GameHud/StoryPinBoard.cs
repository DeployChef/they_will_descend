using System.Collections.Generic;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Stories;
using TheyWillDescend.Simulation.Time;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Screen-space pins. Each icon is an instance of <see cref="pinPrefab"/>.
    /// </summary>
    public sealed class StoryPinBoard : MonoBehaviour
    {
        [SerializeField] StoryPinView pinPrefab;
        [SerializeField] RectTransform pinRoot;
        [SerializeField] float height = 3f;

        readonly Dictionary<Entity, StoryPinView> _pins = new();
        readonly List<Entity> _drop = new();

        void Update()
        {
            if (pinPrefab == null || pinRoot == null || !SimWorld.TryGet(out var em, out var session))
                return;
            if (!em.HasBuffer<StoryDialogDef>(session))
                return;

            var canvas = pinRoot.GetComponentInParent<Canvas>();
            var cam = Camera.main;
            var eventCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var defs = em.GetBuffer<StoryDialogDef>(session);
            var now = Now(em);
            var seen = new HashSet<Entity>();

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveDialog>());
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var live = em.GetComponentData<StoryLiveDialog>(entities[i]);
                if (live.Opened != 0 || live.AnchorReady == 0)
                    continue;
                if ((uint)live.DefIndex >= (uint)defs.Length || defs[live.DefIndex].Immediate != 0)
                    continue;
                if (!em.Exists(live.Anchor) || !em.HasComponent<LocalTransform>(live.Anchor))
                    continue;

                seen.Add(entities[i]);
                if (!_pins.TryGetValue(entities[i], out var view))
                {
                    view = Instantiate(pinPrefab, pinRoot);
                    var dialog = entities[i];
                    if (view.Button != null)
                        view.Button.onClick.AddListener(() => Open(dialog));
                    _pins.Add(dialog, view);
                }

                var world = (Vector3)em.GetComponentData<LocalTransform>(live.Anchor).Position + Vector3.up * height;
                var visible = cam != null && cam.WorldToScreenPoint(world).z > 0f;
                view.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var screen = cam.WorldToScreenPoint(world);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(pinRoot, screen, eventCam, out var local))
                    view.transform.localPosition = local;

                var question = defs[live.DefIndex].IconQuestion != 0;
                var showTimer = live.HasDeadline != 0;
                view.Show(question, showTimer, showTimer ? FormatRemain(live.DeadlineHour - now) : string.Empty);
            }

            _drop.Clear();
            foreach (var pair in _pins)
            {
                if (!seen.Contains(pair.Key))
                    _drop.Add(pair.Key);
            }

            for (var i = 0; i < _drop.Count; i++)
            {
                if (_pins.TryGetValue(_drop[i], out var view) && view != null)
                    Destroy(view.gameObject);
                _pins.Remove(_drop[i]);
            }
        }

        static void Open(Entity dialog)
        {
            SimCommands.TryPost(new StoryUiCommand { Open = 1, Dialog = dialog });
        }

        static float Now(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameTime>());
            if (query.IsEmptyIgnoreFilter)
                return 0f;
            var time = query.GetSingleton<GameTime>();
            return time.Day * 24f + time.HourOfDay;
        }

        static string FormatRemain(float hours)
        {
            if (hours < 0f)
                hours = 0f;
            var whole = (int)hours;
            var minutes = (int)((hours - whole) * 60f);
            return $"{whole:00}:{minutes:00}";
        }
    }
}
