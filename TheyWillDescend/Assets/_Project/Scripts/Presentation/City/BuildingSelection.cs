using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Click-select a player-constructed building or the central Pyramid.
    /// Does not own meshes or HUD.
    /// </summary>
    public sealed class BuildingSelection : MonoBehaviour
    {
        [SerializeField] BuildPlacementController placement;

        public int SelectedBuildingId { get; private set; }
        public bool IsPyramidSelected { get; private set; }

        public void Deselect()
        {
            SelectedBuildingId = 0;
            IsPyramidSelected = false;
        }

        public void SelectBuilding(int buildingId)
        {
            SelectedBuildingId = buildingId;
            IsPyramidSelected = false;
        }

        public void SelectPyramid()
        {
            SelectedBuildingId = 0;
            IsPyramidSelected = true;
        }

        public void ClearIf(int buildingId)
        {
            if (SelectedBuildingId == buildingId)
                SelectedBuildingId = 0;
        }

        void Update()
        {
            if (!TryConsumeClick(out var hitBuildingId, out var hitPyramid))
                return;

            if (hitPyramid)
                SelectPyramid();
            else if (hitBuildingId > 0)
                SelectBuilding(hitBuildingId);
            else
                Deselect();
        }

        bool TryConsumeClick(out int buildingId, out bool hitPyramid)
        {
            buildingId = 0;
            hitPyramid = false;

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
            Component best = null;

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.distance >= bestDist)
                    continue;

                var py = hit.collider.GetComponentInParent<PyramidView>();
                if (py != null)
                {
                    bestDist = hit.distance;
                    best = py;
                    continue;
                }

                var tag = hit.collider.GetComponentInParent<BuildingIdTag>();
                if (tag != null)
                {
                    bestDist = hit.distance;
                    best = tag;
                }
            }

            if (best is PyramidView)
                hitPyramid = true;
            else if (best is BuildingIdTag bTag)
                buildingId = bTag.Id;

            return true;
        }
    }
}