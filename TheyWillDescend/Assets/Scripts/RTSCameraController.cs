using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class RTSCameraController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] InputActionReference moveAction;
    [SerializeField] InputActionReference zoomAction;
    [SerializeField] string sprintActionName = "Sprint";
    
    [Header("Cinemachine Setup")]
    [SerializeField] new CinemachineCamera camera;
    [SerializeField] Transform cameraTarget;
    [SerializeField] CinemachineOrbitalFollow orbitalFollow;

    [Header("Movement Settings")]
    [SerializeField] float moveSpeed = 15f;
    [SerializeField] float sprintSpeedMultiplier = 4f;
    [SerializeField] float acceleration = 10f;
    [SerializeField] float deceleration = 15f;

    [Header("Zoom Steps & Parabola")]
    [SerializeField] float minRadius = 8f;        
    [SerializeField] float maxRadius = 15f;       
    [Tooltip("Сколько шагов зума между minRadius и maxRadius (включая оба края)")]
    [SerializeField, Min(2)] int zoomStepCount = 3;
    [SerializeField] float zoomSmoothness = 10f;  
    [SerializeField] AnimationCurve angleCurve = AnimationCurve.Linear(0, 20, 1, 75);

    [Header("Map Limits (Crater)")]
    [SerializeField] Vector3 mapCenter = Vector3.zero;
    [SerializeField] float maxMapRadius = 50f;

    private Vector3 currentVelocity;
    private bool sprintInput;
    private InputAction move;
    private InputAction zoom;
    private InputAction sprintAction;
    private float currentSpeedMultiplier = 1f;
    private int targetStep;
    private float targetRadius;
    private float currentRadius;

    /// <summary>Радиус одного шага зума, вычисляется из диапазона и числа шагов.</summary>
    float StepSize => (maxRadius - minRadius) / Mathf.Max(1, zoomStepCount - 1);

    private void OnValidate()
    {
        if (camera == null) camera = GetComponent<CinemachineCamera>();
        if (orbitalFollow == null && camera != null) 
            orbitalFollow = camera.GetComponent<CinemachineOrbitalFollow>();
    }

    private void OnEnable()
    {
        move?.actionMap?.Enable();
        move?.Enable();
        zoom?.actionMap?.Enable();
        zoom?.Enable();
    }

    private void Awake()
    {
        var asset = InputSystem.actions;
        move = ActionOrFind(moveAction, asset, "Move");
        zoom = ActionOrFind(zoomAction, asset, "Zoom");
        sprintAction = asset != null ? asset.FindAction(sprintActionName) : null;
        sprintAction?.Enable();
    }

    static InputAction ActionOrFind(InputActionReference reference, InputActionAsset asset, string name)
    {
        var fromRef = reference != null ? reference.action : null;
        return fromRef != null ? fromRef : asset?.FindAction(name);
    }

    private void Start()
    {
        if (orbitalFollow != null)
        {
            targetRadius = orbitalFollow.RadialAxis.Value;
            targetStep = Mathf.RoundToInt((targetRadius - minRadius) / StepSize);
            targetStep = Mathf.Clamp(targetStep, 0, zoomStepCount - 1);
            targetRadius = minRadius + targetStep * StepSize;
            currentRadius = targetRadius;
        }
    }

    private void Update()
    {
        HandleMovementInput();
        HandleZoomInput();
        UpdateMovement();
    }

    void HandleMovementInput()
    {
        if (move == null || camera == null)
            return;

        Vector2 moveInput = move.ReadValue<Vector2>();

        Vector3 forward = camera.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = camera.transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 targetDirection = (forward * moveInput.y + right * moveInput.x).normalized;
        sprintInput = sprintAction != null && sprintAction.IsPressed();
        
        float targetMultiplier = sprintInput ? sprintSpeedMultiplier : 1f;
        currentSpeedMultiplier = Mathf.Lerp(currentSpeedMultiplier, targetMultiplier, Time.deltaTime * 10f);

        Vector3 targetVelocity = targetDirection * (moveSpeed * currentSpeedMultiplier);

        float accelRate = moveInput.sqrMagnitude > 0 ? acceleration : deceleration;
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.deltaTime * accelRate);
    }

    void HandleZoomInput()
    {
        if (zoom == null || zoomStepCount < 2)
            return;

        float scrollInput = zoom.ReadValue<float>();

        if (Mathf.Abs(scrollInput) > 0.1f)
        {
            targetStep = Mathf.Clamp(targetStep + (scrollInput > 0f ? -1 : 1), 0, zoomStepCount - 1);
            targetRadius = minRadius + targetStep * StepSize;
        }

        if (orbitalFollow != null)
        {
            currentRadius = Mathf.Lerp(currentRadius, targetRadius, Time.deltaTime * zoomSmoothness);
            orbitalFollow.RadialAxis.Value = currentRadius;

            float normalizedZoom = Mathf.InverseLerp(minRadius, maxRadius, currentRadius);
            float targetAngle = angleCurve.Evaluate(normalizedZoom);
            orbitalFollow.VerticalAxis.Value = targetAngle;
        }
    }

    void UpdateMovement()
    {
        if (cameraTarget == null)
            return;

        Vector3 newPosition = cameraTarget.position + currentVelocity * Time.unscaledDeltaTime;

        // Проверяем лимиты карты (кратера)
        Vector3 offsetFromCenter = newPosition - mapCenter;
        offsetFromCenter.y = 0f;

        if (offsetFromCenter.magnitude > maxMapRadius)
        {
            newPosition = mapCenter + offsetFromCenter.normalized * maxMapRadius;
            newPosition.y = cameraTarget.position.y;
            currentVelocity = Vector3.zero;
        }

        cameraTarget.position = newPosition;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(mapCenter, 1f);

        int segments = 36;
        float angle = 0f;
        Vector3 lastPoint = mapCenter + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * maxMapRadius;

        for (int i = 1; i <= segments; i++)
        {
            angle += (360f / segments) * Mathf.Deg2Rad;
            Vector3 nextPoint = mapCenter + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * maxMapRadius;
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }
    }
}