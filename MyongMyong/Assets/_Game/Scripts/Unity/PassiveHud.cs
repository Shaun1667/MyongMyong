using System;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// HUD의 가진 패시브 아이콘 줄(얻은 순서대로)과 단계. 그리기만 한다(값은 Core의 World).
    /// 그림 ID에 맞는 그림이 없으면 단색 네모(색은 그림 ID에서) + 짧은 글(텍스트 표).
    /// </summary>
    public sealed class PassiveHud : MonoBehaviour
    {
        [Serializable]
        sealed class Slot
        {
            public GameObject root;
            public UnityEngine.UI.Image background;
            public UnityEngine.UI.Image icon;
            public UnityEngine.UI.Text letter;
            public UnityEngine.UI.Text stage;
        }

        [Serializable]
        sealed class IconPicture
        {
            public string iconId;
            public Sprite sprite;
        }

        [SerializeField] Slot[] slots = new Slot[0];
        [Tooltip("Pictures by icon id. Ids without one are drawn as a plain coloured square with the short text.")]
        [SerializeField] IconPicture[] pictures = new IconPicture[0];

        const float Saturation = 0.55f;
        const float Value = 0.75f;

        int[] shownRows = new int[0];
        int[] shownStages = new int[0];
        int shownCount = -1;
        bool textDirty = true;

        /// <summary>지금 보이는 아이콘 수(확인용).</summary>
        public int ShownCount => Math.Max(0, shownCount);

        /// <summary>i번째 아이콘의 짧은 글 · 단계 글(확인용).</summary>
        public string ShownLetter(int i) => i >= 0 && i < slots.Length && slots[i].letter != null ? slots[i].letter.text : string.Empty;

        public string ShownStage(int i) => i >= 0 && i < slots.Length && slots[i].stage != null ? slots[i].stage.text : string.Empty;

        void Awake()
        {
            shownRows = new int[slots.Length];
            shownStages = new int[slots.Length];
        }

        void OnEnable()
        {
            foreach (var slot in slots)
            {
                if (slot.letter != null) slot.letter.font = UiFont.Get(slot.letter.fontSize);
                if (slot.stage != null) slot.stage.font = UiFont.Get(slot.stage.fontSize);
            }
            Text.Changed += OnTextChanged;
        }

        void OnDisable()
        {
            Text.Changed -= OnTextChanged;
        }

        void OnTextChanged()
        {
            textDirty = true;
        }

        /// <param name="owned">가진 패시브(패시브 표 줄 번호, 얻은 순서). 앞에서 count개.</param>
        /// <param name="stages">패시브 표 줄마다 단계.</param>
        public void Render(int[] owned, int count, int[] stages, PassiveTable table)
        {
            if (table == null) count = 0;
            bool changed = textDirty || count != shownCount;
            for (int i = 0; i < count && i < slots.Length && !changed; i++)
            {
                int row = owned[i];
                changed = shownRows[i] != row || shownStages[i] != stages[row];
            }
            if (!changed) return;

            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                bool used = i < count;
                if (slot.root != null && slot.root.activeSelf != used) slot.root.SetActive(used);
                if (!used) continue;
                int row = owned[i];
                PassiveTable.Row data = table.Rows[row];
                Sprite picture = PictureOf(data.iconId);
                if (slot.icon != null)
                {
                    slot.icon.sprite = picture;
                    slot.icon.enabled = picture != null;
                }
                if (slot.background != null) slot.background.color = PlaceholderColor(data.iconId);
                if (slot.letter != null) slot.letter.text = picture == null && !string.IsNullOrEmpty(data.shortKey) ? Text.Get(data.shortKey) : string.Empty;
                if (slot.stage != null) slot.stage.text = Text.Get("ui.hud.passive.stage", stages[row]);
                shownRows[i] = row;
                shownStages[i] = stages[row];
            }
            shownCount = count;
            textDirty = false;
        }

        Sprite PictureOf(string iconId)
        {
            if (string.IsNullOrEmpty(iconId)) return null;
            foreach (var p in pictures)
            {
                if (p != null && p.iconId == iconId) return p.sprite;
            }
            return null;
        }

        /// <summary>그림이 없을 때의 단색(그림 ID에서 정해짐 — 같은 ID = 같은 색).</summary>
        public static Color PlaceholderColor(string iconId)
        {
            uint hash = 2166136261;
            foreach (char c in iconId ?? string.Empty)
            {
                hash ^= c;
                hash *= 16777619;
            }
            Color color = Color.HSVToRGB((hash % 360u) / 360f, Saturation, Value);
            color.a = 1f;
            return color;
        }
    }
}
