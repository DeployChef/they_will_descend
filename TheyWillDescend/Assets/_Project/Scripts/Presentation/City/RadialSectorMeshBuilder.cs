using System.Collections.Generic;
using _Project.Scripts.Simulation.City;
using Unity.Mathematics;
using UnityEngine;

namespace _Project.Scripts.Presentation.City
{
    /// <summary>
    /// Builds flat annular-sector meshes for cluster footprints (polar "pads").
    /// </summary>
    public static class RadialSectorMeshBuilder
    {
        public static Mesh BuildClusterZoneMesh(
            float3 cityCenter,
            in RadialGridConfig config,
            List<(int cluster, int radial)> clusters,
            float yOffset = 0.04f)
        {
            var verts = new List<Vector3>(clusters.Count * 8);
            var tris = new List<int>(clusters.Count * 12);

            for (var i = 0; i < clusters.Count; i++)
            {
                var (cluster, radial) = clusters[i];
                AppendClusterSector(
                    verts,
                    tris,
                    cityCenter,
                    config,
                    cluster,
                    radial,
                    yOffset,
                    arcSegments: 3);
            }

            var mesh = new Mesh { name = "FootprintZone" };
            if (verts.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void RebuildClusterZoneMesh(
            Mesh mesh,
            float3 cityCenter,
            in RadialGridConfig config,
            List<(int cluster, int radial)> clusters,
            float yOffset = 0.04f)
        {
            var verts = new List<Vector3>(clusters.Count * 8);
            var tris = new List<int>(clusters.Count * 12);

            for (var i = 0; i < clusters.Count; i++)
            {
                var (cluster, radial) = clusters[i];
                AppendClusterSector(
                    verts,
                    tris,
                    cityCenter,
                    config,
                    cluster,
                    radial,
                    yOffset,
                    arcSegments: 3);
            }

            mesh.Clear();
            if (verts.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        static void AppendClusterSector(
            List<Vector3> verts,
            List<int> tris,
            float3 cityCenter,
            in RadialGridConfig config,
            int cluster,
            int radial,
            float yOffset,
            int arcSegments)
        {
            var n = config.GetClusterCount(radial);
            if (n <= 0)
                return;

            var r0 = config.RingLineRadius(radial);
            var r1 = config.RingLineRadius(radial + 1);
            var a0 = cluster / (float)n * Mathf.PI * 2f;
            var a1 = (cluster + 1) / (float)n * Mathf.PI * 2f;
            var y = cityCenter.y + yOffset;
            var cx = cityCenter.x;
            var cz = cityCenter.z;

            var start = verts.Count;
            // Strip: pairs (inner, outer) along the arc.
            for (var s = 0; s <= arcSegments; s++)
            {
                var t = s / (float)arcSegments;
                var a = Mathf.Lerp(a0, a1, t);
                var dirX = Mathf.Sin(a);
                var dirZ = Mathf.Cos(a);
                verts.Add(new Vector3(cx + dirX * r0, y, cz + dirZ * r0));
                verts.Add(new Vector3(cx + dirX * r1, y, cz + dirZ * r1));
            }

            for (var s = 0; s < arcSegments; s++)
            {
                var i = start + s * 2;
                // two tris per quad (winding up)
                tris.Add(i);
                tris.Add(i + 1);
                tris.Add(i + 3);
                tris.Add(i);
                tris.Add(i + 3);
                tris.Add(i + 2);
            }
        }
    }
}
