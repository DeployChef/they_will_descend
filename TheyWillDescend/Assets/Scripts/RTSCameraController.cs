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
    [SerializeField] float radiusStep = 2f;       
    [SerializeField] float zoomSmoothness = 10f;  
    [SerializeField] AnimationCurve angleCurve = AnimationCurve.Linear(0, 20, 1, 75);

    [Header("Map Limits (Crater)")]
    [SerializeField] Vector3 mapCenter = Vector3.zero;
    [SerializeField] float maxMapRadius = 50f;

    private Vector3 currentVelocity;
    private bool sprintInput;
    private InputAction sprintAction;
    private float currentSpeedMultiplier = 1f;
    private float targetRadius;
    private float currentRadius;

    private void OnValidate()
    {
        if (camera == null) camera = GetComponent<CinemachineCamera>();
        if (orbitalFollow == null && camera != null) 
            orbitalFollow = camera.GetComponent<CinemachineOrbitalFollow>();
    }

    private void OnEnable()
    {
        EnableMap(moveAction);
        EnableMap(zoomAction);
    }

    private static void EnableMap(InputActionReference reference)
    {
        var action = reference != null ? reference.action : null;
        action?.actionMap?.Enable();
        action?.Enable();
    }

    private void Awake()
    {
        sprintAction = InputSystem.actions != null
            ? InputSystem.actions.FindAction(sprintActionName)
            : null;
        sprintAction?.Enable();
    }

    private void Start()
    {
        if (orbitalFollow != null)
        {
            targetRadius = orbitalFollow.RadialAxis.Value;
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
        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();

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
        float scrollInput = zoomAction.action.ReadValue<float>();
        
        if (Mathf.Abs(scrollInput) > 0.1f)
        {
            targetRadius -= Mathf.Sign(scrollInput) * radiusStep;
            targetRadius = Mathf.Clamp(targetRadius, minRadius, maxRadius);
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