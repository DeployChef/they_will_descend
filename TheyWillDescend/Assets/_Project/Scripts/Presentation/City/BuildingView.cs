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

        [Header("World UI billboard")]
        [Tooltip("Высота иконки над зданием, в метрах")]
        [SerializeField] float uiHeight = 2.2f;
        [Tooltip("Дистанция, на которой иконка имеет базовый масштаб (как на префабе). Крупнее — иконки крупнее на экране")]
        [SerializeField] float uiReferenceDistance = 25f;
        [Tooltip("Базовый масштаб UI из префаба (_BuildingWorldUi). Должен совпадать с localScale префаба")]
        [SerializeField] float uiBaseScale = 0.02f;
        [Header("World UI visibility")]
        [Tooltip("Камера ниже этой высоты (Y) — ВСЕ иконки зданий скрыты разом. Одно значение для всех зданий")]
        [SerializeField] float uiHideBelowCameraHeight = 18f;

        readonly List<MeshRenderer> _bodyRenderers = new(4);
        MaterialPropertyBlock _block;

        BuildingWorldUi _worldUi;
        Animator _animator;
        bool _cached;

        // Глобальное решение «видны ли иконки» — одно на кадр для всех зданий сразу
        static int _visibilityFrame = -1;
        static bool _iconsVisible = true;

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
            _worldUi = GetComponentInChildren<BuildingWorldUi>(true);

            _animator = GetComponentInChildren<Animator>(true);
            _block ??= new MaterialPropertyBlock();
            _bodyRenderers.Clear();
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].GetComponentInParent<BuildingWorldUi>(true) != null)
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
            if (_worldUi == null)
                return;

            var constructing = em.HasComponent<Construction>(entity);
            var slots = em.HasComponent<BuildingType>(entity)
                ? em.GetComponentData<BuildingType>(entity).WorkplaceSlots
                : 0;

            var workplace = em.HasComponent<Workplace>(entity)
                ? em.GetComponentData<Workplace>(entity)
                : default;

            // Переключаем группы
            if (_worldUi.ConstructionRoot != null)
                _worldUi.ConstructionRoot.SetActive(constructing);
            if (_worldUi.WorkerRoot != null)
                _worldUi.WorkerRoot.SetActive(!constructing && slots > 0);

            // Заполнение полосы строительства
            var fill = constructing && _worldUi.ConstructionFill != null
                ? _worldUi.ConstructionFill
                : _worldUi.ConstructionFill;
            if (constructing && fill != null)
            {
                var construction = em.GetComponentData<Construction>(entity);
                fill.fillAmount = construction.Normalized;
            }

            // Заполнение полосы рабочих
            var workerFill = _worldUi.WorkerFill;
            if (!constructing && workerFill != null && slots > 0)
            {
                workerFill.fillAmount = Workplace.Load01(workplace.AssignedCount, slots);
            }

            var roof = _worldUi.transform;
            roof.position = transform.position + Vector3.up * uiHeight;

            if (cam == null)
                return;

            var toUi = roof.position - cam.transform.position;
            var distance = toUi.magnitude;

            // Billboard: канвас всегда плоскостью к камере
            roof.rotation = Quaternion.LookRotation(toUi);

            // Компенсация перспективы: масштаб растёт с дистанцией →
            // размер иконки на экране постоянный
            var refDist = Mathf.Max(0.1f, uiReferenceDistance);
            roof.localScale = Vector3.one * (uiBaseScale * distance / refDist);

            // Все иконки скрываются разом, когда камера опускается ниже порога
            roof.gameObject.SetActive(IsIconsVisible(cam));
        }

        /// <summary>
        /// Решение общее для всех зданий: считается один раз за кадр.
        /// Камера выше порога — иконки видны, ниже — скрыты у всех зданий.
        /// </summary>
        bool IsIconsVisible(Camera cam)
        {
            if (Time.frameCount != _visibilityFrame)
            {
                _visibilityFrame = Time.frameCount;
                _iconsVisible = cam.transform.position.y >= uiHideBelowCameraHeight;
            }

            return _iconsVisible;
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
