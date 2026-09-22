using TheyWillDescend.Simulation.Economy;
using TheyWillDescend.Simulation.Session;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Presentation.GameHud
{
    /// <summary>
    /// Single place that reads the energy reservoir out of the simulation world.
    /// Shared by <see cref="ResourceWidget"/> (text + bar) and <see cref="OrbEnergyGlow"/>
    /// so both show the very same normalized value.
    /// </summary>
    public static class EnergyReadout
    {
        const float CapEpsilon = 0.001f;

        /// <summary>Resource id of the reservoir, shared so no other file repeats the literal.</summary>
        public static readonly FixedString64Bytes EnergyId = new("energy");

        /// <summary>
        /// Reads energy from the world singleton. Returns false while the simulation
        /// world or the resource bag is not available yet.
        /// </summary>
        /// <param name="fallbackCap">Used when the resource info buffer carries no cap.</param>
        public static bool TryGet(float fallbackCap, out float amount, out float cap, out float normalized)
        {
            if (!SimWorld.TryGet(out var em, out var bag))
            {
                amount = 0f;
                cap = fallbackCap;
                normalized = 0f;
                return false;
            }

            return TryGet(em, bag, fallbackCap, out amount, out cap, out normalized);
        }

        /// <summary>
        /// Same as <see cref="TryGet(float,out float,out float,out float)"/> for callers
        /// that already resolved the world singleton.
        /// </summary>
        public static bool TryGet(
            EntityManager em,
            Entity bag,
            float fallbackCap,
            out float amount,
            out float cap,
            out float normalized)
        {
            amount = 0f;
            cap = fallbackCap;
            normalized = 0f;

            if (!em.HasBuffer<ResourceAmount>(bag))
                return false;

            amount = ResourceLedger.Get(em.GetBuffer<ResourceAmount>(bag), EnergyId);

            if (em.HasBuffer<ResourceInfo>(bag))
            {
                var infoCap = ResourceLedger.StockCap(em.GetBuffer<ResourceInfo>(bag), EnergyId);
                if (infoCap > CapEpsilon)
                    cap = infoCap;
            }

            normalized = cap > CapEpsilon ? Mathf.Clamp01(amount / cap) : 0f;
            return true;
        }
    }
}
