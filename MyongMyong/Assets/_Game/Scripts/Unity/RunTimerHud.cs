using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 화면 위 가운데 판 시간 mm:ss. 그리기만 한다(시간은 Core의 틱 — 레벨업 선택 중에는 틱이 멈추므로 같이 멈춘다).
    /// 생존 목표에 닿으면(1-11) 시간은 목표(30:00)에서 멈추고 글자가 금색이 되며, 잠깐 목표 글 · 마무리 적 이름 글이 뜬다.
    /// </summary>
    public sealed class RunTimerHud : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Text timerLabel;
        [Tooltip("Big message shown for a moment when the survival goal is reached.")]
        [SerializeField] UnityEngine.UI.Text goalLabel;
        [Tooltip("Second line under the goal message: the end-of-run enemy is coming.")]
        [SerializeField] UnityEngine.UI.Text finaleLabel;
        [Tooltip("Timer color after the survival goal (display only). Before the goal the label keeps its own color.")]
        [SerializeField] Color goalTimerColor = new Color(1f, 0.86f, 0.4f, 1f);

        long shownSeconds = -1;
        bool shownGoal;
        bool timerColorSaved;
        Color normalTimerColor;
        bool bannerShown;
        string shownFinaleNameKey;
        bool bannerDirty = true;

        void OnEnable()
        {
            if (timerLabel != null) timerLabel.font = UiFont.Get(timerLabel.fontSize);
            if (goalLabel != null) goalLabel.font = UiFont.Get(goalLabel.fontSize);
            if (finaleLabel != null) finaleLabel.font = UiFont.Get(finaleLabel.fontSize);
            Text.Changed += OnTextChanged;
        }

        void OnDisable()
        {
            Text.Changed -= OnTextChanged;
        }

        void OnTextChanged()
        {
            shownSeconds = -1;
            bannerDirty = true;
        }

        /// <summary>지금 보이는 글(확인용).</summary>
        public string Shown => timerLabel != null ? timerLabel.text : string.Empty;

        /// <summary>목표 글이 떠 있는지 · 그 글 · 타이머가 목표 색인지(확인용).</summary>
        public bool BannerShown => bannerShown;

        public string ShownGoal => goalLabel != null && goalLabel.gameObject.activeSelf ? goalLabel.text : string.Empty;

        public string ShownFinale => finaleLabel != null && finaleLabel.gameObject.activeSelf ? finaleLabel.text : string.Empty;

        public bool TimerInGoalColor => shownGoal;

        /// <param name="runTicks">판 시간(틱).</param>
        public void Render(long runTicks, int ticksPerSecond)
        {
            Render(runTicks, ticksPerSecond, false, false, null);
        }

        /// <param name="runTicks">판 시간(틱). 목표에 닿았으면 목표 틱.</param>
        /// <param name="goalReached">생존 목표에 닿았는지(타이머 색).</param>
        /// <param name="showBanner">목표 글을 지금 보일지.</param>
        /// <param name="finaleNameKey">마무리 적 이름 글 키(비면 둘째 줄 없음).</param>
        public void Render(long runTicks, int ticksPerSecond, bool goalReached, bool showBanner, string finaleNameKey)
        {
            if (timerLabel != null)
            {
                if (!timerColorSaved)
                {
                    normalTimerColor = timerLabel.color;
                    timerColorSaved = true;
                }
                if (goalReached != shownGoal)
                {
                    timerLabel.color = goalReached ? goalTimerColor : normalTimerColor;
                    shownGoal = goalReached;
                }
                RunClock.Split(runTicks, ticksPerSecond, out long minutes, out int seconds);
                long total = minutes * 60 + seconds;
                if (total != shownSeconds)
                {
                    timerLabel.text = Text.Get("ui.hud.timer", minutes.ToString("00"), seconds.ToString("00"));
                    shownSeconds = total;
                }
            }

            string nameKey = string.IsNullOrEmpty(finaleNameKey) ? null : finaleNameKey;
            if (showBanner == bannerShown && !bannerDirty && nameKey == shownFinaleNameKey) return;
            bannerShown = showBanner;
            shownFinaleNameKey = nameKey;
            bannerDirty = false;
            if (goalLabel != null)
            {
                goalLabel.gameObject.SetActive(showBanner);
                if (showBanner) goalLabel.text = Text.Get("ui.hud.goal_reached");
            }
            if (finaleLabel != null)
            {
                bool showName = showBanner && nameKey != null;
                finaleLabel.gameObject.SetActive(showName);
                if (showName) finaleLabel.text = Text.Get("ui.hud.finale_warning", Text.Get(nameKey));
            }
        }
    }
}
