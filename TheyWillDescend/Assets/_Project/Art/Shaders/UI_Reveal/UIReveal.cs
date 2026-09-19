using UnityEngine;
using UnityEngine.UI;

public class UIReveal : MonoBehaviour
{
    [SerializeField] private Image revealImage;
    [SerializeField] private float duration = 0.8f;
    [Tooltip("Проигрывать раскрытие при каждом включении объекта (например при открытии меню паузы).")]
    [SerializeField] private bool playOnEnable = true;
    [Tooltip("Максимальное значение _Reveal_Radius, до которого происходит анимация.")]
    [SerializeField] private float maxRadius = 0.3f;
    [SerializeField] private CanvasGroup menuCanvasGroup;
    private Material material;
    // Shader Graph генерирует имя свойства из отображаемого имени "Reveal Radius" -"_Reveal_Radius".
    private static readonly int RevealRadius = Shader.PropertyToID("_Reveal_Radius");

    private Coroutine _routine;

    private void Awake()
    {
        if (revealImage == null)
        {
            enabled = false;
            return;
        }

        material = new Material(revealImage.material);
        revealImage.material = material;
    }

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        // Кор рутина умирает вместе с выключенным объектом — сбрасываем ссылку.
        _routine = null;
    }

    /// <summary>
    /// Запускает раскрытие. Можно вызывать из кода открытия меню.
    /// </summary>
    public void Play()
    {
        if (material == null)
            return;

        if (_routine != null)
            StopCoroutine(_routine);

        material.SetFloat(RevealRadius, 0f);
        menuCanvasGroup.alpha = 0f;
        _routine = StartCoroutine(Reveal(Mathf.Max(0f, maxRadius)));
    }

    private System.Collections.IEnumerator Reveal(float endRadius)
    {
        float time = 0f;

        while (time < duration)
        {
            // Unscaled время: меню может останавливать игру через Time.timeScale.
            time += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(time / duration);

            material.SetFloat(RevealRadius, Mathf.Lerp(0f, endRadius, t));
            menuCanvasGroup.alpha = t;

            yield return null;
        }

        material.SetFloat(RevealRadius, endRadius);
        _routine = null;
    }
}