using System;
using TheyWillDescend.Simulation.Content;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    [DisallowMultipleComponent]
    public sealed class BuildingCostAuthoring : MonoBehaviour
    {
        [SerializeField] BuildingCostEntry[] costs = Array.Empty<BuildingCostEntry>();

        public BuildingCostEntry[] Costs => costs ?? Array.Empty<BuildingCostEntry>();
    }
}
