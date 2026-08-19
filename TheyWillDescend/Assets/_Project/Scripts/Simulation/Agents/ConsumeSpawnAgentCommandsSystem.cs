using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Io;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Agents
{
    [UpdateInGroup(typeof(CommandSystemGroup))]
    [UpdateAfter(typeof(ConsumeDespawnBuildingsSystem))]
    public partial struct ConsumeSpawnAgentCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimBridge>();
            state.RequireForUpdate<SimPrototypes>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Run(state.EntityManager);
        }

        public static void Run(EntityManager em)
        {
            if (!SimBridgeAccess.TryGet(em, out var session))
                return;

            var commands = em.GetBuffer<SpawnAgentCommand>(session);
            if (commands.Length == 0)
                return;

            var catalog = em.GetComponentData<SimPrototypes>(session);
            if (catalog.Agent == Entity.Null)
            {
                commands.Clear();
                return;
            }

            var bridge = em.GetComponentData<SimBridge>(session);
            var copy = commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            for (var i = 0; i < copy.Length; i++)
                Spawn(em, ref bridge, catalog.Agent, copy[i]);
            copy.Dispose();
            em.SetComponentData(session, bridge);
        }

        static void Spawn(
            EntityManager em,
            ref SimBridge bridge,
            Entity prototype,
            in SpawnAgentCommand command)
        {
            bridge.NextAgentId += 1;
            var walk = new CircleWalk
            {
                Center = command.Center,
                Radius = command.Radius,
                Speed = command.Speed,
                Direction = command.Direction,
                AngleRadians = command.AngleRadians
            };
            var transform = command.HasPose != 0
                ? LocalTransform.FromPositionRotation(
                    command.Position,
                    quaternion.LookRotationSafe(command.Facing, math.up()))
                : walk.ToLocalTransform();

            var entity = em.Instantiate(prototype);
            em.SetComponentData(entity, new AgentId { Value = bridge.NextAgentId });
            em.SetComponentData(entity, new AgentType { Kind = command.Kind });
            em.SetComponentData(entity, walk);
            SimEntityPose.Apply(em, entity, transform);
#if UNITY_EDITOR
            em.SetName(entity, $"Agent_{bridge.NextAgentId}");
#endif
        }
    }
}
