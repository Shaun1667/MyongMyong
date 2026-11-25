using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 플레이어 머리 위 작은 체력 바(그리기만, 값은 Core의 World). 플레이어 리그와 따로 있는 오브젝트라 리그 · 프리팹을 건드리지 않는다.
    /// 1도트 흰 스프라이트를 코드로 만들어 도트 단위로 늘린다(번지지 않게 도트 격자에 맞춘다).
    /// </summary>
    public sealed class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField] Material spriteMaterial;
        [SerializeField] int sortingOrder = 5;
        [Tooltip("Bar width in dots (the fill).")]
        [SerializeField, Min(2)] int widthDots = 24;
        [Tooltip("Bar height in dots (the fill).")]
        [SerializeField, Min(1)] int heightDots = 3;
        [Tooltip("Height of the bar above the player's feet in units (the rig is about 79 dots tall).")]
        [SerializeField] float heightAboveFeet = 2.6f;
        [SerializeField] Color backColor = new Color(0.08f, 0.06f, 0.08f, 0.9f);
        [SerializeField] Color fillColor = new Color(0.36f, 0.86f, 0.4f, 1f);
        [Tooltip("Fill colour when the health ratio is at or below Low Ratio.")]
        [SerializeField] Color lowColor = new Color(0.95f, 0.3f, 0.25f, 1f);
        [SerializeField, Range(0f, 1f)] float lowRatio = 0.3f;

        Texture2D texture;
        Sprite pixel;
        SpriteRenderer back;
        SpriteRenderer fill;
        int shownFillDots = -1;
        bool shownLow;

        /// <summary>지금 채워진 도트 수(확인용).</summary>
        public int FillDots => shownFillDots;

        void Awake()
        {
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "HealthBarPixel" };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            // 피벗 왼쪽 가운데: X 크기를 도트 수로 두면 왼쪽부터 찬다.
            pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), PixelArt.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            pixel.name = "HealthBarPixel";
            back = MakePart("Back", backColor, sortingOrder);
            fill = MakePart("Fill", fillColor, sortingOrder + 1);
            back.transform.localPosition = new Vector3(-1f / PixelArt.PixelsPerUnit, 0f, 0f);
            back.transform.localScale = new Vector3(widthDots + 2, heightDots + 2, 1f);
            fill.transform.localScale = new Vector3(widthDots, heightDots, 1f);
        }

        SpriteRenderer MakePart(string name, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = pixel;
            r.color = color;
            r.sortingOrder = order;
            if (spriteMaterial != null) r.sharedMaterial = spriteMaterial;
            return r;
        }

        /// <param name="alpha">직전 틱(0)과 지금 틱(1) 사이 어디를 그릴지(플레이어 그리기와 같은 값).</param>
        public void Render(in PlayerState state, float alpha, float maxHealth)
        {
            if (fill == null) return;
            Vec2 p = Vec2.Lerp(state.PreviousPosition, state.Position, alpha);
            float left = p.X - widthDots * 0.5f / PixelArt.PixelsPerUnit;
            transform.position = PixelArt.Snap(left, p.Y + heightAboveFeet, transform.position.z);

            float ratio = maxHealth > 0f ? Mathf.Clamp01(state.Health / maxHealth) : 0f;
            int dots = Mathf.CeilToInt(ratio * widthDots);
            if (dots != shownFillDots)
            {
                fill.enabled = dots > 0;
                fill.transform.localScale = new Vector3(Mathf.Max(dots, 1), heightDots, 1f);
                shownFillDots = dots;
            }
            bool low = ratio <= lowRatio;
            if (low != shownLow)
            {
                fill.color = low ? lowColor : fillColor;
                shownLow = low;
            }
        }

        void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
            if (texture != null) Destroy(texture);
        }
    }
}
