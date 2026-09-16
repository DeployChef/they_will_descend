using Unity.Collections;
using Unity.Mathematics;

namespace TheyWillDescend.Simulation.City
{
    /// <summary>
    /// Polar graph node: ring line + fine angle. Fine is denser than house
    /// clusters so an arc is not glued to a cell, but every step is still a
    /// ray from the city center or a ring arc.
    /// </summary>
    public struct RoadNode
    {
        public int Ring;
        public int Fine;
    }

    /// <summary>
    /// Roads live on the radial grid. Arc = along a ring line (angle freer than
    /// a cluster). Ray = constant angle through a section. Face interiors of
    /// occupied cells block; cell <b>edges</b> stay streets around houses.
    /// Interior partitions of one footprint are not streets.
    /// </summary>
    public static class RoadMath
    {
        public const int FinePerCluster = 4;
        public const float WidthScale = 0.72f;
        public const float DefaultSectionSeconds = 5f;

        public static int FineCount(in RadialGridConfig config) =>
            math.max(1, config.InnerBandClusterCount) * FinePerCluster;

        public static float SectionLength(in RadialGridConfig config) =>
            math.max(0.35f, config.TargetClusterWorldWidth);

        public static float RoadWidth(in RadialGridConfig config) =>
            SectionLength(config) * WidthScale;

        public static int WrapFine(int fine, int fineCount)
        {
            if (fineCount <= 0)
                return 0;
            fine %= fineCount;
            if (fine < 0)
                fine += fineCount;
            return fine;
        }

        public static int FineDelta(int from, int to, int fineCount)
        {
            var d = WrapFine(to - from, fineCount);
            if (d > fineCount / 2)
                d -= fineCount;
            return d;
        }

        public static float2 NodeWorld(float3 center, in RadialGridConfig config, in RoadNode node)
        {
            var n = FineCount(config);
            var turns = WrapFine(node.Fine, n) / (float)n;
            var p = RadialGridMath.PolarToWorld(center, turns, config.RingLineRadius(node.Ring));
            return new float2(p.x, p.z);
        }

        public static RoadNode SnapNode(float3 center, in RadialGridConfig config, float2 xz)
        {
            var n = FineCount(config);
            var delta = xz - new float2(center.x, center.z);
            var radius = math.length(delta);
            var turns = RadialGridMath.NormalizedTurns(delta.x, delta.y);
            var ring = (int)math.round((radius - config.InnerRadius) / math.max(0.001f, config.RadialStep));
            if (ring < 0)
                ring = 0;
            if (ring > config.RingCount)
                ring = config.RingCount;
            var fine = (int)math.round(turns * n);
            return new RoadNode { Ring = ring, Fine = WrapFine(fine, n) };
        }

        /// <summary>
        /// Radial drag keeps one Fine (a straight ray). Tiny angular noise
        /// from snapping must not put a kink in the ray.
        /// </summary>
        public static RoadNode LockPaintGoal(in RadialGridConfig config, in RoadNode start, in RoadNode goal)
        {
            var n = FineCount(config);
            var ringDelta = math.abs(goal.Ring - start.Ring);
            var fineAbs = math.abs(FineDelta(start.Fine, goal.Fine, n));
            if (ringDelta > 0 && fineAbs <= FinePerCluster / 2)
                return new RoadNode { Ring = goal.Ring, Fine = start.Fine };
            return goal;
        }

        public static bool TrySnapToRoads(
            float3 center,
            in RadialGridConfig config,
            NativeArray<RoadSegment> roads,
            float2 xz,
            out RoadNode node)
        {
            node = SnapNode(center, config, xz);
            if (roads.Length == 0)
                return false;

            var best = SectionLength(config) * 0.65f;
            best *= best;
            var found = false;
            for (var i = 0; i < roads.Length; i++)
            {
                var a = new RoadNode { Ring = roads[i].RingA, Fine = roads[i].FineA };
                var b = new RoadNode { Ring = roads[i].RingB, Fine = roads[i].FineB };
                Consider(center, config, xz, a, ref best, ref node, ref found);
                Consider(center, config, xz, b, ref best, ref node, ref found);
            }

            return found;
        }

        static void Consider(
            float3 center, in RadialGridConfig config, float2 xz, in RoadNode candidate,
            ref float bestSq, ref RoadNode node, ref bool found)
        {
            var w = NodeWorld(center, config, candidate);
            var d = math.lengthsq(w - xz);
            if (d >= bestSq)
                return;
            bestSq = d;
            node = candidate;
            found = true;
        }

        public static int BillableSections(NativeArray<RoadNode> path, int count, in RadialGridConfig config)
        {
            var corners = new NativeList<RoadNode>(8, Allocator.Temp);
            CollapseToSections(path, count, corners, config);
            var sections = math.max(0, corners.Length - 1);
            corners.Dispose();
            return sections;
        }

