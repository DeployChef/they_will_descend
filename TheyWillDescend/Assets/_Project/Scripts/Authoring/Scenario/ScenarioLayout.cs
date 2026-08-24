using System.Collections.Generic;
using TheyWillDescend.Simulation.City;
using Unity.Collections;

namespace TheyWillDescend.Authoring.Scenario
{
    public static class ScenarioLayout
    {
        public static bool TryFindFreeAnchor(
            in RadialGridConfig config,
            IReadOnlyList<ScenarioBuildingRecord> existing,
            IReadOnlyList<BuildingFootprint> existingFootprints,
            in BuildingFootprint footprint,
            out int cluster,
            out int radial)
        {
            cluster = 0;
            radial = 0;
            var occupied = BuildOccupied(config, existing, existingFootprints);
            try
            {
                for (var ring = 0; ring < config.RingCount; ring++)
                {
                    var n = config.GetClusterCount(ring);
                    var step = footprint.WidthClusters > 1 ? footprint.WidthClusters : 1;
                    for (var c = 0; c < n; c += step)
                    {
                        if (!Fits(config, occupied, c, ring, footprint))
                            continue;
                        cluster = c;
                        radial = ring;
                        return true;
                    }
                }
            }
            finally
            {
                occupied.Dispose();
            }

            return false;
        }

        public static bool CanPlace(
            in RadialGridConfig config,
            IReadOnlyList<ScenarioBuildingRecord> existing,
            IReadOnlyList<BuildingFootprint> existingFootprints,
            int cluster,
            int radial,
            in BuildingFootprint footprint)
        {
            var occupied = BuildOccupied(config, existing, existingFootprints);
            try
            {
                return Fits(config, occupied, cluster, radial, footprint);
            }
            finally
            {
                occupied.Dispose();
            }
        }

        public static bool HasOverlap(
            in RadialGridConfig config,
            IReadOnlyList<ScenarioBuildingRecord> buildings,
            IReadOnlyList<BuildingFootprint> footprints)
        {
            var occupied = new NativeHashSet<long>(64, Allocator.Temp);
            var cells = new NativeList<OccupiedCell>(64, Allocator.Temp);
            try
            {
                for (var i = 0; i < buildings.Count; i++)
                {
                    var record = buildings[i];
                    var footprint = footprints[i];
                    cells.Clear();
                    if (!RadialFootprintMath.TryExpandClusters(
                            config, record.Cluster, record.Radial, footprint, cells))
                        return true;
                    for (var c = 0; c < cells.Length; c++)
                    {
                        var key = CellKey(cells[c].Cluster, cells[c].Radial);
                        if (!occupied.Add(key))
                            return true;
                    }
                }
            }
            finally
            {
                cells.Dispose();
                occupied.Dispose();
            }

            return false;
        }

        static NativeHashSet<long> BuildOccupied(
            in RadialGridConfig config,
            IReadOnlyList<ScenarioBuildingRecord> existing,
            IReadOnlyList<BuildingFootprint> footprints)
        {
            var occupied = new NativeHashSet<long>(64, Allocator.Temp);
            var cells = new NativeList<OccupiedCell>(64, Allocator.Temp);
            for (var i = 0; i < existing.Count; i++)
            {
                var record = existing[i];
                var footprint = footprints[i];
                cells.Clear();
                if (!RadialFootprintMath.TryExpandClusters(
                        config, record.Cluster, record.Radial, footprint, cells))
                    continue;
                for (var c = 0; c < cells.Length; c++)
                    occupied.Add(CellKey(cells[c].Cluster, cells[c].Radial));
            }

            cells.Dispose();
            return occupied;
        }

        static bool Fits(
            in RadialGridConfig config,
            NativeHashSet<long> occupied,
            int cluster,
            int radial,
            in BuildingFootprint footprint)
        {
            var cells = new NativeList<OccupiedCell>(64, Allocator.Temp);
            try
            {
                if (!RadialFootprintMath.TryExpandClusters(
                        config, cluster, radial, footprint, cells))
                    return false;
                for (var i = 0; i < cells.Length; i++)
                {
                    if (occupied.Contains(CellKey(cells[i].Cluster, cells[i].Radial)))
                        return false;
                }

                return true;
            }
            finally
            {
                cells.Dispose();
            }
        }

        static long CellKey(int cluster, int radial) =>
            ((long)radial << 32) ^ (uint)cluster;
    }
}
