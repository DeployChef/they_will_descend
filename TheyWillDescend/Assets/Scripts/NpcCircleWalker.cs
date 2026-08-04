using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// NPC ходит по кругу с возможностью переключения анимаций
/// Использует новый Input System (программно созданные InputAction, Подход 2)
/// </summary>
public class NpcCircleWalker : MonoBehaviour
{
    // ===================== НАСТРОЙКИ ДВИЖЕНИЯ =====================
    [Header("Параметры движения")]
    [Tooltip("Радиус круга")]
    public float radius = 5f;

    [Tooltip("Скорость (оборотов в секунду)")]
    public float speed = 0.5f;

    [Tooltip("Направление: 1 = против часовой, -1 = по часовой")]
    public float direction = 1f;

    [Header("Настройки круга")]
    [Tooltip("Высота центра круга относительно стартовой позиции")]
    public float circleHeightOffset = 0f;

    // ===================== НАСТРОЙКИ АНИМАЦИЙ =====================
    [Header("Выбор анимации")]
    [Tooltip("Текущая анимация (по умолчанию Walk 1)")]
    public AnimationType currentAnimation = AnimationType.Walk1;

    [Header("Параметры Animator (должны совпадать с параметрами в Animator!)")]
    public string walk1Param = "Walk 1";
    public string walk2Param = "Walk 2";
    public string run1Param = "Run 1";
    public string slowRunParam = "Slow Run";
    public string jumpParam = "Jump";
    public string idle2Param = "Idle 2";
    public string sadWalkParam = "Sad Walk";

    // ===================== ДОПОЛНИТЕЛЬНО =====================
    [Header("Готовые пресеты")]
    [Tooltip("Быстрый запуск: просто выбери пресет")]
    public Preset preset = Preset.Walk1;

    // ===================== ВНУТРЕННИЕ ПЕРЕМЕННЫЕ =====================
    private Animator animator;
    private Vector3 circleCenter;
    private float angle = 0f;

    // ===================== НОВЫЙ INPUT SYSTEM (ПОДХОД 2) =====================
    // Действия создаются программно, привязки остаются в коде.
    // Событийная модель: реагируем на "performed", а не опрашиваем клавиши каждый кадр.
    private InputAction walk1Action;
    private InputAction walk2Action;
    private InputAction run1Action;
    private InputAction slowRunAction;
    private InputAction jumpAction;
    private InputAction idle2Action;
    private InputAction sadWalkAction;

    // ===================== ENUM-Ы =====================
    public enum AnimationType
    {
        Walk1,
        Walk2,
        Run1,
        SlowRun,
        Jump,
        Idle2,
        SadWalk
    }

    public enum Preset
    {
        None,
        Walk1,
        Walk2,
        Run1,
        SlowRun,
        Jump,
        Idle2,
        SadWalk
    }

    // ===================== AWAKE =====================
    void Awake()
    {
        // Создаём InputAction программно (Подход 2).
        // Формат привязки: "<Keyboard>/1" — клавиша 1 на клавиатуре.
        walk1Action = new InputAction("Walk1", InputActionType.Button, "<Keyboard>/1");
        walk2Action = new InputAction("Walk2", InputActionType.Button, "<Keyboard>/2");
        run1Action = new InputAction("Run1", InputActionType.Button, "<Keyboard>/3");
        slowRunAction = new InputAction("SlowRun", InputActionType.Button, "<Keyboard>/4");
        jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/5");
        idle2Action = new InputAction("Idle2", InputActionType.Button, "<Keyboard>/6");
        sadWalkAction = new InputAction("SadWalk", InputActionType.Button, "<Keyboard>/7");
    }

    // ===================== ON ENABLE / ON DISABLE =====================
    // Включаем действия и подписываемся на события, когда объект активен.
    void OnEnable()
    {
        walk1Action.performed += OnWalk1;
        walk2Action.performed += OnWalk2;
        run1Action.performed += OnRun1;
        slowRunAction.performed += OnSlowRun;
        jumpAction.performed += OnJump;
        idle2Action.performed += OnIdle2;
        sadWalkAction.performed += OnSadWalk;

        walk1Action.Enable();
        walk2Action.Enable();
        run1Action.Enable();
        slowRunAction.Enable();
        jumpAction.Enable();
        idle2Action.Enable();
        sadWalkAction.Enable();
    }

    void OnDisable()
    {
        walk1Action.performed -= OnWalk1;
        walk2Action.performed -= OnWalk2;
        run1Action.performed -= OnRun1;
        slowRunAction.performed -= OnSlowRun;
        jumpAction.performed -= OnJump;
        idle2Action.performed -= OnIdle2;
        sadWalkAction.performed -= OnSadWalk;

        walk1Action.Disable();
        walk2Action.Disable();
        run1Action.Disable();
        slowRunAction.Disable();
        jumpAction.Disable();
        idle2Action.Disable();
        sadWalkAction.Disable();
    }

