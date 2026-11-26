using System;
using System.Collections.Generic;
using Myong.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Myong.Unity
{
    /// <summary>
    /// 강화 화면(uGUI, 1-12): 제목 · 지갑 · 영구 강화 줄마다 이름 · 설명 · 단계 · 다음 가격 · [사기] · [모두 되돌리기] · [닫기].
    /// 보여 주기만 하고, 누르면 BuyPressed · RefundPressed · ClosePressed로 알린다(사기 · 되돌리기 · 저장은 GameRoot → Core MetaRules).
    /// 줄은 본(rowTemplate)을 표 줄 수만큼 복제한다 — 새 영구 강화 = 표 한 줄. 코인이 모자라거나 최대 단계면 [사기]를 막는다.
    /// </summary>
    public sealed class ShopView : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] UnityEngine.UI.Text title;
        [SerializeField] UnityEngine.UI.Text wallet;
        [SerializeField] RectTransform rowsRoot;
        [SerializeField] ShopRowView rowTemplate;
        [Tooltip("Height of one row in HUD units (layout only).")]
        [SerializeField, Min(1f)] float rowHeight = 46f;
        [SerializeField] UnityEngine.UI.Button refundButton;
        [SerializeField] UnityEngine.UI.Text refundLabel;
        [SerializeField] UnityEngine.UI.Button closeButton;
        [SerializeField] UnityEngine.UI.Text closeLabel;
        [Tooltip("Label colour of a button that cannot be pressed (not enough coins, max stage, nothing to refund). Presentation only.")]
        [SerializeField] Color disabledLabelColor = new Color(0.5f, 0.5f, 0.55f, 1f);
        [SerializeField] Color enabledLabelColor = Color.white;

        readonly List<ShopRowView> rows = new List<ShopRowView>();
        bool dirty = true;

        /// <summary>[사기]를 눌렀다(영구 강화 표 줄 번호).</summary>
        public event Action<int> BuyPressed;

        /// <summary>[모두 되돌리기]를 눌렀다.</summary>
        public event Action RefundPressed;

        /// <summary>[닫기]를 눌렀다.</summary>
        public event Action ClosePressed;

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>확인용: 줄 수 · 줄의 [사기] 버튼 · 지금 보이는 지갑 글.</summary>
        public int RowCount => rows.Count;

        public UnityEngine.UI.Button BuyButton(int row) => row >= 0 && row < rows.Count ? rows[row].buyButton : null;

        public UnityEngine.UI.Button RefundButton => refundButton;

        public UnityEngine.UI.Button CloseButton => closeButton;

        public string ShownWallet => wallet != null ? wallet.text : string.Empty;

        public string ShownPrice(int row) => row >= 0 && row < rows.Count && rows[row].price != null ? rows[row].price.text : string.Empty;

        public string ShownStage(int row) => row >= 0 && row < rows.Count && rows[row].stage != null ? rows[row].stage.text : string.Empty;

        void Awake()
        {
            if (refundButton != null) refundButton.onClick.AddListener(() => RefundPressed?.Invoke());
            if (closeButton != null) closeButton.onClick.AddListener(() => ClosePressed?.Invoke());
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (panel != null) panel.SetActive(false);
        }

        void OnEnable()
        {
            SetFont(title);
            SetFont(wallet);
            SetFont(refundLabel);
            SetFont(closeLabel);
            foreach (var row in rows) SetRowFonts(row);
            Text.Changed += MarkDirty;
        }

        void OnDisable()
        {
            Text.Changed -= MarkDirty;
        }

        /// <summary>다음 Render에서 글을 다시 채운다(사기 · 되돌리기 뒤).</summary>
        public void MarkDirty()
        {
            dirty = true;
        }

        static void SetFont(UnityEngine.UI.Text label)
        {
            if (label != null) label.font = UiFont.Get(label.fontSize);
        }

        static void SetRowFonts(ShopRowView row)
        {
            SetFont(row.title);
            SetFont(row.description);
            SetFont(row.stage);
            SetFont(row.price);
            SetFont(row.buyLabel);
        }

        /// <summary>화면을 연다(첫 [사기] 또는 [닫기]가 고른 상태 — 패드 · 키보드가 바로 된다).</summary>
        public void Open()
        {
            if (panel == null) return;
            panel.SetActive(true);
            dirty = true;
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>열려 있으면 지갑 · 줄을 채운다(바뀐 때만).</summary>
        public void Render(MetaProgress progress, PermanentRulesData rules, PermanentUpgradeTable table)
        {
            if (!IsOpen || progress == null || rules == null || table == null) return;
            bool built = EnsureRows(table.Rows.Count);
            if (!dirty && !built) return;

            if (title != null) title.text = Text.Get("ui.shop.title");
            if (wallet != null) wallet.text = Text.Get("ui.shop.wallet", progress.Coins);
            bool anyBought = false;
            int shown = Math.Min(rows.Count, table.Rows.Count);
            for (int i = 0; i < shown; i++)
            {
                ShopRowView view = rows[i];
                PermanentUpgradeTable.Row data = table.Rows[i];
                int stage = i < progress.Stages.Length ? progress.Stages[i] : 0;
                int max = i < rules.Rows.Length ? rules.Rows[i].MaxStage : data.MaxStage;
                long price = MetaRules.PriceOfNext(rules, progress, i);
                if (stage > 0) anyBought = true;
                if (view.title != null) view.title.text = Text.Get(data.nameKey);
                if (view.description != null) view.description.text = Text.Get(data.descriptionKey, LevelUpView.FormatValue(data.op, data.value));
                if (view.stage != null) view.stage.text = Text.Get("ui.shop.stage", stage, max);
                if (view.price != null) view.price.text = price < 0 ? Text.Get("ui.shop.maxed") : Text.Get("ui.shop.price", price);
                if (view.buyLabel != null) view.buyLabel.text = Text.Get("ui.shop.buy");
                bool canBuy = MetaRules.CanBuy(rules, progress, i);
                if (view.buyButton != null) view.buyButton.interactable = canBuy;
                if (view.buyLabel != null) view.buyLabel.color = canBuy ? enabledLabelColor : disabledLabelColor;
            }
            if (refundLabel != null) refundLabel.text = Text.Get("ui.shop.refund");
            if (refundButton != null) refundButton.interactable = anyBought;
            if (refundLabel != null) refundLabel.color = anyBought ? enabledLabelColor : disabledLabelColor;
            if (closeLabel != null) closeLabel.text = Text.Get("ui.shop.close");

            // 막힌 버튼이 골라져 있으면 패드가 갈 곳을 잃는다 — 살 수 있는 첫 줄, 없으면 [닫기]를 고른다.
            if (EventSystem.current != null)
            {
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                var selectable = selected != null ? selected.GetComponent<UnityEngine.UI.Selectable>() : null;
                bool selectionOk = selectable != null && selectable.interactable && selected.activeInHierarchy && selected.transform.IsChildOf(panel.transform);
                if (!selectionOk) EventSystem.current.SetSelectedGameObject(FirstSelectable());
            }
            dirty = false;
        }

        GameObject FirstSelectable()
        {
            foreach (var row in rows)
            {
                if (row.buyButton != null && row.buyButton.interactable) return row.buyButton.gameObject;
            }
            return closeButton != null ? closeButton.gameObject : null;
        }

        // 본을 복제해 줄을 count개로 맞춘다(늘 때만 만든다, 남는 줄은 끈다). 새로 만들었으면 true.
        bool EnsureRows(int count)
        {
            if (rowTemplate == null || rowsRoot == null) return false;
            bool built = false;
            while (rows.Count < count)
            {
                int index = rows.Count;
                ShopRowView row = Instantiate(rowTemplate, rowsRoot);
                row.name = "Row" + (index + 1);
                var rect = (RectTransform)row.transform;
                rect.anchoredPosition = new Vector2(0f, -index * rowHeight);
                row.gameObject.SetActive(true);
                SetRowFonts(row);
                if (row.buyButton != null) row.buyButton.onClick.AddListener(() => BuyPressed?.Invoke(index));
                rows.Add(row);
                built = true;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < count;
                if (rows[i].gameObject.activeSelf != used) rows[i].gameObject.SetActive(used);
            }
            return built;
        }
    }
}
