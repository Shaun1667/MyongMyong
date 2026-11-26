using System;

namespace Myong.Core
{
    /// <summary>
    /// 한 판의 길이 규칙(데이터 에셋 RunTuning에서 옮겨 온 값). 생존 목표 시간에 닿으면 그 순간 승리가 확정된다(World.GoalReached).
    /// 마무리 시간(FinaleSeconds)이 0이면 바로 판 끝(1-7 방식), 아니면 남은 적이 사라지고 마무리 웨이브(목표 이후에 시작하는 웨이브)만 나오며,
    /// 그 뒤 죽거나 마무리 시간이 다 되면 판이 승리(RunOutcome.Victory)로 끝난다(1-11).
    /// </summary>
    public readonly struct RunRulesData
    {
        /// <summary>시간 승리가 없는 규칙(이동 · 전투만 볼 때, 테스트).</summary>
        public static readonly RunRulesData None = new RunRulesData(0f);

        /// <summary>이만큼(초) 버티면 승리. 0 이하 = 시간 승리 없음.</summary>
        public readonly float SurvivalSeconds;

        /// <summary>목표에 닿은 뒤 판이 끝날 때까지 길어야(초). 이 안에 잡히지 않아도 이만큼 지나면 판이 끝난다. 0 이하 = 목표에서 바로 판 끝.</summary>
        public readonly float FinaleSeconds;

        public RunRulesData(float survivalSeconds, float finaleSeconds = 0f)
        {
            SurvivalSeconds = survivalSeconds;
            FinaleSeconds = finaleSeconds;
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
