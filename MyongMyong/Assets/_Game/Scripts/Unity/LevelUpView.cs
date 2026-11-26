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
            public UnityEngine.UI.Text kind;
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
                SetFont(card.kind);
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

        /// <param name="choices">선택지 값(앞에서 count개). describe로 강화 줄 · 패시브 줄 · 대체 보상으로 푼다.</param>
        /// <param name="upgradeStages">강화 표 줄마다 지금 단계.</param>
        /// <param name="passiveStages">패시브 표 줄마다 지금 단계(0 = 안 가짐).</param>
        /// <param name="pending">남은 레벨업 수(지금 것 포함). 0이면 창을 닫는다.</param>
        public void Render(int pending, int[] choices, int count, Func<int, LevelUpOption> describe, int[] upgradeStages, UpgradeTable table, int[] passiveStages, PassiveTable passiveTable)
        {
            bool open = pending > 0 && count > 0 && table != null && describe != null;
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
                int option = choices[i];
                LevelUpOption o = describe(option);
                if (o.Kind == LevelUpOptionKind.Upgrade) ShowUpgrade(card, table.Rows[o.Row], o.Row < upgradeStages.Length ? upgradeStages[o.Row] : 0);
                else if (o.Kind == LevelUpOptionKind.Passive && passiveTable != null) ShowPassive(card, passiveTable.Rows[o.Row], o.Row < passiveStages.Length ? passiveStages[o.Row] : 0);
                else ShowFallback(card, passiveTable);
                if (card.key != null) card.key.text = Text.Get("ui.levelup.key", i + 1);
                if (i < shownRows.Length) shownRows[i] = option;
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

        static void ShowUpgrade(Card card, UpgradeTable.Row data, int stage)
        {
            if (card.title != null) card.title.text = Text.Get(data.nameKey);
            if (card.description != null) card.description.text = Text.Get(data.descriptionKey, FormatValue(data.op, data.value));
            if (card.stage != null) card.stage.text = Text.Get("ui.levelup.stage", stage + 1, data.maxStage);
            if (card.kind != null) card.kind.text = Text.Get("ui.levelup.kind.upgrade");
        }

        // 패시브 카드: 이름 · 설명(효과마다 한 단계 값이 {0}, {1}…) · 고른 뒤의 단계 · 새 것인지.
        static void ShowPassive(Card card, PassiveTable.Row data, int stage)
        {
            int n = data.effects != null ? data.effects.Count : 0;
            var values = new object[n];
            for (int e = 0; e < n; e++) values[e] = FormatValue(data.effects[e].op, data.effects[e].value);
            if (card.title != null) card.title.text = Text.Get(data.nameKey);
            if (card.description != null) card.description.text = Text.Get(data.descriptionKey, values);
            if (card.stage != null) card.stage.text = Text.Get("ui.levelup.stage", stage + 1, data.maxStage);
            if (card.kind != null) card.kind.text = stage == 0 ? Text.Get("ui.levelup.kind.passive_new") : Text.Get("ui.levelup.kind.passive");
        }

        // 대체 보상 카드(고를 것이 다 떨어졌을 때): 이름 · 설명({0} = 회복량), 단계 없음.
        static void ShowFallback(Card card, PassiveTable passiveTable)
        {
            string nameKey = passiveTable != null ? passiveTable.FallbackNameKey : string.Empty;
            string descriptionKey = passiveTable != null ? passiveTable.FallbackDescriptionKey : string.Empty;
            float heal = passiveTable != null ? passiveTable.FallbackHeal : 0f;
            if (card.title != null) card.title.text = string.IsNullOrEmpty(nameKey) ? string.Empty : Text.Get(nameKey);
            if (card.description != null) card.description.text = string.IsNullOrEmpty(descriptionKey) ? string.Empty : Text.Get(descriptionKey, heal.ToString("0.#", CultureInfo.InvariantCulture));
            if (card.stage != null) card.stage.text = string.Empty;
            if (card.kind != null) card.kind.text = Text.Get("ui.levelup.kind.reward");
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
