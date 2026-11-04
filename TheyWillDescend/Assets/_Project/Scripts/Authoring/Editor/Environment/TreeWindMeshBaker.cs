using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>
    /// Editor-only preparation of reusable tree meshes. No source FBX, UV0 or
    /// vertex-colour edits. New meshes share root-local Y-up space and metadata.
    /// UV1/UV2 are reserved for wind, so these are not lightmapped static meshes.
    /// </summary>
    public static class TreeWindMeshBaker
    {
        public static Mesh Bake(Mesh source, Matrix4x4 rootFromMesh, bool foliage,
            float treeHeight, float crownStart, float flexibility, out int cardCount)
        {
            if (!source || treeHeight <= .001f)
                throw new ArgumentException("A mesh and positive whole-tree height are required.");
            if (rootFromMesh.determinant <= 0f)
                throw new ArgumentException("Apply mirrored/negative scale before preparing tree wind.");
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = source.name + "_Wind";
            try
            {
                var vertices = source.vertices;
                var normals = source.normals;
                var tangents = source.tangents;
                if (normals.Length != vertices.Length)
                    throw new ArgumentException("The source needs normals before wind preparation.");
                if (tangents.Length != vertices.Length)
                {
                    // Opaque shadow proxies have no UVs/tangents; provide a stable
                    // orthogonal basis without touching the source mesh.
                    tangents = new Vector4[vertices.Length];
                    for (var i = 0; i < tangents.Length; i++)
                    {
                        var t = Vector3.Cross(Mathf.Abs(normals[i].y) > .9f ? Vector3.right : Vector3.up, normals[i]).normalized;
                        tangents[i] = new Vector4(t.x, t.y, t.z, 1f);
                    }
                }
                var normalMatrix = rootFromMesh.inverse.transpose;
                for (var i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = rootFromMesh.MultiplyPoint3x4(vertices[i]);
                    normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                    var tangent = rootFromMesh.MultiplyVector(tangents[i]).normalized;
                    tangent = (tangent - normals[i] * Vector3.Dot(normals[i], tangent)).normalized;
                    tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
                }
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.tangents = tangents;
                var hinges = new List<Vector4>(new Vector4[vertices.Length]);
                cardCount = 0;
                var maxCardReach = 0f;
                if (foliage)
                {
                    var authored = new List<Vector4>();
                    source.GetUVs(1, authored);
                    if (authored.Count == vertices.Length && authored.TrueForAll(v => v.w > .5f))
                    {
                        var ids = new HashSet<Vector4>();
                        var colors = source.colors;
                        if (colors.Length != vertices.Length)
                            throw new ArgumentException("Authored foliage hinges still require COLOR.r and COLOR.g.");
                        for (var i = 0; i < authored.Count; i++)
                        {
                            var pivot = rootFromMesh.MultiplyPoint3x4(authored[i]);
                            hinges[i] = new Vector4(pivot.x, pivot.y, pivot.z, 1f);
                            ids.Add(new Vector4(pivot.x, pivot.y, pivot.z, colors[i].g));
                            maxCardReach = Mathf.Max(maxCardReach, Vector3.Distance(vertices[i], pivot));
                        }
                        cardCount = ids.Count;
                    }
                    else
                    {
                        var cardIds = new List<Vector4>();
                        source.GetUVs(3, cardIds);
                        maxCardReach = BakeCardHinges(vertices, source.colors, source.triangles,
                            cardIds, treeHeight, hinges, out cardCount);
                    }
                }
                mesh.SetUVs(1, hinges);
                var data = new List<Vector4>(vertices.Length);
                for (var i = 0; i < vertices.Length; i++)
                    data.Add(new Vector4(treeHeight, Mathf.Clamp(crownStart, .05f, .9f),
                        Mathf.Clamp(flexibility, 0f, 2f), 0f));
                mesh.SetUVs(2, data);
                mesh.RecalculateBounds();
                var radius = 0f;
                foreach (var v in vertices)
                    radius = Mathf.Max(radius, new Vector2(v.x, v.z).magnitude);
                // Conservative envelope for the shader's bounded rotations, including storms.
                var horizontal = .3f * treeHeight + .5f * radius + .75f * maxCardReach;
                var vertical = .07f * treeHeight + .6f * radius + .75f * maxCardReach;
                var bounds = mesh.bounds;
                bounds.Expand(new Vector3(horizontal * 2f, vertical * 2f, horizontal * 2f));
                mesh.bounds = bounds;
                return mesh;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }
        }

        static float BakeCardHinges(Vector3[] vertices, Color[] colors, int[] triangles,
            List<Vector4> cardIds, float height, List<Vector4> hinges, out int count)
        {
            if (colors.Length != vertices.Length)
                throw new ArgumentException("Foliage requires COLOR.r root-to-tip and COLOR.g card phase.");
            var parent = new int[vertices.Length];
            for (var i = 0; i < parent.Length; i++) parent[i] = i;
            if (cardIds.Count == vertices.Length && cardIds.TrueForAll(v => v.y > .5f))
            {
                // Optional authored card IDs: UV3.x = integer ID, UV3.y = 1.
                // Essential when a card contains disconnected cut-out islands.
                var cards = new Dictionary<int, int>();
                for (var i = 0; i < vertices.Length; i++)
                {
                    var id = Mathf.RoundToInt(cardIds[i].x);
                    if (cards.TryGetValue(id, out var other)) Union(parent, i, other);
                    else cards.Add(id, i);
                }
            }
            // Importers split corners at UV/normal seams. Rejoin coincident corners
            // of the same card phase for topology analysis, without welding geometry.
            var corners = new Dictionary<(int, int, int, int), int>();
            var quantization = 100000f / height;
            for (var i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                var key = (Mathf.RoundToInt(v.x * quantization), Mathf.RoundToInt(v.y * quantization),
                    Mathf.RoundToInt(v.z * quantization), Mathf.RoundToInt(colors[i].g * 65535f));
                if (corners.TryGetValue(key, out var other)) Union(parent, i, other);
                else corners.Add(key, i);
            }
            for (var i = 0; i < triangles.Length; i += 3)
            {
                Union(parent, triangles[i], triangles[i + 1]);
                Union(parent, triangles[i], triangles[i + 2]);
            }
            var groups = new Dictionary<int, List<int>>();
            for (var i = 0; i < vertices.Length; i++)
            {
                var root = Find(parent, i);
                if (!groups.TryGetValue(root, out var group)) groups.Add(root, group = new List<int>());
                group.Add(i);
            }
            var maxReach = 0f;
            foreach (var group in groups.Values)
            {
                var min = 1f;
                var max = 0f;
                foreach (var i in group) { min = Mathf.Min(min, colors[i].r); max = Mathf.Max(max, colors[i].r); }
                if (min > .05f || max - min < .2f)
                    throw new ArgumentException("A foliage component has no usable root-to-tip mask. " +
                        "Disconnected card islands need authored IDs (UV3.x = card ID, UV3.y = 1) " +
                        "or explicit hinges (UV1.xyz = pivot, UV1.w = 1).");
                var rootPoints = new HashSet<Vector3>();
                foreach (var i in group)
                    if (colors[i].r <= min + .005f) rootPoints.Add(vertices[i]);
                var pivot = Vector3.zero;
                foreach (var p in rootPoints) pivot += p;
                pivot /= rootPoints.Count;
                foreach (var i in group)
                {
                    hinges[i] = new Vector4(pivot.x, pivot.y, pivot.z, 1f);
                    maxReach = Mathf.Max(maxReach, Vector3.Distance(vertices[i], pivot));
                }
            }
            count = groups.Count;
            return maxReach;
        }

        static int Find(int[] parent, int i)
        {
            while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
            return i;
        }

        static void Union(int[] parent, int a, int b)
        {
            a = Find(parent, a); b = Find(parent, b);
            if (a != b) parent[b] = a;
        }

        /// <summary>Creates an art-only prefab; gameplay components/LODs are intentionally not copied.</summary>
        public static GameObject CreatePrefab(GameObject sourceRoot, MeshRenderer[] foliage,
            Material barkMaterial, Material leafMaterial, string outputFolder,
            float crownStart = .4f, float flexibility = 1f)
        {
            if (!sourceRoot || !barkMaterial || !leafMaterial || foliage == null || foliage.Length == 0)
                throw new ArgumentException("Choose the source tree, foliage renderers and both wind materials.");
            if (barkMaterial.shader != leafMaterial.shader ||
                !barkMaterial.HasProperty("_TrunkBend") || !leafMaterial.HasProperty("_LeafFlutter"))
                throw new ArgumentException("Both materials must use SG_TreeWind.");
            if (!outputFolder.StartsWith("Assets/_Project/", StringComparison.Ordinal) ||
                outputFolder.Contains("..") || Directory.Exists(outputFolder))
                throw new ArgumentException("Choose a NEW output folder inside Assets/_Project/.");
            if (sourceRoot.GetComponentInChildren<SkinnedMeshRenderer>(true))
                throw new ArgumentException("This preparation supports static MeshRenderers, not skinned trees.");
            if (sourceRoot.GetComponentInChildren<LODGroup>(true))
                throw new ArgumentException("Prepare one LOD at a time, then assemble the LODGroup. All LOD renderers must not be flattened together.");
            var renderers = sourceRoot.GetComponentsInChildren<MeshRenderer>(true);
            var leafSet = new HashSet<MeshRenderer>(foliage);
            foreach (var leaf in foliage)
                if (!leaf || !leaf.transform.IsChildOf(sourceRoot.transform) && leaf.gameObject != sourceRoot)
                    throw new ArgumentException("Every foliage renderer must belong to the source tree.");
            var toRoot = sourceRoot.transform.worldToLocalMatrix;
            var height = 0f;
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh || renderer.sharedMaterials.Length != 1)
                    throw new ArgumentException("Every renderer needs a MeshFilter and exactly one material.");
                var matrix = toRoot * renderer.transform.localToWorldMatrix;
                foreach (var p in filter.sharedMesh.vertices)
                    height = Mathf.Max(height, matrix.MultiplyPoint3x4(p).y);
            }
            if (height <= .001f) throw new ArgumentException("Tree root must be at ground level, with Y pointing up.");
            // Bake everything before touching the asset database, so invalid foliage leaves no partial output.
            var meshes = new List<Mesh>();
            var root = new GameObject(sourceRoot.name + "_Wind");
            try
            {
                foreach (var renderer in renderers)
                {
                    var matrix = toRoot * renderer.transform.localToWorldMatrix;
                    var mesh = Bake(renderer.GetComponent<MeshFilter>().sharedMesh, matrix,
                        leafSet.Contains(renderer), height, crownStart, flexibility, out var cards);
                    meshes.Add(mesh);
                    var child = new GameObject(renderer.name);
                    child.transform.SetParent(root.transform, false);
                    child.layer = renderer.gameObject.layer;
                    child.SetActive(renderer.gameObject.activeInHierarchy);
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var copy = child.AddComponent<MeshRenderer>();
                    EditorUtility.CopySerialized(renderer, copy);
                    copy.sharedMaterial = leafSet.Contains(renderer) ? leafMaterial : barkMaterial;
                    // Tree wind owns no alpha-tested foliage shadows; retain the existing proxy policy.
                    if (leafSet.Contains(renderer)) copy.shadowCastingMode = ShadowCastingMode.Off;
                    Debug.Log($"Prepared {renderer.name}: {mesh.vertexCount} vertices, {cards} foliage cards.");
                }
                Directory.CreateDirectory(outputFolder);
                AssetDatabase.Refresh();
                for (var i = 0; i < meshes.Count; i++)
                {
                    AssetDatabase.CreateAsset(meshes[i], outputFolder + "/Mesh_" + i + ".asset");
                    var serializedMesh = new SerializedObject(meshes[i]);
                    serializedMesh.FindProperty("m_IsReadable").boolValue = false;
                    serializedMesh.ApplyModifiedPropertiesWithoutUndo();
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, outputFolder + "/" + root.name + ".prefab");
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var mesh in meshes)
                    if (mesh && !AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
