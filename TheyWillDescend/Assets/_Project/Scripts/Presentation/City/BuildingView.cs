using System.Collections.Generic;
using TheyWillDescend.Simulation.City;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Live house view. Catalog card on the stamp prefab; Play instance pulls
    /// packs from its entity. Does not write simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingView : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int WorkingHash = Animator.StringToHash("Working");
        static readonly int ConstructingHash = Animator.StringToHash("Constructing");

        [SerializeField] string displayName;
        [SerializeField] Color idleColor = new(0.55f, 0.55f, 0.58f, 1f);
        [SerializeField] Color workingColor = new(0.35f, 0.82f, 0.42f, 1f);
        [SerializeField] Color constructionColor = new(0.95f, 0.78f, 0.28f, 1f);
        [SerializeField] float workingPulseHz = 1.15f;

        readonly List<MeshRenderer> _bodyRenderers = new(4);
        MaterialPropertyBlock _block;

        BuildingWidget _widget;
        Animator _animator;
        bool _cached;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public Color IdleColor => idleColor;

        public Color WorkingColor => workingColor;

        public Color ConstructionColor => constructionColor;

        public void Sync(EntityManager em, Entity entity, Camera cam)
        {
            EnsureCache();
            ApplyPose(em, entity);
            ApplyBar(em, entity, cam);
            ApplyBody(em, entity);
        }

        public static string NameOf(GameObject prefab)
        {
            if (prefab == null)
                return string.Empty;
            var view = prefab.GetComponent<BuildingView>();
            return view != null ? view.DisplayName : prefab.name;
        }

        void EnsureCache()
        {
            if (_cached)
                return;
            _cached = true;
            _widget = GetComponentInChildren<BuildingWidget>(true);

            _animator = GetComponentInChildren<Animator>(true);
            _block ??= new MaterialPropertyBlock();
            _bodyRenderers.Clear();
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].GetComponentInParent<BuildingWidget>(true) != null)
                    continue;
                if (renderers[i].GetComponentInParent<BuildingOverlay>(true) != null)
                    continue;
                _bodyRenderers.Add(renderers[i]);
            }
        }

        void ApplyPose(EntityManager em, Entity entity)
        {
            if (em.HasComponent<LocalTransform>(entity))
            {
                var local = em.GetComponentData<LocalTransform>(entity);
                transform.SetPositionAndRotation(local.Position, local.Rotation);
                return;
            }

            if (!em.HasComponent<LocalToWorld>(entity))
                return;
            var ltw = em.GetComponentData<LocalToWorld>(entity);
            transform.SetPositionAndRotation(ltw.Position, ltw.Rotation);
        }

        void ApplyBar(EntityManager em, Entity entity, Camera cam)
        {
            if (_widget == null)
                return;

            var constructing = em.HasComponent<Construction>(entity);
            var slots = em.HasComponent<BuildingType>(entity)
                ? em.GetComponentData<BuildingType>(entity).WorkplaceSlots
                : 0;

            var workplace = em.HasComponent<Workplace>(entity)
                ? em.GetComponentData<Workplace>(entity)
                : default;

            if (_widget.ConstructionRoot != null)
                _widget.ConstructionRoot.SetActive(constructing);
            if (_widget.WorkerRoot != null)
                _widget.WorkerRoot.SetActive(!constructing && slots > 0);

            if (constructing && _widget.ConstructionFill != null)
            {
                var construction = em.GetComponentData<Construction>(entity);
                _widget.ConstructionFill.fillAmount = construction.Normalized;
            }

            if (!constructing && _widget.WorkerFill != null && slots > 0)
                _widget.WorkerFill.fillAmount = Workplace.Load01(workplace.AssignedCount, slots);

            _widget.FaceCamera(cam);
        }

        void ApplyBody(EntityManager em, Entity entity)
        {
            var constructing = em.HasComponent<Construction>(entity);
            var workplace = em.HasComponent<Workplace>(entity)
                ? em.GetComponentData<Workplace>(entity)
                : default;
            var working = !constructing
                && em.HasComponent<Workplace>(entity)
                && !workplace.IsPaused
                && workplace.WorkingCount > 0;

            var color = constructing
                ? constructionColor
                : working
                    ? Pulse(idleColor, workingColor)
                    : idleColor;
            WriteBodyColor(color);

            if (_animator == null || _animator.runtimeAnimatorController == null)
                return;
            _animator.SetBool(ConstructingHash, constructing);
            _animator.SetBool(WorkingHash, working);
        }

        Color Pulse(Color from, Color to)
        {
            var hz = workingPulseHz > 0.01f ? workingPulseHz : 1.15f;
            var t = 0.5f + 0.5f * math.sin((float)(Time.time * math.PI * 2f * hz));
            return Color.Lerp(from, to, t);
        }

        void WriteBodyColor(Color color)
        {
            _block.Clear();
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            for (var i = 0; i < _bodyRenderers.Count; i++)
            {
                if (_bodyRenderers[i] != null)
                    _bodyRenderers[i].SetPropertyBlock(_block);
            }
        }
    }
}
