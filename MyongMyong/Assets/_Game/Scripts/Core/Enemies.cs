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

        public EnemyKindData(float moveSpeed, float radius, float maxHealth = 1f, int experienceValue = 0)
        {
            MoveSpeed = moveSpeed;
            Radius = radius;
            MaxHealth = maxHealth;
            ExperienceValue = experienceValue;
        }
    }

    /// <summary>스폰 표의 한 시간대: StartSeconds부터 다음 시간대 전까지 초당 SpawnsPerSecond마리의 Kind.</summary>
    public readonly struct SpawnPhaseData
    {
        public readonly float StartSeconds;
        public readonly float SpawnsPerSecond;
        /// <summary>EnemyRulesData.Kinds의 번호.</summary>
        public readonly int Kind;

        public SpawnPhaseData(float startSeconds, float spawnsPerSecond, int kind)
        {
            StartSeconds = startSeconds;
            SpawnsPerSecond = spawnsPerSecond;
            Kind = kind;
        }
    }

    /// <summary>적 규칙 수치 전부(데이터 에셋 EnemyTable · SpawnTable에서 옮겨 온 값). 만들 때 한 번 검사 · 정렬한다.</summary>
    public sealed class EnemyRulesData
    {
        /// <summary>적이 없는 규칙(이동만 볼 때).</summary>
        public static readonly EnemyRulesData None = new EnemyRulesData(new EnemyKindData[0], new SpawnPhaseData[0], 0f, 0, 0f);

        public readonly EnemyKindData[] Kinds;
        /// <summary>StartSeconds 오름차순.</summary>
        public readonly SpawnPhaseData[] Phases;
        /// <summary>플레이어로부터 스폰할 원의 반지름(유닛). 화면 밖이 되도록 화면 대각선 절반보다 크게.</summary>
        public readonly float SpawnRadius;
        /// <summary>동시에 살아 있을 수 있는 최대 수 = World의 적 배열 크기.</summary>
        public readonly int MaxAlive;
        /// <summary>겹친 적끼리 한 틱에 겹친 거리의 얼마를 풀지(0 = 밀어내기 없음, 1 = 한 틱에 다 풂).</summary>
        public readonly float SeparationPush;
        /// <summary>가장 큰 몸 반지름(해시 격자 칸 크기).</summary>
        public readonly float MaxRadius;

        public EnemyRulesData(EnemyKindData[] kinds, SpawnPhaseData[] phases, float spawnRadius, int maxAlive, float separationPush)
        {
            if (kinds == null) throw new ArgumentNullException(nameof(kinds));
            if (phases == null) throw new ArgumentNullException(nameof(phases));
            if (maxAlive < 0) throw new ArgumentOutOfRangeException(nameof(maxAlive));
            Kinds = (EnemyKindData[])kinds.Clone();
            Phases = (SpawnPhaseData[])phases.Clone();
            Array.Sort(Phases, (a, b) => a.StartSeconds.CompareTo(b.StartSeconds));
            foreach (var phase in Phases)
            {
                if (phase.Kind < 0 || phase.Kind >= Kinds.Length) throw new ArgumentOutOfRangeException(nameof(phases), "spawn phase refers to a missing enemy kind");
            }
            SpawnRadius = spawnRadius;
            MaxAlive = maxAlive;
            SeparationPush = separationPush < 0f ? 0f : (separationPush > 1f ? 1f : separationPush);
            foreach (var kind in Kinds)
            {
                if (kind.Radius > MaxRadius) MaxRadius = kind.Radius;
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
                            if (j == i) continue;
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
