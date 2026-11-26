using System;
using Myong.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Myong.Unity
{
    /// <summary>
    /// 판 끝 창(uGUI): 제목 · 생존 시간 mm:ss · 레벨 · 처치 수 · [다시 시작]. 보여 주기만 하고, 버튼을 누르면 RestartPressed로 알린다(다시 시작은 GameRoot).
    /// 마우스 클릭 · 패드(South) · Enter는 Input System UI 모듈이 버튼으로 넘긴다(창이 뜨면 버튼이 선택된 상태).
    /// 승리(30분 생존)도 같은 창에서 제목 키만 바꿔 받는다.
    /// </summary>
    public sealed class GameOverView : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] UnityEngine.UI.Text title;
        [SerializeField] UnityEngine.UI.Text time;
        [SerializeField] UnityEngine.UI.Text level;
        [SerializeField] UnityEngine.UI.Text kills;
        [SerializeField] UnityEngine.UI.Text hint;
        [SerializeField] UnityEngine.UI.Button restartButton;
        [SerializeField] UnityEngine.UI.Text restartLabel;
        [Tooltip("This run's coins and the wallet total (1-12).")]
        [SerializeField] UnityEngine.UI.Text coins;
        [SerializeField] UnityEngine.UI.Button shopButton;
        [SerializeField] UnityEngine.UI.Text shopLabel;
        [Tooltip("Title color when the run is won (display only). Defeat keeps the label's own color.")]
        [SerializeField] Color victoryTitleColor = new Color(1f, 0.86f, 0.4f, 1f);

        Color defeatTitleColor;

        /// <summary>[다시 시작]을 눌렀다.</summary>
        public event Action RestartPressed;

        /// <summary>[강화]를 눌렀다(1-12, 강화 화면을 연다).</summary>
        public event Action ShopPressed;

        long shownWallet = -1;

        bool textDirty = true;

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>[다시 시작] 버튼(확인용 · 디버그가 같은 길로 누른다).</summary>
        public UnityEngine.UI.Button RestartButton => restartButton;

        /// <summary>지금 보이는 제목 · 시간 글(확인용).</summary>
        public string ShownTitle => title != null ? title.text : string.Empty;

        public string ShownTime => time != null ? time.text : string.Empty;

        /// <summary>지금 보이는 코인 글 · [강화] 버튼(확인용).</summary>
        public string ShownCoins => coins != null ? coins.text : string.Empty;

        public UnityEngine.UI.Button ShopButton => shopButton;

        /// <summary>[다시 시작]을 고른 상태로(강화 화면을 닫은 뒤).</summary>
        public void SelectRestart()
        {
            if (EventSystem.current != null && restartButton != null) EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }

        void Awake()
        {
            if (restartButton != null) restartButton.onClick.AddListener(() => RestartPressed?.Invoke());
            if (shopButton != null) shopButton.onClick.AddListener(() => ShopPressed?.Invoke());
            if (title != null) defeatTitleColor = title.color;
            if (panel != null) panel.SetActive(false);
        }

        void OnEnable()
        {
            SetFont(title);
            SetFont(time);
            SetFont(level);
            SetFont(kills);
            SetFont(hint);
            SetFont(restartLabel);
            SetFont(coins);
            SetFont(shopLabel);
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

        /// <summary>판이 끝났으면 창을 띄우고 결과를 채운다. 진행 중이면 닫는다.</summary>
        /// <param name="survivedSeconds">생존 시간(초).</param>
        public void Render(in RunResult result, float survivedSeconds)
        {
            Render(result, survivedSeconds, -1);
        }

        /// <param name="survivedSeconds">생존 시간(초).</param>
        /// <param name="walletCoins">지갑 합계(이번 판 코인을 더한 뒤). -1이면 코인 줄을 비운다.</param>
        public void Render(in RunResult result, float survivedSeconds, long walletCoins)
        {
            if (walletCoins != shownWallet)
            {
                shownWallet = walletCoins;
                textDirty = true;
            }
            bool open = result.Outcome != RunOutcome.Playing;
            if (!open)
            {
                if (panel != null && panel.activeSelf) panel.SetActive(false);
                textDirty = true;
                return;
            }

            bool opening = panel != null && !panel.activeSelf;
            if (opening) panel.SetActive(true);
            if (!opening && !textDirty) return;

            int total = Mathf.Max(0, Mathf.FloorToInt(survivedSeconds));
            if (title != null)
            {
                title.text = Text.Get(TitleKey(result.Outcome));
                title.color = result.Outcome == RunOutcome.Victory ? victoryTitleColor : defeatTitleColor;
            }
            if (time != null) time.text = Text.Get("ui.gameover.time", (total / 60).ToString("00"), (total % 60).ToString("00"));
            if (level != null) level.text = Text.Get("ui.gameover.level", result.Level);
            if (kills != null) kills.text = Text.Get("ui.gameover.kills", result.EnemiesKilled);
            if (hint != null) hint.text = Text.Get("ui.gameover.hint");
            if (restartLabel != null) restartLabel.text = Text.Get("ui.gameover.restart");
            if (coins != null)
            {
                if (walletCoins < 0) coins.text = string.Empty;
                else if (result.DebugUsed) coins.text = Text.Get("ui.gameover.coins_debug", result.Coins, walletCoins);
                else coins.text = Text.Get("ui.gameover.coins", result.Coins, walletCoins);
            }
            if (shopLabel != null) shopLabel.text = Text.Get("ui.gameover.shop");
            textDirty = false;

            // 패드 · Enter가 바로 되도록 버튼을 고른 상태로 둔다.
            if (opening && EventSystem.current != null && restartButton != null)
            {
                EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
            }
        }

        static string TitleKey(RunOutcome outcome)
        {
            return outcome == RunOutcome.Victory ? "ui.gameover.victory" : "ui.gameover.title";
        }
    }
}
