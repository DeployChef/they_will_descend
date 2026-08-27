using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Agents
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumeAssignWorkerCommandsSystem))]
    public partial struct ConsumeUnassignWorkerCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimBridge>();
        }

        public void OnUpdate(ref SystemState state) => Run(state.EntityManager);

        public static void Run(EntityManager em)
        {
            if (!SimBridgeAccess.TryGet(em, out var session))
                return;

            var commands = em.GetBuffer<UnassignWorkerCommand>(session);
            if (commands.Length == 0)
                return;

            var copy = commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            for (var i = 0; i < copy.Length; i++)
                Unassign(em, copy[i].BuildingId);
            copy.Dispose();
        }

        static void Unassign(EntityManager em, int buildingId)
        {
            if (buildingId <= 0)
                return;

            using var buildings = em.CreateEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadWrite<Workplace>());
            using var buildingEntities = buildings.ToEntityArray(Allocator.Temp);
            var buildingData = buildings.ToComponentDataArray<Building>(Allocator.Temp);
            var workerId = 0;
            for (var i = 0; i < buildingData.Length; i++)
            {
                if (buildingData[i].Id != buildingId)
                    continue;
                var workplace = em.GetComponentData<Workplace>(buildingEntities[i]);
                workerId = workplace.WorkerAgentId;
                workplace.WorkerAgentId = 0;
                workplace.Working = 0;
                em.SetComponentData(buildingEntities[i], workplace);
                break;
            }

            buildingData.Dispose();
            if (workerId == 0)
                return;

            using var agents = em.CreateEntityQuery(
                ComponentType.ReadOnly<AgentId>(),
                ComponentType.ReadWrite<AgentAssignment>());
            using var agentEntities = agents.ToEntityArray(Allocator.Temp);
            var ids = agents.ToComponentDataArray<AgentId>(Allocator.Temp);
            for (var i = 0; i < ids.Length; i++)
            {
                if (ids[i].Value != workerId)
                    continue;
                em.SetComponentData(agentEntities[i], new AgentAssignment());
                break;
            }

            ids.Dispose();
        }
    }
}
