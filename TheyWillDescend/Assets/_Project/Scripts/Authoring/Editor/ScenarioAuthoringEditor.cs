using System.Collections.Generic;
using TheyWillDescend.Authoring.City;
using TheyWillDescend.Authoring.Scenario;
using TheyWillDescend.Content;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Simulation.City;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace TheyWillDescend.Authoring.Editor
{
    [CustomEditor(typeof(ScenarioAuthoring))]
    public sealed class ScenarioAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var authoring = (ScenarioAuthoring)target;
            DrawDefaultInspector();

            if (authoring.Definition == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Scenario Definition. That asset is the document; this scene object is the editor.",
                    MessageType.Warning);
                return;
            }

            if (!authoring.TryGetPlacement(out var config, out _, out var catalog))
            {
                EditorGUILayout.HelpBox(
                    "Need CityGridAuthoring, BuildingCatalogAuthoring, and HeadquarterAuthoring in this SubScene.",
                    MessageType.Warning);
                return;
            }

            var footprints = CollectFootprints(catalog, authoring.Definition.Buildings);
            if (footprints != null && ScenarioLayout.HasOverlap(config, authoring.Definition.Buildings, footprints))
                EditorGUILayout.HelpBox("Some houses overlap on the grid. Bake will reject the extras.", MessageType.Error);

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            var workers = EditorGUILayout.IntField("Starting Workers", authoring.Definition.StartingWorkers);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(authoring.Definition, "Scenario Starting Workers");
                authoring.Definition.StartingWorkers = workers;
                EditorUtility.SetDirty(authoring.Definition);
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply config → scene"))
                    Apply(authoring);
                if (GUILayout.Button("Capture scene → config"))
                    Capture(authoring);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Add building", EditorStyles.boldLabel);
            var prefabs = catalog.Catalog != null ? catalog.Catalog.Prefabs : null;
            if (prefabs == null || prefabs.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Building catalog is empty. Add house prefabs to the catalog.",
                    MessageType.Warning);
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var i = 0; i < prefabs.Count; i++)
                    {
                        var prefab = prefabs[i];
                        if (prefab == null)
                            continue;
                        if (!BuildingStampRead.TryFootprint(prefab, out var footprint))
                            continue;
                        var typeId = BuildingStampRead.TypeId(prefab);
                        var label = $"{BuildingView.NameOf(prefab)} ({footprint.WidthClusters}×{footprint.DepthRadialRings})";
                        if (GUILayout.Button(label))
                            AddHouse(authoring, typeId);
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "Scene view tool «Place scenario buildings» (left toolbar while this object is selected): pick a type, click the grid.\n" +
                "Move tool: drag previews — they snap and Capture. Right-click a preview in Place tool to remove it.\n" +
                "Capture rewrites buildings only; starting stock and worker count stay on the Scenario Definition.",
                MessageType.Info);
        }

        public static void Apply(ScenarioAuthoring authoring)
        {
            if (authoring.Definition == null || !authoring.TryGetPlacement(out var config, out var center, out var catalog))
                return;

            Undo.RegisterFullObjectHierarchyUndo(authoring.gameObject, "Apply Scenario");
            EnsureBakingOnly(authoring.gameObject);

            var root = authoring.PreviewRoot;
            for (var i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

            var buildings = authoring.Definition.Buildings;
            for (var i = 0; i < buildings.Count; i++)
                SpawnPreview(authoring, catalog, config, center, buildings[i]);

            EditorUtility.SetDirty(authoring);
        }

        public static void Capture(ScenarioAuthoring authoring)
        {
            if (authoring.Definition == null)
                return;

            var records = new List<ScenarioBuildingRecord>();
            var previews = authoring.PreviewRoot.GetComponentsInChildren<ScenarioBuildingPreview>(true);
            for (var i = 0; i < previews.Length; i++)
                records.Add(previews[i].ToRecord());

            Undo.RecordObject(authoring.Definition, "Capture Scenario");
            authoring.Definition.ReplaceBuildings(records);
            EditorUtility.SetDirty(authoring.Definition);
            AssetDatabase.SaveAssetIfDirty(authoring.Definition);
        }

        static void AddHouse(ScenarioAuthoring authoring, string typeId)
        {
            if (authoring.Definition == null || !authoring.TryGetPlacement(out var config, out _, out var catalog))
                return;
            if (!TryFootprint(catalog, typeId, out var footprint))
            {
                Debug.LogError($"Unknown building type {typeId}.");
                return;
            }

            var existing = new List<ScenarioBuildingRecord>(authoring.Definition.Buildings);
            var existingFootprints = CollectFootprints(catalog, existing);
            if (!ScenarioLayout.TryFindFreeAnchor(
                    config, existing, existingFootprints, footprint, out var cluster, out var radial))
            {
                Debug.LogError("No free grid cell for that footprint.");
                return;
            }

            TryPlaceAt(authoring, typeId, cluster, radial);
        }

        public static bool TryPlaceAt(ScenarioAuthoring authoring, string typeId, int cluster, int radial)
        {
            if (authoring == null || authoring.Definition == null)
                return false;
            if (!authoring.TryGetPlacement(out var config, out var center, out var catalog))
                return false;
            if (!TryFootprint(catalog, typeId, out var footprint))
            {
                Debug.LogError($"Unknown building type {typeId}.");
                return false;
            }

            var existing = new List<ScenarioBuildingRecord>(authoring.Definition.Buildings);
            var existingFootprints = CollectFootprints(catalog, existing);
            if (!ScenarioLayout.CanPlace(config, existing, existingFootprints, cluster, radial, footprint))
                return false;

            var record = new ScenarioBuildingRecord
            {
                TypeId = typeId,
                Cluster = cluster,
                Radial = radial
            };
            existing.Add(record);
            Undo.RecordObject(authoring.Definition, "Add Scenario House");
            authoring.Definition.ReplaceBuildings(existing);
            EditorUtility.SetDirty(authoring.Definition);
            AssetDatabase.SaveAssetIfDirty(authoring.Definition);

            SpawnPreview(authoring, catalog, config, center, record);
            EditorUtility.SetDirty(authoring);
            return true;
        }

        static void SpawnPreview(
            ScenarioAuthoring authoring,
            BuildingCatalogAuthoring catalog,
            in RadialGridConfig config,
            float3 center,
            ScenarioBuildingRecord record)
        {
            if (!catalog.TryGet(record.TypeId, out var prefab) || prefab == null)
            {
                Debug.LogError($"Scenario preview: missing prefab for type {record.TypeId}.");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, authoring.PreviewRoot);
            if (go == null)
                go = Object.Instantiate(prefab, authoring.PreviewRoot);
            if (PrefabUtility.IsPartOfPrefabInstance(go))
                PrefabUtility.UnpackPrefabInstance(
                    go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = $"{BuildingView.NameOf(prefab)}_c{record.Cluster}_r{record.Radial}";
            Undo.RegisterCreatedObjectUndo(go, "Spawn Scenario Preview");
            EnsureBakingOnly(authoring.gameObject);
            StripRuntimeAuthoring(go);
            EnsureBakingOnlyHierarchy(go);

            var preview = go.GetComponent<ScenarioBuildingPreview>();
            if (preview == null)
                preview = Undo.AddComponent<ScenarioBuildingPreview>(go);
            preview.TypeId = record.TypeId;
            preview.Cluster = record.Cluster;
            preview.Radial = record.Radial;
            BuildingStampRead.TryFootprint(prefab, out var footprint);
            preview.ApplyPose(
                config, center, footprint, BuildingStampRead.MeshSize(prefab));
        }

        static void StripRuntimeAuthoring(GameObject root)
        {
            var stamps = root.GetComponentsInChildren<TheyWillDescend.Simulation.Content.BuildingStamp>(true);
            for (var i = 0; i < stamps.Length; i++)
                Undo.DestroyObjectImmediate(stamps[i]);
        }

        static void EnsureBakingOnly(GameObject go)
        {
            if (go.GetComponent<ScenarioBakingOnlyAuthoring>() == null)
                Undo.AddComponent<ScenarioBakingOnlyAuthoring>(go);
        }

        static void EnsureBakingOnlyHierarchy(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
                EnsureBakingOnly(transforms[i].gameObject);
        }

        internal static bool TryFootprint(BuildingCatalogAuthoring catalog, string typeId, out BuildingFootprint footprint)
        {
            if (catalog != null && catalog.TryGet(typeId, out var prefab))
                return BuildingStampRead.TryFootprint(prefab, out footprint);

            footprint = default;
            return false;
        }

        internal static float MeshSize(BuildingCatalogAuthoring catalog, string typeId)
        {
            if (catalog == null || !catalog.TryGet(typeId, out var prefab))
                return 1f;
            return BuildingStampRead.MeshSize(prefab);
        }

        static List<BuildingFootprint> CollectFootprints(
            BuildingCatalogAuthoring catalog,
            IReadOnlyList<ScenarioBuildingRecord> records)
        {
            var footprints = new List<BuildingFootprint>(records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                if (!TryFootprint(catalog, records[i].TypeId, out var footprint))
                    footprint = BuildingFootprint.House6x2;
                footprints.Add(footprint);
            }

            return footprints;
        }
    }
}
