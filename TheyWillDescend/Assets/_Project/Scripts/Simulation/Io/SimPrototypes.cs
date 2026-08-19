using Unity.Entities;

namespace TheyWillDescend.Simulation.Io
{
    /// <summary>
    /// Baked entity stamps for Instantiate. Not GameObject prefabs.
    /// Agent is a blank sim body; houses are Entities Graphics meshes.
    /// </summary>
    public struct SimPrototypes : IComponentData
    {
        public Entity Agent;
        public Entity House6x2;
        public Entity House2x2;
        public float House6x2MeshSize;
        public float House2x2MeshSize;
    }
}
