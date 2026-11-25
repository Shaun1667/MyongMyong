using System;
using System.Collections.Generic;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 탄 그리기 한 곳. SpriteRenderer를 탄 최대 수만큼 미리 만들어 두고 재사용한다(틱 · 프레임마다 만들거나 지우지 않음).
    /// 그리는 것만 한다: 보간 + 도트 격자 맞춤, 무기의 탄 비주얼(그림이 없으면 단색 도트).
    /// </summary>
    public sealed class BulletRenderPool : MonoBehaviour
    {
        [Serializable]
        sealed class VisualEntry
        {
            public string visualId;
            public Sprite sprite;
        }

        [SerializeField] Material spriteMaterial;
        [SerializeField] int sortingOrder;
        [Tooltip("Sprites by visual id. Ids with no sprite here are drawn as a solid placeholder dot.")]
        [SerializeField] List<VisualEntry> visuals = new List<VisualEntry>();

        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        readonly List<Transform> transforms = new List<Transform>();
        readonly List<int> shownKind = new List<int>();
        readonly Dictionary<string, Sprite> placeholders = new Dictionary<string, Sprite>();
        Sprite[] kindSprites = new Sprite[0];
        int shown;

        public int PooledCount => renderers.Count;

        /// <summary>판을 시작할 때: 렌더러를 capacity개까지 미리 만들고 무기마다 탄 그림을 정한다.</summary>
        public void Prepare(int capacity, WeaponTable table)
        {
            while (renderers.Count < capacity)
            {
                var go = new GameObject("Bullet" + renderers.Count);
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
        public void Render(BulletState[] bullets, int count, float alpha)
        {
            int n = Math.Min(count, renderers.Count);
            float z = transform.position.z;
            for (int i = 0; i < n; i++)
            {
                ref readonly BulletState b = ref bullets[i];
                SpriteRenderer r = renderers[i];
                if (shownKind[i] != b.Kind)
                {
                    r.sprite = b.Kind >= 0 && b.Kind < kindSprites.Length ? kindSprites[b.Kind] : null;
                    shownKind[i] = b.Kind;
                }
                Vec2 p = Vec2.Lerp(b.PreviousPosition, b.Position, alpha);
                transforms[i].position = PixelArt.Snap(p.X, p.Y, z);
                if (!r.enabled) r.enabled = true;
            }
            for (int i = n; i < shown; i++) renderers[i].enabled = false;
            shown = n;
        }

        Sprite SpriteFor(WeaponTable.Row row)
        {
            foreach (var entry in visuals)
            {
                if (entry.visualId == row.visualId && entry.sprite != null) return entry.sprite;
            }
            string key = (row.visualId ?? string.Empty) + ":" + row.bulletRadius.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!placeholders.TryGetValue(key, out Sprite sprite))
            {
                sprite = PlaceholderShapes.Dot(row.visualId ?? string.Empty, row.bulletRadius);
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
