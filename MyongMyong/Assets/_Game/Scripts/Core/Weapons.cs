using System;

namespace Myong.Core
{
    /// <summary>무기 한 종류의 규칙 수치(데이터 에셋 WeaponTable의 한 줄). 비주얼은 Unity 쪽이 같은 줄의 ID로 찾는다.</summary>
    public readonly struct WeaponKindData
    {
        /// <summary>발사 간격(초). 틱 수로 반올림해서 쓴다(최소 1틱).</summary>
        public readonly float FireIntervalSeconds;
        /// <summary>탄 속도(유닛/초).</summary>
        public readonly float BulletSpeed;
        /// <summary>한 발이 적 하나에 주는 피해.</summary>
        public readonly float Damage;
        /// <summary>사거리(유닛). 탄은 이만큼 날아가면 사라지고(탄 수명 = 사거리 / 속도), 이보다 먼 적은 노리지 않는다.</summary>
        public readonly float Range;
        /// <summary>한 번에 쏘는 수. n발이면 가까운 적 n마리를 한 발씩(적이 적으면 가까운 순서로 다시).</summary>
        public readonly int ShotsPerVolley;
        /// <summary>관통 수: 첫 적을 맞힌 뒤 더 뚫고 지나갈 적의 수(0 = 첫 적에서 사라짐).</summary>
        public readonly int Pierce;
        /// <summary>탄 반지름(유닛). 맞기 판정에 쓴다.</summary>
        public readonly float BulletRadius;

        public WeaponKindData(float fireIntervalSeconds, float bulletSpeed, float damage, float range, int shotsPerVolley, int pierce, float bulletRadius)
        {
            FireIntervalSeconds = fireIntervalSeconds;
            BulletSpeed = bulletSpeed;
            Damage = damage;
            Range = range;
            ShotsPerVolley = shotsPerVolley;
            Pierce = pierce;
            BulletRadius = bulletRadius;
        }
    }

    /// <summary>무기 규칙 수치 전부(데이터 에셋 WeaponTable에서 옮겨 온 값). 만들 때 한 번 검사한다.</summary>
    public sealed class WeaponRulesData
    {
        /// <summary>무기가 없는 규칙(이동 · 적만 볼 때).</summary>
        public static readonly WeaponRulesData None = new WeaponRulesData(new WeaponKindData[0], new int[0], 0, Vec2.Zero);

        public readonly WeaponKindData[] Kinds;
        /// <summary>판을 시작할 때 가진 무기(Kinds의 번호).</summary>
        public readonly int[] StartingWeapons;
        /// <summary>동시에 날아갈 수 있는 탄의 최대 수 = World의 탄 배열 크기. 가득 차면 그 발은 쏘지 않는다.</summary>
        public readonly int MaxBullets;
        /// <summary>탄이 나오는 자리 = 플레이어 위치 + 이 값(유닛).</summary>
        public readonly Vec2 MuzzleOffset;
        /// <summary>가장 큰 한 번에 쏘는 수(조준 작업 공간 크기).</summary>
        public readonly int MaxShotsPerVolley;
        /// <summary>탄 하나가 맞힐 수 있는 가장 많은 적 수 = 가장 큰 관통 수 + 1(맞힌 적 기억 칸 수).</summary>
        public readonly int HitMemory;
        /// <summary>가장 큰 탄 반지름.</summary>
        public readonly float MaxBulletRadius;

