using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class BuildingHoverVFX : MonoBehaviour
{
    [SerializeField] private VisualEffect hoverVFX;

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        if (hoverVFX != null) hoverVFX.Stop();
    }

    private bool isHovered;

    void Update()
    {
        if (mainCamera == null || hoverVFX == null) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        bool hit = Physics.Raycast(ray, out RaycastHit raycastHit);
        bool currentlyHovered = hit && raycastHit.transform == transform;

        if (currentlyHovered != isHovered)
        {
            isHovered = currentlyHovered;
            if (isHovered)
            {
                hoverVFX.Play();
            }
            else
            {
                hoverVFX.Stop();
            }
        }
    }
}