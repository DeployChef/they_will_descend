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

        public string ResourceId => ContentId.Normalize(resourceId, name);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }
}
