using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheyWillDescend.Simulation.Agents
{
    /// <summary>
    /// Patrol (if tagged) writes a world-point Target. Motor walks there.
    /// Hunt will write Target the same way — not a Building.Id on the motor.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Io.CommandSystemGroup))]
    [UpdateAfter(typeof(AdvanceConstructionSystem))]
    public partial struct AdvanceAgentLocomotionSystem : ISystem
    {
        const float ArriveDistance = 0.6f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimControl>();
            state.RequireForUpdate<AgentLocomotion>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<SimControl>(out var sim))
                return;

            var dt = sim.DeltaTime;
            if (dt <= 0f)
                return;

            var houses = new NativeList<HousePoint>(8, Allocator.Temp);
            foreach (var (building, transform) in
                     SystemAPI.Query<RefRO<Building>, RefRO<LocalTransform>>().WithNone<Construction>())
            {
                houses.Add(new HousePoint
                {
                    Id = building.ValueRO.Id,
                    Position = transform.ValueRO.Position
                });
            }

            SortById(houses);

            foreach (var (locomotion, transform) in
                     SystemAPI.Query<RefRW<AgentLocomotion>, RefRO<LocalTransform>>()
                         .WithAll<AgentHousePatrol>())
            {
                ChooseHouseTarget(ref locomotion.ValueRW, transform.ValueRO.Position, houses);
            }

            houses.Dispose();

            foreach (var (locomotion, transform) in
                     SystemAPI.Query<RefRW<AgentLocomotion>, RefRW<LocalTransform>>())
            {
                Steer(ref locomotion.ValueRW, ref transform.ValueRW, dt);
            }
        }

        static void ChooseHouseTarget(
            ref AgentLocomotion motor,
            float3 position,
            in NativeList<HousePoint> houses)
        {
            if (houses.Length < 2)
            {
                motor.Moving = 0;
                return;
            }

            if (motor.Moving != 0 && math.distance(Flat(position), Flat(motor.Target)) > ArriveDistance)
                return;

            var next = NextHouseIndex(position, motor.Target, motor.Moving, houses);
            motor.Target = houses[next].Position;
            motor.Moving = 1;
        }

        static void Steer(ref AgentLocomotion motor, ref LocalTransform pose, float dt)
        {
            if (motor.Moving == 0)
                return;

            var pos = pose.Position;
            var delta = motor.Target - pos;
            delta.y = 0f;
            var distance = math.length(delta);
            if (distance <= 0.0001f)
                return;

            var speed = motor.Speed > 0.001f ? motor.Speed : 2f;
            var step = math.min(speed * dt, distance);
            var direction = delta / distance;
            pos += direction * step;
            pose = LocalTransform.FromPositionRotation(
                pos,
                quaternion.LookRotationSafe(direction, math.up()));
        }

        static int NextHouseIndex(
            float3 position,
            float3 currentTarget,
            byte moving,
            in NativeList<HousePoint> houses)
        {
            if (moving != 0)
            {
                var at = IndexNearestTo(currentTarget, houses);
                return (at + 1) % houses.Length;
            }

            return IndexNearestTo(position, houses);
        }

        static int IndexNearestTo(float3 point, in NativeList<HousePoint> houses)
        {
            var best = 0;
            var bestDist = float.MaxValue;
            var flat = Flat(point);
            for (var i = 0; i < houses.Length; i++)
            {
                var dist = math.distancesq(flat, Flat(houses[i].Position));
                if (dist >= bestDist)
                    continue;
                bestDist = dist;
                best = i;
            }

            return best;
        }

        static float3 Flat(float3 value) => new(value.x, 0f, value.z);

        static void SortById(NativeList<HousePoint> houses)
        {
            for (var i = 1; i < houses.Length; i++)
            {
                var current = houses[i];
                var j = i - 1;
                while (j >= 0 && houses[j].Id > current.Id)
                {
                    houses[j + 1] = houses[j];
                    j--;
                }

                houses[j + 1] = current;
            }
        }

        struct HousePoint
        {
            public int Id;
            public float3 Position;
        }
    }
}
