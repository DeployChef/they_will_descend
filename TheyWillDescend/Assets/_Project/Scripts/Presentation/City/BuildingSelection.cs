using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Click-select a building overlay. Does not own meshes or HUD.
    /// </summary>
    public sealed class BuildingSelection : MonoBehaviour
    {
        [SerializeField] BuildPlacementController placement;

        public int SelectedBuildingId { get; private set; }

        public void Deselect() => SelectedBuildingId = 0;

        public void ClearIf(int buildingId)
        {
            if (SelectedBuildingId == buildingId)
                SelectedBuildingId = 0;
        }

        void Update()
        {
            if (!TryConsumeClick(out var hitBuildingId))
                return;
            SelectedBuildingId = hitBuildingId;
        }

        bool TryConsumeClick(out int buildingId)
        {
            buildingId = 0;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            if (placement != null && placement.IsPlacing)
                return false;

            var cam = Camera.main;
            if (cam == null)
                return false;
            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            var hits = Physics.RaycastAll(ray, 500f);
            var bestDist = float.MaxValue;
            BuildingIdTag best = null;
            for (var i = 0; i < hits.Length; i++)
            {
                var tag = hits[i].collider.GetComponentInParent<BuildingIdTag>();
                if (tag == null || hits[i].distance >= bestDist)
                    continue;
                bestDist = hits[i].distance;
                best = tag;
            }

            buildingId = best != null ? best.Id : 0;
            return true;
        }
    }
}
