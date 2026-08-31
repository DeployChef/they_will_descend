using System;
using TheyWillDescend.Simulation.Content;
using UnityEngine;

namespace TheyWillDescend.Authoring.City
{
    [DisallowMultipleComponent]
    public sealed class BuildingRecipeAuthoring : MonoBehaviour
    {
        [SerializeField] ResourceRate[] inputs = Array.Empty<ResourceRate>();
        [SerializeField] ResourceRate[] outputs = Array.Empty<ResourceRate>();

        public ResourceRate[] Inputs => inputs ?? Array.Empty<ResourceRate>();
        public ResourceRate[] Outputs => outputs ?? Array.Empty<ResourceRate>();
    }
}