        public WeaponRulesData(WeaponKindData[] kinds, int[] startingWeapons, int maxBullets, Vec2 muzzleOffset)
        {
            if (kinds == null) throw new ArgumentNullException(nameof(kinds));
            if (startingWeapons == null) throw new ArgumentNullException(nameof(startingWeapons));
            if (maxBullets < 0) throw new ArgumentOutOfRangeException(nameof(maxBullets));
            Kinds = (WeaponKindData[])kinds.Clone();
            StartingWeapons = (int[])startingWeapons.Clone();
            foreach (int w in StartingWeapons)
            {
                if (w < 0 || w >= Kinds.Length) throw new ArgumentOutOfRangeException(nameof(startingWeapons), "starting weapon refers to a missing weapon kind");
            }
            foreach (var k in Kinds)
            {
                if (k.FireIntervalSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(kinds), "fire interval must be positive");
                if (k.BulletSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(kinds), "bullet speed must be positive");
                if (k.Range <= 0f) throw new ArgumentOutOfRangeException(nameof(kinds), "range must be positive");
                if (k.ShotsPerVolley < 1) throw new ArgumentOutOfRangeException(nameof(kinds), "shots per volley must be at least 1");
                if (k.Pierce < 0) throw new ArgumentOutOfRangeException(nameof(kinds), "pierce must not be negative");
                if (k.BulletRadius < 0f) throw new ArgumentOutOfRangeException(nameof(kinds), "bullet radius must not be negative");
                MaxShotsPerVolley = Math.Max(MaxShotsPerVolley, k.ShotsPerVolley);
                HitMemory = Math.Max(HitMemory, k.Pierce + 1);
                MaxBulletRadius = Math.Max(MaxBulletRadius, k.BulletRadius);
            }
            MaxBullets = maxBullets;
            MuzzleOffset = muzzleOffset;
        }
    }

    /// <summary>플레이어가 가진 무기 하나.</summary>
    public struct WeaponState
    {
        /// <summary>WeaponRulesData.Kinds의 번호.</summary>
        public int Kind;
        /// <summary>다음 발사까지 남은 틱(0 = 쏠 수 있음, 쏠 적이 생기면 바로 쏜다).</summary>
        public int CooldownTicks;
    }

    public struct BulletState
    {
        public int Id;
        /// <summary>WeaponRulesData.Kinds의 번호(쏜 무기).</summary>
        public int Kind;
        public Vec2 Position;
        /// <summary>직전 틱의 위치(표시 보간용). 쏜 틱에는 총구 자리.</summary>
        public Vec2 PreviousPosition;
        /// <summary>날아가는 방향(길이 1).</summary>
        public Vec2 Direction;
        /// <summary>남은 사거리(유닛). 0이 되면 사라진다.</summary>
        public float RangeLeft;
        /// <summary>더 맞힐 수 있는 적 수(관통 수 + 1에서 시작). 0이 되면 사라진다.</summary>
        public int HitsLeft;
        /// <summary>맞힌 적 수(World.BulletHitIds에서 이 탄의 칸 중 앞의 몇 개를 쓰는지).</summary>
        public int HitCount;
    }

    /// <summary>
    /// 자동 사격과 탄: 조준(사거리 안 가장 가까운 적) → 발사 → 탄 이동 · 맞기 → 죽은 적 빼기.
    /// 상태는 World에만 두고 여기는 계산용 작업 공간(고정 크기 배열)만 든다 — 틱마다 할당하지 않는다.
    /// </summary>
    sealed class WeaponRules
    {
        // 탄 하나가 한 틱에 살필 격자 칸의 최대 수. 넘으면(아주 빠른 탄) 적 전부를 본다.
        const int MaxCellsVisited = 25;

        readonly WeaponRulesData data;
        readonly EnemyKindData[] enemyKinds;
        readonly EnemyGrid grid;
        readonly float tickSeconds;
        readonly int[] intervalTicks;
        readonly int[] nearest;
        readonly float[] nearestDistanceSquared;
        readonly int[] visited = new int[MaxCellsVisited];
        readonly float reach;
        readonly int ticksPerSecond;
        readonly int[] maxShots;
        readonly int[] maxPierce;
        readonly int hitMemory;

        public WeaponRules(WeaponRulesData data, UpgradeRulesData upgrades, EnemyRulesData enemies, EnemyGrid grid, int ticksPerSecond)
        {
            this.ticksPerSecond = ticksPerSecond;
            // 강화를 모두 최대 단계까지 받아도 넘치지 않게 작업 공간 · 맞힌 적 기억 칸을 판 시작 전에 잡는다.
            maxShots = new int[data.Kinds.Length];
            maxPierce = new int[data.Kinds.Length];
            int widestVolley = 1;
            hitMemory = 1;
            for (int k = 0; k < data.Kinds.Length; k++)
            {
                maxShots[k] = Math.Max(1, upgrades.MaxPossible(StatTarget.ShotsPerVolley, k, data.Kinds[k].ShotsPerVolley));
                maxPierce[k] = Math.Max(0, upgrades.MaxPossible(StatTarget.Pierce, k, data.Kinds[k].Pierce));
                widestVolley = Math.Max(widestVolley, maxShots[k]);
                hitMemory = Math.Max(hitMemory, maxPierce[k] + 1);
            }
            this.data = data;
            enemyKinds = enemies.Kinds;
            this.grid = grid;
            tickSeconds = 1f / ticksPerSecond;
            intervalTicks = new int[data.Kinds.Length];
            for (int k = 0; k < intervalTicks.Length; k++) intervalTicks[k] = IntervalTicks(data.Kinds[k].FireIntervalSeconds, ticksPerSecond);
            nearest = new int[widestVolley];
            nearestDistanceSquared = new float[nearest.Length];
            reach = data.MaxBulletRadius + enemies.MaxRadius;
        }

