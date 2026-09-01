using UnityEngine;
using UnityEngine.UI;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Roof chrome shared by houses: bar + status icons. Prefab instance, not
    /// assembled in code.
    /// </summary>
    public sealed class BuildingWorldUi : MonoBehaviour
    {
        [SerializeField] GameObject barRoot;
        [SerializeField] Image fill;
        [SerializeField] GameObject statusRoot;

        static Sprite _white;

        public GameObject BarRoot => barRoot;

        public Image Fill => fill;

        public GameObject StatusRoot => statusRoot;

        void Awake()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                if (canvas.sortingOrder < 20)
                    canvas.sortingOrder = 20;
            }

            EnsureSprites();
        }

        void EnsureSprites()
        {
            if (fill != null)
            {
                if (fill.sprite == null)
                    fill.sprite = WhiteSprite();
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            }

            if (barRoot == null)
                return;

            var images = barRoot.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].sprite == null)
                    images[i].sprite = WhiteSprite();
            }
        }

        static Sprite WhiteSprite()
        {
            if (_white != null)
                return _white;
            var texture = Texture2D.whiteTexture;
            _white = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _white.name = "BuildingWorldUiWhite";
            return _white;
        }
    }
}
