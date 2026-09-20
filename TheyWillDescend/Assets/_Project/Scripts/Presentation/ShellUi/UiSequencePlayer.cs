using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheyWillDescend.Presentation.ShellUi
{
    /// <summary>
    /// Пошаговое появление списка элементов UI и обратное исчезновение.
    /// Порядок появления — по списку, исчезновение — в обратном порядке.
    /// Шаг можно пометить как скрывающий: тогда при открытии меню он прячется, а при закрытии возвращается.
    /// Время unscaled, чтобы меню работало на остановленной игре.
    /// </summary>
    public sealed class UiSequencePlayer : MonoBehaviour
    {
        public enum StepMode
        {
            /// <summary>Показать/скрыть без анимации.</summary>
            Instant,
            /// <summary>Только прозрачность.</summary>
            Fade,
            /// <summary>Прозрачность и лёгкий scale.</summary>
            FadeScale,
            /// <summary>Прозрачность, scale и приезд из смещения.</summary>
            FadeScaleSlide
        }

        [Serializable]
        class Step
        {
            public GameObject target = null;

            [Tooltip("Этот элемент при открытии меню прячется (пропадает), а при закрытии возвращается. " +
                     "Для меню и панелей, которые должны уступить место меню паузы.")]
            public bool hideInsteadOfShow = false;

            [Min(0f), Tooltip("Пауза перед этим шагом, после завершения предыдущего.")]
            public float delay = 0.05f;

            [Min(0f), Tooltip("Длительность анимации шага. 0 — мгновенно.")]
            public float duration = 0.18f;

            public StepMode mode = StepMode.FadeScale;

            [Tooltip("Из какого смещения (в пикселях от позиции layout) приезжает элемент.")]
            public Vector2 slideFrom = new Vector2(-48f, 0f);

            [Range(0f, 1.5f), Tooltip("Из какого масштаба элемент дорастает до своего.")]
            public float scaleFrom = 0.9f;

            [NonSerialized] public CanvasGroup Group;
            [NonSerialized] public RectTransform Rect;
            [NonSerialized] public Vector3 BaseScale;
            [NonSerialized] public Vector2 LayoutPosition;
            [NonSerialized] public bool SlideOverridden;
            [NonSerialized] public float Progress;
        }

        [SerializeField, Tooltip("Шаги последовательности появления. Исчезновение идёт в обратном порядке.")]
        List<Step> steps = new List<Step>();

        bool _playing;
        bool _initialized;
        Action _onFinished;

        public bool IsPlaying => _playing;

        /// <summary>Состояние шага в покое: показан, либо спрятан если шаг помечен как скрывающий.</summary>
        static float RestProgress(Step step) => step.hideInsteadOfShow ? 1f : 0f;

        /// <summary>Целевое состояние шага при открытии меню.</summary>
        static float OpenProgress(Step step) => step.hideInsteadOfShow ? 0f : 1f;

        void OnEnable()
        {
            Canvas.willRenderCanvases += ApplySlides;
        }

        void OnDisable()
        {
            Canvas.willRenderCanvases -= ApplySlides;
            Cancel();
            // Объект выключили на середине проигрыша — возвращаем шаги в состояние покоя,
            // иначе спрятанные шаги так и останутся невидимыми.
            ResetSteps();
        }

        /// <summary>Проигрывает появление шагов по порядку.</summary>
        public void PlayIn()
        {
            Play(true, null);
        }

        /// <summary>Проигрывает исчезновение в обратном порядке.</summary>
        public void PlayOut(Action onFinished = null)
        {
            Play(false, onFinished);
        }

        /// <summary>
        /// Сбрасывает все шаги в состояние покоя без анимации:
        /// обычные шаги прячутся, помеченные как скрывающие — показываются.
        /// </summary>
        public void HideImmediate()
        {
            Initialize();
            Cancel();
            ResetSteps();
        }

        void ResetSteps()
        {
            if (!_initialized)
                return;

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null || step.target == null)
                    continue;
                step.Progress = RestProgress(step);
                Apply(step);
            }
        }

        void Play(bool forward, Action onFinished)
        {
            Initialize();
            Cancel();
            _onFinished = onFinished;
            _playing = true;
            StartCoroutine(Run(forward));
        }

        void Cancel()
        {
            StopAllCoroutines();
            _playing = false;
            _onFinished = null;
        }

        IEnumerator Run(bool forward)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                int index = forward ? i : steps.Count - 1 - i;
                var step = steps[index];
                if (step == null || step.target == null)
                    continue;

                float target = forward ? OpenProgress(step) : RestProgress(step);
                if (Mathf.Abs(step.Progress - target) < 0.0001f)
                {
                    // Шаг уже в целевом состоянии — не тратим на него ни задержку, ни время.
                    step.Progress = target;
                    Apply(step);
                    continue;
                }

                if (step.delay > 0f)
                    yield return WaitUnscaled(step.delay);

                yield return Animate(step, target);
            }

            _playing = false;
            var callback = _onFinished;
            _onFinished = null;
            callback?.Invoke();
        }

        IEnumerator Animate(Step step, float target)
        {
            // Позицию layout снимаем до первого смещения, пока объект стоит в покоящейся позиции.
            if (step.mode == StepMode.FadeScaleSlide && step.Rect != null && !step.SlideOverridden)
                step.LayoutPosition = step.Rect.anchoredPosition;

            if (step.mode == StepMode.Instant || step.duration <= 0f)
            {
                step.Progress = target;
                Apply(step);
                yield break;
            }

            while (step.Progress != target)
            {
                step.Progress = Mathf.MoveTowards(step.Progress, target, Time.unscaledDeltaTime / step.duration);
                Apply(step);
                if (step.Progress == target)
                    break;
                yield return null;
            }
        }

        static IEnumerator WaitUnscaled(float seconds)
        {
            float remaining = seconds;
            while (remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void Apply(Step step)
        {
            float t = Mathf.SmoothStep(0f, 1f, step.Progress);

            if (step.Group != null)
            {
                step.Group.alpha = t;
                step.Group.interactable = step.Progress >= 1f;
                step.Group.blocksRaycasts = step.Progress >= 1f;
            }

            if (step.mode == StepMode.FadeScale || step.mode == StepMode.FadeScaleSlide)
                step.target.transform.localScale = step.BaseScale * Mathf.Lerp(step.scaleFrom, 1f, t);

            if (step.mode != StepMode.FadeScaleSlide || step.Rect == null)
                return;

            if (step.Progress >= 1f || step.Progress <= 0f)
            {
                // В покое позиция полностью принадлежит layout: смещение снимаем,
                // чтобы следующий запуск снял актуальную позицию.
                step.SlideOverridden = false;
                step.Rect.anchoredPosition = step.LayoutPosition;
            }
            else
            {
                step.SlideOverridden = true;
                SetSlide(step, t);
            }
        }

        void SetSlide(Step step, float t)
        {
            step.Rect.anchoredPosition = step.LayoutPosition + step.slideFrom * (1f - t);
        }

        /// <summary>
        /// Вызывается после пересчёта layout в том же кадре, поэтому смещение не затирается LayoutGroup.
        /// </summary>
        void ApplySlides()
        {
            if (!_playing)
                return;

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null || !step.SlideOverridden || step.Rect == null)
                    continue;
                SetSlide(step, Mathf.SmoothStep(0f, 1f, step.Progress));
            }
        }

        void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null || step.target == null)
                    continue;

                step.Group = step.target.GetComponent<CanvasGroup>();
                if (step.Group == null)
                    step.Group = step.target.AddComponent<CanvasGroup>();
                step.Rect = step.target.GetComponent<RectTransform>();
                step.BaseScale = step.target.transform.localScale;
                if (step.Rect != null)
                    step.LayoutPosition = step.Rect.anchoredPosition;
                step.Progress = RestProgress(step);
                Apply(step);
            }
        }
    }
}
