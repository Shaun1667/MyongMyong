namespace Myong.Core
{
    /// <summary>
    /// 난수의 용도. 용도마다 따로 흐르는 스트림을 쓴다 — 한 용도에서 난수를 더 뽑아도 다른 용도의 결과가 바뀌지 않는다.
    /// 새 용도 = 줄 하나 (값은 바꾸지 않는다: 같은 시드의 결과가 바뀐다).
    /// </summary>
    public enum RandomStream
    {
        Spawn = 1,
        /// <summary>레벨업 선택지 뽑기.</summary>
        LevelUp = 2,
        /// <summary>적 처치 드롭(코인, 1-12).</summary>
        Drop = 3,
    }

    /// <summary>
    /// 시드 난수(SplitMix64). 엔진 난수 · System.Random을 쓰지 않는다 — 어디서 돌려도 같은 시드 = 같은 수열.
    /// 상태는 ulong 하나라 World에 그대로 들어간다.
    /// </summary>
    public sealed class Rng
    {
        const ulong Golden = 0x9E3779B97F4A7C15UL;
        const ulong MixA = 0xBF58476D1CE4E5B9UL;
        const ulong MixB = 0x94D049BB133111EBUL;
        const float FloatUnit = 1f / (1 << 24);

        ulong state;

        public Rng(ulong state)
        {
            this.state = state;
        }

        /// <summary>판의 시드와 용도로 스트림 하나를 만든다.</summary>
        public static Rng ForStream(int seed, RandomStream stream)
        {
            ulong mixed = Mix(((ulong)(uint)seed << 32) ^ (ulong)(uint)stream);
            return new Rng(mixed);
        }

        /// <summary>지금 상태(같으면 이후 수열이 같다).</summary>
        public ulong State => state;

        public ulong NextULong()
        {
            state += Golden;
            return Mix(state);
        }

        /// <summary>[0, 1).</summary>
        public float NextFloat()
        {
            return (NextULong() >> 40) * FloatUnit;
        }

        /// <summary>[min, max).</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>[minInclusive, maxExclusive). 범위가 비면 minInclusive.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextULong() % span));
        }

        static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * MixA;
            z = (z ^ (z >> 27)) * MixB;
            return z ^ (z >> 31);
        }
    }
}
