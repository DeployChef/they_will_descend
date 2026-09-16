using System.Collections.Generic;
using TheyWillDescend.Simulation.City;
using Unity.Collections;
using Unity.Mathematics;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Polar path: long ray / long arc, turn only to go around a house.
    /// Unobstructed diagonal is one L, never a staircase.
    /// </summary>
    public static class RoadPathfinder
    {
        const int MaxExpand = 8000;
        const int DirNone = 0;
        const int DirRadial = 1;
        const int DirArc = 2;
        const float TurnPenalty = 0.85f;

        public static int Build(
            float3 cityCenter,
            in RadialGridConfig config,
            NativeArray<OccupiedCell> occupied,
            NativeArray<RoadSegment> roads,
            float2 rawStart,
            float2 rawGoal,
            List<RoadNode> pathOut)
        {
            pathOut.Clear();
            var start = RoadMath.SnapNode(cityCenter, config, rawStart);
            var goal = RoadMath.SnapNode(cityCenter, config, rawGoal);
            if (RoadMath.TrySnapToRoads(cityCenter, config, roads, rawStart, out var snapped))
                start = snapped;
            goal = RoadMath.LockPaintGoal(config, start, goal);

            if (TryAStar(config, occupied, start, goal, pathOut))
                return pathOut.Count;

            pathOut.Clear();
            WalkLegs(config, occupied, start, goal, pathOut, out var valid);
            return valid;
        }

        static bool TryAStar(
            in RadialGridConfig config,
            NativeArray<OccupiedCell> occupied,
            in RoadNode start,
            in RoadNode goal,
            List<RoadNode> pathOut)
        {
            var fineCount = RoadMath.FineCount(config);
            var rings = config.RingCount + 1;
            var polarCount = rings * fineCount;
            var count = polarCount * 3;
            if (polarCount <= 0 || count > 36000)
                return false;

            var startPolar = PolarId(start, fineCount);
            var goalPolar = PolarId(goal, fineCount);
            if (startPolar < 0 || goalPolar < 0 || startPolar >= polarCount || goalPolar >= polarCount)
                return false;

            var came = new int[count];
            var gScore = new float[count];
            var closed = new byte[count];
            var open = new int[MaxExpand];
            var openF = new float[MaxExpand];
            var openN = 0;
            for (var i = 0; i < count; i++)
            {
                came[i] = -1;
                gScore[i] = 1e9f;
            }

            var startState = StateId(startPolar, DirNone);
            gScore[startState] = 0f;
            Push(open, openF, ref openN, startState, Heuristic(start, goal, fineCount));

            var expanded = 0;
            var foundState = -1;
            while (openN > 0 && expanded < MaxExpand)
            {
                var current = Pop(open, openF, ref openN);
                if (closed[current] != 0)
                    continue;
                closed[current] = 1;
                expanded++;
                if (PolarOf(current) == goalPolar)
                {
                    foundState = current;
                    break;
                }

                var node = FromPolar(PolarOf(current), fineCount);
                var dir = DirOf(current);
                TryStep(config, occupied, node, new RoadNode { Ring = node.Ring + 1, Fine = node.Fine },
                    DirRadial, dir, current, goal, fineCount, gScore, came, open, openF, ref openN, closed);
                TryStep(config, occupied, node, new RoadNode { Ring = node.Ring - 1, Fine = node.Fine },
                    DirRadial, dir, current, goal, fineCount, gScore, came, open, openF, ref openN, closed);
                TryStep(config, occupied, node, new RoadNode { Ring = node.Ring, Fine = RoadMath.WrapFine(node.Fine + 1, fineCount) },
                    DirArc, dir, current, goal, fineCount, gScore, came, open, openF, ref openN, closed);
                TryStep(config, occupied, node, new RoadNode { Ring = node.Ring, Fine = RoadMath.WrapFine(node.Fine - 1, fineCount) },
                    DirArc, dir, current, goal, fineCount, gScore, came, open, openF, ref openN, closed);
            }

            if (foundState < 0)
                return false;

            var stack = new List<RoadNode>(64);
            var cursor = foundState;
            stack.Add(FromPolar(PolarOf(cursor), fineCount));
            while (PolarOf(cursor) != startPolar)
            {
                cursor = came[cursor];
                if (cursor < 0)
                    return false;
                stack.Add(FromPolar(PolarOf(cursor), fineCount));
            }

            pathOut.Add(start);
            for (var i = stack.Count - 2; i >= 0; i--)
            {
                var node = stack[i];
                var last = pathOut[pathOut.Count - 1];
                if (last.Ring == node.Ring && last.Fine == node.Fine)
                    continue;
                pathOut.Add(node);
            }

            return pathOut.Count >= 2;
        }

        static void TryStep(
            in RadialGridConfig config,
            NativeArray<OccupiedCell> occupied,
            in RoadNode from,
            in RoadNode to,
            int moveDir,
            int prevDir,
            int currentId,
            in RoadNode goal,
            int fineCount,
            float[] gScore,
            int[] came,
            int[] open,
            float[] openF,
            ref int openN,
            byte[] closed)
        {
            if (to.Ring < 0 || to.Ring > config.RingCount)
                return;
            var nextPolar = PolarId(to, fineCount);
            if (nextPolar < 0)
                return;
            var next = StateId(nextPolar, moveDir);
            if (next < 0 || next >= closed.Length || closed[next] != 0)
                return;
            if (RoadMath.EdgeBlocked(config, occupied, from, to))
                return;

            var step = moveDir == DirRadial ? 1f : 1f / RoadMath.FinePerCluster;
            if (prevDir != DirNone && prevDir != moveDir)
                step += TurnPenalty;
            var tentative = gScore[currentId] + step;
            if (tentative >= gScore[next])
                return;
            came[next] = currentId;
            gScore[next] = tentative;
            Push(open, openF, ref openN, next, tentative + Heuristic(to, goal, fineCount));
        }

        static void WalkLegs(
            in RadialGridConfig config,
            NativeArray<OccupiedCell> occupied,
            in RoadNode start,
            in RoadNode goal,
            List<RoadNode> pathOut,
            out int validCount)
        {
            var fineCount = RoadMath.FineCount(config);
            pathOut.Add(start);
            var ring = start.Ring;
            var dir = goal.Ring >= start.Ring ? 1 : -1;
            while (ring != goal.Ring)
            {
                ring += dir;
                pathOut.Add(new RoadNode { Ring = ring, Fine = start.Fine });
            }

            var fine = start.Fine;
            var delta = RoadMath.FineDelta(start.Fine, goal.Fine, fineCount);
            var step = delta >= 0 ? 1 : -1;
            var steps = math.abs(delta);
            for (var i = 0; i < steps; i++)
            {
                fine = RoadMath.WrapFine(fine + step, fineCount);
                pathOut.Add(new RoadNode { Ring = goal.Ring, Fine = fine });
            }

            validCount = 1;
            for (var i = 1; i < pathOut.Count; i++)
            {
                if (RoadMath.EdgeBlocked(config, occupied, pathOut[i - 1], pathOut[i]))
                    break;
                validCount = i + 1;
            }
        }

        static int PolarId(in RoadNode node, int fineCount) =>
            node.Ring * fineCount + RoadMath.WrapFine(node.Fine, fineCount);

        static RoadNode FromPolar(int polar, int fineCount) =>
            new() { Ring = polar / fineCount, Fine = polar % fineCount };

        static int StateId(int polar, int dir) => polar * 3 + dir;

        static int PolarOf(int state) => state / 3;

        static int DirOf(int state) => state % 3;

        static float Heuristic(in RoadNode a, in RoadNode b, int fineCount)
        {
            var radial = math.abs(a.Ring - b.Ring);
            var arc = math.abs(RoadMath.FineDelta(a.Fine, b.Fine, fineCount)) / (float)RoadMath.FinePerCluster;
            return radial + arc;
        }

        static void Push(int[] heap, float[] score, ref int n, int id, float f)
        {
            if (n >= heap.Length)
                return;
            var i = n++;
            heap[i] = id;
            score[i] = f;
            while (i > 0)
            {
                var p = (i - 1) / 2;
                if (score[p] <= score[i])
                    break;
                Swap(heap, score, i, p);
                i = p;
            }
        }

        static int Pop(int[] heap, float[] score, ref int n)
        {
            var id = heap[0];
            n--;
            heap[0] = heap[n];
            score[0] = score[n];
            var i = 0;
            while (true)
            {
                var l = i * 2 + 1;
                var r = l + 1;
                var best = i;
                if (l < n && score[l] < score[best])
                    best = l;
                if (r < n && score[r] < score[best])
                    best = r;
                if (best == i)
                    break;
                Swap(heap, score, i, best);
                i = best;
            }

            return id;
        }

        static void Swap(int[] heap, float[] score, int a, int b)
        {
            var hid = heap[a];
            heap[a] = heap[b];
            heap[b] = hid;
            var hs = score[a];
            score[a] = score[b];
            score[b] = hs;
        }
    }
}
