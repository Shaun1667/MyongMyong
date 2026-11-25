using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>화면 위 가운데 판 시간 mm:ss. 그리기만 한다(시간은 Core의 틱 — 레벨업 선택 중에는 틱이 멈추므로 같이 멈춘다).</summary>
    public sealed class RunTimerHud : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Text timerLabel;

        long shownSeconds = -1;

        void OnEnable()
        {
            if (timerLabel != null) timerLabel.font = UiFont.Get(timerLabel.fontSize);
            Text.Changed += OnTextChanged;
        }

        void OnDisable()
        {
            Text.Changed -= OnTextChanged;
        }

        void OnTextChanged()
        {
            shownSeconds = -1;
        }

        /// <summary>지금 보이는 글(확인용).</summary>
        public string Shown => timerLabel != null ? timerLabel.text : string.Empty;

        /// <param name="runTicks">판 시간(틱).</param>
        public void Render(long runTicks, int ticksPerSecond)
        {
            RunClock.Split(runTicks, ticksPerSecond, out long minutes, out int seconds);
            long total = minutes * 60 + seconds;
            if (timerLabel == null || total == shownSeconds) return;
            timerLabel.text = Text.Get("ui.hud.timer", minutes.ToString("00"), seconds.ToString("00"));
            shownSeconds = total;
        }
    }
}
