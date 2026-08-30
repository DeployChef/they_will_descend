using UnityEngine;

namespace TheyWillDescend.Simulation.Content
{
    /// <summary>
    /// Design-time resource type. Not the run ledger.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ResourceDefinition",
        menuName = "They Will Descend/Resource Definition")]
    public sealed class ResourceDefinition : ScriptableObject
    {
        [SerializeField] string resourceId = "resource";
        [SerializeField] string displayName = "Resource";
        [SerializeField] [Min(0f)] float energyValue = 1f;
        [SerializeField]
        [Min(0f)]
        [Tooltip("0 = use Sim Rules default stock cap.")]
        float stockCap;
        [SerializeField]
        [Tooltip("Slider feed on the pyramid. Off for energy and later crystals.")]
        bool canFeed = true;

        public string ResourceId => ContentId.Normalize(resourceId, name);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public float EnergyValue => energyValue < 0f ? 0f : energyValue;
        public float StockCap => stockCap < 0f ? 0f : stockCap;
        public bool CanFeed => canFeed;
    }
}
