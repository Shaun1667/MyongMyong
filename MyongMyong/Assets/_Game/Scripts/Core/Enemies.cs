using System;

namespace Myong.Core
{
    /// <summary>적 한 종류의 규칙 수치(데이터 에셋 EnemyTable의 한 줄). 비주얼은 Unity 쪽이 같은 줄의 ID로 찾는다.</summary>
    public readonly struct EnemyKindData
    {
        /// <summary>이동 속도(유닛/초).</summary>
        public readonly float MoveSpeed;
        /// <summary>몸 반지름(유닛). 적끼리 밀어내기 · 탄 맞기에 쓴다.</summary>
        public readonly float Radius;
        /// <summary>처음 체력. 맞으면 줄고 0 이하가 되면 죽는다.</summary>
        public readonly float MaxHealth;
        /// <summary>죽으면 떨어뜨리는 경험치 젬의 값(0 = 젬 없음).</summary>
        public readonly int ExperienceValue;
        /// <summary>플레이어와 겹치면(반지름 합) 주는 피해(0 = 닿아도 피해 없음).</summary>
        public readonly float ContactDamage;

        /// <summary>
        /// 무적 몸: 탄이 조준하지 않고 지나가며(체력이 줄지 않음), 다른 적과 밀어내지 않는다(겹쳐 지나감). 30분 마무리 적(1-11).
        /// </summary>
        public readonly bool Invulnerable;
        /// <summary>닿으면 즉사: 맞은 뒤 무적 시간 · 받는 피해 배율과 상관없이 체력이 0이 된다(디버그 플레이어 무적만 막음). 30분 마무리 적(1-11).</summary>
        public readonly bool LethalContact;

        public EnemyKindData(float moveSpeed, float radius, float maxHealth = 1f, int experienceValue = 0, float contactDamage = 0f, bool invulnerable = false, bool lethalContact = false)
        {
            Invulnerable = invulnerable;
            LethalContact = lethalContact;
            ContactDamage = contactDamage;
            MoveSpeed = moveSpeed;
            Radius = radius;
            MaxHealth = maxHealth;
            ExperienceValue = experienceValue;
        }
    }

    /// <summary>시간대 안에서 한 종류가 나올 비중. 비중은 서로의 비율로만 쓴다(합이 1일 필요 없음).</summary>
    public readonly struct SpawnWeight
    {
        /// <summary>EnemyRulesData.Kinds의 번호.</summary>
        public readonly int Kind;
        public readonly float Weight;

        public SpawnWeight(int kind, float weight)
        {
            Kind = kind;
            Weight = weight;
        }
    }

    /// <summary>
    /// 스폰 표의 한 시간대: StartSeconds부터 다음 시간대 전까지 초당 SpawnsPerSecond마리. 한 마리마다 Mix의 비중대로 종류를 고른다(스폰 난수 스트림).
    /// 종류가 하나뿐이면 난수를 뽑지 않는다 — 한 종류 시간대의 결과는 종류 섞기가 생기기 전과 같다.
    /// </summary>
    public readonly struct SpawnPhaseData
    {
        public readonly float StartSeconds;
        public readonly float SpawnsPerSecond;
        /// <summary>첫 종류(Mix[0]). 한 종류 시간대면 그 종류.</summary>
        public readonly int Kind;
        /// <summary>나올 종류와 비중(1개 이상).</summary>
        public readonly SpawnWeight[] Mix;
        /// <summary>Mix 비중의 합.</summary>
        public readonly float TotalWeight;

        /// <summary>한 종류만 나오는 시간대.</summary>
        public SpawnPhaseData(float startSeconds, float spawnsPerSecond, int kind)
            : this(startSeconds, spawnsPerSecond, new[] { new SpawnWeight(kind, 1f) })
        {
        }

        public SpawnPhaseData(float startSeconds, float spawnsPerSecond, SpawnWeight[] mix)
        {
            if (mix == null || mix.Length == 0) throw new ArgumentException("a spawn phase needs at least one enemy kind", nameof(mix));
            StartSeconds = startSeconds;
            SpawnsPerSecond = spawnsPerSecond;
            Mix = (SpawnWeight[])mix.Clone();
            Kind = Mix[0].Kind;
            float total = 0f;
            foreach (var w in Mix)
            {
                if (!(w.Weight > 0f)) throw new ArgumentOutOfRangeException(nameof(mix), "spawn weight must be positive");
                total += w.Weight;
            }
            TotalWeight = total;
        }

        /// <summary>[0, 1)의 수 r로 종류를 고른다(누적 비중). 한 종류면 r을 보지 않는다.</summary>
        public int KindAt(float r)
        {
            if (Mix.Length == 1) return Mix[0].Kind;
            float x = r * TotalWeight;
            for (int i = 0; i < Mix.Length - 1; i++)
            {
                x -= Mix[i].Weight;
                if (x < 0f) return Mix[i].Kind;
            }
            return Mix[Mix.Length - 1].Kind;
        }
    }

