using UnityEngine;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// View data on a house prefab. Not baked into ECS. HUD / ghost / tint read this.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingView : MonoBehaviour
    {
        [SerializeField] string displayName;
        [SerializeField] BuildingWorldUi worldUiPrefab;
        [SerializeField] Color idleColor = new(0.55f, 0.55f, 0.58f, 1f);
        [SerializeField] Color workingColor = new(0.35f, 0.82f, 0.42f, 1f);
        [SerializeField] Color constructionColor = new(0.95f, 0.78f, 0.28f, 1f);

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public BuildingWorldUi WorldUiPrefab => worldUiPrefab;
        public Color IdleColor => idleColor;
        public Color WorkingColor => workingColor;
        public Color ConstructionColor => constructionColor;

        public static string NameOf(GameObject prefab)
        {
            if (prefab == null)
                return string.Empty;
            var view = prefab.GetComponent<BuildingView>();
            return view != null ? view.DisplayName : prefab.name;
        }
    }
}
