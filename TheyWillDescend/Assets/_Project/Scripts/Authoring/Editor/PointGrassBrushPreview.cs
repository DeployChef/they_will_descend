using System;
using System.Reflection;
using MicahW.PointGrass;
using UnityEngine;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>
    /// Mesh preview bridge for Point Grass. Its Mesh importer reads mesh.colors
    /// twice per vertex, allocating a full copy each time. Read once here and
    /// retain the package's point distribution and rendering implementation.
    /// </summary>
    internal static class PointGrassBrushPreview
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly MethodInfo ClearBuffers = typeof(PointGrassRenderer).GetMethod("ClearBuffers", PrivateInstance);
        private static readonly MethodInfo CreateBuffers = typeof(PointGrassRenderer).GetMethod("CreateBuffers", PrivateInstance);
        private static readonly FieldInfo BoundingBox = typeof(PointGrassRenderer).GetField("boundingBox", PrivateInstance);

        internal static void Build(PointGrassRenderer renderer)
        {
            if (renderer.distSource != PointGrassCommon.DistributionSource.Mesh ||
                renderer.projectType != PointGrassCommon.ProjectionType.None)
            {
                renderer.BuildPoints();
                return;
            }
            if (ClearBuffers == null || CreateBuffers == null || BoundingBox == null)
                throw new NotSupportedException("Point Grass buffer API changed; update the editor preview bridge.");

            Mesh mesh = renderer.baseMesh;
            if (!mesh) return;
            Color[] colors = mesh.colors;
            var attributes = new Vector2[mesh.vertexCount];
            bool useColors = colors.Length == attributes.Length && (renderer.useDensity || renderer.useLength);
            for (int i = 0; i < attributes.Length; i++)
                attributes[i] = useColors ? new Vector2(colors[i].r, colors[i].g) : Vector2.one;

            var data = new PointGrassCommon.MeshData(mesh.vertices, mesh.normals, mesh.uv, mesh.triangles, attributes);
            int seed = renderer.randomiseSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : renderer.seed;
            Vector3? forcedNormal = renderer.overwriteNormalDirection
                ? renderer.transform.InverseTransformDirection(renderer.forcedNormal.normalized) : (Vector3?)null;
            var points = DistributePointsAlongMesh.DistributePoints(data, renderer.transform.localScale,
                renderer.pointCount, seed, renderer.multiplyByArea, forcedNormal, true,
                renderer.useDensity, renderer.useLength, renderer.densityCutoff, renderer.lengthMapping);

            ClearBuffers.Invoke(renderer, null);
            BoundingBox.SetValue(renderer, data.bounds);
            // Also clear buffer references when the final painted area is erased.
            CreateBuffers.Invoke(renderer, new object[] { points });
        }
    }
}
