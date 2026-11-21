using System;
using System.Linq;
using System.Reflection;
using MicahW.PointGrass;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TheyWillDescend.Authoring.Editor
{
    public sealed class PointGrassBrushWindow : EditorWindow
    {
        [SerializeField] private PointGrassRenderer grass;
        [SerializeField] private bool painting;
        [SerializeField] private int mode;
        [SerializeField] private float radius = 5f;
        [SerializeField] private float hardness = .35f;
        [SerializeField] private float strength = .4f;
        [SerializeField] private float density = 1f;
        [SerializeField] private float height = .8f;

        private static readonly MethodInfo IntersectMesh = typeof(HandleUtility).GetMethod(
            "IntersectRayMesh", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private Mesh cachedMesh;
        private Matrix4x4 cachedMatrix;
        private Vector3[] positions;
        private Color[] colors;
        private bool stroke;
        private int undoGroup;
        private int strokeControl;
        private Vector3 previousPoint;
        private bool needsRefresh;
        private double lastRefresh;
        private Label status;
        private VisualElement settings;
        private Toggle paintToggle;

        [MenuItem("Tools/They Will Descend/Кисть травы")]
        public static void Open()
        {
            var window = GetWindow<PointGrassBrushWindow>();
            window.titleContent = new GUIContent("Кисть травы");
            window.minSize = new Vector2(370f, 530f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += UpdatePreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (!grass)
                grass = FindObjectsByType<PointGrassRenderer>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(x => x.name == "Plateau_Painted_Grass");
        }

        private void OnDisable()
        {
            FinishStroke();
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= UpdatePreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = root.style.paddingBottom = 10;
            var scroll = new ScrollView();
            root.Add(scroll);
            scroll.Add(new Label("Трава на плато") { style = { fontSize = 17, unityFontStyleAndWeight = FontStyle.Bold } });
            scroll.Add(new HelpBox("ЛКМ — рисовать. Shift + ЛКМ — стирать / понижать.\nAlt — навигация. [ и ] — размер кисти. Escape — выключить.\nCtrl+Z / Ctrl+Y — отменить / вернуть мазок.", HelpBoxMessageType.Info));
            var target = new ObjectField("Трава") { objectType = typeof(PointGrassRenderer), allowSceneObjects = true, value = grass };
            target.RegisterValueChangedCallback(evt => { FinishStroke(); grass = evt.newValue as PointGrassRenderer; Invalidate(); CreateGUI(); });
            scroll.Add(target);
            scroll.Add(new Button(() => { if (grass) { Selection.activeGameObject = grass.gameObject; EditorGUIUtility.PingObject(grass.gameObject); } }) { text = "Выбрать объект травы" });
            paintToggle = new Toggle("Кисть включена") { value = painting };
            paintToggle.RegisterValueChangedCallback(evt => { FinishStroke(); painting = evt.newValue; SceneView.RepaintAll(); });
            scroll.Add(paintToggle);
            settings = new VisualElement();
            scroll.Add(settings);
            var modes = new RadioButtonGroup("Режим", new System.Collections.Generic.List<string> { "Плотность", "Высота" }) { value = mode };
            modes.RegisterValueChangedCallback(evt => { FinishStroke(); mode = evt.newValue; });
            settings.Add(modes);
            AddSlider(settings, "Радиус, м", .5f, 25f, radius, v => radius = v);
            AddSlider(settings, "Жёсткость края", 0f, 1f, hardness, v => hardness = v);
            AddSlider(settings, "Сила кисти", .05f, 1f, strength, v => strength = v);
            AddSlider(settings, "Целевая плотность", 0f, 1f, density, v => density = v);
            AddSlider(settings, "Целевая высота", 0f, 1f, height, v => height = v);
            settings.Add(new HelpBox("Высота 0–1 задаёт положение внутри Length Mapping. Общий размер травинки — Width / Height в материале. Камни не блокируют кисть: вокруг них можно стереть траву.", HelpBoxMessageType.None));
            if (grass)
            {
                var areaMode = new Toggle("Количество на квадратный метр") { value = grass.multiplyByArea };
                areaMode.RegisterValueChangedCallback(evt =>
                {
                    ChangeRenderer(() => grass.multiplyByArea = evt.newValue);
                    rootVisualElement.schedule.Execute(CreateGUI);
                });
                settings.Add(areaMode);
                var count = new FloatField(grass.multiplyByArea ? "Травинок на м²" : "Всего травинок") { value = grass.pointCount };
                count.RegisterValueChangedCallback(evt => ChangeRenderer(() => grass.pointCount = Mathf.Max(0f, evt.newValue)));
                settings.Add(count);
                AddRandomHeightControls(settings);
                settings.Add(new Button(() => { if (grass.material) Selection.activeObject = grass.material; }) { text = "Открыть материал травы" });
            }
            status = new Label { style = { whiteSpace = WhiteSpace.Normal, marginTop = 8 } };
            scroll.Add(status);
            scroll.Add(new Button(SaveMask) { text = "Сохранить маску" });
            scroll.Add(new Button(MakeIndependentMask) { text = "Создать независимую копию маски" });
            RefreshStatus();
        }

        private static void AddSlider(VisualElement root, string label, float min, float max, float value, Action<float> changed)
        {
            var slider = new Slider(label, min, max) { value = value, showInputField = true };
            slider.RegisterValueChangedCallback(evt => changed(Mathf.Clamp(evt.newValue, min, max)));
            root.Add(slider);
        }

        private void AddRandomHeightControls(VisualElement root)
        {
            Material material = grass.material;
            if (!material || !material.HasProperty("_RandomHeightMin") || !material.HasProperty("_RandomHeightMax"))
                return;

            root.Add(new Label("Случайная высота травинок") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } });
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var minimum = new FloatField("Мин.") { value = material.GetFloat("_RandomHeightMin"), style = { flexGrow = 1, minWidth = 0 } };
            var maximum = new FloatField("Макс.") { value = material.GetFloat("_RandomHeightMax"), style = { flexGrow = 1, minWidth = 0 } };
            minimum.labelElement.style.minWidth = maximum.labelElement.style.minWidth = 40;
            minimum.labelElement.style.width = maximum.labelElement.style.width = 40;
            row.Add(minimum);
            row.Add(maximum);
            root.Add(row);
            minimum.RegisterValueChangedCallback(evt =>
            {
                float min = Mathf.Clamp01(evt.newValue);
                float max = Mathf.Max(min, Mathf.Clamp01(maximum.value));
                minimum.SetValueWithoutNotify(min);
                maximum.SetValueWithoutNotify(max);
                SetRandomHeightRange(new Vector2(min, max));
            });
            maximum.RegisterValueChangedCallback(evt =>
            {
                float max = Mathf.Clamp01(evt.newValue);
                float min = Mathf.Min(max, Mathf.Clamp01(minimum.value));
                minimum.SetValueWithoutNotify(min);
                maximum.SetValueWithoutNotify(max);
                SetRandomHeightRange(new Vector2(min, max));
            });
            root.Add(new HelpBox("Каждая травинка получает устойчивую случайную высоту в этом диапазоне. 0,4–1 = 40–100% высоты из материала с учётом маски. Диапазон общий для травы с этим материалом; режим «Высота» меняет отдельные участки.", HelpBoxMessageType.None));
        }

        private void SetRandomHeightRange(Vector2 range)
        {
            if (!grass || !grass.material) return;
            Material material = grass.material;
            if (!material.HasProperty("_RandomHeightMin") || !material.HasProperty("_RandomHeightMax")) return;
            Undo.RecordObject(material, "Разброс высоты травы");
            material.SetFloat("_RandomHeightMin", Mathf.Clamp01(Mathf.Min(range.x, range.y)));
            material.SetFloat("_RandomHeightMax", Mathf.Clamp01(Mathf.Max(range.x, range.y)));
            EditorUtility.SetDirty(material);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            RefreshStatus();
        }

        private string ValidationError()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Кисть работает вне Play Mode.";
            if (!grass || !grass.baseMesh) return "Выбери объект Point Grass с маской поверхности.";
            if (grass.distSource != PointGrassCommon.DistributionSource.Mesh) return "Нужен Distribution Source = Mesh.";
            if (!grass.baseMesh.isReadable) return "Маска должна быть доступна для чтения.";
            if (!grass.enabled || !grass.gameObject.activeInHierarchy) return "Включи объект травы и компонент рендера.";
            string path = AssetDatabase.GetAssetPath(grass.baseMesh);
            if (!path.StartsWith("Assets/_Project/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return "Сначала создай независимую копию маски в проекте.";
            if (!grass.useDensity || !grass.useLength) return "Включи Use Density Inputs и Use Length Inputs.";
            return null;
        }

        private void RefreshStatus()
        {
            if (status == null) return;
            string error = ValidationError();
            settings?.SetEnabled(error == null);
            status.text = error ?? $"Маска: {grass.baseMesh.vertexCount:N0} вершин. " +
                (EditorUtility.IsDirty(grass.baseMesh) || grass.material && EditorUtility.IsDirty(grass.material)
                    ? "Есть несохранённые изменения." : "Сохранено.") +
                $"\nСохранение — только кнопкой «Сохранить маску».\nКисть: {(mode == 0 ? "плотность" : "высота")}, радиус {radius:0.0} м.";
        }

        private void ChangeRenderer(Action change)
        {
            FinishStroke();
            Undo.RecordObject(grass, "Настройки травы");
            change();
            EditorUtility.SetDirty(grass);
            needsRefresh = true;
        }

        private void Invalidate()
        {
            cachedMesh = null;
            positions = null;
            colors = null;
        }

        private void EnsureCache()
        {
            Matrix4x4 matrix = grass.transform.localToWorldMatrix;
            if (cachedMesh == grass.baseMesh && matrix == cachedMatrix && colors != null) return;
            cachedMesh = grass.baseMesh;
            cachedMatrix = matrix;
            positions = cachedMesh.vertices;
            for (int i = 0; i < positions.Length; i++) positions[i] = matrix.MultiplyPoint3x4(positions[i]);
            colors = PointGrassBrushMask.ReadColors(cachedMesh);
        }

        private void OnSceneGUI(SceneView view)
        {
            if (!painting || ValidationError() != null) return;
            Event evt = Event.current;
            int control = GUIUtility.GetControlID("PointGrassBrush".GetHashCode(), FocusType.Passive);
            if (evt.type == EventType.KeyDown)
            {
                if (evt.keyCode == KeyCode.Escape) { FinishStroke(); painting = false; paintToggle?.SetValueWithoutNotify(false); evt.Use(); return; }
                if (evt.keyCode == KeyCode.LeftBracket || evt.keyCode == KeyCode.RightBracket)
                {
                    radius = Mathf.Clamp(radius * (evt.keyCode == KeyCode.LeftBracket ? .85f : 1.15f), .5f, 25f);
                    evt.Use(); view.Repaint(); CreateGUI(); return;
                }
            }
            if (evt.type == EventType.MouseUp && evt.button == 0 && stroke)
            {
                FinishStroke(); evt.Use(); return;
            }
            if (evt.alt || Tools.current == Tool.View) return;
            if (evt.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
            if (IntersectMesh == null) return;
            object[] args = { HandleUtility.GUIPointToWorldRay(evt.mousePosition), grass.baseMesh, grass.transform.localToWorldMatrix, null };
            if (!(bool)IntersectMesh.Invoke(null, args)) return;
            var hit = (RaycastHit)args[3];
            if (evt.type == EventType.Repaint)
            {
                using (new Handles.DrawingScope(evt.shift ? new Color(1f, .45f, .2f) : new Color(.3f, 1f, .5f)))
                {
                    Handles.DrawWireDisc(hit.point + Vector3.up * .03f, Vector3.up, radius);
                    Handles.DrawWireDisc(hit.point + Vector3.up * .03f, Vector3.up, radius * hardness);
                }
            }
            if (evt.type == EventType.MouseMove) view.Repaint();
            if (evt.button != 0) return;
            if (evt.type == EventType.MouseDown)
            {
                if (FindObjectsByType<PointGrassRenderer>(FindObjectsInactive.Include).Count(x => x.baseMesh == grass.baseMesh) > 1)
                {
                    ShowNotification(new GUIContent("Эта маска общая. Сначала создай независимую копию.")); evt.Use(); return;
                }
                EnsureCache();
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(mode == 0 ? "Рисование травы" : "Высота травы");
                Undo.RegisterCompleteObjectUndo(cachedMesh, mode == 0 ? "Рисование травы" : "Высота травы");
                stroke = true;
                strokeControl = control;
                GUIUtility.hotControl = control;
                previousPoint = hit.point;
                Stamp(hit.point, evt.shift);
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && stroke && GUIUtility.hotControl == strokeControl)
            {
                float distance = Vector3.Distance(previousPoint, hit.point);
                if (distance > radius * .12f)
                {
                    int steps = Mathf.Clamp(Mathf.CeilToInt(distance / (radius * .2f)), 1, 128);
                    for (int i = 1; i <= steps; i++) Stamp(Vector3.Lerp(previousPoint, hit.point, (float)i / steps), evt.shift);
                    previousPoint = hit.point;
                }
                evt.Use();
            }
        }

        private void Stamp(Vector3 center, bool erase)
        {
            float value = erase ? 0f : mode == 0 ? density : height;
            if (!PointGrassBrushMask.Paint(positions, colors, center, radius, hardness, strength, value, mode == 1)) return;
            cachedMesh.colors = colors;
            EditorUtility.SetDirty(cachedMesh);
            needsRefresh = true;
        }

        private void FinishStroke()
        {
            if (!stroke) return;
            stroke = false;
            if (GUIUtility.hotControl == strokeControl) GUIUtility.hotControl = 0;
            Undo.CollapseUndoOperations(undoGroup);
            needsRefresh = true;
        }

        private void UpdatePreview()
        {
            if (!needsRefresh || !grass || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (EditorApplication.timeSinceStartup - lastRefresh < .12) return;
            needsRefresh = false;
            lastRefresh = EditorApplication.timeSinceStartup;
            PointGrassBrushPreview.Build(grass);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            RefreshStatus();
        }

        private void OnUndoRedo()
        {
            Invalidate();
            needsRefresh = true;
            rootVisualElement.schedule.Execute(CreateGUI);
        }

        private void SaveMask()
        {
            FinishStroke();
            if (grass && grass.baseMesh) AssetDatabase.SaveAssetIfDirty(grass.baseMesh);
            if (grass && grass.material) AssetDatabase.SaveAssetIfDirty(grass.material);
            RefreshStatus();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            FinishStroke();
            Invalidate();
            if (state == PlayModeStateChange.EnteredEditMode) needsRefresh = true;
            RefreshStatus();
        }

        private void MakeIndependentMask()
        {
            if (!grass || !grass.baseMesh || EditorApplication.isPlayingOrWillChangePlaymode) return;
            FinishStroke();
            Mesh copy = Instantiate(grass.baseMesh);
            copy.name = grass.baseMesh.name + "_Paint";
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/_Project/Art/Models/Grass/" + copy.name + ".asset");
            AssetDatabase.CreateAsset(copy, path);
            Undo.RecordObject(grass, "Независимая маска травы");
            grass.baseMesh = copy;
            EditorUtility.SetDirty(grass);
            Invalidate();
            needsRefresh = true;
            RefreshStatus();
        }
    }
}