    /// <summary>웨이브가 적을 놓는 방식.</summary>
    public enum SpawnPattern
    {
        /// <summary>한 마리씩 스폰 원 위 아무 각도에 — 보통 적처럼 플레이어를 쫓는다.</summary>
        Scatter = 0,
        /// <summary>한 각도에 떼로 모여 나와, 나온 순간의 플레이어 쪽으로 곧게 돌진한다(쫓지 않음). 멀어지면 사라진다(처치 아님).</summary>
        Swarm = 1,
    }

    /// <summary>시간대 스폰과 따로, 정해진 때마다 나오는 웨이브(정예 · 떼 돌진). StartSeconds부터 IntervalSeconds마다(0 = 한 번).</summary>
    public readonly struct SpawnWaveData
    {
        /// <summary>EnemyRulesData.Kinds의 번호.</summary>
        public readonly int Kind;
        public readonly float StartSeconds;
        public readonly float IntervalSeconds;
        public readonly int Count;
        public readonly SpawnPattern Pattern;
        /// <summary>Swarm: 떼 안 적 사이 간격(유닛).</summary>
        public readonly float Spacing;
        /// <summary>Swarm: 돌진 속도 = 종류 이동 속도 × 이 값.</summary>
        public readonly float SpeedScale;
        /// <summary>Scatter: 플레이어에게서 이만큼(유닛) 떨어진 원 위에 나온다. 0 = 스폰 반경(EnemyRulesData.SpawnRadius). 큰 적이 화면 밖에서 나오게(1-11).</summary>
        public readonly float Distance;

        public SpawnWaveData(int kind, float startSeconds, float intervalSeconds, int count, SpawnPattern pattern = SpawnPattern.Scatter, float spacing = 0f, float speedScale = 1f, float distance = 0f)
        {
            Distance = distance < 0f ? 0f : distance;
            Kind = kind;
            StartSeconds = startSeconds;
            IntervalSeconds = intervalSeconds;
            Count = count;
            Pattern = pattern;
            Spacing = spacing;
            SpeedScale = speedScale;
        }

        /// <summary>Swarm 떼의 한 줄 폭(마리). 거의 정사각형으로 놓는다.</summary>
        public int SwarmRowWidth => Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Count)));

        /// <summary>Swarm 떼가 스폰 원에서 뒤로 뻗는 길이(유닛).</summary>
        public float SwarmDepth => Count <= 0 ? 0f : ((Count - 1) / SwarmRowWidth) * Spacing;
    }

    /// <summary>적 규칙 수치 전부(데이터 에셋 EnemyTable · SpawnTable에서 옮겨 온 값). 만들 때 한 번 검사 · 정렬한다.</summary>
    public sealed class EnemyRulesData
    {
        /// <summary>적이 없는 규칙(이동만 볼 때).</summary>
        public static readonly EnemyRulesData None = new EnemyRulesData(new EnemyKindData[0], new SpawnPhaseData[0], 0f, 0, 0f);

        /// <summary>웨이브(정예 · 떼 돌진). 비었으면 시간대 스폰만.</summary>
        public readonly SpawnWaveData[] Waves;
        /// <summary>돌진하는 적(Swarm)이 플레이어에게서 이만큼(유닛) 멀어지면 사라진다. 0 = 사라지지 않음.</summary>
        public readonly float ChargeDespawnDistance;

        public readonly EnemyKindData[] Kinds;
        /// <summary>StartSeconds 오름차순.</summary>
        public readonly SpawnPhaseData[] Phases;
        /// <summary>플레이어로부터 스폰할 원의 반지름(유닛). 화면 밖이 되도록 화면 대각선 절반보다 크게.</summary>
        public readonly float SpawnRadius;
        /// <summary>동시에 살아 있을 수 있는 최대 수 = World의 적 배열 크기.</summary>
        public readonly int MaxAlive;
        /// <summary>겹친 적끼리 한 틱에 겹친 거리의 얼마를 풀지(0 = 밀어내기 없음, 1 = 한 틱에 다 풂).</summary>
        public readonly float SeparationPush;
        /// <summary>가장 큰 몸 반지름(해시 격자 칸 크기). 무적 몸(Invulnerable)은 빼고 센다.</summary>
        public readonly float MaxRadius;

        public EnemyRulesData(EnemyKindData[] kinds, SpawnPhaseData[] phases, float spawnRadius, int maxAlive, float separationPush)
            : this(kinds, phases, spawnRadius, maxAlive, separationPush, new SpawnWaveData[0], 0f)
        {
        }

        public EnemyRulesData(EnemyKindData[] kinds, SpawnPhaseData[] phases, float spawnRadius, int maxAlive, float separationPush, SpawnWaveData[] waves, float chargeDespawnDistance)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            if (kinds == null) throw new ArgumentNullException(nameof(kinds));
            if (phases == null) throw new ArgumentNullException(nameof(phases));
            if (maxAlive < 0) throw new ArgumentOutOfRangeException(nameof(maxAlive));
            Kinds = (EnemyKindData[])kinds.Clone();
            Phases = (SpawnPhaseData[])phases.Clone();
            Array.Sort(Phases, (a, b) => a.StartSeconds.CompareTo(b.StartSeconds));
            foreach (var phase in Phases)
            {
                if (phase.Mix == null) throw new ArgumentException("spawn phase without enemy kinds", nameof(phases));
                foreach (var w in phase.Mix)
                {
                    if (w.Kind < 0 || w.Kind >= Kinds.Length) throw new ArgumentOutOfRangeException(nameof(phases), "spawn phase refers to a missing enemy kind");
                }
            }
            Waves = (SpawnWaveData[])waves.Clone();
            foreach (var wave in Waves)
            {
                if (wave.Kind < 0 || wave.Kind >= Kinds.Length) throw new ArgumentOutOfRangeException(nameof(waves), "spawn wave refers to a missing enemy kind");
                if (wave.Count < 0 || wave.IntervalSeconds < 0f || wave.StartSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(waves), "spawn wave count, start and interval must not be negative");
            }
            ChargeDespawnDistance = chargeDespawnDistance < 0f ? 0f : chargeDespawnDistance;
            SpawnRadius = spawnRadius;
            MaxAlive = maxAlive;
            SeparationPush = separationPush < 0f ? 0f : (separationPush > 1f ? 1f : separationPush);
            // 무적 몸(Invulnerable)은 밀어내기 · 탄 맞기에 들지 않으므로 격자 칸 크기에서 뺀다(그런 적이 생겨도 다른 적의 결과가 그대로).
            foreach (var kind in Kinds)
            {
                if (!kind.Invulnerable && kind.Radius > MaxRadius) MaxRadius = kind.Radius;
            }
        }

        /// <summary>경과 시간의 시간대 번호. 첫 시간대 전이면 -1.</summary>
        public int PhaseAt(float seconds)
        {
            int found = -1;
            for (int i = 0; i < Phases.Length; i++)
            {
                if (Phases[i].StartSeconds <= seconds) found = i;
                else break;
            }
            return found;
        }
    }

    public struct EnemyState
    {
        /// <summary>LastHitTick에 넣는 값: 맞은 적 없음.</summary>
        public const long NeverHit = long.MinValue / 2;

        public int Id;
        /// <summary>EnemyRulesData.Kinds의 번호.</summary>
        public int Kind;
        public Vec2 Position;
        /// <summary>직전 틱의 위치(표시 보간용).</summary>
        public Vec2 PreviousPosition;
        public float Health;
        /// <summary>이번 틱에 죽어 틱 끝에 빠질 적(탄 판정에서 건너뛴다).</summary>
        public bool Dead;
        /// <summary>마지막으로 탄에 맞은 틱(표시 쪽 번쩍임용). 맞은 적 없으면 NeverHit.</summary>
        public long LastHitTick;
        /// <summary>돌진 속도(유닛/초). 0이면 플레이어를 쫓고, 아니면 이 방향으로 곧게 간다(Swarm 웨이브).</summary>
        public Vec2 Charge;
    }

    /// <summary>
    /// 적 위치의 해시 격자(고정 크기 배열). 밀어내기와 탄 맞기가 같이 쓴다 — 틱마다 할당하지 않는다.
    /// 상태가 아닌 계산용 작업 공간이라 World가 아니라 Simulation이 든다. 적이 움직인 뒤에는 Build를 다시 부른다.
    /// </summary>
    sealed class EnemyGrid
    {
        public const int BucketCount = 4096; // 2의 거듭제곱
        const int BucketMask = BucketCount - 1;
        const int HashX = 73856093;
        const int HashY = 19349663;
        const float MinCellSize = 0.0001f;

        /// <summary>칸마다 첫 적 번호(-1 = 비었음).</summary>
        public readonly int[] Head = new int[BucketCount];
        /// <summary>같은 칸의 다음 적 번호(-1 = 끝).</summary>
        public readonly int[] Next;
        public readonly float CellSize;

        public EnemyGrid(int capacity, float maxRadius)
        {
            Next = new int[Math.Max(capacity, 1)];
            CellSize = Math.Max(maxRadius * 2f, MinCellSize);
        }

        public void Build(EnemyState[] enemies, int count)
        {
            for (int b = 0; b < BucketCount; b++) Head[b] = -1;
            for (int i = 0; i < count; i++)
            {
                int bucket = Bucket(Cell(enemies[i].Position.X), Cell(enemies[i].Position.Y));
                Next[i] = Head[bucket];
                Head[bucket] = i;
            }
        }

        public int Cell(float v) => (int)Math.Floor(v / CellSize);

        public static int Bucket(int cx, int cy) => unchecked((cx * HashX) ^ (cy * HashY)) & BucketMask;
    }

    /// <summary>적끼리 겹침 밀어내기. 해시 격자로 가까운 것만 본다 — 틱마다 할당하지 않는다.</summary>
    sealed class EnemySeparation
    {
        const float SamePointSquared = 1e-12f;

        readonly EnemyGrid grid;
        readonly Vec2[] push;
        readonly int[] visited = new int[9];

        public EnemySeparation(EnemyGrid grid, int capacity)
        {
            this.grid = grid;
            push = new Vec2[Math.Max(capacity, 1)];
        }

        public void Apply(EnemyState[] enemies, int count, EnemyKindData[] kinds, float strength)
        {
            if (strength <= 0f || count < 2) return;

            grid.Build(enemies, count);

            // 양쪽이 서로를 보고 겹친 거리의 절반씩 물러난다.
            float half = strength * 0.5f;
            for (int i = 0; i < count; i++)
            {
                if (kinds[enemies[i].Kind].Invulnerable)
                {
                    push[i] = Vec2.Zero; // 무적 몸은 밀지도 밀리지도 않는다
                    continue;
                }
                Vec2 p = enemies[i].Position;
                float r = kinds[enemies[i].Kind].Radius;
                int cx = grid.Cell(p.X);
                int cy = grid.Cell(p.Y);
                int visitedCount = 0;
                Vec2 sum = Vec2.Zero;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int bucket = EnemyGrid.Bucket(cx + dx, cy + dy);
                        if (Seen(visited, bucket, visitedCount)) continue;
                        visited[visitedCount++] = bucket;
                        for (int j = grid.Head[bucket]; j >= 0; j = grid.Next[j])
                        {
                            if (j == i || kinds[enemies[j].Kind].Invulnerable) continue;
                            float minDistance = r + kinds[enemies[j].Kind].Radius;
                            Vec2 delta = p - enemies[j].Position;
                            float d2 = delta.SqrMagnitude;
                            if (d2 >= minDistance * minDistance) continue;
                            if (d2 > SamePointSquared)
                            {
                                float d = (float)Math.Sqrt(d2);
                                sum = sum + delta * ((minDistance - d) / d * half);
                            }
                            else
                            {
                                // 완전히 같은 자리: 번호 순서로 좌우로 가른다(난수 없이 같은 결과).
                                sum = sum + new Vec2(i < j ? -minDistance * half : minDistance * half, 0f);
                            }
                        }
                    }
                }
                push[i] = sum;
            }

            for (int i = 0; i < count; i++) enemies[i].Position = enemies[i].Position + push[i];
        }

        internal static bool Seen(int[] visited, int bucket, int visitedCount)
        {
            for (int k = 0; k < visitedCount; k++)
            {
                if (visited[k] == bucket) return true;
            }
            return false;
        }
    }
}