        public static void CollapseToSections(
            NativeArray<RoadNode> path,
            int count,
            NativeList<RoadNode> corners,
            in RadialGridConfig config)
        {
            corners.Clear();
            if (count < 1 || path.Length < count)
                return;

            corners.Add(path[0]);
            var i = 0;
            var n = FineCount(config);
            while (i < count - 1)
            {
                var a = path[i];
                var b = path[i + 1];
                if (a.Fine == b.Fine)
                {
                    corners.Add(b);
                    i++;
                    continue;
                }

                if (a.Ring != b.Ring)
                {
                    corners.Add(b);
                    i++;
                    continue;
                }

                var dir = FineDelta(a.Fine, b.Fine, n) >= 0 ? 1 : -1;
                var taken = 0;
                var ring = a.Ring;
                while (i < count - 1 && taken < FinePerCluster)
                {
                    var p = path[i];
                    var q = path[i + 1];
                    if (p.Ring != ring || q.Ring != ring)
                        break;
                    if (FineDelta(p.Fine, q.Fine, n) != dir)
                        break;
                    i++;
                    taken++;
                }

                corners.Add(path[i]);
            }
        }

        public static bool SameSpan(in RoadSegment segment, in RoadNode a, in RoadNode b)
        {
            var fwd = segment.RingA == a.Ring && segment.FineA == a.Fine
                      && segment.RingB == b.Ring && segment.FineB == b.Fine;
            var back = segment.RingA == b.Ring && segment.FineA == b.Fine
                       && segment.RingB == a.Ring && segment.FineB == a.Fine;
            return fwd || back;
        }

