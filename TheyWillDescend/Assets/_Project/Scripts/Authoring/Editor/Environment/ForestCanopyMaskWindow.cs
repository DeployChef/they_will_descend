using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>Authoring-only canopy shelter. No runtime scans, cameras or lights.</summary>
    public sealed class ForestCanopyMaskWindow : EditorWindow
    {
        const string MaskPath = "Assets/_Project/Art/Textures/Mountain/ForestCanopy/Forest_Canopy_Coverage.png";
        const int Resolution = 1024;
        const string MaskProperty = "_CanopyMask";
        const string RectProperty = "_CanopyMaskWorldRect";

        [SerializeField] Transform forestRoot;
        [SerializeField] Transform groundRoot;
        [SerializeField] float crownSpread = 1.15f;
        [SerializeField] float feather = 5f;
        [SerializeField] float density = 1.4f;

        [MenuItem("They Will Descend/Environment/Forest Canopy Shade...")]
        static void Open() => GetWindow<ForestCanopyMaskWindow>("Forest Canopy Shade");

        void OnEnable()
        {
            if (!forestRoot) forestRoot = FindRoot("Forest_Composition_Test");
            if (!groundRoot) groundRoot = FindRoot("OuterBank_Forest_Joined");
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Bake broad shelter under the exterior forest. Rebuild after moving, scaling or removing trees. This does not bake sunlight or change tree shadows.", MessageType.Info);
            forestRoot = (Transform)EditorGUILayout.ObjectField("Forest trees", forestRoot, typeof(Transform), true);
            groundRoot = (Transform)EditorGUILayout.ObjectField("Ground sectors", groundRoot, typeof(Transform), true);
            crownSpread = EditorGUILayout.Slider("Crown spread", crownSpread, .8f, 1.6f);
            feather = EditorGUILayout.Slider("Soft edge (world units)", feather, 1f, 12f);
            density = EditorGUILayout.Slider("Coverage density", density, .4f, 3f);
            EditorGUILayout.LabelField("Mask", "1024 × 1024, linear single channel");
            EditorGUILayout.HelpBox("Adjust Canopy Shade Strength on the ground material for brightness. Baking overwrites the generated PNG; it can be rebuilt from the trees.", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !forestRoot || !groundRoot))
            {
                if (GUILayout.Button("Bake / Update Mask"))
                {
                    try
                    {
                        int count = Bake(forestRoot, groundRoot, crownSpread, feather, density);
                        ShowNotification(new GUIContent($"Updated from {count} tree crowns"));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        EditorUtility.DisplayDialog("Forest canopy mask", exception.Message, "OK");
                    }
                }
            }
        }

        static Transform FindRoot(string rootName)
        {
            var root = GameObject.Find(rootName);
            return root ? root.transform : null;
        }

        public static int BakeCurrentScene() => Bake(FindRoot("Forest_Composition_Test"),
            FindRoot("OuterBank_Forest_Joined"), 1.15f, 5f, 1.4f);

        public static int Bake(Transform trees, Transform ground, float spread, float softness, float coverDensity)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Bake in Edit Mode.");
            if (!trees || !ground || !trees.gameObject.scene.IsValid() || trees.gameObject.scene != ground.gameObject.scene)
                throw new ArgumentException("Assign forest trees and ground sectors from the same open scene.");
            if (spread <= 0 || softness <= 0 || coverDensity <= 0)
                throw new ArgumentOutOfRangeException(nameof(spread), "Bake parameters must be positive.");

            var groundRenderers = ground.GetComponentsInChildren<MeshRenderer>(false);
            if (groundRenderers.Length == 0) throw new ArgumentException("No ground renderers found.");
            var materials = groundRenderers.SelectMany(r => r.sharedMaterials).Where(m => m && m.HasProperty(MaskProperty) && m.HasProperty(RectProperty)).Distinct().ToArray();
            if (materials.Length == 0) throw new ArgumentException("The ground shader must support Canopy Shade Mask and its world rectangle.");
            var bounds = groundRenderers[0].bounds;
            foreach (var renderer in groundRenderers) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.x < 1 || bounds.size.z < 1) throw new ArgumentException("Ground bounds are too small.");
            var worldRect = new Vector4(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z);
            var coverage = new float[Resolution * Resolution];
            int crownCount = 0;

            foreach (var filter in trees.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.name != "Leaves" || !filter.sharedMesh) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.enabled) continue;
                var mesh = filter.sharedMesh;
                if (mesh.subMeshCount == 0) continue;
                // Overall mesh bounds include a large wind/culling margin. The
                // submesh bounds retain the actual canopy and need no Read/Write.
                var localBounds = mesh.GetSubMesh(0).bounds;
                for (int submesh = 1; submesh < mesh.subMeshCount; submesh++)
                    localBounds.Encapsulate(mesh.GetSubMesh(submesh).bounds);
                var crown = ProjectBounds(localBounds, filter.transform.localToWorldMatrix);
                float rx = Mathf.Max(.5f, crown.extents.x * spread) + softness;
                float rz = Mathf.Max(.5f, crown.extents.z * spread) + softness;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((crown.center.x - rx - worldRect.x) / worldRect.z * Resolution), 0, Resolution - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((crown.center.x + rx - worldRect.x) / worldRect.z * Resolution), 0, Resolution - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((crown.center.z - rz - worldRect.y) / worldRect.w * Resolution), 0, Resolution - 1);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((crown.center.z + rz - worldRect.y) / worldRect.w * Resolution), 0, Resolution - 1);
                for (int z = z0; z <= z1; z++)
                {
                    float pz = worldRect.y + (z + .5f) / Resolution * worldRect.w;
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = worldRect.x + (x + .5f) / Resolution * worldRect.z;
                        float dx = (px - crown.center.x) / rx, dz = (pz - crown.center.z) / rz;
                        float radius = Mathf.Sqrt(dx * dx + dz * dz);
                        if (radius >= 1) continue;
                        float contribution = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.15f, 1f, radius));
                        coverage[z * Resolution + x] += contribution;
                    }
                }
                crownCount++;
            }
            // Zero trees is valid: rebaking clears shelter left by removed trees.
            var pixels = new Color32[coverage.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte value = (byte)Mathf.RoundToInt((1f - Mathf.Exp(-coverDensity * coverage[i])) * 255f);
                pixels[i] = new Color32(value, value, value, 255);
            }
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false);
                Directory.CreateDirectory(Path.GetDirectoryName(MaskPath));
                File.WriteAllBytes(MaskPath, texture.EncodeToPNG());
            }
            finally { DestroyImmediate(texture); }

            AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(MaskPath);
            importer.textureType = TextureImporterType.SingleChannel;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            settings.alphaSource = TextureImporterAlphaSource.None;
            importer.SetTextureSettings(settings);
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Resolution;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.SaveAndReimport();
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);
            foreach (var material in materials)
            {
                Undo.RecordObject(material, "Assign forest canopy mask");
                material.SetTexture(MaskProperty, mask);
                material.SetVector(RectProperty, worldRect);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }
            SceneView.RepaintAll();
            Debug.Log($"Forest canopy mask: {crownCount} crowns, {mask.width}×{mask.height}, {mask.format}. Adjust Canopy Shade Strength on the ground material.");
            return crownCount;
        }

        static Bounds ProjectBounds(Bounds source, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(source.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                var offset = new Vector3((corner & 1) == 0 ? -source.extents.x : source.extents.x,
                    (corner & 2) == 0 ? -source.extents.y : source.extents.y,
                    (corner & 4) == 0 ? -source.extents.z : source.extents.z);
                result.Encapsulate(matrix.MultiplyPoint3x4(source.center + offset));
            }
            return result;
        }
    }
}
