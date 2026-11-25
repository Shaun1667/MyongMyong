using System;
using System.Globalization;
using Myong.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Myong.Unity
{
    /// <summary>
    /// 레벨업 창(uGUI): 카드마다 이름 · 설명 · 단계. 보여 주기만 하고, 고른 카드 번호를 Chosen으로 알린다(적용은 Core).
    /// 마우스 클릭 · 패드(이동 + 확인)는 Input System UI 모듈이 버튼으로 넘기고, 숫자키는 PlayerInputReader가 읽어 GameRoot가 넘긴다.
    /// </summary>
    public sealed class LevelUpView : MonoBehaviour
    {
        [Serializable]
        sealed class Card
        {
            public UnityEngine.UI.Button button;
            public UnityEngine.UI.Text title;
            public UnityEngine.UI.Text description;
            public UnityEngine.UI.Text stage;
            public UnityEngine.UI.Text key;
        }

        [SerializeField] GameObject panel;
        [SerializeField] UnityEngine.UI.Text title;
        [SerializeField] UnityEngine.UI.Text hint;
        [SerializeField] UnityEngine.UI.Text remaining;
        [SerializeField] Card[] cards = new Card[0];

        /// <summary>카드를 눌렀다(0부터).</summary>
        public event Action<int> Chosen;

        int[] shownRows = new int[0];
        int shownCount = -1;
        int shownPending = -1;
        bool textDirty = true;

        public bool IsOpen => panel != null && panel.activeSelf;

        void Awake()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                if (cards[i].button != null) cards[i].button.onClick.AddListener(() => Chosen?.Invoke(index));
            }
            shownRows = new int[cards.Length];
            if (panel != null) panel.SetActive(false);
        }

        void OnEnable()
        {
            SetFont(title);
            SetFont(hint);
            SetFont(remaining);
            foreach (var card in cards)
            {
                SetFont(card.title);
                SetFont(card.description);
                SetFont(card.stage);
                SetFont(card.key);
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

        static void SetFont(UnityEngine.UI.Text label)
        {
            if (label != null) label.font = UiFont.Get(label.fontSize);
        }

        /// <param name="choices">강화 표 줄 번호(앞에서 count개).</param>
        /// <param name="stages">강화 표 줄마다 지금 단계.</param>
        /// <param name="pending">남은 레벨업 수(지금 것 포함). 0이면 창을 닫는다.</param>
        public void Render(int pending, int[] choices, int count, int[] stages, UpgradeTable table)
        {
            bool open = pending > 0 && count > 0 && table != null;
            if (!open)
            {
                if (panel != null && panel.activeSelf) panel.SetActive(false);
                shownCount = -1;
                return;
            }

            bool changed = textDirty || count != shownCount || pending != shownPending;
            for (int i = 0; i < count && i < shownRows.Length && !changed; i++) changed = shownRows[i] != choices[i];
            bool opening = panel != null && !panel.activeSelf;
            if (opening) panel.SetActive(true);
            if (!changed && !opening) return;

            if (title != null) title.text = Text.Get("ui.levelup.title");
            if (hint != null) hint.text = Text.Get("ui.levelup.hint");
            if (remaining != null)
            {
                remaining.text = pending > 1 ? Text.Get("ui.levelup.remaining", pending - 1) : string.Empty;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                Card card = cards[i];
                bool used = i < count;
                if (card.button != null) card.button.gameObject.SetActive(used);
                if (!used) continue;
                int row = choices[i];
                UpgradeTable.Row data = table.Rows[row];
                int stage = row < stages.Length ? stages[row] : 0;
                if (card.title != null) card.title.text = Text.Get(data.nameKey);
                if (card.description != null) card.description.text = Text.Get(data.descriptionKey, FormatValue(data.op, data.value));
                if (card.stage != null) card.stage.text = Text.Get("ui.levelup.stage", stage + 1, data.maxStage);
                if (card.key != null) card.key.text = Text.Get("ui.levelup.key", i + 1);
                if (i < shownRows.Length) shownRows[i] = row;
            }
            shownCount = count;
            shownPending = pending;
            textDirty = false;

            // 패드 · 키보드 이동이 바로 되도록 첫 카드를 고른 상태로 둔다(새 선택지가 뜰 때마다).
            if (EventSystem.current != null && cards.Length > 0 && cards[0].button != null)
            {
                EventSystem.current.SetSelectedGameObject(cards[0].button.gameObject);
            }
        }

        /// <summary>한 단계 값을 설명의 {0}에 넣을 모양으로: 더하기 = +1, 곱하기 = -12% / +25%.</summary>
        public static string FormatValue(StatOp op, float value)
        {
            if (op == StatOp.Multiply)
            {
                float percent = (value - 1f) * 100f;
                return percent.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture) + "%";
            }
            return value.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);
        }
    }
}
