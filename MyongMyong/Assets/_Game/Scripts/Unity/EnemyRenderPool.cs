using System;
using System.Collections.Generic;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 적 그리기 한 곳. SpriteRenderer를 동시 최대 수만큼 미리 만들어 두고 재사용한다(틱 · 프레임마다 만들거나 지우지 않음).
    /// 그리는 것만 한다: 보간 + 도트 격자 맞춤, 종류의 비주얼, 맞은 직후 흰색 번쩍임. 나중에 GPU 인스턴싱으로 바꿔도 이 클래스만 바뀐다.
    /// </summary>
    public sealed class EnemyRenderPool : MonoBehaviour
    {
        [Serializable]
        sealed class VisualEntry
        {
            public string visualId;
            public Sprite sprite;
            [Tooltip("White silhouette shown right after a hit. Empty: no flash for this sprite.")]
            public Sprite flashSprite;
            [Tooltip("With no sprite: colour of the placeholder disc. Alpha 0 = colour picked from the visual id.")]
            public Color placeholderColor = new Color(0f, 0f, 0f, 0f);
        }

        [SerializeField] Material spriteMaterial;
        [SerializeField] int sortingOrder;
        [Tooltip("Ticks an enemy stays white after a bullet hit (presentation only).")]
        [SerializeField, Min(0)] int hitFlashTicks = 2;
        [Tooltip("Sprites by visual id. Ids with no sprite here are drawn as a solid placeholder shape.")]
        [SerializeField] List<VisualEntry> visuals = new List<VisualEntry>();

        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        readonly List<Transform> transforms = new List<Transform>();
        readonly List<int> shownLook = new List<int>();
        readonly Dictionary<string, Sprite> placeholders = new Dictionary<string, Sprite>();
        Sprite[] kindSprites = new Sprite[0];
        Sprite[] kindFlashSprites = new Sprite[0];
        int shown;

        public int PooledCount => renderers.Count;

        /// <summary>지난 Render에서 흰색으로 그린 적 수(확인용).</summary>
        public int FlashingCount { get; private set; }

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
                shownLook.Add(-1);
            }

            int kinds = table != null ? table.Rows.Count : 0;
            kindSprites = new Sprite[kinds];
            kindFlashSprites = new Sprite[kinds];
            for (int k = 0; k < kinds; k++)
            {
                kindSprites[k] = SpriteFor(table.Rows[k], false);
                kindFlashSprites[k] = SpriteFor(table.Rows[k], true);
            }
            for (int i = 0; i < renderers.Count; i++)
            {
                shownLook[i] = -1;
                renderers[i].enabled = false;
            }
            shown = 0;
        }

        /// <param name="alpha">직전 틱(0)과 지금 틱(1) 사이 어디를 그릴지.</param>
        /// <param name="tick">World.Tick(맞은 뒤 몇 틱이 지났는지 보는 데 쓴다).</param>
        public void Render(EnemyState[] enemies, int count, float alpha, long tick)
        {
            int n = Math.Min(count, renderers.Count);
            float z = transform.position.z;
            int flashing = 0;
            for (int i = 0; i < n; i++)
            {
                ref readonly EnemyState e = ref enemies[i];
                SpriteRenderer r = renderers[i];
                bool validKind = e.Kind >= 0 && e.Kind < kindSprites.Length;
                bool flash = validKind && tick - e.LastHitTick <= hitFlashTicks && kindFlashSprites[e.Kind] != null;
                int look = e.Kind * 2 + (flash ? 1 : 0);
                if (shownLook[i] != look)
                {
                    r.sprite = validKind ? (flash ? kindFlashSprites[e.Kind] : kindSprites[e.Kind]) : null;
                    shownLook[i] = look;
                }
                if (flash) flashing++;
                Vec2 p = Vec2.Lerp(e.PreviousPosition, e.Position, alpha);
                transforms[i].position = PixelArt.Snap(p.X, p.Y, z);
                if (!r.enabled) r.enabled = true;
            }
            FlashingCount = flashing;
            HideFrom(n);
        }

        void HideFrom(int start)
        {
            for (int i = start; i < shown; i++) renderers[i].enabled = false;
            shown = start;
        }

        Sprite SpriteFor(EnemyTable.Row row, bool flash)
        {
            Color? color = null;
            foreach (var entry in visuals)
            {
                if (entry.visualId != row.visualId) continue;
                if (entry.sprite != null) return flash ? entry.flashSprite : entry.sprite;
                if (entry.placeholderColor.a > 0f) color = entry.placeholderColor;
            }
            // 같은 비주얼이라도 반지름이 다르면 다른 그림(크기로 구분).
            string key = (flash ? "flash:" : "") + (row.visualId ?? string.Empty) + "@" + row.radius.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (!placeholders.TryGetValue(key, out Sprite sprite))
            {
                sprite = PlaceholderShapes.Disc(row.visualId ?? string.Empty, row.radius, flash, color);
                placeholders.Add(key, sprite);
            }
            return sprite;
        }

        void OnDestroy()
        {
            PlaceholderShapes.DestroyAll(placeholders);
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
        // 탄 도트: 밝은 노랑~주황(초록 바닥 · 분홍 적과 구분되게). 색상은 ID에서 그 사이로.
        const float DotHueStart = 0.1f;
        const float DotHueSpan = 0.07f;
        const float DotSaturation = 0.6f;
        const float DotValue = 1f;
        const int MinDotDiameter = 2;
        // 경험치 젬 도트: 하늘~파랑(노란 탄 · 분홍 적 · 초록 바닥과 구분되게).
        const float GemHueStart = 0.52f;
        const float GemHueSpan = 0.06f;
        const float GemSaturation = 0.8f;
        static readonly Color32 Eye = new Color32(0x20, 0x1A, 0x24, 0xFF);
        static readonly Color32 Flash = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        /// <summary>반지름(유닛)만 한 원. 1도트 어두운 테두리 + 눈 두 개(앞이 어디인지 보이게). flash면 같은 모양을 온통 흰색으로.</summary>
        public static Sprite Disc(string visualId, float radiusUnits, bool flash = false, Color? color = null)
        {
            int radiusDots = Mathf.Max(MinDiameter / 2, Mathf.RoundToInt(radiusUnits * PixelArt.PixelsPerUnit));
            int diameter = radiusDots * 2;
            int size = Mathf.Max(Unit, Mathf.CeilToInt(diameter / (float)Unit) * Unit);
            float center = size / 2f;

            Color fill = color ?? Color.HSVToRGB(Hue(visualId, HueStart, HueSpan), Saturation, Value);
            fill.a = 1f;
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
                    pixels[y * size + x] = flash ? Flash : (d > radiusDots - 1f ? (Color32)outline : (Color32)fill);
                }
            }

            if (!flash)
            {
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
            }

            return ToSprite(pixels, size, (flash ? "PlaceholderFlash_" : "Placeholder_") + visualId);
        }

        /// <summary>반지름(유닛)만 한 단색 도트(탄). 테두리 없음.</summary>
        public static Sprite Dot(string visualId, float radiusUnits)
        {
            return SolidDot(visualId, radiusUnits, DotHueStart, DotHueSpan, DotSaturation, "PlaceholderDot_");
        }

        /// <summary>반지름(유닛)만 한 단색 도트(경험치 젬). 탄과 같은 모양, 색만 하늘~파랑.</summary>
        public static Sprite GemDot(string visualId, float radiusUnits)
        {
            return SolidDot(visualId, radiusUnits, GemHueStart, GemHueSpan, GemSaturation, "PlaceholderGem_");
        }

        static Sprite SolidDot(string visualId, float radiusUnits, float hueStart, float hueSpan, float saturation, string namePrefix)
        {
            int diameter = Mathf.Max(MinDotDiameter, Mathf.RoundToInt(radiusUnits * 2f * PixelArt.PixelsPerUnit));
            // 짝수 크기로 만들어 가운데 피벗이 도트 경계에 오게 한다(격자 맞춤 때 번지지 않게).
            if (diameter % 2 == 1) diameter++;
            int size = diameter;
            float center = size / 2f;
            float radius = size / 2f;
            Color32 fill = Color.HSVToRGB(Hue(visualId, hueStart, hueSpan), saturation, DotValue);

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    if (dx * dx + dy * dy <= radius * radius) pixels[y * size + x] = fill;
                }
            }
            return ToSprite(pixels, size, namePrefix + visualId);
        }

        /// <summary>만든 스프라이트와 텍스처를 지운다(풀의 OnDestroy에서).</summary>
        public static void DestroyAll(Dictionary<string, Sprite> sprites)
        {
            foreach (var sprite in sprites.Values)
            {
                if (sprite == null) continue;
                UnityEngine.Object.Destroy(sprite.texture);
                UnityEngine.Object.Destroy(sprite);
            }
            sprites.Clear();
        }

        static Sprite ToSprite(Color32[] pixels, int size, string name)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelArt.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        // FNV-1a — 실행마다 바뀌지 않는 해시. 색상은 start부터 span만큼만.
        static float Hue(string id, float start, float span)
        {
            uint hash = 2166136261u;
            foreach (char ch in id ?? string.Empty)
            {
                hash ^= ch;
                hash *= 16777619u;
            }
            return (start + (hash % 1000u) / 1000f * span) % 1f;
        }
    }
}
