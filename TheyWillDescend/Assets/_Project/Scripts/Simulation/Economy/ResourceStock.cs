using Unity.Entities;

namespace TheyWillDescend.Simulation.Economy
{
    /// <summary>
    /// Session ledger. UI pulls; production writes. Names are placeholders.
    /// </summary>
    public struct ResourceStock : IComponentData
    {
        public float Resource1;
        public float Resource2;
        public float Resource3;
        public float Resource4;
    }
}
