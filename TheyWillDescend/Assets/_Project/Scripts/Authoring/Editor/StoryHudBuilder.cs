#if UNITY_EDITOR
using System.IO;
using TheyWillDescend.Content;
using TheyWillDescend.Presentation.GameHud;
using TheyWillDescend.Simulation.Stories;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheyWillDescend.Authoring.Editor
{
    /// <summary>
    /// Writes the first story pack and the HUD prefabs. Play mode only instantiates those prefabs.
    /// </summary>
    static class StoryHudBuilder
    {
        const string Folder = "Assets/_Project/Content/Stories";
        const string PrefabFolder = "Assets/_Project/Prefabs/Hud/Story";
        const string PackPath = Folder + "/FirstStoryPack.asset";
        const string GameScene = "Assets/_Project/Scenes/Game.unity";

        [InitializeOnLoadMethod]
        static void Schedule()
        {
            EditorSceneManager.sceneOpened += (scene, _) =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                if (scene.path != GameScene)
                    return;
                EnsurePack();
                EnsureHud(scene);
            };
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                EnsurePack();
                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.path == GameScene)
                        EnsureHud(scene);
                }
            };
        }

        [MenuItem("They Will Descend/Stories/Build First Pack")]
        public static void Build()
        {
            EnsurePack();
            var scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
            EnsureHud(scene);
        }

        static void EnsurePack()
        {
            if (AssetDatabase.LoadAssetAtPath<StoryPackAsset>(PackPath) != null)
                return;
            Directory.CreateDirectory(Folder);
            var intro = Dialog("PyramidIntro", "pyramid_intro", "Пирамида",
                "Пирамида кормит богов. Её надо кормить данью, иначе вера остынет.",
                StoryDelivery.MapPin, StoryIconKind.Exclamation, StoryAnchorRule.Headquarters);
            var offer = Dialog("SawmillsOffer", "sawmills_offer", "Работа",
                "Люди хотят работать. Нужно поставить лесопилки.",
                StoryDelivery.MapPin, StoryIconKind.Question, StoryAnchorRule.Agent);
            var thanks = Dialog("SawmillsThanks", "sawmills_thanks", "Лесопилки",
                "Лесопилки стоят. Люди вышли на работу.",
                StoryDelivery.Immediate, StoryIconKind.Exclamation, StoryAnchorRule.Headquarters);
            var sick = Dialog("SickWorker", "sick_worker", "Болезнь",
                "Этот человек заболел.",
                StoryDelivery.MapPin, StoryIconKind.Exclamation, StoryAnchorRule.Agent);
            var quest = Quest();
            Wire(intro, Choice("ok", "Я понял", true, StartDialog(offer)));
            Wire(offer,
                Choice("agree", "Поставить", false, StartQuest(quest)),
                Choice("refuse", "Не сейчас", true, Loyalty(-10f)));
            Wire(thanks, Choice("ok", "Я понял", true));
            Wire(sick,
                Choice("work", "Заставить работать", true, Loyalty(-10f)),
                Choice("sacrifice", "Принести в жертву", false, Kill()));
            quest.onSuccess = new[] { StartDialog(thanks) };
            EditorUtility.SetDirty(quest);

            var run = Trigger("RunStart", "run_start", StoryFactKind.RunStarted, null, intro);
            var era = Trigger("EraSick", "era_sick", StoryFactKind.EraReached, "hunger", sick);
            var pack = ScriptableObject.CreateInstance<StoryPackAsset>();
            pack.dialogs = new[] { intro, offer, thanks, sick };
            pack.quests = new[] { quest };
            pack.triggers = new[] { run, era };
            AssetDatabase.CreateAsset(pack, PackPath);
            AssignPack(pack);
            AssetDatabase.SaveAssets();
        }

        static void EnsureHud(UnityEngine.SceneManagement.Scene scene)
        {
            var canvas = Find(scene, "GameHudCanvas");
            if (canvas == null)
                return;
            var pin = EnsurePinPrefab();
            var choice = EnsureChoicePrefab();
            var row = EnsureRowPrefab();
            var pack = AssetDatabase.LoadAssetAtPath<StoryPackAsset>(PackPath);
            var changed = false;
            changed |= EnsurePins(canvas.transform, pin);
            changed |= EnsureDialog(canvas.transform, choice, pack);
            changed |= EnsureQuests(canvas.transform, row, pack);
            changed |= KeepQuestsBehind(canvas.transform);
            if (!changed)
                return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static bool EnsurePins(Transform canvas, StoryPinView prefab)
        {
            if (canvas.Find("StoryPins") != null)
                return false;
            var go = new GameObject("StoryPins", typeof(RectTransform), typeof(StoryPinBoard));
            go.transform.SetParent(canvas, false);
            Stretch(go.GetComponent<RectTransform>());
            var board = go.GetComponent<StoryPinBoard>();
            var so = new SerializedObject(board);
            so.FindProperty("pinPrefab").objectReferenceValue = prefab;
            so.FindProperty("pinRoot").objectReferenceValue = go.GetComponent<RectTransform>();
            so.FindProperty("height").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        static bool EnsureDialog(Transform canvas, StoryChoiceView choicePrefab, StoryPackAsset pack)
        {
            if (canvas.Find("StoryDialog") != null)
                return false;
            var root = new GameObject("StoryDialog", typeof(RectTransform), typeof(StoryDialogPanel));
            root.transform.SetParent(canvas, false);
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(560f, 340f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.06f, 0.94f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            var title = Text(panel.transform, "Title", 28, FontStyles.Bold);
            var body = Text(panel.transform, "Body", 20, FontStyles.Normal);
            body.GetComponent<LayoutElement>().preferredHeight = 140f;
            var choices = new GameObject("Choices", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            choices.transform.SetParent(panel.transform, false);
            var row = choices.GetComponent<HorizontalLayoutGroup>();
            row.spacing = 12f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            choices.GetComponent<LayoutElement>().preferredHeight = 48f;

            var widget = root.GetComponent<StoryDialogPanel>();
            var so = new SerializedObject(widget);
            so.FindProperty("pack").objectReferenceValue = pack;
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("title").objectReferenceValue = title.GetComponent<TMP_Text>();
            so.FindProperty("body").objectReferenceValue = body.GetComponent<TMP_Text>();
            so.FindProperty("choiceRoot").objectReferenceValue = choices.GetComponent<RectTransform>();
            so.FindProperty("choicePrefab").objectReferenceValue = choicePrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.SetActive(false);
            return true;
        }

        static bool EnsureQuests(Transform canvas, StoryQuestRowView rowPrefab, StoryPackAsset pack)
        {
            if (canvas.Find("StoryQuests") != null)
                return false;
            var go = new GameObject("StoryQuests", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(StoryQuestWidget));
            go.transform.SetParent(canvas, false);
            go.transform.SetAsFirstSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(24f, 80f);
            rt.sizeDelta = new Vector2(420f, 280f);
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var widget = go.GetComponent<StoryQuestWidget>();
            var so = new SerializedObject(widget);
            so.FindProperty("pack").objectReferenceValue = pack;
            so.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
            so.FindProperty("rowRoot").objectReferenceValue = rt;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        static bool KeepQuestsBehind(Transform canvas)
        {
            var quests = canvas.Find("StoryQuests");
            if (quests == null || quests.GetSiblingIndex() == 0)
                return false;
            quests.SetAsFirstSibling();
            return true;
        }

        static StoryPinView EnsurePinPrefab()
        {
            const string path = PrefabFolder + "/StoryPin.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<StoryPinView>(path);
            if (existing != null)
                return existing;
            Directory.CreateDirectory(PrefabFolder);
            var go = new GameObject("StoryPin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(StoryPinView));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 64f);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.16f, 0.1f, 0.07f, 0.92f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var excl = Mark(go.transform, "Exclamation", "!");
            var ques = Mark(go.transform, "Question", "?");
            ques.SetActive(false);
            var timer = Text(go.transform, "Timer", 16, FontStyles.Normal);
            var timerRt = timer.GetComponent<RectTransform>();
            timerRt.anchorMin = timerRt.anchorMax = new Vector2(0.5f, 0f);
            timerRt.pivot = new Vector2(0.5f, 1f);
            timerRt.anchoredPosition = new Vector2(0f, -4f);
            timerRt.sizeDelta = new Vector2(72f, 22f);
            var view = go.GetComponent<StoryPinView>();
            var so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("exclamation").objectReferenceValue = excl;
            so.FindProperty("question").objectReferenceValue = ques;
            so.FindProperty("timer").objectReferenceValue = timer.GetComponent<TMP_Text>();
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<StoryPinView>();
        }

        static StoryChoiceView EnsureChoicePrefab()
        {
            const string path = PrefabFolder + "/StoryChoiceButton.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<StoryChoiceView>(path);
            if (existing != null)
                return existing;
            Directory.CreateDirectory(PrefabFolder);
            var go = new GameObject("StoryChoiceButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(StoryChoiceView));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 44f);
            go.GetComponent<LayoutElement>().preferredHeight = 44f;
            var image = go.GetComponent<Image>();
            image.color = new Color(0.35f, 0.24f, 0.14f, 1f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var label = Text(go.transform, "Label", 18, FontStyles.Normal);
            Stretch(label.GetComponent<RectTransform>());
            var view = go.GetComponent<StoryChoiceView>();
            var so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("label").objectReferenceValue = label.GetComponent<TMP_Text>();
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<StoryChoiceView>();
        }

        static StoryQuestRowView EnsureRowPrefab()
        {
            const string path = PrefabFolder + "/StoryQuestRow.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<StoryQuestRowView>(path);
            if (existing != null)
                return existing;
            Directory.CreateDirectory(PrefabFolder);
            var go = new GameObject("StoryQuestRow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(StoryQuestRowView));
            go.GetComponent<LayoutElement>().preferredHeight = 110f;
            go.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f, 0.88f);
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            var title = Text(go.transform, "Title", 20, FontStyles.Bold);
            var description = Text(go.transform, "Description", 16, FontStyles.Normal);
            var progress = Text(go.transform, "Progress", 16, FontStyles.Normal);
            var slider = Slider(go.transform);
            var view = go.GetComponent<StoryQuestRowView>();
            var so = new SerializedObject(view);
            so.FindProperty("title").objectReferenceValue = title.GetComponent<TMP_Text>();
            so.FindProperty("description").objectReferenceValue = description.GetComponent<TMP_Text>();
            so.FindProperty("progress").objectReferenceValue = progress.GetComponent<TMP_Text>();
            so.FindProperty("deadline").objectReferenceValue = slider;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<StoryQuestRowView>();
        }

        static GameObject Mark(Transform parent, string name, string glyph)
        {
            var text = Text(parent, name, 28, FontStyles.Bold);
            text.GetComponent<TMP_Text>().text = glyph;
            Stretch(text.GetComponent<RectTransform>());
            return text;
        }

        static TMP_FontAsset Font()
        {
            if (TMP_Settings.defaultFontAsset != null)
                return TMP_Settings.defaultFontAsset;
            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            if (guids.Length == 0)
                return null;
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        static GameObject Text(Transform parent, string name, int size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = Font();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.93f, 0.88f, 0.78f, 1f);
            tmp.text = name;
            tmp.raycastTarget = false;
            go.GetComponent<LayoutElement>().preferredHeight = size + 10f;
            return go;
        }

        static Slider Slider(Transform parent)
        {
            var go = new GameObject("Deadline", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 14f;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 14f);
            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(go.transform, false);
            Stretch(background.GetComponent<RectTransform>());
            background.GetComponent<Image>().color = new Color(0.2f, 0.16f, 0.12f, 1f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            Stretch(fillRt);
            fill.GetComponent<Image>().color = new Color(0.75f, 0.45f, 0.2f, 1f);
            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRt;
            slider.targetGraphic = background.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.interactable = false;
            return slider;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static GameObject Find(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
                var child = root.transform.Find(name);
                if (child != null)
                    return child.gameObject;
            }

            return null;
        }

        static StoryDialogAsset Dialog(
            string file, string id, string title, string body,
            StoryDelivery delivery, StoryIconKind icon, StoryAnchorRule anchor)
        {
            var asset = ScriptableObject.CreateInstance<StoryDialogAsset>();
            asset.dialogId = id;
            asset.title = title;
            asset.body = body;
            asset.delivery = delivery;
            asset.icon = icon;
            asset.anchor = anchor;
            asset.openHours = 0f;
            asset.choices = System.Array.Empty<StoryChoiceAuthoring>();
            AssetDatabase.CreateAsset(asset, $"{Folder}/{file}.asset");
            return asset;
        }

        static StoryQuestAsset Quest()
        {
            var asset = ScriptableObject.CreateInstance<StoryQuestAsset>();
            asset.questId = "build_sawmills";
            asset.title = "Две лесопилки";
            asset.description = "Достроить две лесопилки за отведённое время.";
            asset.goal = StoryGoalKind.BuildingsAtLeast;
            asset.goalId = "sawmill";
            asset.required = 2;
            asset.deadlineHours = 24f;
            asset.onSuccess = System.Array.Empty<StoryEffectAuthoring>();
            asset.onFail = System.Array.Empty<StoryEffectAuthoring>();
            AssetDatabase.CreateAsset(asset, Folder + "/BuildSawmills.asset");
            return asset;
        }

        static StoryTriggerAsset Trigger(string file, string id, StoryFactKind fact, string factId, StoryDialogAsset dialog)
        {
            var asset = ScriptableObject.CreateInstance<StoryTriggerAsset>();
            asset.triggerId = id;
            asset.armedAtStart = true;
            asset.repeat = false;
            asset.fact = fact;
            asset.factId = factId;
            asset.dialog = dialog;
            AssetDatabase.CreateAsset(asset, $"{Folder}/{file}.asset");
            return asset;
        }

        static void Wire(StoryDialogAsset dialog, params StoryChoiceAuthoring[] choices)
        {
            dialog.choices = choices;
            EditorUtility.SetDirty(dialog);
        }

        static StoryChoiceAuthoring Choice(string id, string label, bool fallback, params StoryEffectAuthoring[] effects)
        {
            return new StoryChoiceAuthoring
            {
                choiceId = id,
                label = label,
                fallback = fallback,
                effects = effects ?? System.Array.Empty<StoryEffectAuthoring>()
            };
        }

        static StoryEffectAuthoring StartDialog(StoryDialogAsset dialog)
        {
            return new StoryEffectAuthoring { kind = StoryEffectKind.StartDialog, dialog = dialog };
        }

        static StoryEffectAuthoring StartQuest(StoryQuestAsset quest)
        {
            return new StoryEffectAuthoring { kind = StoryEffectKind.StartQuest, quest = quest };
        }

        static StoryEffectAuthoring Loyalty(float amount)
        {
            return new StoryEffectAuthoring { kind = StoryEffectKind.Loyalty, amount = amount };
        }

        static StoryEffectAuthoring Kill()
        {
            return new StoryEffectAuthoring { kind = StoryEffectKind.KillAnchor };
        }

        static void AssignPack(StoryPackAsset pack)
        {
            Assign("Assets/_Project/Content/Scenarios/DefaultScenario.asset", pack);
            Assign("Assets/_Project/Content/Scenarios/DebugScenario.asset", pack);
        }

        static void Assign(string path, StoryPackAsset pack)
        {
            var scenario = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(path);
            if (scenario == null)
                return;
            var so = new SerializedObject(scenario);
            so.FindProperty("storyPack").objectReferenceValue = pack;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
