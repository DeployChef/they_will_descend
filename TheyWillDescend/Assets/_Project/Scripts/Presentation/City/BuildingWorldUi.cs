using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Roof chrome shared by houses: bar + status icons. Prefab instance, not
    /// assembled in code.
    /// </summary>
    public sealed class BuildingWorldUi : MonoBehaviour
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

        static Sprite _white;

        public GameObject ConstructionRoot => constructionRoot;
        public Image ConstructionBackground => constructionBackground;
        public Image ConstructionFill => constructionFill;

        public GameObject WorkerRoot => workerRoot;
        public Image WorkerBackground => workerBackground;
        public Image WorkerFill => workerFill;

        public GameObject StatusRoot => statusRoot;

        void Awake()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                if (canvas.sortingOrder < 20)
                    canvas.sortingOrder = 20;
            }
        }
    }
}
