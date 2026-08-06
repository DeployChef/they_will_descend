using _Project.Scripts.Simulation.Session;
using Unity.Entities;

namespace _Project.Scripts.Shell
{
    /// <summary>
    /// Bridge: copies Shell <see cref="SimGate"/>.Current into ECS singleton <see cref="SimControl"/>.
    /// Shell never writes EntityManager; simulation never references Shell types except this seam in Shell asm later.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SimControlSyncSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var gate = SimGate.Active;
            if (gate == null)
                return;

            if (!SystemAPI.TryGetSingletonRW<SimControl>(out var control))
                return;

            if (control.ValueRO.Mode == gate.Current)
                return;

            control.ValueRW.Mode = gate.Current;
        }
    }
}
