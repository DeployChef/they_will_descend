using TheyWillDescend.Simulation.Content;
using TheyWillDescend.Simulation.Gods;
using TheyWillDescend.Simulation.Time;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.Session
{
    /// <summary>
    /// Points at <see cref="SimRulesAsset"/>. Must sit on the same GO as SimControlAuthoring.
    /// Bakes the day clock onto the session entity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimRulesAuthoring : MonoBehaviour
    {
        [SerializeField] SimRulesAsset rules;

        public SimRulesAsset Rules => rules;

        class Baker : Baker<SimRulesAuthoring>
        {
            public override void Bake(SimRulesAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var so = authoring.rules;
                if (so == null)
                {
                    Debug.LogError("SimRulesAuthoring: assign a Sim Rules asset.", authoring);
                    AddComponent(entity, new GameTime
                    {
                        DayDuration = 60f,
                        WorkShiftStartHour = 6f,
                        WorkShiftEndHour = 18f
                    });
                    AddComponent(entity, new PyramidConfig
                    {
                        EraChangeHour = 8f,
                        DefaultStockCap = 2000f,
                        LoyaltyDecayPerDay = 12f
                    });
                    return;
                }

                DependsOn(so);
                AddComponent(entity, so.CreateClock());
                AddComponent(entity, new PyramidConfig
                {
                    EraChangeHour = so.EraChangeHour,
                    DefaultStockCap = so.DefaultStockCap,
                    LoyaltyDecayPerDay = so.LoyaltyDecayPerDay
                });
            }
        }
    }
}
