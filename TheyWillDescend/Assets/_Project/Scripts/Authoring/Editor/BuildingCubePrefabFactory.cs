using TheyWillDescend.Authoring.City;
using TheyWillDescend.Content;
using TheyWillDescend.Presentation.City;
using TheyWillDescend.Simulation.Content;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>
        /// Cube stamps + shared overlay / widget prefabs + catalog.
    /// </summary>
    public static class BuildingCubePrefabFactory
    {
        const string Folder = "Assets/_Project/Content/Buildings/Prefabs";
        const string CatalogPath = "Assets/_Project/Content/Buildings/DefaultBuildingCatalog.asset";
        const string WoodPath = "Assets/_Project/Content/Economy/Wood.asset";
        const string FoodPath = "Assets/_Project/Content/Economy/Food.asset";
        const string MaterialPath = "Assets/_Project/Content/Buildings/Prefabs/BuildingCube.mat";
        const string WidgetPath = Folder + "/_BuildingWidget.prefab";
        const string OverlayPath = Folder + "/_BuildingOverlay.prefab";
        const string HqOverlayPath = Folder + "/_HqOverlay.prefab";

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

            var widget = CreateWidgetPrefab();
            var overlay = CreateOverlayPrefab();
            var hqOverlay = CreateHqOverlayPrefab();
            var mat = CreateMaterial();
            var kitchen = CreateStamp(
                "Kitchen",
                "kitchen",
                width: 2,
                depth: 2,
                duration: 8f,
                workplace: true,
                slots: 10,
                recipe: true,
                inputs: new[] { Rate(wood, 6f) },
                outputs: new[] { Rate(food, 12f) },
                costs: new[] { Cost(wood, 8f) },
                idle: new Color(0.82f, 0.62f, 0.38f),
                working: new Color(0.35f, 0.82f, 0.42f),
                construction: new Color(0.95f, 0.78f, 0.28f),
                mat,
                widget,
                displayName: "Kitchen");

            var sawmill = CreateStamp(
                "Sawmill",
                "sawmill",
                width: 6,
                depth: 2,
                duration: 8f,
                workplace: true,
                slots: 10,
                recipe: true,
                inputs: null,
                outputs: new[] { Rate(wood, 12f) },
                costs: new[] { Cost(wood, 15f) },
                idle: new Color(0.45f, 0.52f, 0.62f),
                working: new Color(0.35f, 0.82f, 0.42f),
                construction: new Color(0.95f, 0.78f, 0.28f),
                mat,
                widget,
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
            WireScene(overlay, hqOverlay, widget);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Cube stamps + overlay/widget prefabs created. Kitchen + Sawmill are in DefaultBuildingCatalog.");
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
            bool workplace,
            int slots,
            bool recipe,
            (ResourceDefinition Resource, float PerHour)[] inputs,
            (ResourceDefinition Resource, float PerHour)[] outputs,
            (ResourceDefinition Resource, float Amount)[] costs,
            Color idle,
            Color working,
            Color construction,
            Material mat,
            BuildingWidget widget,
            string displayName)
        {
            var go = new GameObject(fileName);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            var renderer = body.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = mat;

            var stamp = go.AddComponent<BuildingStamp>();
            Write(stamp, "typeId", typeId);
            Write(stamp, "widthClusters", width);
            Write(stamp, "depthRadialRings", depth);
            Write(stamp, "constructionDuration", duration);
            Write(stamp, "workplace", workplace);
            Write(stamp, "workplaceSlots", slots);
            Write(stamp, "recipe", recipe);
            WriteRates(stamp, "recipeInputs", inputs);
            WriteRates(stamp, "recipeOutputs", outputs);
            WriteCosts(stamp, costs);

            var view = go.AddComponent<BuildingView>();
            Write(view, "displayName", displayName);
            Write(view, "idleColor", idle);
            Write(view, "workingColor", working);
            Write(view, "constructionColor", construction);

            if (widget != null)
            {
                var nested = (GameObject)PrefabUtility.InstantiatePrefab(widget.gameObject, go.transform);
                nested.name = "Widget";
                nested.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            }

            var path = $"{Folder}/{fileName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static BuildingWidget CreateWidgetPrefab()
        {
            var root = new GameObject("Widget", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20;
            var group = root.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 48f);
            root.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            root.transform.localScale = Vector3.one * 0.02f;

            var bar = CreateUiPanel(root.transform, "Bar", new Vector2(180f, 22f));
            var bg = CreateUiImage(bar.transform, "Bg", new Color(0.08f, 0.1f, 0.12f, 0.85f));
            Stretch(bg.rectTransform);
            var fill = CreateUiImage(bar.transform, "Fill", new Color(0.25f, 0.85f, 0.45f, 0.95f));
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            bg.raycastTarget = false;

            var status = new GameObject("Status", typeof(RectTransform));
            status.transform.SetParent(root.transform, false);
            var statusRect = status.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 1f);
            statusRect.anchorMax = new Vector2(0.5f, 1f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 6f);
            statusRect.sizeDelta = new Vector2(180f, 24f);

            var ui = root.AddComponent<BuildingWidget>();
            var so = new SerializedObject(ui);
            so.FindProperty("constructionRoot").objectReferenceValue = bar;
            so.FindProperty("constructionFill").objectReferenceValue = fill;
            so.FindProperty("statusRoot").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, WidgetPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<BuildingWidget>();
        }

        static BuildingOverlay CreateOverlayPrefab()
        {
            var root = new GameObject("BuildingOverlay");
            var tag = root.AddComponent<BuildingIdTag>();
            var zone = new GameObject("FootprintZone");
            zone.transform.SetParent(root.transform, false);
            var filter = zone.AddComponent<MeshFilter>();
            var renderer = zone.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var collider = zone.AddComponent<MeshCollider>();
            var overlay = root.AddComponent<BuildingOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("idTag").objectReferenceValue = tag;
            so.FindProperty("zoneFilter").objectReferenceValue = filter;
            so.FindProperty("zoneRenderer").objectReferenceValue = renderer;
            so.FindProperty("zoneCollider").objectReferenceValue = collider;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, OverlayPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<BuildingOverlay>();
        }

        static HqOverlay CreateHqOverlayPrefab()
        {
            var root = new GameObject("HqOverlay");
            var tag = root.AddComponent<BuildingIdTag>();
            var ring = new GameObject("PlazaRing");
            ring.transform.SetParent(root.transform, false);
            var filter = ring.AddComponent<MeshFilter>();
            var renderer = ring.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var click = new GameObject("ClickProxy");
            click.transform.SetParent(root.transform, false);
            var capsule = click.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            var overlay = root.AddComponent<HqOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("idTag").objectReferenceValue = tag;
            so.FindProperty("plazaFilter").objectReferenceValue = filter;
            so.FindProperty("plazaRenderer").objectReferenceValue = renderer;
            so.FindProperty("clickProxy").objectReferenceValue = capsule;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HqOverlayPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<HqOverlay>();
        }

        static void WireScene(BuildingOverlay overlay, HqOverlay hq, BuildingWidget widget)
        {
            var boards = Object.FindObjectsByType<BuildingViewBoard>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < boards.Length; i++)
            {
                var so = new SerializedObject(boards[i]);
                so.FindProperty("overlayPrefab").objectReferenceValue = overlay;
                so.FindProperty("hqOverlayPrefab").objectReferenceValue = hq;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(boards[i]);
            }

            var placement = Object.FindObjectsByType<BuildPlacementController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < placement.Length; i++)
            {
                var so = new SerializedObject(placement[i]);
                so.FindProperty("overlayPrefab").objectReferenceValue = overlay;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(placement[i]);
            }
        }

        static GameObject CreateUiPanel(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return go;
        }

        static Image CreateUiImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void WriteRates(
            BuildingStamp stamp,
            string property,
            (ResourceDefinition Resource, float PerHour)[] rates)
        {
            var so = new SerializedObject(stamp);
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
            BuildingStamp stamp,
            (ResourceDefinition Resource, float Amount)[] costs)
        {
            var so = new SerializedObject(stamp);
            var list = so.FindProperty("costs");
            if (costs == null || costs.Length == 0)
            {
                list.arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

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

        static void Write(Object target, string property, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).boolValue = value;
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
