using System;
using System.Collections.Generic;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 경험치 젬 그리기 한 곳. SpriteRenderer를 젬 최대 수만큼 미리 만들어 두고 재사용한다(틱 · 프레임마다 만들거나 지우지 않음).
    /// 그리는 것만 한다: 보간 + 도트 격자 맞춤. 그림이 없는 비주얼 ID는 단색 작은 도트(PlaceholderShapes.GemDot).
    /// </summary>
    public sealed class GemRenderPool : MonoBehaviour
    {
        [Serializable]
        sealed class VisualEntry
        {
            public string visualId;
            public Sprite sprite;
        }

        [SerializeField] Material spriteMaterial;
        [SerializeField] int sortingOrder;
        [Tooltip("Radius (units) of the placeholder dot drawn when the gem visual has no sprite. Presentation only.")]
        [SerializeField, Min(0f)] float placeholderRadius = 0.09f;
        [Tooltip("Gems worth at least this much experience are drawn big (an elite's gem). 0 = never. Presentation only.")]
        [SerializeField, Min(0)] int bigGemValue = 10;
        [Tooltip("Radius (units) of the big placeholder dot.")]
        [SerializeField, Min(0f)] float bigPlaceholderRadius = 0.16f;
        [Tooltip("Sprites by visual id. An id with no sprite here is drawn as a solid placeholder dot.")]
        [SerializeField] List<VisualEntry> visuals = new List<VisualEntry>();
        [Tooltip("On: the placeholder dot uses Placeholder Color (coins). Off: a colour from the visual id (gems). Presentation only.")]
        [SerializeField] bool usePlaceholderColor;
        [SerializeField] Color placeholderColor = Color.white;

        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        readonly List<Transform> transforms = new List<Transform>();
        readonly Dictionary<string, Sprite> placeholders = new Dictionary<string, Sprite>();
        readonly List<bool> shownBig = new List<bool>();
        Sprite gemSprite;
        Sprite bigGemSprite;
        int shown;

        /// <summary>지난 Render에서 크게 그린 젬 수(확인용).</summary>
        public int BigShownCount { get; private set; }

        public int PooledCount => renderers.Count;

        /// <summary>판을 시작할 때: 렌더러를 capacity개까지 미리 만들고 젬 그림을 정한다.</summary>
        public void Prepare(int capacity, string visualId)
        {
            gemSprite = SpriteFor(visualId ?? string.Empty, false);
            bigGemSprite = SpriteFor(visualId ?? string.Empty, true);
            while (renderers.Count < capacity)
            {
                var go = new GameObject("Gem" + renderers.Count);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                if (spriteMaterial != null) r.sharedMaterial = spriteMaterial;
                r.sortingOrder = sortingOrder;
                r.enabled = false;
                renderers.Add(r);
                transforms.Add(go.transform);
                shownBig.Add(false);
            }
            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].sprite = gemSprite;
                renderers[i].enabled = false;
                shownBig[i] = false;
            }
            shown = 0;
        }

        /// <param name="alpha">직전 틱(0)과 지금 틱(1) 사이 어디를 그릴지.</param>
        /// <param name="alpha">직전 틱(0)과 지금 틱(1) 사이 어디를 그릴지.</param>
        public void Render(GemState[] gems, int count, float alpha)
        {
            int n = Math.Min(count, renderers.Count);
            float z = transform.position.z;
            int bigCount = 0;
            for (int i = 0; i < n; i++)
            {
                ref readonly GemState g = ref gems[i];
                bool big = bigGemValue > 0 && g.Value >= bigGemValue;
                if (shownBig[i] != big)
                {
                    renderers[i].sprite = big ? bigGemSprite : gemSprite;
                    shownBig[i] = big;
                }
                if (big) bigCount++;
                Vec2 p = Vec2.Lerp(g.PreviousPosition, g.Position, alpha);
                transforms[i].position = PixelArt.Snap(p.X, p.Y, z);
                if (!renderers[i].enabled) renderers[i].enabled = true;
            }
            for (int i = n; i < shown; i++) renderers[i].enabled = false;
            shown = n;
            BigShownCount = bigCount;
        }

        Sprite SpriteFor(string visualId, bool big)
        {
            foreach (var entry in visuals)
            {
                if (entry.visualId == visualId && entry.sprite != null) return entry.sprite;
            }
            string key = (big ? "big:" : "") + visualId;
            if (!placeholders.TryGetValue(key, out Sprite sprite))
            {
                float radius = big ? bigPlaceholderRadius : placeholderRadius;
                sprite = usePlaceholderColor ? PlaceholderShapes.ColorDot(visualId, radius, (Color32)placeholderColor) : PlaceholderShapes.GemDot(visualId, radius);
                placeholders.Add(key, sprite);
            }
            return sprite;
        }

        void OnDestroy()
        {
            PlaceholderShapes.DestroyAll(placeholders);
        }
    }
}
