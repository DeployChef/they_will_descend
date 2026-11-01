using TheyWillDescend.Presentation.Cameras;
using UnityEngine;

namespace TheyWillDescend.Presentation.City
{
    [DisallowMultipleComponent]
    public sealed class PyramidZoomLod : MonoBehaviour
    {
        [SerializeField] RTSCameraController cameraController;
        [SerializeField] LODGroup lodGroup;
        [SerializeField] float switchRadius = 10f;
        [SerializeField, Min(0f)] float hysteresis = 0.25f;

        int _currentLod = -1;

        void LateUpdate()
        {
            if (cameraController == null || lodGroup == null)
                return;

            float radius = cameraController.CurrentZoomRadius;
            int nextLod;

            if (_currentLod < 0)
                nextLod = radius <= switchRadius ? 0 : 1;
            else if (_currentLod == 0)
                nextLod = radius > switchRadius + hysteresis ? 1 : 0;
            else
                nextLod = radius < switchRadius - hysteresis ? 0 : 1;

            if (nextLod == _currentLod)
                return;

            lodGroup.ForceLOD(nextLod);
            Debug.Log($"Пирамида: LOD{nextLod}, радиус {radius:F2}", this);
            _currentLod = nextLod;
        }

        void OnDisable()
        {
            if (lodGroup != null)
                lodGroup.ForceLOD(-1);

            _currentLod = -1;
        }
    }
}
