using TheyWillDescend.Presentation.GameHud;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>
    /// One-off authoring helper. Runs ONLY from the menu item — never on editor load,
    /// so it cannot recreate/overwrite scene HUD objects behind the user's back.
    /// </summary>
    public static class SetupResourceHud
    {
        static readonly Color PanelBg = new(0.07f, 0.08f, 0.1f, 0.78f);
        static readonly Color ChipBg = new(0.12f, 0.12f, 0.14f, 0.95f);
        static readonly Color Ink = new(0.92f, 0.9f, 0.84f, 1f);
        static readonly Color InkDim = new(0.62f, 0.58f, 0.52f, 1f);
        static readonly Color EnergyTrack = new(0.06f, 0.09f, 0.12f, 0.95f);
        static readonly Color EnergyFill = new(0.2f, 0.85f, 0.98f, 1f);

        const float ChipWidth = 150f;
        const float ChipHeight = 28f;
        const float PanelPadding = 8f;
        const float ChipSpacing = 4f;

        [MenuItem("They Will Descend/Setup Scene Resource HUD")]
        public static void BuildSceneResourceHud()
        {
            const string scenePath = "Assets/_Project/Scenes/Game.unity";
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            var canvasGo = GameObject.Find("GameHudCanvas");
            if (canvasGo == null)
            {
                Debug.LogError("GameHudCanvas not found in Game.unity");
                return;
            }

            // Look up among canvas children directly: GameObject.Find skips inactive objects
            // and would cause a second ResourceBar to be created next to a disabled one.
            GameObject resourceBarGo = null;
            for (var i = 0; i < canvasGo.transform.childCount; i++)
            {
                if (canvasGo.transform.GetChild(i).name != "ResourceBar")
                    continue;
                resourceBarGo = canvasGo.transform.GetChild(i).gameObject;
                break;
            }
            if (resourceBarGo == null)
            {
                resourceBarGo = new GameObject("ResourceBar", typeof(RectTransform));
                resourceBarGo.transform.SetParent(canvasGo.transform, false);
            }
            else if (resourceBarGo.transform.childCount > 0)
            {
                // Rebuild is destructive — never do it silently.
                var confirmed = EditorUtility.DisplayDialog(
                    "Rebuild Resource HUD",
                    "ResourceBar already exists. All its children will be deleted and rebuilt from scratch.",
                    "Rebuild", "Cancel");
                if (!confirmed)
                    return;
            }

            // Ensure ResourceBar stretches over canvas so child panels can anchor freely
            var barRt = resourceBarGo.GetComponent<RectTransform>();
            barRt.anchorMin = Vector2.zero;
            barRt.anchorMax = Vector2.one;
            barRt.offsetMin = Vector2.zero;
            barRt.offsetMax = Vector2.zero;
            barRt.pivot = new Vector2(0.5f, 0.5f);

            // Remove any legacy layout components from ResourceBar
            var hlg = resourceBarGo.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) Object.DestroyImmediate(hlg);
            var csf = resourceBarGo.GetComponent<ContentSizeFitter>();
            if (csf != null) Object.DestroyImmediate(csf);
            var barImg = resourceBarGo.GetComponent<Image>();
            if (barImg != null) Object.DestroyImmediate(barImg);

            // Clear old children
            for (var i = resourceBarGo.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(resourceBarGo.transform.GetChild(i).gameObject);
            }

            var widget = resourceBarGo.GetComponent<ResourceWidget>();
            if (widget == null)
                widget = resourceBarGo.AddComponent<ResourceWidget>();

            // 1. Build Left Panel: Materials
            var leftPanel = CreatePanel(resourceBarGo.transform, "MaterialPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f));
            var woodValue = CreateChip(leftPanel.transform, "Chip_Wood", "Дерево");
            var copalValue = CreateChip(leftPanel.transform, "Chip_Copal", "Копал");
            var obsidianValue = CreateChip(leftPanel.transform, "Chip_Obsidian", "Обсидиан");
            var shardValue = CreateChip(leftPanel.transform, "Chip_Shard", "Осколок");

            // 2. Build Right Panel: Provisions
            var rightPanel = CreatePanel(resourceBarGo.transform, "ProvisionPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f));
            var cornValue = CreateChip(rightPanel.transform, "Chip_Corn", "Кукуруза");
            var meatValue = CreateChip(rightPanel.transform, "Chip_RawMeat", "Сырое мясо");
            var livestockValue = CreateChip(rightPanel.transform, "Chip_Livestock", "Живой скот");
            var rationsValue = CreateChip(rightPanel.transform, "Chip_Rations", "Пайки");

            // 3. Build Energy Panel: Next to TimeBar
            var energyPanel = CreatePanel(resourceBarGo.transform, "EnergyPanel", new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-195f, -16f), false);
            energyPanel.sizeDelta = new Vector2(236f, 44f);
            var (energyVal, energyFill) = CreateEnergyContent(energyPanel.transform);

            // Bind serialized references on ResourceWidget
            widget.WoodValue = woodValue;
            widget.CopalValue = copalValue;
            widget.ObsidianValue = obsidianValue;
            widget.ShardValue = shardValue;

            widget.CornValue = cornValue;
            widget.MeatValue = meatValue;
            widget.LivestockValue = livestockValue;
            widget.RationsValue = rationsValue;

            widget.EnergyValue = energyVal;
            widget.EnergyFill = energyFill;

            EditorUtility.SetDirty(widget);
            EditorUtility.SetDirty(resourceBarGo);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("Scene Resource HUD successfully authored into Game.unity!");
        }

        static RectTransform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, bool addLayout = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;

            var bg = go.AddComponent<Image>();
            bg.color = PanelBg;
            bg.raycastTarget = false;

            if (addLayout)
            {
                var layout = go.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset((int)PanelPadding, (int)PanelPadding, (int)PanelPadding, (int)PanelPadding);
                layout.spacing = ChipSpacing;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                var fitter = go.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            return rt;
        }

        static TMP_Text CreateChip(Transform parent, string chipName, string titleText)
        {
            var chip = new GameObject(chipName, typeof(RectTransform), typeof(CanvasRenderer));
            chip.transform.SetParent(parent, false);

            var chipRt = chip.GetComponent<RectTransform>();
            var layout = chip.AddComponent<LayoutElement>();
            layout.preferredWidth = ChipWidth;
            layout.preferredHeight = ChipHeight;
            layout.minHeight = ChipHeight;

            var bg = chip.AddComponent<Image>();
            bg.color = ChipBg;
            bg.raycastTarget = false;

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(chip.transform, false);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = titleText;
            titleTmp.fontSize = 12f;
            titleTmp.color = InkDim;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleTmp.textWrappingMode = TextWrappingModes.NoWrap;
            titleTmp.raycastTarget = false;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(0.65f, 1f);
            titleRt.offsetMin = new Vector2(8f, 0f);
            titleRt.offsetMax = new Vector2(0f, 0f);

            // Value
            var valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(chip.transform, false);
            var valueTmp = valueGo.AddComponent<TextMeshProUGUI>();
            valueTmp.text = "0";
            valueTmp.fontSize = 13f;
            valueTmp.color = Ink;
            valueTmp.alignment = TextAlignmentOptions.MidlineRight;
            valueTmp.textWrappingMode = TextWrappingModes.NoWrap;
            valueTmp.raycastTarget = false;
            var valueRt = valueGo.GetComponent<RectTransform>();
            valueRt.anchorMin = new Vector2(0.65f, 0f);
            valueRt.anchorMax = new Vector2(1f, 1f);
            valueRt.offsetMin = new Vector2(0f, 0f);
            valueRt.offsetMax = new Vector2(-8f, 0f);

            return valueTmp;
        }

        static (TMP_Text, Image) CreateEnergyContent(Transform parent)
        {
            var container = new GameObject("EnergyContainer", typeof(RectTransform));
            container.transform.SetParent(parent, false);
            var containerRt = container.GetComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.offsetMin = new Vector2(PanelPadding, PanelPadding);
            containerRt.offsetMax = new Vector2(-PanelPadding, -PanelPadding);

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(container.transform, false);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Энергия";
            titleTmp.fontSize = 12f;
            titleTmp.color = InkDim;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleTmp.textWrappingMode = TextWrappingModes.NoWrap;
            titleTmp.raycastTarget = false;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(0.32f, 1f);
            titleRt.offsetMin = new Vector2(4f, 0f);
            titleRt.offsetMax = new Vector2(-2f, 0f);

            // Track
            var trackGo = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer));
            trackGo.transform.SetParent(container.transform, false);
            var trackRt = trackGo.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0.33f, 0.15f);
            trackRt.anchorMax = new Vector2(0.80f, 0.85f);
            trackRt.offsetMin = Vector2.zero;
            trackRt.offsetMax = Vector2.zero;
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.color = EnergyTrack;
            trackImg.raycastTarget = false;

            // Fill
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer));
            fillGo.transform.SetParent(trackGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = EnergyFill;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0.5f;
            fillImg.raycastTarget = false;

            // Value
            var valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(container.transform, false);
            var valueTmp = valueGo.AddComponent<TextMeshProUGUI>();
            valueTmp.text = "0";
            valueTmp.fontSize = 13f;
            valueTmp.color = Ink;
            valueTmp.alignment = TextAlignmentOptions.MidlineRight;
            valueTmp.textWrappingMode = TextWrappingModes.NoWrap;
            valueTmp.raycastTarget = false;
            var valueRt = valueGo.GetComponent<RectTransform>();
            valueRt.anchorMin = new Vector2(0.82f, 0f);
            valueRt.anchorMax = new Vector2(1f, 1f);
            valueRt.offsetMin = new Vector2(2f, 0f);
            valueRt.offsetMax = new Vector2(-4f, 0f);

            return (valueTmp, fillImg);
        }
    }
}