        /// <summary>발사 간격(초)을 틱 수로: 반올림, 최소 1틱.</summary>
        public static int IntervalTicks(float seconds, int ticksPerSecond)
        {
            return Math.Max(1, (int)Math.Round(seconds * ticksPerSecond, MidpointRounding.AwayFromZero));
        }

        public int IntervalTicksOf(int kind) => intervalTicks[kind];

        /// <summary>강화가 걸린 발사 간격(틱).</summary>
        public int IntervalTicksOf(World world, int kind) => IntervalTicks(Value(world, kind, StatTarget.FireInterval), ticksPerSecond);

        /// <summary>강화가 걸린 무기 값. 표에 없는 대상(플레이어 값)은 0에서 시작한다.</summary>
        public float Value(World world, int kind, StatTarget target)
        {
            ref readonly WeaponKindData k = ref data.Kinds[kind];
            float baseValue = target == StatTarget.FireInterval ? k.FireIntervalSeconds
                : target == StatTarget.Damage ? k.Damage
                : target == StatTarget.ShotsPerVolley ? k.ShotsPerVolley
                : target == StatTarget.Pierce ? k.Pierce
                : target == StatTarget.BulletSpeed ? k.BulletSpeed
                : target == StatTarget.Range ? k.Range
                : 0f;
            if (target == StatTarget.ShotsPerVolley) return Stats.IntValue(world, kind, target, k.ShotsPerVolley, 1, maxShots[kind]);
            if (target == StatTarget.Pierce) return Stats.IntValue(world, kind, target, k.Pierce, 0, maxPierce[kind]);
            return Math.Max(0f, Stats.Value(world, kind, target, baseValue));
        }

        public void InitWorld(World world)
        {
            world.Weapons = new WeaponState[data.StartingWeapons.Length];
            for (int i = 0; i < world.Weapons.Length; i++) world.Weapons[i] = new WeaponState { Kind = data.StartingWeapons[i] };
            world.Bullets = new BulletState[data.MaxBullets];
            world.BulletHitStride = hitMemory;
            world.BulletHitIds = new int[data.MaxBullets * world.BulletHitStride];
        }

        public Vec2 Muzzle(World world) => world.Player.Position + data.MuzzleOffset;

        // 무기마다: 쿨다운을 한 틱 줄이고, 0이면 사거리 안 가장 가까운 적을 노려 쏜다. 쏠 적이 없으면 0에서 기다린다.
        public void Fire(World world)
        {
            Vec2 muzzle = Muzzle(world);
            for (int w = 0; w < world.Weapons.Length; w++)
            {
                ref WeaponState weapon = ref world.Weapons[w];
                if (weapon.CooldownTicks > 0) weapon.CooldownTicks--;
                if (weapon.CooldownTicks > 0 || world.FiringPaused) continue;

                int kindIndex = weapon.Kind;
                float range = Value(world, kindIndex, StatTarget.Range);
                int shots = Stats.IntValue(world, kindIndex, StatTarget.ShotsPerVolley, data.Kinds[kindIndex].ShotsPerVolley, 1, maxShots[kindIndex]);
                int pierce = Stats.IntValue(world, kindIndex, StatTarget.Pierce, data.Kinds[kindIndex].Pierce, 0, maxPierce[kindIndex]);
                int found = FindNearest(world, muzzle, range, shots);
                if (found == 0) continue;

                for (int s = 0; s < shots; s++)
                {
                    if (world.BulletCount >= world.Bullets.Length) break;
                    Vec2 target = world.Enemies[nearest[s % found]].Position;
                    Vec2 direction = (target - muzzle).Normalized;
                    if (direction.SqrMagnitude == 0f) direction = new Vec2(world.Player.FacingX, 0f);
                    world.Bullets[world.BulletCount] = new BulletState
                    {
                        Id = world.AllocateEntityId(),
                        Kind = weapon.Kind,
                        Position = muzzle,
                        PreviousPosition = muzzle,
                        Direction = direction,
                        RangeLeft = range,
                        HitsLeft = pierce + 1,
                        HitCount = 0,
                    };
                    world.BulletCount++;
                    world.BulletsFiredTotal++;
                }
                weapon.CooldownTicks = IntervalTicksOf(world, kindIndex);
            }
        }

