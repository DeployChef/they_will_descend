using System.Collections.Generic;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Stories;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// One dialog panel on the HUD. This behaviour stays active; <see cref="panel"/> is the visual.
    /// </summary>
    public sealed class StoryDialogPanel : MonoBehaviour
    {
        [SerializeField] StoryPackAsset pack;
        [SerializeField] GameObject panel;
        [SerializeField] TMPro.TMP_Text title;
        [SerializeField] TMPro.TMP_Text body;
        [SerializeField] RectTransform choiceRoot;
        [SerializeField] StoryChoiceView choicePrefab;

        Entity _shown = Entity.Null;
        readonly List<StoryChoiceView> _choices = new();

        void Update()
        {
            if (panel == null)
                return;
            if (!SimWorld.TryGet(out var em, out var session) || !em.HasBuffer<StoryDialogDef>(session))
            {
                Hide();
                return;
            }

            if (!TryOpen(em, out var entity, out var live))
            {
                Hide();
                return;
            }

            var defs = em.GetBuffer<StoryDialogDef>(session);
            if ((uint)live.DefIndex >= (uint)defs.Length)
            {
                Hide();
                return;
            }

            panel.SetActive(true);
            var def = defs[live.DefIndex];
            var asset = pack != null ? pack.FindDialog(def.Id.ToString()) : null;
            if (title != null)
                title.text = asset != null ? asset.title : def.Id.ToString();
            if (body != null)
                body.text = asset != null ? asset.body : string.Empty;

            if (_shown != entity)
            {
                _shown = entity;
                RebuildChoices(asset, def, entity);
            }
        }

        void Hide()
        {
            _shown = Entity.Null;
            ClearChoices();
            if (panel != null)
                panel.SetActive(false);
        }

        void RebuildChoices(StoryDialogAsset asset, in StoryDialogDef def, Entity dialog)
        {
            ClearChoices();
            if (choicePrefab == null || choiceRoot == null)
                return;
            var labels = asset != null ? asset.choices : null;
            for (var i = 0; i < def.ChoiceCount; i++)
            {
                var view = Instantiate(choicePrefab, choiceRoot);
                var label = labels != null && i < labels.Length ? labels[i].label : "OK";
                view.SetLabel(label);
                var index = i;
                var target = dialog;
                if (view.Button != null)
                    view.Button.onClick.AddListener(() => Choose(target, index));
                _choices.Add(view);
            }
        }

        void ClearChoices()
        {
            for (var i = 0; i < _choices.Count; i++)
            {
                if (_choices[i] != null)
                    Destroy(_choices[i].gameObject);
            }

            _choices.Clear();
        }

        static bool TryOpen(EntityManager em, out Entity entity, out StoryLiveDialog live)
        {
            entity = Entity.Null;
            live = default;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StoryLiveDialog>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var row = em.GetComponentData<StoryLiveDialog>(entities[i]);
                if (row.Opened == 0)
                    continue;
                entity = entities[i];
                live = row;
                return true;
            }

            return false;
        }

        static void Choose(Entity dialog, int index)
        {
            SimCommands.TryPost(new StoryUiCommand { Dialog = dialog, Choice = index });
        }
    }
}
