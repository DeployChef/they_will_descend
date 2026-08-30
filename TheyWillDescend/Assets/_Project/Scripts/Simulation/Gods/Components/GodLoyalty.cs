using Unity.Entities;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.Gods
{
    /// <summary>
    /// Session faith axis 0–100. Wrath is low Value. Not a resource.
    /// </summary>
    public struct GodLoyalty : IComponentData
    {
        public float Value;
        public float EffectiveMax;

        public static GodLoyalty Full()
        {
            return new GodLoyalty
            {
                Value = 100f,
                EffectiveMax = 100f
            };
        }

        public void ClampToEffectiveMax()
        {
            Value = math.clamp(Value, 0f, EffectiveMax);
        }
    }
}
