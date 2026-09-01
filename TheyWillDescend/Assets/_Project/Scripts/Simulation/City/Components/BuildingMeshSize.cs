using Unity.Entities;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Horizontal mesh size of the Unity stamp, measured at catalog bake. Place /
    /// ghost scale the house into the footprint cell. Copied onto the instance.
    /// </summary>
    public struct BuildingMeshSize : IComponentData
    {
        public float Horizontal;
    }
}
