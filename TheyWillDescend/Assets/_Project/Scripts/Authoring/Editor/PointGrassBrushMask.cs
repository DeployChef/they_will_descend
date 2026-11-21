using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheyWillDescend.Authoring.Editor
{
    internal static class PointGrassBrushMask
    {
        internal static bool Paint(Vector3[] positions, Color[] colors, Vector3 center,
            float radius, float hardness, float strength, float value, bool height)
        {
            bool changed = false;
            for (int i = 0; i < positions.Length; i++)
            {
                float dx = positions[i].x - center.x;
                float dz = positions[i].z - center.z;
                float distance = Mathf.Sqrt(dx * dx + dz * dz) / radius;
                if (distance >= 1f)
                    continue;

                float falloff = distance <= hardness ? 1f
                    : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hardness, 1f, distance));
                Color color = colors[i];
                float before = height ? color.g : color.r;
                float after = Mathf.MoveTowards(before, Mathf.Clamp01(value), strength * falloff);
                if (Mathf.Approximately(before, after))
                    continue;
                if (height) color.g = after;
                else color.r = after;
                colors[i] = color;
                changed = true;
            }
            return changed;
        }

        internal static Color[] ReadColors(Mesh mesh)
        {
            Color[] colors = mesh.colors;
            if (colors.Length == mesh.vertexCount)
                return colors;
            colors = new Color[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.white;
            return colors;
        }

        // Rasterize the plateau's top onto a regular grid. This also works when
        // the imported surface is not CPU-readable and does not have a collider.
        internal static Mesh CreatePlateauMask(MeshFilter surface, float spacing)
        {
            Mesh source = surface.sharedMesh;
            using var data = MeshUtility.AcquireReadOnlyMeshData(source);
            using var nativeVertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp);
            data[0].GetVertices(nativeVertices);
            Vector3[] sourceVertices = nativeVertices.ToArray();
            Matrix4x4 matrix = surface.transform.localToWorldMatrix;
            Bounds bounds = new Bounds(matrix.MultiplyPoint3x4(sourceVertices[0]), Vector3.zero);
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                sourceVertices[i] = matrix.MultiplyPoint3x4(sourceVertices[i]);
                bounds.Encapsulate(sourceVertices[i]);
            }

            int width = Mathf.CeilToInt(bounds.size.x / spacing) + 1;
            int depth = Mathf.CeilToInt(bounds.size.z / spacing) + 1;
            var heights = new float[width * depth];
            for (int i = 0; i < heights.Length; i++) heights[i] = float.NegativeInfinity;

            for (int sub = 0; sub < data[0].subMeshCount; sub++)
            {
                var descriptor = data[0].GetSubMesh(sub);
                if (descriptor.topology != MeshTopology.Triangles) continue;
                using var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp);
                data[0].GetIndices(indices, sub);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = sourceVertices[indices[i]];
                    Vector3 b = sourceVertices[indices[i + 1]];
                    Vector3 c = sourceVertices[indices[i + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                    if (Mathf.Abs(normal.y) < .85f || Mathf.Min(a.y, b.y, c.y) < bounds.max.y - .65f)
                        continue;
                    Rasterize(a, b, c, bounds.min, spacing, width, depth, heights);
                }
            }

            var remap = new int[heights.Length];
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var colors = new List<Color>();
            for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
            {
                int index = z * width + x;
                remap[index] = -1;
                if (float.IsNegativeInfinity(heights[index])) continue;
                float px = bounds.min.x + x * spacing;
                float pz = bounds.min.z + z * spacing;
                remap[index] = vertices.Count;
                vertices.Add(new Vector3(px, heights[index] + .015f, pz));
                float angle = Mathf.PerlinNoise(px * .17f + 25f, pz * .17f + 45f) * Mathf.PI * 4f;
                normals.Add(new Vector3(Mathf.Cos(angle) * .22f, 1f, Mathf.Sin(angle) * .22f).normalized);
                uv.Add(new Vector2(px, pz));
                colors.Add(new Color(0f, .8f, 0f, 1f));
            }

            var triangles = new List<int>();
            for (int z = 0; z < depth - 1; z++)
            for (int x = 0; x < width - 1; x++)
            {
                int k = z * width + x;
                AddTriangle(remap[k], remap[k + width], remap[k + 1], triangles);
                AddTriangle(remap[k + 1], remap[k + width], remap[k + width + 1], triangles);
            }
            if (triangles.Count == 0)
                throw new System.InvalidOperationException("На выбранной поверхности не найдена верхняя площадка.");

            var mesh = new Mesh { name = "PointGrass_Plateau_PaintMask", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTriangle(int a, int b, int c, List<int> triangles)
        {
            if (a < 0 || b < 0 || c < 0) return;
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }

        private static void Rasterize(Vector3 a, Vector3 b, Vector3 c, Vector3 origin,
            float spacing, int width, int depth, float[] heights)
        {
            float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(denominator) < .000001f) return;
            int minX = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x, c.x) - origin.x) / spacing), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, b.x, c.x) - origin.x) / spacing), 0, width - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z, b.z, c.z) - origin.z) / spacing), 0, depth - 1);
            int maxZ = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z, b.z, c.z) - origin.z) / spacing), 0, depth - 1);
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                float px = origin.x + x * spacing, pz = origin.z + z * spacing;
                float u = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / denominator;
                float v = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / denominator;
                float w = 1f - u - v;
                if (u < -.0001f || v < -.0001f || w < -.0001f) continue;
                int k = z * width + x;
                heights[k] = Mathf.Max(heights[k], a.y * u + b.y * v + c.y * w);
            }
        }
    }
}
