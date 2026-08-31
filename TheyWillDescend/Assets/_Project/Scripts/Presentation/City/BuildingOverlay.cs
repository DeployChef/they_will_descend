using UnityEngine;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Grid footprint pick zone. Mesh is assigned at runtime from the polar cells.
    /// </summary>
    public sealed class BuildingOverlay : MonoBehaviour
    {
        [SerializeField] BuildingIdTag idTag;
        [SerializeField] MeshFilter zoneFilter;
        [SerializeField] MeshRenderer zoneRenderer;
        [SerializeField] MeshCollider zoneCollider;

        public BuildingIdTag IdTag => idTag;

        public MeshFilter ZoneFilter => zoneFilter;

        public MeshRenderer ZoneRenderer => zoneRenderer;

        public MeshCollider ZoneCollider => zoneCollider;
    }
}
