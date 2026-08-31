using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Horizontal mesh size of the stamp, measured at bake. Place / ghost scale the
    /// house into the footprint cell. Not presentation flavour.
    /// </summary>
    public struct BuildingMeshSize : IComponentData
    {
        public float Horizontal;
    }
}