        // 사거리(+ 적 몸) 안의 살아 있는 적 중 가까운 순서로 최대 want마리를 nearest에 담는다. 거리가 같으면 번호가 앞인 쪽.
        int FindNearest(World world, Vec2 from, float range, int want)
        {
            int found = 0;
            EnemyState[] enemies = world.Enemies;
            for (int i = 0; i < world.EnemyCount; i++)
            {
                if (enemies[i].Dead) continue;
                float limit = range + enemyKinds[enemies[i].Kind].Radius;
                float d2 = (enemies[i].Position - from).SqrMagnitude;
                if (d2 > limit * limit) continue;
                if (found == want && d2 >= nearestDistanceSquared[found - 1]) continue;

                int slot = found < want ? found++ : found - 1;
                while (slot > 0 && nearestDistanceSquared[slot - 1] > d2)
                {
                    nearest[slot] = nearest[slot - 1];
                    nearestDistanceSquared[slot] = nearestDistanceSquared[slot - 1];
                    slot--;
                }
                nearest[slot] = i;
                nearestDistanceSquared[slot] = d2;
            }
            return found;
        }

        /// <summary>탄을 한 틱 움직이고 지나간 길에 닿은 적을 맞힌다. 죽은 적은 표시만 하고(Dead) 빼는 것은 RemoveDeadEnemies.</summary>
        public void StepBullets(World world)
        {
            if (world.BulletCount == 0) return;
            grid.Build(world.Enemies, world.EnemyCount);

            int i = 0;
            while (i < world.BulletCount)
            {
                ref BulletState b = ref world.Bullets[i];
                WeaponKindData kind = data.Kinds[b.Kind];
                b.PreviousPosition = b.Position;
                float travel = Math.Min(Value(world, b.Kind, StatTarget.BulletSpeed) * tickSeconds, b.RangeLeft);
                Vec2 start = b.Position;
                Vec2 end = start + b.Direction * travel;
                b.Position = end;
                b.RangeLeft -= travel;

                HitAlong(world, i, start, end, kind, Value(world, b.Kind, StatTarget.Damage));

                if (b.HitsLeft <= 0 || b.RangeLeft <= 0f)
                {
                    RemoveBullet(world, i);
                    continue; // 맨 끝 탄이 이 자리로 왔다(아직 이번 틱에 안 움직인 탄)
                }
                i++;
            }
        }

