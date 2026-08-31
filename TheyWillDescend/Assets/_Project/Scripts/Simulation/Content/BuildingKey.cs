using UnityEngine;

namespace TheyWillDescend.Simulation.Content
{
    /// <summary>
    /// Design-time type id on a house prefab. Required. Baker copies it onto
    /// <see cref="TheyWillDescend.Simulation.City.BuildingType"/> — not a runtime flavour string.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingKey : MonoBehaviour
    {
        [SerializeField] string typeId;

        public string TypeId => ContentId.Normalize(typeId, name);
    }
}
