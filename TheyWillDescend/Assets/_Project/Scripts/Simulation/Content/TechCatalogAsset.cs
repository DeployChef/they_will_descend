using System;
using TheyWillDescend.Simulation.Research;
using UnityEngine;

namespace TheyWillDescend.Simulation.Content
{
    [Serializable]
    public struct TechSpec
    {
        public string techId;
        public string displayName;
        [TextArea(2, 5)] public string summary;
        [Min(0.1f)] public float requiredHours;
        [Min(1)] public int requiredTier;
        public string requiresTechId;
        public int treeColumn;
        public int treeRow;
        public TechEffectKind effect;
        public string effectTarget;
        [Min(0)] public int effectTier;
        public BuildingCostEntry[] costs;
    }

    /// <summary>
    /// Design-time tech table. Runtime truth is one entity per row, spawned
    /// into the world at run start. Extra assets (DLC/plugins) append to the
    /// same list on GameSession.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TechCatalog",
        menuName = "They Will Descend/Tech Catalog")]
    public sealed class TechCatalogAsset : ScriptableObject
    {
        [SerializeField] TechSpec[] techs = Array.Empty<TechSpec>();

        public TechSpec[] Techs => techs ?? Array.Empty<TechSpec>();
    }
}