        public static bool Exists(NativeArray<RoadSegment> roads, in RoadNode a, in RoadNode b)
        {
            for (var i = 0; i < roads.Length; i++)
            {
                if (SameSpan(roads[i], a, b))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// One billed section under the cursor: arc bucket on the ring, or one
        /// radial step. Whichever midpoint is closer.
        /// </summary>
        public static bool TryPickPaintSection(
            float3 center,
            in RadialGridConfig config,
            float2 xz,
            out RoadNode a,
            out RoadNode b)
        {
            a = default;
            b = default;
            var node = SnapNode(center, config, xz);
            var n = FineCount(config);
            var arcStart = WrapFine(node.Fine, n) / FinePerCluster * FinePerCluster;
            var arcA = new RoadNode { Ring = node.Ring, Fine = arcStart };
            var arcB = new RoadNode { Ring = node.Ring, Fine = WrapFine(arcStart + FinePerCluster, n) };
            var arcDist = DistSqToSpan(center, config, xz, arcA, arcB);

            var radius = math.length(xz - new float2(center.x, center.z));
            var ringRadius = config.RingLineRadius(node.Ring);
            var radialBRing = radius >= ringRadius ? node.Ring + 1 : node.Ring - 1;
            var useRadial = radialBRing >= 0 && radialBRing <= config.RingCount;
            if (!useRadial)
            {
                a = arcA;
                b = arcB;
                return true;
            }

            var radA = node;
            var radB = new RoadNode { Ring = radialBRing, Fine = node.Fine };
            var radDist = DistSqToSpan(center, config, xz, radA, radB);
            if (arcDist <= radDist)
            {
                a = arcA;
                b = arcB;
            }
            else
            {
                a = radA;
                b = radB;
            }

            return true;
        }

        public static int NearestSegmentIndex(
            float3 center,
            in RadialGridConfig config,
            NativeArray<RoadSegment> roads,
            float2 xz)
        {
            var maxSq = SectionLength(config) * 0.55f;
            maxSq *= maxSq;
            var best = maxSq;
            var index = -1;
            for (var i = 0; i < roads.Length; i++)
            {
                var a = new RoadNode { Ring = roads[i].RingA, Fine = roads[i].FineA };
                var b = new RoadNode { Ring = roads[i].RingB, Fine = roads[i].FineB };
                var d = DistSqToSpan(center, config, xz, a, b);
                if (d >= best)
                    continue;
                best = d;
                index = i;
            }

            return index;
        }

        static float DistSqToSpan(
            float3 center, in RadialGridConfig config, float2 xz, in RoadNode a, in RoadNode b)
        {
            var wa = NodeWorld(center, config, a);
            var wb = NodeWorld(center, config, b);
            var ab = wb - wa;
            var lenSq = math.lengthsq(ab);
            if (lenSq < 0.0001f)
                return math.lengthsq(xz - wa);
            var t = math.clamp(math.dot(xz - wa, ab) / lenSq, 0f, 1f);
            var p = wa + ab * t;
            return math.lengthsq(xz - p);
        }

        public static bool EdgeBlocked(
            in RadialGridConfig config,
            NativeArray<OccupiedCell> occupied,
            in RoadNode a,
            in RoadNode b)
        {
            if (a.Ring == b.Ring)
            {
                var n = FineCount(config);
                var delta = FineDelta(a.Fine, b.Fine, n);
                var step = delta >= 0 ? 1 : -1;
                var steps = math.abs(delta);
                if (steps <= 0)
                    return true;
                var fine = a.Fine;
                for (var i = 0; i < steps; i++)
                {
                    var next = WrapFine(fine + step, n);
                    if (ArcStepBlocked(config, occupied, a.Ring, fine, next))
                        return true;
                    fine = next;
                }

                return false;
            }

            if (a.Fine != b.Fine)
                return true;

            var dir = b.Ring >= a.Ring ? 1 : -1;
            var ring = a.Ring;
            while (ring != b.Ring)
            {
                if (RadialStepBlocked(config, occupied, a.Fine, ring, ring + dir))
                    return true;
                ring += dir;
            }

            return false;
        }

        public static bool FootprintBlockedByRoads(
            in RadialGridConfig config,
            NativeArray<RoadSegment> roads,
            NativeArray<OccupiedCell> footprint)
        {
            if (roads.Length == 0 || footprint.Length == 0)
                return false;

            var stamped = new NativeArray<OccupiedCell>(footprint.Length, Allocator.Temp);
            for (var i = 0; i < footprint.Length; i++)
            {
                var cell = footprint[i];
                cell.BuildingId = 1;
                stamped[i] = cell;
            }

            var blocked = false;
            for (var i = 0; i < roads.Length; i++)
            {
                var a = new RoadNode { Ring = roads[i].RingA, Fine = roads[i].FineA };
                var b = new RoadNode { Ring = roads[i].RingB, Fine = roads[i].FineB };
                if (!EdgeBlocked(config, stamped, a, b))
                    continue;
                blocked = true;
                break;
            }

            stamped.Dispose();
            return blocked;
        }

        static bool RadialStepBlocked(
            in RadialGridConfig config,
            NativeArray<OccupiedCell> occupied,
            int fine,
            int ringA,
            int ringB)
        {
            var inner = math.min(ringA, ringB);
            var outer = math.max(ringA, ringB);
            if (inner < 0 || outer > config.RingCount || outer - inner != 1)
                return true;
            if (inner >= config.RingCount)
                return true;

            var n = config.GetClusterCount(inner);
            var fineCount = FineCount(config);
            var turns = WrapFine(fine, fineCount) / (float)fineCount;
            if (OnClusterRay(turns, n))
            {
                var ray = WrapCluster((int)math.round(turns * n), n);
                var left = WrapCluster(ray - 1, n);
                var right = WrapCluster(ray, n);
                var idL = FaceBuildingId(occupied, left, inner);
                var idR = FaceBuildingId(occupied, right, inner);
                return idL != 0 && idL == idR;
            }

            var cluster = RadialGridMath.TurnsToCluster(turns, n);
            return FaceOccupied(occupied, cluster, inner);
        }

        static bool ArcStepBlocked(
            in RadialGridConfig config,
            NativeArray<OccupiedCell> occupied,
            int ringLine,
            int fineA,
            int fineB)
        {
            if (ringLine < 0 || ringLine > config.RingCount)
                return true;
            var fineCount = FineCount(config);
            if (math.abs(FineDelta(fineA, fineB, fineCount)) != 1)
                return true;

            var turns = WrapFine(fineA, fineCount) / (float)fineCount;
            var idIn = 0;
            var idOut = 0;
            if (ringLine > 0)
            {
                var nIn = config.GetClusterCount(ringLine - 1);
                idIn = FaceBuildingId(occupied, RadialGridMath.TurnsToCluster(turns, nIn), ringLine - 1);
            }

            if (ringLine < config.RingCount)
            {
                var nOut = config.GetClusterCount(ringLine);
                idOut = FaceBuildingId(occupied, RadialGridMath.TurnsToCluster(turns, nOut), ringLine);
            }

            return idIn != 0 && idIn == idOut;
        }

        static bool OnClusterRay(float turns, int clusterCount)
        {
            if (clusterCount <= 0)
                return true;
            var x = turns * clusterCount;
            var nearest = math.round(x);
            return math.abs(x - nearest) < 0.12f;
        }

        static int WrapCluster(int cluster, int count)
        {
            if (count <= 0)
                return 0;
            cluster %= count;
            if (cluster < 0)
                cluster += count;
            return cluster;
        }

        static bool FaceOccupied(NativeArray<OccupiedCell> occupied, int cluster, int radial)
        {
            return FaceBuildingId(occupied, cluster, radial) != 0;
        }

        static int FaceBuildingId(NativeArray<OccupiedCell> occupied, int cluster, int radial)
        {
            for (var i = 0; i < occupied.Length; i++)
            {
                if (occupied[i].Cluster == cluster && occupied[i].Radial == radial)
                {
                    var id = occupied[i].BuildingId;
                    return id != 0 ? id : cluster * 4096 + radial + 1;
                }
            }

            return 0;
        }
    }
}
