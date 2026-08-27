using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Simulation.City
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateBefore(typeof(ConsumePlaceBuildingCommandsSystem))]
    public partial struct ConsumePendingScenarioPlacesSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PendingScenarioPlace>();
            state.RequireForUpdate<SimBridge>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!Application.isPlaying)
                return;

            var session = SystemAPI.GetSingletonEntity<PendingScenarioPlace>();
            var pending = SystemAPI.GetBuffer<PendingScenarioPlace>(session);
            if (pending.Length == 0)
                return;

            var copy = pending.ToNativeArray(Allocator.Temp);
            pending.Clear();
            var commands = SystemAPI.GetBuffer<PlaceBuildingCommand>(session);
            for (var i = 0; i < copy.Length; i++)
            {
                var place = copy[i];
                commands.Add(new PlaceBuildingCommand
                {
                    TypeId = place.TypeId,
                    AnchorCluster = place.Cluster,
                    AnchorRadial = place.Radial,
                    InstantComplete = 1
                });
            }

            copy.Dispose();
        }
    }
}