    // ===================== START =====================
    void Start()
    {
        // Получаем компонент Animator
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("❌ На персонаже нет компонента Animator!");
            return;
        }

        // Запоминаем центр круга
        circleCenter = transform.position;
        circleCenter.y += circleHeightOffset;

        // Применяем пресет если выбран
        if (preset != Preset.None)
        {
            SetAnimation(preset);
        }
        else
        {
            // Иначе применяем currentAnimation
            SetAnimation(currentAnimation);
        }
    }

    // ===================== UPDATE =====================
    void Update()
    {
        if (animator == null) return;

        // Двигаемся по кругу
        angle += speed * direction * Time.deltaTime * Mathf.PI * 2f;

        // Новая позиция
        float x = circleCenter.x + Mathf.Cos(angle) * radius;
        float z = circleCenter.z + Mathf.Sin(angle) * radius;

        transform.position = new Vector3(x, circleCenter.y, z);

        // Поворачиваем персонажа по ходу движения.
        // Направление движения = касательный вектор к окружности (производная позиции по углу).
        // Работает для обоих направлений (direction = 1 или -1).
        Vector3 moveDirection = new Vector3(
            -Mathf.Sin(angle) * direction,
            0f,
            Mathf.Cos(angle) * direction
        );
        transform.rotation = Quaternion.LookRotation(moveDirection);
    }

    // ===================== ПЕРЕКЛЮЧЕНИЕ АНИМАЦИЙ =====================
    /// <summary>
    /// Переключает анимацию по строковому имени
    /// </summary>
    public void SetAnimation(string paramName)
    {
        if (animator == null) return;

        animator.ResetTrigger("Jump"); // Сбрасываем триггер если был

        // Сначала выключаем все
        animator.SetBool(walk1Param, false);
        animator.SetBool(walk2Param, false);
        animator.SetBool(run1Param, false);
        animator.SetBool(slowRunParam, false);
        animator.SetBool(idle2Param, false);
        animator.SetBool(sadWalkParam, false);

        // Включаем нужную
        animator.SetBool(paramName, true);
    }

    /// <summary>
    /// Переключает анимацию по Enum
    /// </summary>
    public void SetAnimation(AnimationType animType)
    {
        switch (animType)
        {
            case AnimationType.Walk1:
                SetAnimation(walk1Param);
                break;
            case AnimationType.Walk2:
                SetAnimation(walk2Param);
                break;
            case AnimationType.Run1:
                SetAnimation(run1Param);
                break;
            case AnimationType.SlowRun:
                SetAnimation(slowRunParam);
                break;
            case AnimationType.Jump:
                SetAnimation(jumpParam);
                break;
            case AnimationType.Idle2:
                SetAnimation(idle2Param);
                break;
            case AnimationType.SadWalk:
                SetAnimation(sadWalkParam);
                break;
        }
    }

    /// <summary>
    /// Переключает анимацию по Preset
    /// </summary>
    public void SetAnimation(Preset animPreset)
    {
        switch (animPreset)
        {
            case Preset.Walk1:
                SetAnimation(walk1Param);
                break;
            case Preset.Walk2:
                SetAnimation(walk2Param);
                break;
            case Preset.Run1:
                SetAnimation(run1Param);
                break;
            case Preset.SlowRun:
                SetAnimation(slowRunParam);
                break;
            case Preset.Jump:
                SetAnimation(jumpParam);
                break;
            case Preset.Idle2:
                SetAnimation(idle2Param);
                break;
            case Preset.SadWalk:
                SetAnimation(sadWalkParam);
                break;
            default:
                break;
        }
    }

    // ===================== НОВЫЙ INPUT SYSTEM HANDLERS =====================
    // Обработчики событий "performed" для каждого действия.
    // Вызываются только в момент нажатия (аналог wasPressedThisFrame).
    private void OnWalk1(InputAction.CallbackContext ctx) => SetAnimation(AnimationType.Walk1);
    private void OnWalk2(InputAction.CallbackContext ctx) => SetAnimation(AnimationType.Walk2);
    private void OnRun1(InputAction.CallbackContext ctx) => SetAnimation(AnimationType.Run1);
    private void OnSlowRun(InputAction.CallbackContext ctx) => SetAnimation(AnimationType.SlowRun);
    private void OnJump(InputAction.CallbackContext ctx) => SetAnimation(AnimationType.Jump);
    private void OnIdle2(InputAction.CallbackContext ctx) => SetAnimation(AnimationType.Idle2);
    private void OnSadWalk(InputAction.CallbackContext ctx) => SetAnimation(AnimationType.SadWalk);

    // ===================== ВИЗУАЛИЗАЦИЯ В РЕДАКТОРЕ =====================
    void OnDrawGizmosSelected()
    {
        // Рисуем круг в редакторе для наглядности
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
