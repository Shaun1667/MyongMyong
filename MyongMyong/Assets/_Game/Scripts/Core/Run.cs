using System;

namespace Myong.Core
{
    /// <summary>한 판의 길이 규칙(데이터 에셋 RunTuning에서 옮겨 온 값). 생존 목표 시간에 닿으면 승리(RunOutcome.Victory).</summary>
    public readonly struct RunRulesData
    {
        /// <summary>시간 승리가 없는 규칙(이동 · 전투만 볼 때, 테스트).</summary>
        public static readonly RunRulesData None = new RunRulesData(0f);

        /// <summary>이만큼(초) 버티면 승리. 0 이하 = 시간 승리 없음.</summary>
        public readonly float SurvivalSeconds;

        public RunRulesData(float survivalSeconds)
        {
            SurvivalSeconds = survivalSeconds;
        }
    }

    /// <summary>판 시간(틱) 계산 한 곳: 초 → 틱, 틱 → 표시용 분 · 초.</summary>
    public static class RunClock
    {
        /// <summary>초 → 틱(반올림). 0 이하면 0(= 없음).</summary>
        public static long TicksOf(float seconds, int ticksPerSecond)
        {
            if (seconds <= 0f || ticksPerSecond <= 0) return 0;
            return (long)Math.Round((double)seconds * ticksPerSecond, MidpointRounding.AwayFromZero);
        }

        /// <summary>틱 → 지난 분 · 초(초는 내림 — 29:59.9는 29:59). 분은 60을 넘을 수 있다.</summary>
        public static void Split(long ticks, int ticksPerSecond, out long minutes, out int seconds)
        {
            long total = ticks <= 0 || ticksPerSecond <= 0 ? 0 : ticks / ticksPerSecond;
            minutes = total / 60;
            seconds = (int)(total % 60);
        }
    }
}
