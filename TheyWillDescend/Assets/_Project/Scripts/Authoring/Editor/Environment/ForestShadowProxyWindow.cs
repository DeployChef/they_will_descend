using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>Editor-built, static shadow proxy trial for an exterior forest sector.</summary>
    public sealed class ForestShadowProxyWindow : EditorWindow
    {
        const string CombinedName = "Forest_ShadowProxy_Combined_STATIC_TEST";
        const string CoarseName = "Forest_ShadowProxy_Coarse_BLENDER_TEST";
        const string MeshPath = "Assets/_Project/Art/Models/Environment/Forest_ShadowProxies/Forest_Sector01_Static.asset";
        const string MaterialPath = "Assets/_Project/Art/Materials/Trees/M_Forest_Combined_Shadow_Static.mat";
        const string ShaderPath = "Assets/_Project/Art/Shaders/Foliage/ForestCombinedShadow.shader";

        [SerializeField] Transform forestRoot;

        [MenuItem("They Will Descend/Environment/Forest Shadow Proxy Test...")]
        static void Open() => GetWindow<ForestShadowProxyWindow>("Forest Shadow Test");

        void OnEnable()
        {
            if (!forestRoot) forestRoot = DefaultRoot();
        }

        static Transform DefaultRoot()
        {
            var root = GameObject.Find("Forest_Composition_Test");
            return root ? root.transform : null;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Single-sector trial: rebuilding replaces this trial mesh. One static shadow replaces individual canopy proxies under the assigned root. Visible trees retain their wind; the default root excludes plateau trees.", MessageType.Info);
            forestRoot = (Transform)EditorGUILayout.ObjectField("Forest sector/root", forestRoot, typeof(Transform), true);
            var combined = FindCombined(forestRoot);
            var coarse = FindCoarse(forestRoot);
            using (new EditorGUI.DisabledScope(!forestRoot || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Build / Rebuild Combined Proxy"))
                {
                    try { Build(forestRoot); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
            }
            using (new EditorGUI.DisabledScope(!forestRoot || !combined))
            {
                if (GUILayout.Button("Use Individual Proxies (wind)")) SetMode(forestRoot, false);
                if (GUILayout.Button("Use Combined Proxy (static)")) SetMode(forestRoot, true);
            }
            using (new EditorGUI.DisabledScope(!forestRoot || !coarse))
            {
                if (GUILayout.Button("Use Coarse Blender Proxy (1,440 tris, static)")) SetComparisonMode(forestRoot, 2);
            }
            EditorGUILayout.HelpBox("Switching works in Play Mode for comparison. Wait for the frame rate to settle and keep the camera/light fixed. Building is Edit Mode only. The merged shadow does not yet follow wind.", MessageType.None);
            if (coarse && coarse.enabled) EditorGUILayout.LabelField("Current", "Coarse Blender, static shadow");
            else if (combined) EditorGUILayout.LabelField("Current", combined.enabled ? "Combined detailed, static shadow" : "Individual shadows");
        }

        public static string BuildCurrentScene() => Build(DefaultRoot());

        public static string Build(Transform root)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build in Edit Mode.");
            if (!root || !root.gameObject.scene.IsValid()) throw new ArgumentException("Assign an exterior forest scene root.");
            if (root.lossyScale != Vector3.one)
                throw new ArgumentException("Use an unscaled forest root for this trial.");
            var sources = IndividualProxies(root);
            if (sources.Length == 0) throw new ArgumentException("No Crown_ShadowProxy renderers found.");
            var layers = sources.Select(r => r.gameObject.layer).Distinct().ToArray();
            if (layers.Length != 1) throw new ArgumentException("This trial expects one shared layer for the source proxies.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (!shader || !shader.isSupported) throw new InvalidOperationException("The static URP shadow shader is unavailable.");

            // A sector-local origin avoids large vertex coordinates. No source
            // meshes, vertex wind metadata, tree transforms or materials change.
            var worldBounds = sources[0].bounds;
            foreach (var renderer in sources) worldBounds.Encapsulate(renderer.bounds);
            var origin = worldBounds.center;
            var worldToCombined = Matrix4x4.Translate(-origin);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();
            var cache = new Dictionary<Mesh, SourceGeometry>();
            foreach (var renderer in sources)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) throw new InvalidOperationException($"Missing source mesh: {renderer.name}");
                var source = filter.sharedMesh;
                if (!cache.TryGetValue(source, out var data)) cache.Add(source, data = ReadGeometry(source));
                var matrix = worldToCombined * filter.transform.localToWorldMatrix;
                if (matrix.determinant <= 0) throw new ArgumentException("Apply mirrored or zero scales before combining.");
                var normalMatrix = matrix.inverse.transpose;
                int offset = vertices.Count;
                for (int i = 0; i < data.Vertices.Length; i++)
                {
                    vertices.Add(matrix.MultiplyPoint3x4(data.Vertices[i]));
                    normals.Add(normalMatrix.MultiplyVector(data.Normals[i]).normalized);
                }
                foreach (var index in data.Indices) indices.Add(index + offset);
            }

            var mesh = new Mesh { name = "Forest_Sector01_Static_Shadow", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(indices, 0, true);
            mesh.RecalculateBounds();
            EnsureFolder("Assets/_Project/Art/Models/Environment/Forest_ShadowProxies");
            var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (savedMesh)
            {
                EditorUtility.CopySerialized(mesh, savedMesh);
                DestroyImmediate(mesh);
                mesh = savedMesh;
            }
            else AssetDatabase.CreateAsset(mesh, MeshPath);
            mesh.UploadMeshData(true);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                material = new Material(shader) { name = "M_Forest_Combined_Shadow_Static", enableInstancing = true };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            if (material.shader != shader) throw new InvalidOperationException("Unexpected material on the trial proxy.");

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build forest sector shadow proxy");
            var existing = FindCombined(root);
            if (existing) Undo.DestroyObjectImmediate(existing.gameObject);
            var go = new GameObject(CombinedName);
            go.transform.SetParent(root, false);
            go.transform.position = origin;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = layers[0];
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var combined = go.AddComponent<MeshRenderer>();
            combined.sharedMaterial = material;
            combined.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            combined.receiveShadows = false;
            combined.lightProbeUsage = LightProbeUsage.Off;
            combined.reflectionProbeUsage = ReflectionProbeUsage.Off;
            combined.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            Undo.RegisterCreatedObjectUndo(go, "Create combined forest shadow");
            SetMode(root, true);
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            SceneView.RepaintAll();
            string report = $"{sources.Length} proxy renderers → 1 renderer; {vertices.Count} vertices, {indices.Count / 3} triangles. Geometry unchanged, shadow wind disabled for this trial.";
            Debug.Log(report);
            return report;
        }

        public static void SetMode(Transform root, bool useCombined)
            => SetComparisonMode(root, useCombined ? 1 : 0);

        /// <param name="mode">0: individual wind; 1: detailed combined static; 2: coarse Blender static.</param>
        public static void SetComparisonMode(Transform root, int mode)
        {
            if (!root) throw new ArgumentException("Assign a forest root.");
            if (mode < 0 || mode > 2) throw new ArgumentOutOfRangeException(nameof(mode));
            var combined = FindCombined(root);
            var coarse = FindCoarse(root);
            if (mode == 1 && !combined) throw new InvalidOperationException("Build the combined proxy first.");
            if (mode == 2 && !coarse) throw new InvalidOperationException("Import the coarse Blender proxy first.");
            var individual = IndividualProxies(root);
            var renderers = individual.Concat(new[] { combined, coarse }).Where(r => r).ToArray();
            bool playing = EditorApplication.isPlaying;
            if (!playing) Undo.RecordObjects(renderers, "Switch forest shadow mode");
            foreach (var renderer in individual)
            {
                // Off would make an opaque proxy visible; keep ShadowsOnly.
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                renderer.enabled = mode == 0;
                if (!playing) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            if (combined) combined.enabled = mode == 1;
            if (coarse) coarse.enabled = mode == 2;
            if (!playing) EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            SceneView.RepaintAll();
        }

        static MeshRenderer[] IndividualProxies(Transform root) => root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.name == "Crown_ShadowProxy").ToArray();

        static MeshRenderer FindCombined(Transform root) => root ? root.GetComponentsInChildren<MeshRenderer>(true)
            .FirstOrDefault(r => r.name == CombinedName) : null;

        static MeshRenderer FindCoarse(Transform root) => root ? root.GetComponentsInChildren<MeshRenderer>(true)
            .FirstOrDefault(r => r.name == CoarseName) : null;

        sealed class SourceGeometry
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public int[] Indices;
        }

        static SourceGeometry ReadGeometry(Mesh mesh)
        {
            using (var read = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var data = read[0];
                var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
                var normals = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
                try
                {
                    data.GetVertices(vertices);
                    data.GetNormals(normals);
                    int[] buffer = data.indexFormat == IndexFormat.UInt16
                        ? data.GetIndexData<ushort>().ToArray().Select(i => (int)i).ToArray()
                        : data.GetIndexData<int>().ToArray();
                    var indices = new List<int>();
                    for (int s = 0; s < data.subMeshCount; s++)
                    {
                        var submesh = data.GetSubMesh(s);
                        if (submesh.topology != MeshTopology.Triangles) throw new ArgumentException("Triangle source meshes required.");
                        for (int i = submesh.indexStart; i < submesh.indexStart + submesh.indexCount; i++)
                            indices.Add(buffer[i] + submesh.baseVertex);
                    }
                    return new SourceGeometry { Vertices = vertices.ToArray(), Normals = normals.ToArray(), Indices = indices.ToArray() };
                }
                finally { vertices.Dispose(); normals.Dispose(); }
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            string parent = path.Substring(0, split);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }
    }
}
