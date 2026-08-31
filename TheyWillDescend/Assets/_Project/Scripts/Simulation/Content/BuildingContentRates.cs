using System;
using UnityEngine;

namespace TheyWillDescend.Simulation.Content
{
    [Serializable]
    public struct BuildingCostEntry
    {
        public ResourceDefinition Resource;
        [Min(0f)] public float Amount;
    }

    /// <summary>
    /// Recipe rate on a building type. Unit is per game hour (HUD metric).
    /// Not place cost — that is <see cref="BuildingCostEntry"/>.
    /// </summary>
    [Serializable]
    public struct ResourceRate
    {
        public ResourceDefinition Resource;
        [Min(0f)] public float PerHour;
    }
}
