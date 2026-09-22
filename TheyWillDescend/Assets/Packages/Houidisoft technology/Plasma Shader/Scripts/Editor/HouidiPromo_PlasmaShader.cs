using UnityEditor;
using UnityEngine;

namespace HouidiSoft
{
    [InitializeOnLoad]
    public static class HouidiPromo_PlasmaShader
    {
        private const string ASSET_NAME = "Plasma Shader";
        private const string PREFS_KEY  = "Houidisoft_Promo_DontShow_PlasmaShader";

        private const string PLANAR_URL    = "https://assetstore.unity.com/packages/vfx/shaders/planar-reflection-1-325879";
        private const string WATER_URL     = "https://assetstore.unity.com/packages/slug/essw-easy-setup-stylized-water-2-0-317597";
        private const string PUBLISHER_URL = "https://assetstore.unity.com/publishers/105962";

        static HouidiPromo_PlasmaShader()
        {
            EditorApplication.delayCall += TryShow;
        }

        private static void TryShow()
        {
            if (!EditorPrefs.GetBool(PREFS_KEY, false))
                HouidiPromoWindow_PlasmaShader.Open(ASSET_NAME, PREFS_KEY, PLANAR_URL, WATER_URL, PUBLISHER_URL);
        }

        [MenuItem("Window/Houidisoft/More Assets")]
        public static void ShowManual()
            => HouidiPromoWindow_PlasmaShader.Open(ASSET_NAME, PREFS_KEY, PLANAR_URL, WATER_URL, PUBLISHER_URL);
    }

    public class HouidiPromoWindow_PlasmaShader : EditorWindow
    {
        private const string PLANAR_B64 = "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";
        private const string WATER_B64  = "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";

        private const float WIN_W   = 440f;
        private const float WIN_H   = 360f;
        private const float THUMB_W = 100f;
        private const float THUMB_H = 66f;

        private string    _assetName;
        private string    _prefsKey;
        private string    _planarUrl;
        private string    _waterUrl;
        private string    _publisherUrl;
        private Texture2D _planarTex;
        private Texture2D _waterTex;

        private GUIStyle _headerStyle;
        private GUIStyle _subStyle;
        private GUIStyle _assetTitleStyle;
        private GUIStyle _descStyle;
        private GUIStyle _linkStyle;
        private GUIStyle _starsStyle;
        private GUIStyle _bodyStyle;
        private bool     _stylesReady;

        public static void Open(string assetName, string prefsKey,
            string planarUrl, string waterUrl, string publisherUrl)
        {
            var win = GetWindow<HouidiPromoWindow_PlasmaShader>(true, "Houidisoft Technology", true);
            win._assetName    = assetName;
            win._prefsKey     = prefsKey;
            win._planarUrl    = planarUrl;
            win._waterUrl     = waterUrl;
            win._publisherUrl = publisherUrl;
            win.minSize = new Vector2(WIN_W, WIN_H);
            win.maxSize = new Vector2(WIN_W, WIN_H);
            win.Show();
        }

        private void OnEnable()
        {
            _planarTex = Decode(PLANAR_B64);
            _waterTex  = Decode(WATER_B64);
        }

        private void OnDestroy()
        {
            if (_planarTex != null) DestroyImmediate(_planarTex);
            if (_waterTex  != null) DestroyImmediate(_waterTex);
        }

        private void InitStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 13,
                alignment = TextAnchor.MiddleLeft
            };
            _subStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 11,
                normal    = { textColor = new Color(0.5f, 0.5f, 0.5f) },
                alignment = TextAnchor.MiddleRight
            };
            _assetTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            _descStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal   = { textColor = new Color(0.58f, 0.58f, 0.58f) }
            };
            _linkStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                normal   = { textColor = new Color(0.35f, 0.6f, 1f) }
            };
            _starsStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                normal   = { textColor = new Color(0.92f, 0.64f, 0.12f) }
            };
            _bodyStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal   = { textColor = new Color(0.62f, 0.62f, 0.62f) }
            };
        }

        private void OnGUI()
        {
            InitStyles();

            EditorGUI.DrawRect(new Rect(0, 0, WIN_W, 40), new Color(0.13f, 0.13f, 0.13f));
            EditorGUI.DrawRect(new Rect(0, 40, WIN_W, 1), new Color(0.22f, 0.22f, 0.22f));
            GUI.Label(new Rect(14, 0, 220, 40), "Houidisoft Technology", _headerStyle);
            GUI.Label(new Rect(220, 0, WIN_W - 230, 40),
                "Free Asset  /  " + (_assetName ?? ""), _subStyle);

            GUILayout.Space(50);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(14);
            EditorGUILayout.LabelField(
                "This asset is free. If it helps your project, check out the paid assets from the same developer.",
                _bodyStyle, GUILayout.Width(WIN_W - 28));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(14);

            DrawCard(_planarTex, "Planar Reflection 1",
                "Real-time planar reflections for URP. No bake needed.", true, _planarUrl);

            GUILayout.Space(10);

            DrawCard(_waterTex, "Easy Setup Stylized Water 2.0",
                "Stylized water with foam, depth color, and animated waves.", false, _waterUrl);

            GUILayout.FlexibleSpace();
            EditorGUI.DrawRect(new Rect(0, WIN_H - 42, WIN_W, 1), new Color(0.22f, 0.22f, 0.22f));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(14);
            if (GUILayout.Button("View all assets", _linkStyle,
                GUILayout.Width(100), GUILayout.Height(42)))
                Application.OpenURL(_publisherUrl);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Don't show again", GUILayout.Width(116), GUILayout.Height(24)))
            {
                EditorPrefs.SetBool(_prefsKey, true);
                Close();
            }
            GUILayout.Space(6);
            if (GUILayout.Button("Close", GUILayout.Width(60), GUILayout.Height(24)))
                Close();
            GUILayout.Space(14);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCard(Texture2D thumb, string title, string desc,
            bool showStars, string url)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(14);

            Rect r = GUILayoutUtility.GetRect(THUMB_W, THUMB_H,
                GUILayout.Width(THUMB_W), GUILayout.Height(THUMB_H));
            EditorGUI.DrawRect(r, new Color(0.11f, 0.11f, 0.11f));
            if (thumb != null)
                GUI.DrawTexture(r, thumb, ScaleMode.ScaleAndCrop);

            GUILayout.Space(12);

            EditorGUILayout.BeginVertical(GUILayout.Height(THUMB_H));
            EditorGUILayout.LabelField(title, _assetTitleStyle);

            if (showStars)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("★★★★★", _starsStyle, GUILayout.Width(76));
                EditorGUILayout.LabelField("5.0", new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.48f, 0.48f, 0.48f) }
                }, GUILayout.Width(26));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField(desc, _descStyle);
            if (GUILayout.Button("View on Asset Store", _linkStyle,
                GUILayout.Width(130), GUILayout.Height(18)))
                Application.OpenURL(url);

            EditorGUILayout.EndVertical();
            GUILayout.Space(14);
            EditorGUILayout.EndHorizontal();
        }

        private static Texture2D Decode(string b64)
        {
            if (string.IsNullOrEmpty(b64)) return null;
            try
            {
                var tex = new Texture2D(2, 2);
                tex.LoadImage(System.Convert.FromBase64String(b64));
                return tex;
            }
            catch { return null; }
        }
    }
}
