using System;
using System.Collections.Generic;
using TheyWillDescend.Simulation.Content;
using UnityEngine;

namespace TheyWillDescend.Content
{
    [Serializable]
    public struct ScenarioBuildingRecord
    {
        public string TypeId;
        public int Cluster;
        public int Radial;
    }

    [Serializable]
    public struct ScenarioResourceRecord
    {
        public ResourceDefinition Resource;
        [Min(0f)] public float Amount;
    }

    /// <summary>
    /// Design-time starting city. Not a player save slot.
    /// Address is (type, cluster, ring); pose and occupancy are derived.
    /// Starting stock is authored here — not on a SubScene float component.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ScenarioDefinition",
        menuName = "They Will Descend/Scenario Definition")]
    public sealed class ScenarioDefinition : ScriptableObject
    {
        [SerializeField] List<ScenarioBuildingRecord> buildings = new();
        [SerializeField] List<ScenarioResourceRecord> startingStock = new();
        [SerializeField, Min(0)] int startingWorkers = 8;

        public IReadOnlyList<ScenarioBuildingRecord> Buildings => buildings;
        public IReadOnlyList<ScenarioResourceRecord> StartingStock => startingStock;
        public int StartingWorkers
        {
            get => startingWorkers < 0 ? 0 : startingWorkers;
            set => startingWorkers = value < 0 ? 0 : value;
        }

        public void ReplaceBuildings(IReadOnlyList<ScenarioBuildingRecord> next)
        {
            buildings.Clear();
            if (next == null)
                return;
            for (var i = 0; i < next.Count; i++)
                buildings.Add(next[i]);
        }
    }
}