        // 길 위에서 먼저 닿는 적부터 HitsLeft가 남는 동안 맞힌다. 한 탄은 같은 적을 두 번 맞히지 않는다.
        void HitAlong(World world, int bulletIndex, Vec2 start, Vec2 end, in WeaponKindData kind, float damage)
        {
            ref BulletState b = ref world.Bullets[bulletIndex];
            EnemyState[] enemies = world.Enemies;
            if (world.EnemyCount == 0) return;

            float pad = kind.BulletRadius + (reach - data.MaxBulletRadius);
            int cx0 = grid.Cell(Math.Min(start.X, end.X) - pad);
            int cx1 = grid.Cell(Math.Max(start.X, end.X) + pad);
            int cy0 = grid.Cell(Math.Min(start.Y, end.Y) - pad);
            int cy1 = grid.Cell(Math.Max(start.Y, end.Y) + pad);
            long cells = (long)(cx1 - cx0 + 1) * (cy1 - cy0 + 1);
            bool everyone = cells > MaxCellsVisited;

            while (b.HitsLeft > 0)
            {
                int best = -1;
                float bestT = float.MaxValue;
                if (everyone)
                {
                    for (int j = 0; j < world.EnemyCount; j++) Consider(world, bulletIndex, j, start, end, kind.BulletRadius, ref best, ref bestT);
                }
                else
                {
                    int visitedCount = 0;
                    for (int cy = cy0; cy <= cy1; cy++)
                    {
                        for (int cx = cx0; cx <= cx1; cx++)
                        {
                            int bucket = EnemyGrid.Bucket(cx, cy);
                            if (EnemySeparation.Seen(visited, bucket, visitedCount)) continue;
                            visited[visitedCount++] = bucket;
                            for (int j = grid.Head[bucket]; j >= 0; j = grid.Next[j]) Consider(world, bulletIndex, j, start, end, kind.BulletRadius, ref best, ref bestT);
                        }
                    }
                }
                if (best < 0) return;

                ref EnemyState e = ref enemies[best];
                e.LastHitTick = world.Tick;
                if (!world.EnemiesInvincible)
                {
                    e.Health -= damage;
                }
                if (!world.EnemiesInvincible && e.Health <= 0f)
                {
                    e.Dead = true;
                    world.EnemiesKilledTotal++;
                    if (e.Kind < world.EnemiesKilledByKind.Length) world.EnemiesKilledByKind[e.Kind]++;
                }
                world.BulletHitIds[bulletIndex * world.BulletHitStride + b.HitCount] = e.Id;
                b.HitCount++;
                b.HitsLeft--;
            }
        }

        void Consider(World world, int bulletIndex, int enemyIndex, Vec2 start, Vec2 end, float bulletRadius, ref int best, ref float bestT)
        {
            ref readonly EnemyState e = ref world.Enemies[enemyIndex];
            if (e.Dead || AlreadyHit(world, bulletIndex, e.Id)) return;
            float r = bulletRadius + enemyKinds[e.Kind].Radius;
            float t = SegmentHit(start, end, e.Position, r);
            if (t < 0f) return;
            if (t < bestT || (t == bestT && enemyIndex < best))
            {
                best = enemyIndex;
                bestT = t;
            }
        }

        /// <summary>선분 start→end가 원(center, radius)에 닿으면 가장 가까이 가는 지점의 비율 t(0~1), 아니면 -1.</summary>
        static float SegmentHit(Vec2 start, Vec2 end, Vec2 center, float radius)
        {
            Vec2 d = end - start;
            float len2 = d.SqrMagnitude;
            float t = 0f;
            if (len2 > 0f)
            {
                Vec2 toCenter = center - start;
                t = (toCenter.X * d.X + toCenter.Y * d.Y) / len2;
                t = t < 0f ? 0f : (t > 1f ? 1f : t);
            }
            Vec2 closest = start + d * t;
            return (center - closest).SqrMagnitude <= radius * radius ? t : -1f;
        }

        static bool AlreadyHit(World world, int bulletIndex, int enemyId)
        {
            int baseIndex = bulletIndex * world.BulletHitStride;
            int count = world.Bullets[bulletIndex].HitCount;
            for (int k = 0; k < count; k++)
            {
                if (world.BulletHitIds[baseIndex + k] == enemyId) return true;
            }
            return false;
        }

        // 맨 끝 탄을 빈자리로 당긴다(맞힌 적 기억도 같이).
        static void RemoveBullet(World world, int index)
        {
            int last = world.BulletCount - 1;
            if (index != last)
            {
                world.Bullets[index] = world.Bullets[last];
                int stride = world.BulletHitStride;
                Array.Copy(world.BulletHitIds, last * stride, world.BulletHitIds, index * stride, stride);
            }
            world.BulletCount--;
        }

        /// <summary>이번 틱에 죽은 적을 뺀다: 빈자리에 맨 끝 적을 당겨 채운다. ID는 다시 쓰지 않는다.</summary>
        public static void RemoveDeadEnemies(World world)
        {
            int i = 0;
            while (i < world.EnemyCount)
            {
                if (!world.Enemies[i].Dead)
                {
                    i++;
                    continue;
                }
                int last = world.EnemyCount - 1;
                world.Enemies[i] = world.Enemies[last];
                world.EnemyCount--;
            }
        }

        /// <summary>탄을 모두 없앤다(디버그).</summary>
        public static void ClearBullets(World world)
        {
            world.BulletCount = 0;
        }
    }
}
