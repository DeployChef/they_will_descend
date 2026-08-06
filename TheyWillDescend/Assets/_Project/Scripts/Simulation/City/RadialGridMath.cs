using Unity.Mathematics;

namespace _Project.Scripts.Simulation.City
{
    /// <summary>
    /// Pure polar fine-grid math. Convention (fixed):
    /// angle = atan2(delta.x, delta.z), 0 along +Z, increases toward +X.
    /// Rings are shared; fine density is angular. Cell center uses (index + 0.5).
    /// </summary>
    public static class RadialGridMath
    {
        public static bool TryWorldToCell(
            float3 center,
            in RadialGridConfig config,
            float3 world,
            out RadialCell cell)
        {
            cell = default;

            if (!config.IsValid)
                return false;

            var delta = world - center;
            var radius = math.length(new float2(delta.x, delta.z));

            if (radius < config.InnerRadius)
                return false;

            var radialIndex = (int)math.floor((radius - config.InnerRadius) / config.RadialStep);
            if (radialIndex < 0 || radialIndex >= config.RingCount)
                return false;

            var angle = math.atan2(delta.x, delta.z); // [-π, π]
            var turns = angle / (2f * math.PI);
            if (turns < 0f)
                turns += 1f;

            var angularIndex = (int)math.floor(turns * config.AngularDivisions);
            if (angularIndex >= config.AngularDivisions)
                angularIndex = 0;

            cell = new RadialCell(angularIndex, radialIndex);
            return true;
        }

        public static float3 CellToWorld(float3 center, in RadialGridConfig config, RadialCell cell)
        {
            var theta = (cell.AngularIndex + 0.5f) * (2f * math.PI / config.AngularDivisions);
            var radius = config.InnerRadius + (cell.RadialIndex + 0.5f) * config.RadialStep;
            var offset = new float3(math.sin(theta), 0f, math.cos(theta)) * radius;
            return new float3(center.x, center.y, center.z) + offset;
        }

        public static float OuterRadius(in RadialGridConfig config) =>
            config.RingLineRadius(config.RingCount);

        /// <summary>Snap fine angular index down to quantum start.</summary>
        public static int FineAngularToQuantum(int fineAngularIndex, in RadialGridConfig config) =>
            fineAngularIndex / config.AngularQuantum;

        public static int QuantumToFineAngularStart(int quantumIndex, in RadialGridConfig config) =>
            quantumIndex * config.AngularQuantum;
    }
}
