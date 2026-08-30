using System;
using UnityEngine;

namespace TheyWillDescend.Simulation.Content
{
    [Serializable]
    public struct EraSpec
    {
        public string eraId;
        public string displayName;
        [Min(1)] public int durationDays;
        [Range(0f, 100f)] public float maxLoyalty;
        public ResourceDefinition[] tribute;
        [Min(0f)] public float tributeEnergyMultiplier;
        [Min(0f)] public float loyaltyPerEnergy;
    }

    /// <summary>
    /// Campaign eras. Runtime copy is session buffers, not this asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TimelineCatalog",
        menuName = "They Will Descend/Timeline Catalog")]
    public sealed class TimelineCatalogAsset : ScriptableObject
    {
        [SerializeField] EraSpec[] eras = Array.Empty<EraSpec>();

        public EraSpec[] Eras => eras ?? Array.Empty<EraSpec>();
    }
}
