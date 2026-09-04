using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using TheyWillDescend.Simulation.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Gods
{
    /// <summary>
    /// HQ furnace: 24/7 consume slider feeds, write energy, tribute adds loyalty.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TheyWillDescend.Simulation.Economy.BuildingProductionSystem))]
    [UpdateAfter(typeof(AdvanceTimelineSystem))]
    public partial struct PyramidBurnSystem : ISystem
    {
        EntityQuery _session;
        FixedString64Bytes _energyId;

        public void OnCreate(ref SystemState state)
        {
            _energyId = new FixedString64Bytes("energy");
            _session = state.GetEntityQuery(
                ComponentType.ReadOnly<SimControl>(),
                ComponentType.ReadOnly<GameTime>(),
                ComponentType.ReadOnly<Timeline>(),
                ComponentType.ReadWrite<GodLoyalty>(),
                ComponentType.ReadWrite<ResourceAmount>(),
                ComponentType.ReadOnly<ResourceInfo>(),
                ComponentType.ReadOnly<EraLine>(),
                ComponentType.ReadOnly<EraTributeLine>(),
                ComponentType.ReadOnly<PyramidConfig>());
            state.RequireForUpdate(_session);
            state.RequireForUpdate<Headquarters>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var control = _session.GetSingleton<SimControl>();
            if (!control.IsRunning)
                return;
            var dt = control.DeltaTime;
            if (dt <= 0f)
                return;

            var time = _session.GetSingleton<GameTime>();
            var dayDuration = time.DayDuration;
            var timeline = _session.GetSingleton<Timeline>();
            var loyalty = _session.GetSingleton<GodLoyalty>();
            var stock = _session.GetSingletonBuffer<ResourceAmount>();
            var info = _session.GetSingletonBuffer<ResourceInfo>(true);
            var eras = _session.GetSingletonBuffer<EraLine>(true);
            var tribute = _session.GetSingletonBuffer<EraTributeLine>(true);
            var config = _session.GetSingleton<PyramidConfig>();
            var eraIndex = timeline.EraIndex;
            var loyaltyPerEnergy = 0f;
            if (eraIndex >= 0 && eraIndex < eras.Length)
                loyaltyPerEnergy = eras[eraIndex].LoyaltyPerEnergy;

            if (config.LoyaltyDecayPerDay > 0.0001f && dayDuration > 0.0001f)
            {
                loyalty.Value -= config.LoyaltyDecayPerDay * (dt / dayDuration);
                loyalty.ClampToEffectiveMax();
            }

            foreach (var feed in
                     SystemAPI.Query<DynamicBuffer<PyramidFeedLine>>()
                         .WithAll<Headquarters>())

            {
                for (var i = 0; i < feed.Length; i++)
                {
                    var line = feed[i];
                    if (line.PerHour <= 0.0001f || line.ResourceId == _energyId)
                        continue;
                    if (!ResourceLedger.CanFeed(info, line.ResourceId))
                        continue;

                    var want = BuildingRecipes.FrameAmount(line.PerHour, dt, dayDuration);
                    if (want <= 0f)
                        continue;
                    var have = ResourceLedger.Get(stock, line.ResourceId);
                    var consumed = math.min(want, have);
                    if (consumed <= 0.0001f)
                        continue;

                    ResourceLedger.Add(stock, line.ResourceId, -consumed);
                    var unit = PyramidFeed.UnitEnergy(info, tribute, eras, eraIndex, line.ResourceId);
                    if (unit > 0.0001f)
                        ResourceLedger.AddClamped(stock, info, _energyId, consumed * unit);

                    if (loyaltyPerEnergy > 0.0001f
                        && PyramidFeed.IsTribute(tribute, eraIndex, line.ResourceId))
                    {
                        loyalty.Value += consumed * unit * loyaltyPerEnergy;
                        loyalty.ClampToEffectiveMax();
                    }
                }
            }

            _session.SetSingleton(loyalty);
        }
    }
}
