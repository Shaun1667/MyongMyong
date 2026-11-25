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

        /// <summary>[다시 시작]을 눌렀다.</summary>
        public event Action RestartPressed;

        bool textDirty = true;

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>[다시 시작] 버튼(확인용 · 디버그가 같은 길로 누른다).</summary>
        public UnityEngine.UI.Button RestartButton => restartButton;

        void Awake()
        {
            if (restartButton != null) restartButton.onClick.AddListener(() => RestartPressed?.Invoke());
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
            if (title != null) title.text = Text.Get(TitleKey(result.Outcome));
            if (time != null) time.text = Text.Get("ui.gameover.time", (total / 60).ToString("00"), (total % 60).ToString("00"));
            if (level != null) level.text = Text.Get("ui.gameover.level", result.Level);
            if (kills != null) kills.text = Text.Get("ui.gameover.kills", result.EnemiesKilled);
            if (hint != null) hint.text = Text.Get("ui.gameover.hint");
            if (restartLabel != null) restartLabel.text = Text.Get("ui.gameover.restart");
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
