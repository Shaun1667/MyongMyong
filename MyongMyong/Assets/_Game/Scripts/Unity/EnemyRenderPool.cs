using System;
using System.Collections.Generic;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 적 그리기 한 곳. SpriteRenderer를 동시 최대 수만큼 미리 만들어 두고 재사용한다(틱 · 프레임마다 만들거나 지우지 않음).
    /// 그리는 것만 한다: 보간 + 도트 격자 맞춤, 종류의 비주얼. 나중에 GPU 인스턴싱으로 바꿔도 이 클래스만 바뀐다.
    /// </summary>
    public sealed class EnemyRenderPool : MonoBehaviour
    {
        [Serializable]
        sealed class VisualEntry
        {
            public string visualId;
            public Sprite sprite;
        }

        [SerializeField] Material spriteMaterial;
        [SerializeField] int sortingOrder;
        [Tooltip("Sprites by visual id. Ids with no sprite here are drawn as a solid placeholder shape.")]
        [SerializeField] List<VisualEntry> visuals = new List<VisualEntry>();

        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        readonly List<Transform> transforms = new List<Transform>();
        readonly List<int> shownKind = new List<int>();
        readonly Dictionary<string, Sprite> placeholders = new Dictionary<string, Sprite>();
        Sprite[] kindSprites = new Sprite[0];
        int shown;

        public int PooledCount => renderers.Count;

        /// <summary>판을 시작할 때: 렌더러를 capacity개까지 미리 만들고 종류마다 그림을 정한다.</summary>
        public void Prepare(int capacity, EnemyTable table)
        {
            while (renderers.Count < capacity)
            {
                var go = new GameObject("Enemy" + renderers.Count);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                if (spriteMaterial != null) r.sharedMaterial = spriteMaterial;
                r.sortingOrder = sortingOrder;
                r.enabled = false;
                renderers.Add(r);
                transforms.Add(go.transform);
                shownKind.Add(-1);
            }

            int kinds = table != null ? table.Rows.Count : 0;
            kindSprites = new Sprite[kinds];
            for (int k = 0; k < kinds; k++) kindSprites[k] = SpriteFor(table.Rows[k]);
            for (int i = 0; i < renderers.Count; i++)
            {
                shownKind[i] = -1;
                renderers[i].enabled = false;
            }
            shown = 0;
        }

        /// <param name="alpha">직전 틱(0)과 지금 틱(1) 사이 어디를 그릴지.</param>
        public void Render(EnemyState[] enemies, int count, float alpha)
        {
            int n = Math.Min(count, renderers.Count);
            float z = transform.position.z;
            for (int i = 0; i < n; i++)
            {
                ref readonly EnemyState e = ref enemies[i];
                SpriteRenderer r = renderers[i];
                if (shownKind[i] != e.Kind)
                {
                    r.sprite = e.Kind >= 0 && e.Kind < kindSprites.Length ? kindSprites[e.Kind] : null;
                    shownKind[i] = e.Kind;
                }
                Vec2 p = Vec2.Lerp(e.PreviousPosition, e.Position, alpha);
                transforms[i].position = PixelArt.Snap(p.X, p.Y, z);
                if (!r.enabled) r.enabled = true;
            }
            HideFrom(n);
        }

        void HideFrom(int start)
        {
            for (int i = start; i < shown; i++) renderers[i].enabled = false;
            shown = start;
        }

        Sprite SpriteFor(EnemyTable.Row row)
        {
            foreach (var entry in visuals)
            {
                if (entry.visualId == row.visualId && entry.sprite != null) return entry.sprite;
            }
            string key = row.visualId ?? string.Empty;
            if (!placeholders.TryGetValue(key, out Sprite sprite))
            {
                sprite = PlaceholderShapes.Disc(key, row.radius);
                placeholders.Add(key, sprite);
            }
            return sprite;
        }

        void OnDestroy()
        {
            foreach (var sprite in placeholders.Values)
            {
                if (sprite == null) continue;
                Destroy(sprite.texture);
                Destroy(sprite);
            }
            placeholders.Clear();
        }
    }

    /// <summary>
    /// 그림이 없는 비주얼 ID를 단색 도형 도트로 그린다(코드가 텍스처를 만든다, 32도트 단위).
    /// 색은 ID에서 정해진다 — 같은 ID = 같은 색.
    /// </summary>
    public static class PlaceholderShapes
    {
        const int Unit = 32;
        const int MinDiameter = 4;
        const float Saturation = 0.6f;
        const float HueStart = 0.75f;
        const float HueSpan = 0.42f;
        const float Value = 0.9f;
        const float OutlineShade = 0.55f;
        static readonly Color32 Eye = new Color32(0x20, 0x1A, 0x24, 0xFF);

        /// <summary>반지름(유닛)만 한 원. 1도트 어두운 테두리 + 눈 두 개(앞이 어디인지 보이게).</summary>
        public static Sprite Disc(string visualId, float radiusUnits)
        {
            int radiusDots = Mathf.Max(MinDiameter / 2, Mathf.RoundToInt(radiusUnits * PixelArt.PixelsPerUnit));
            int diameter = radiusDots * 2;
            int size = Mathf.Max(Unit, Mathf.CeilToInt(diameter / (float)Unit) * Unit);
            float center = size / 2f;

            Color fill = Color.HSVToRGB(Hue(visualId), Saturation, Value);
            Color outline = fill * OutlineShade;
            outline.a = 1f;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > radiusDots) continue;
                    pixels[y * size + x] = d > radiusDots - 1f ? (Color32)outline : (Color32)fill;
                }
            }

            // 눈: 가운데에서 좌우로 radius/3, 위로 한 칸. 2x2 도트.
            int c = size / 2;
            int eyeOffset = Mathf.Max(1, radiusDots / 3);
            foreach (int ex in new[] { c - eyeOffset - 2, c + eyeOffset })
            {
                for (int y = c; y < c + 2; y++)
                {
                    for (int x = ex; x < ex + 2; x++)
                    {
                        if (x >= 0 && x < size && y >= 0 && y < size) pixels[y * size + x] = Eye;
                    }
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Placeholder_" + visualId,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelArt.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            return sprite;
        }

        // FNV-1a — 실행마다 바뀌지 않는 해시. 색상은 보라 → 빨강 → 주황 → 노랑 사이만(초록 바닥에서 도드라지게).
        static float Hue(string id)
        {
            uint hash = 2166136261u;
            foreach (char ch in id ?? string.Empty)
            {
                hash ^= ch;
                hash *= 16777619u;
            }
            return (HueStart + (hash % 1000u) / 1000f * HueSpan) % 1f;
        }
    }
}
