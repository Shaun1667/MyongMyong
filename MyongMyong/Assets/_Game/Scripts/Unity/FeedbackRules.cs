using System;
using System.Collections.Generic;
using Myong.Core;

namespace Myong.Unity
{
    /// <summary>플레이어 스프라이트가 지금 어떤 모습인지(피격 연출).</summary>
    public enum PlayerLook
    {
        Normal = 0,
        /// <summary>온몸이 흰색.</summary>
        Flash = 1,
        /// <summary>깜빡임의 흐린 쪽.</summary>
        BlinkDim = 2,
    }

    /// <summary>
    /// 피격 · 사망 연출의 계산(표시만 — Core의 규칙 · 결과에 영향을 주지 않는다). 엔진을 쓰지 않아 테스트할 수 있다.
    /// Core에서는 읽기만 한다: PlayerState.LastHurtTick · InvulnerableUntilTick, EnemyState.LastHitTick, World.Tick.
    /// </summary>
    public static class FeedbackRules
    {
        /// <summary>초 → 틱(반올림). 0 이하는 0(끔), 0보다 크면 적어도 1틱.</summary>
        public static int TicksOf(float seconds, int ticksPerSecond)
        {
            if (seconds <= 0f || ticksPerSecond <= 0) return 0;
            return Math.Max(1, (int)Math.Round(seconds * ticksPerSecond, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// 맞은 직후 flashTicks 동안 흰색, 그 뒤 무적 시간이 끝날 때까지 blinkTicks마다 흐림 ↔ 보통.
        /// 깜빡임은 무적이 끝나는 틱에서 거꾸로 센다 — 마지막 흐린 구간이 끝나는 때 = 무적이 끝나는 때.
        /// </summary>
        public static PlayerLook PlayerLookAt(long tick, long lastHurtTick, long invulnerableUntilTick, int flashTicks, int blinkTicks)
        {
            if (lastHurtTick == PlayerState.NeverHurt) return PlayerLook.Normal;
            long since = tick - lastHurtTick;
            if (since < 0) return PlayerLook.Normal;
            if (flashTicks > 0 && since <= flashTicks) return PlayerLook.Flash;
            long left = invulnerableUntilTick - tick;
            if (blinkTicks <= 0 || left <= 0) return PlayerLook.Normal;
            return ((left - 1) / blinkTicks) % 2 == 0 ? PlayerLook.BlinkDim : PlayerLook.Normal;
        }

        /// <summary>탄에 맞은 적이 지금 흰색인지(맞은 뒤 flashTicks 동안).</summary>
        public static bool EnemyFlashAt(long tick, long lastHitTick, int flashTicks)
        {
            if (flashTicks <= 0 || lastHitTick == EnemyState.NeverHit) return false;
            long since = tick - lastHitTick;
            return since >= 0 && since <= flashTicks;
        }

        /// <summary>소멸 연출의 단계(0부터 steps - 1). 끝났거나 아직이면 -1.</summary>
        public static int DeathStepAt(long age, int durationTicks, int steps)
        {
            if (age < 0 || durationTicks <= 0 || steps <= 0 || age >= durationTicks) return -1;
            return (int)(age * steps / durationTicks);
        }

        /// <summary>소멸 단계의 크기: 처음에 popScale로 부풀었다가 단계마다 같은 폭으로 작아진다(마지막 단계 뒤 사라짐).</summary>
        public static float DeathScaleAt(int step, int steps, float popScale)
        {
            if (step < 0 || steps <= 0 || step >= steps) return 0f;
            return popScale * (steps - step) / steps;
        }

        /// <summary>생존 목표에 닿은 뒤(goalTick 이후) bannerTicks 동안 "생존 성공!" 글을 보일지. 목표 전(goalTick &lt; 0)이면 false (1-11).</summary>
        public static bool GoalBannerVisible(long tick, long goalTick, int bannerTicks)
        {
            if (goalTick < 0 || bannerTicks <= 0) return false;
            long since = tick - goalTick;
            return since >= 0 && since < bannerTicks;
        }
    }

    /// <summary>
    /// 처치된 적을 Core 상태만 읽어서 찾는다(Core에 이벤트를 더하지 않는다).
    /// 지난번에 본 적 ID 가운데 지금 없는 것 = 빠진 적. 처치 누계(World.EnemiesKilledTotal)가 오른 때에만 찾는다 —
    /// 디버그 "모두 제거"나 돌진하다 멀어져 사라진 적은 처치가 아니라 연출이 없다.
    /// (같은 사이에 처치와 사라짐이 같이 있으면 둘 다 잡힌다 — 사라지는 적은 화면 밖이다.)
    /// 판 시작 때 한 번 만들고, Observe는 할당하지 않는다.
    /// </summary>
    public sealed class EnemyDeathTracker
    {
        readonly Dictionary<int, int> indexById;
        readonly Vec2[] positions;
        readonly int[] kinds;
        readonly bool[] alive;
        readonly Vec2[] deathPositions;
        readonly int[] deathKinds;
        int count;
        long killedSeen;

        public EnemyDeathTracker(int capacity)
        {
            Capacity = Math.Max(0, capacity);
            indexById = new Dictionary<int, int>(Capacity);
            positions = new Vec2[Capacity];
            kinds = new int[Capacity];
            alive = new bool[Capacity];
            deathPositions = new Vec2[Capacity];
            deathKinds = new int[Capacity];
        }

        public int Capacity { get; }

        /// <summary>지난 Observe에서 찾은 처치 수.</summary>
        public int DeathCount { get; private set; }

        public Vec2 DeathPosition(int index) => deathPositions[index];

        public int DeathKind(int index) => deathKinds[index];

        /// <summary>판을 새로 시작할 때: 본 것을 잊는다.</summary>
        public void Reset()
        {
            indexById.Clear();
            count = 0;
            killedSeen = 0;
            DeathCount = 0;
        }

        /// <summary>지금 살아 있는 적을 보고, 지난번 뒤로 처치된 적(마지막으로 본 자리 · 종류)을 찾는다. 찾은 수를 돌려준다.</summary>
        public int Observe(EnemyState[] enemies, int enemyCount, long killedTotal)
        {
            int n = Math.Min(enemyCount, Capacity);
            int deaths = 0;
            if (killedTotal > killedSeen)
            {
                for (int i = 0; i < count; i++) alive[i] = false;
                for (int j = 0; j < n; j++)
                {
                    if (indexById.TryGetValue(enemies[j].Id, out int seenAt)) alive[seenAt] = true;
                }
                for (int i = 0; i < count; i++)
                {
                    if (alive[i]) continue;
                    deathPositions[deaths] = positions[i];
                    deathKinds[deaths] = kinds[i];
                    deaths++;
                }
            }
            killedSeen = killedTotal;

            indexById.Clear();
            for (int j = 0; j < n; j++)
            {
                indexById[enemies[j].Id] = j;
                positions[j] = enemies[j].Position;
                kinds[j] = enemies[j].Kind;
            }
            count = n;
            DeathCount = deaths;
            return deaths;
        }
    }
}
