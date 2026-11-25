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
        [Tooltip("Sprites by visual id. An id with no sprite here is drawn as a solid placeholder dot.")]
        [SerializeField] List<VisualEntry> visuals = new List<VisualEntry>();

        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        readonly List<Transform> transforms = new List<Transform>();
        readonly Dictionary<string, Sprite> placeholders = new Dictionary<string, Sprite>();
        Sprite gemSprite;
        int shown;

        public int PooledCount => renderers.Count;

        /// <summary>판을 시작할 때: 렌더러를 capacity개까지 미리 만들고 젬 그림을 정한다.</summary>
        public void Prepare(int capacity, string visualId)
        {
            gemSprite = SpriteFor(visualId ?? string.Empty);
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
            }
            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].sprite = gemSprite;
                renderers[i].enabled = false;
            }
            shown = 0;
        }

        /// <param name="alpha">직전 틱(0)과 지금 틱(1) 사이 어디를 그릴지.</param>
        public void Render(GemState[] gems, int count, float alpha)
        {
            int n = Math.Min(count, renderers.Count);
            float z = transform.position.z;
            for (int i = 0; i < n; i++)
            {
                ref readonly GemState g = ref gems[i];
                Vec2 p = Vec2.Lerp(g.PreviousPosition, g.Position, alpha);
                transforms[i].position = PixelArt.Snap(p.X, p.Y, z);
                if (!renderers[i].enabled) renderers[i].enabled = true;
            }
            for (int i = n; i < shown; i++) renderers[i].enabled = false;
            shown = n;
        }

        Sprite SpriteFor(string visualId)
        {
            foreach (var entry in visuals)
            {
                if (entry.visualId == visualId && entry.sprite != null) return entry.sprite;
            }
            if (!placeholders.TryGetValue(visualId, out Sprite sprite))
            {
                sprite = PlaceholderShapes.GemDot(visualId, placeholderRadius);
                placeholders.Add(visualId, sprite);
            }
            return sprite;
        }

        void OnDestroy()
        {
            PlaceholderShapes.DestroyAll(placeholders);
        }
    }
}
