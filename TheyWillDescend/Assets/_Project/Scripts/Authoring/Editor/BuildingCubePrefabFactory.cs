using TheyWillDescend.Authoring.City;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Simulation.Content;
using UnityEditor;
using UnityEngine;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>
    /// One-shot: cube house stamps + catalog. Template <c>_BuildingStamp</c> stays out of the catalog.
    /// </summary>
    public static class BuildingCubePrefabFactory
    {
        const string Folder = "Assets/_Project/Content/Buildings/Prefabs";
        const string CatalogPath = "Assets/_Project/Content/Buildings/DefaultBuildingCatalog.asset";
        const string WoodPath = "Assets/_Project/Content/Economy/Wood.asset";
        const string FoodPath = "Assets/_Project/Content/Economy/Food.asset";
        const string MaterialPath = "Assets/_Project/Content/Buildings/Prefabs/BuildingCube.mat";

        [MenuItem("They Will Descend/Buildings/Create Cube Stamps")]
        public static void Create()
        {
            EnsureFolder(Folder);
            var wood = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(WoodPath);
            var food = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(FoodPath);
            if (wood == null || food == null)
            {
                Debug.LogError("Create Cube Stamps: Wood/Food resource assets missing.");
                return;
            }

            var mat = CreateMaterial();
            CreateStamp(
                "_BuildingStamp",
                "_stamp",
                width: 2,
                depth: 2,
                duration: 0f,
                slots: 0,
                inputs: null,
                outputs: null,
                costs: null,
                idle: new Color(0.55f, 0.55f, 0.58f),
                working: new Color(0.35f, 0.82f, 0.42f),
                construction: new Color(0.95f, 0.78f, 0.28f),
                mat,
                displayName: "Stamp");

            var kitchen = CreateStamp(
                "Kitchen",
                "kitchen",
                width: 2,
                depth: 2,
                duration: 8f,
                slots: 10,
                inputs: new[] { Rate(wood, 6f) },
                outputs: new[] { Rate(food, 12f) },
                costs: new[] { Cost(wood, 8f) },
                idle: new Color(0.82f, 0.62f, 0.38f),
                working: new Color(0.35f, 0.82f, 0.42f),
                construction: new Color(0.95f, 0.78f, 0.28f),
                mat,
                displayName: "Kitchen");

            var sawmill = CreateStamp(
                "Sawmill",
                "sawmill",
                width: 6,
                depth: 2,
                duration: 8f,
                slots: 10,
                inputs: null,
                outputs: new[] { Rate(wood, 12f) },
                costs: new[] { Cost(wood, 15f) },
                idle: new Color(0.45f, 0.52f, 0.62f),
                working: new Color(0.35f, 0.82f, 0.42f),
                construction: new Color(0.95f, 0.78f, 0.28f),
                mat,
                displayName: "Sawmill");

            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"Create Cube Stamps: missing catalog at {CatalogPath}.");
                return;
            }

            var cat = new SerializedObject(catalog);
            var list = cat.FindProperty("buildings");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = kitchen;
            list.GetArrayElementAtIndex(1).objectReferenceValue = sawmill;
            cat.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Cube stamps created. Kitchen + Sawmill are in DefaultBuildingCatalog. _BuildingStamp is the template (not in catalog).");
        }

        static (ResourceDefinition Resource, float Amount) Cost(ResourceDefinition resource, float amount) =>
            (resource, amount);

        static (ResourceDefinition Resource, float PerHour) Rate(ResourceDefinition resource, float perHour) =>
            (resource, perHour);

        static GameObject CreateStamp(
            string fileName,
            string typeId,
            int width,
            int depth,
            float duration,
            int slots,
            (ResourceDefinition Resource, float PerHour)[] inputs,
            (ResourceDefinition Resource, float PerHour)[] outputs,
            (ResourceDefinition Resource, float Amount)[] costs,
            Color idle,
            Color working,
            Color construction,
            Material mat,
            string displayName)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = fileName;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = mat;

            Write(go.AddComponent<BuildingKey>(), "typeId", typeId);
            var footprint = go.AddComponent<BuildingFootprintAuthoring>();
            Write(footprint, "widthClusters", width);
            Write(footprint, "depthRadialRings", depth);

            if (duration > 0.0001f)
                Write(go.AddComponent<BuildingConstructionAuthoring>(), "duration", duration);

            if (slots > 0)
                Write(go.AddComponent<BuildingWorkplaceAuthoring>(), "slots", slots);

            if ((inputs != null && inputs.Length > 0) || (outputs != null && outputs.Length > 0))
            {
                var recipe = go.AddComponent<BuildingRecipeAuthoring>();
                WriteRates(recipe, "inputs", inputs);
                WriteRates(recipe, "outputs", outputs);
            }

            if (costs != null && costs.Length > 0)
                WriteCosts(go.AddComponent<BuildingCostAuthoring>(), costs);

            var view = go.AddComponent<BuildingView>();
            Write(view, "displayName", displayName);
            Write(view, "idleColor", idle);
            Write(view, "workingColor", working);
            Write(view, "constructionColor", construction);

            var path = $"{Folder}/{fileName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void WriteRates(
            BuildingRecipeAuthoring recipe,
            string property,
            (ResourceDefinition Resource, float PerHour)[] rates)
        {
            var so = new SerializedObject(recipe);
            var list = so.FindProperty(property);
            if (rates == null || rates.Length == 0)
            {
                list.arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            list.arraySize = rates.Length;
            for (var i = 0; i < rates.Length; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("Resource").objectReferenceValue = rates[i].Resource;
                el.FindPropertyRelative("PerHour").floatValue = rates[i].PerHour;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WriteCosts(
            BuildingCostAuthoring cost,
            (ResourceDefinition Resource, float Amount)[] costs)
        {
            var so = new SerializedObject(cost);
            var list = so.FindProperty("costs");
            list.arraySize = costs.Length;
            for (var i = 0; i < costs.Length; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("Resource").objectReferenceValue = costs[i].Resource;
                el.FindPropertyRelative("Amount").floatValue = costs[i].Amount;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Write(Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Write(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Write(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Write(Object target, string property, Color value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material CreateMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
                return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "BuildingCube" };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.58f, 1f));
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = "Assets/_Project/Content/Buildings";
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder("Assets/_Project/Content", "Buildings");
            AssetDatabase.CreateFolder(parent, "Prefabs");
        }
    }
}
