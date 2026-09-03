using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// In-world widget shared by houses: bars + status icons. Nested prefab on
    /// the stamp; position and base scale come from the layout, not BuildingView.
    /// </summary>
    public sealed class BuildingWidget : MonoBehaviour
    {
        [Header("Construction group")]
        [SerializeField] GameObject constructionRoot;
        [SerializeField] Image constructionBackground;
        [SerializeField] Image constructionFill;

        [Header("Worker group")]
        [SerializeField] GameObject workerRoot;
        [SerializeField] Image workerBackground;
        [SerializeField] Image workerFill;

        [SerializeField] GameObject statusRoot;

        [Header("Billboard")]
        [Tooltip("На этой дистанции камеры localScale совпадает с префабом. Одно значение на всех домах.")]
        [SerializeField] float referenceDistance = 40f;
        [Tooltip("Камера ниже этой высоты (Y) — все виджеты скрыты.")]
        [SerializeField] float hideBelowCameraHeight = 25f;

        Vector3 _authoredScale;
        bool _capturedScale;

        static int _visibilityFrame = -1;
        static bool _iconsVisible = true;

        public GameObject ConstructionRoot => constructionRoot;
        public Image ConstructionBackground => constructionBackground;
        public Image ConstructionFill => constructionFill;

        public GameObject WorkerRoot => workerRoot;
        public Image WorkerBackground => workerBackground;
        public Image WorkerFill => workerFill;

        public GameObject StatusRoot => statusRoot;

        void Awake()
        {
            CaptureAuthoredScale();
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                if (canvas.sortingOrder < 20)
                    canvas.sortingOrder = 20;
            }
        }

        public void FaceCamera(Camera cam)
        {
            CaptureAuthoredScale();
            if (cam == null)
                return;

            var toUi = transform.position - cam.transform.position;
            if (toUi.sqrMagnitude < 1e-8f)
                return;

            transform.rotation = Quaternion.LookRotation(toUi);

            var refDist = Mathf.Max(0.1f, referenceDistance);
            transform.localScale = _authoredScale * (toUi.magnitude / refDist);
            gameObject.SetActive(IsVisible(cam));
        }

        void CaptureAuthoredScale()
        {
            if (_capturedScale)
                return;

            _authoredScale = transform.localScale;
            if (_authoredScale.sqrMagnitude < 1e-12f)
                _authoredScale = Vector3.one * 0.02f;
            _capturedScale = true;
        }

        bool IsVisible(Camera cam)
        {
            if (Time.frameCount != _visibilityFrame)
            {
                _visibilityFrame = Time.frameCount;
                _iconsVisible = cam.transform.position.y >= hideBelowCameraHeight;
            }

            return _iconsVisible;
        }
    }
}
