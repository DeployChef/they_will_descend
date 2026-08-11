using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.Cinemachine;

public class RTSCameraController : MonoBehaviour
{
    [SerializeField] InputActionReference moveAction;
    [SerializeField] string sprintActionName = "Sprint";
    private Vector3 moveInput3D;
    private bool sprintInput;
    private InputAction sprintAction;
    float currentSpeedMultiplier = 1f;

    [SerializeField] new CinemachineCamera camera;
    [SerializeField] CinemachineOrbitalFollow OrbitalFollow;
    [SerializeField] Transform cameraTarget;
    [SerializeField] float moveSpeed = 15f;
    [SerializeField] float sprintSpeedMultiplier = 4f;
    [SerializeField] float acceleration = 10f; // Настройка резкости старта/остановки
    [SerializeField] float deceleration = 15f;

    private void OnValidate()
    {
        if (camera == null)
            camera = GetComponent<CinemachineCamera>();
            if (OrbitalFollow == null)
                OrbitalFollow = GetComponent<CinemachineOrbitalFollow>();
    }

    private void Awake()
    {
        sprintAction = InputSystem.actions.FindAction(sprintActionName);
    }

    private void LateUpdate()
    {
        HandleInput();
        UpdateMovement();
    }

   void HandleInput()
    {
        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        
        Vector3 forward = camera.transform.forward;
        forward.y = 0f;
        forward.Normalize();
        
        Vector3 right = camera.transform.right;
        right.y = 0f;
        right.Normalize();
        
        // Это целевой вектор, куда мы ХОТИМ двигаться
        Vector3 targetMoveInput3D = forward * moveInput.y + right * moveInput.x;
        
        // Плавно меняем текущий вектор движения к целевому (Инерция)
        float accelRate = (moveInput.sqrMagnitude > 0) ? acceleration : deceleration;
        moveInput3D = Vector3.Lerp(moveInput3D, targetMoveInput3D, Time.deltaTime * accelRate);
        
        sprintInput = sprintAction.IsPressed();
        float targetMultiplier = sprintInput ? sprintSpeedMultiplier : 1f;
        
        currentSpeedMultiplier = Mathf.Lerp(currentSpeedMultiplier, targetMultiplier, Time.deltaTime * 10f);
    }
    void UpdateMovement()
    {



        Vector3 velocity = moveInput3D * moveSpeed * currentSpeedMultiplier;
        Vector3 targetPosition = cameraTarget.position + velocity * Time.deltaTime;
        cameraTarget.position = targetPosition;
    }

}   
