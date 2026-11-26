using System;

namespace Myong.Core
{
    /// <summary>플레이어 수치(데이터 에셋 PlayerTuning에서 옮겨 온 값).</summary>
    public readonly struct PlayerTuningData
    {
        /// <summary>이동 속도(유닛/초).</summary>
        public readonly float MoveSpeed;
        /// <summary>줍는 반경(유닛). 이 안에 들어온 경험치 젬은 플레이어 쪽으로 끌려온다.</summary>
        public readonly float PickupRadius;
        /// <summary>최대 체력(판 시작 때 체력). 0이 되면 판이 끝난다.</summary>
        public readonly float MaxHealth;
        /// <summary>몸 반지름(유닛). 적 반지름과 합보다 가까우면 닿은 것.</summary>
        public readonly float Radius;
        /// <summary>맞은 뒤 접촉 피해를 받지 않는 시간(초, 틱으로 반올림).</summary>
        public readonly float HurtInvulnerableSeconds;

        public PlayerTuningData(float moveSpeed, float pickupRadius = 0f, float maxHealth = 0f, float radius = 0f, float hurtInvulnerableSeconds = 0f)
        {
            MoveSpeed = moveSpeed;
            PickupRadius = pickupRadius;
            MaxHealth = maxHealth;
            Radius = radius;
            HurtInvulnerableSeconds = hurtInvulnerableSeconds;
        }
    }

    /// <summary>한 틱에 들어오는 입력. 키보드 · 패드 · 가상 조이스틱이 모두 이 모양이 된다.</summary>
    public struct InputFrame
    {
        public Vec2 Move;
    }

    public static class MoveRules
    {
        /// <summary>길이가 1을 넘는 입력(키보드 대각선)은 1로 줄인다. 스틱을 살짝 민 값(1 미만)은 그대로 둔다.</summary>
        public static Vec2 ClampMove(Vec2 raw)
        {
            return raw.SqrMagnitude > 1f ? raw.Normalized : raw;
        }
    }

    /// <summary>고정 틱 시뮬레이션. Step 한 번 = 한 틱. 상태는 World에만 있다.</summary>
    public sealed class Simulation
    {
        const float FullTurn = (float)(Math.PI * 2.0);

        readonly PlayerTuningData player;
        readonly EnemyRulesData enemies;
        readonly WeaponRulesData weapons;
        readonly ExperienceRulesData experience;
        readonly UpgradeRulesData upgrades;
        readonly PassiveRulesData passives;
        readonly EnemyGrid enemyGrid;
        readonly EnemySeparation separation;
        readonly WeaponRules weaponRules;
        readonly ProgressionRules progression;
        readonly HealthRules health;
        readonly long survivalTicks;
        // 웨이브마다 시작 틱 · 간격 틱(판 시작 전에 한 번 계산, Step 중 할당 없음).
        readonly long[] waveStartTicks;
        readonly long[] waveIntervalTicks;
        // 목표 이후에 시작하는 웨이브(마무리 웨이브)인지. 마무리 웨이브는 목표에 닿은 뒤에만, 나머지는 닿기 전에만 나온다.
        readonly bool[] waveIsFinale;
        // 목표에 닿은 뒤 판이 끝날 때까지 길어야(틱). 0 = 목표에서 바로 판 끝(1-7 방식).
        readonly long finaleTicks;

        public Simulation(int ticksPerSecond, PlayerTuningData player)
            : this(ticksPerSecond, player, EnemyRulesData.None, WeaponRulesData.None)
        {
        }

        public Simulation(int ticksPerSecond, PlayerTuningData player, EnemyRulesData enemies)
            : this(ticksPerSecond, player, enemies, WeaponRulesData.None)
        {
        }

        public Simulation(int ticksPerSecond, PlayerTuningData player, EnemyRulesData enemies, WeaponRulesData weapons)
            : this(ticksPerSecond, player, enemies, weapons, ExperienceRulesData.None, UpgradeRulesData.None)
        {
        }

        public Simulation(int ticksPerSecond, PlayerTuningData player, EnemyRulesData enemies, WeaponRulesData weapons, ExperienceRulesData experience, UpgradeRulesData upgrades)
            : this(ticksPerSecond, player, enemies, weapons, experience, upgrades, RunRulesData.None)
        {
        }

        public Simulation(int ticksPerSecond, PlayerTuningData player, EnemyRulesData enemies, WeaponRulesData weapons, ExperienceRulesData experience, UpgradeRulesData upgrades, RunRulesData run)
            : this(ticksPerSecond, player, enemies, weapons, experience, upgrades, run, PassiveRulesData.None)
        {
        }

        public Simulation(int ticksPerSecond, PlayerTuningData player, EnemyRulesData enemies, WeaponRulesData weapons, ExperienceRulesData experience, UpgradeRulesData upgrades, RunRulesData run, PassiveRulesData passives)
        {
            if (ticksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            TicksPerSecond = ticksPerSecond;
            TickSeconds = 1f / ticksPerSecond;
            this.player = player;
            this.enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            this.weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            this.experience = experience ?? throw new ArgumentNullException(nameof(experience));
            this.upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
            this.passives = passives ?? throw new ArgumentNullException(nameof(passives));
            foreach (var row in upgrades.Rows)
            {
                if (row.WeaponKind >= weapons.Kinds.Length) throw new ArgumentOutOfRangeException(nameof(upgrades), "upgrade refers to a missing weapon kind");
            }
            enemyGrid = new EnemyGrid(enemies.MaxAlive, enemies.MaxRadius);
            separation = new EnemySeparation(enemyGrid, enemies.MaxAlive);
            weaponRules = new WeaponRules(weapons, upgrades, enemies, enemyGrid, ticksPerSecond);
            progression = new ProgressionRules(experience, upgrades, passives, enemies, player, ticksPerSecond);
            health = new HealthRules(enemies, player, ticksPerSecond);
            survivalTicks = RunClock.TicksOf(run.SurvivalSeconds, ticksPerSecond);
            waveStartTicks = new long[enemies.Waves.Length];
            waveIntervalTicks = new long[enemies.Waves.Length];
            waveIsFinale = new bool[enemies.Waves.Length];
            finaleTicks = RunClock.TicksOf(run.FinaleSeconds, ticksPerSecond);
            for (int w = 0; w < enemies.Waves.Length; w++)
            {
                waveStartTicks[w] = RunClock.TicksOf(enemies.Waves[w].StartSeconds, ticksPerSecond);
                waveIntervalTicks[w] = enemies.Waves[w].IntervalSeconds > 0f ? Math.Max(1L, RunClock.TicksOf(enemies.Waves[w].IntervalSeconds, ticksPerSecond)) : 0L;
                waveIsFinale[w] = survivalTicks > 0 && waveStartTicks[w] >= survivalTicks;
            }
        }

        public int TicksPerSecond { get; }

        public float TickSeconds { get; }

        public EnemyRulesData Enemies => enemies;

        public WeaponRulesData Weapons => weapons;

        public ExperienceRulesData Experience => experience;

        public UpgradeRulesData Upgrades => upgrades;

        public PassiveRulesData Passives => passives;

        /// <summary>무기 종류의 기본 발사 간격(틱, 강화 없이). 초 × 초당 틱을 반올림, 최소 1.</summary>
        public int FireIntervalTicks(int weaponKind) => weaponRules.IntervalTicksOf(weaponKind);

        /// <summary>이 판에서 강화가 걸린 발사 간격(틱).</summary>
        public int FireIntervalTicks(World world, int weaponKind) => weaponRules.IntervalTicksOf(world, weaponKind);

        /// <summary>이 판에서 강화가 걸린 무기 값(표의 기본 값에 강화를 건 것).</summary>
        public float WeaponValue(World world, int weaponKind, StatTarget target) => weaponRules.Value(world, weaponKind, target);

        /// <summary>이 판에서 강화가 걸린 이동 속도.</summary>
        public float MoveSpeed(World world) => progression.MoveSpeed(world);

        /// <summary>이 판에서 강화가 걸린 줍는 반경.</summary>
        public float PickupRadius(World world) => progression.PickupRadius(world);

        /// <summary>level → level+1에 필요한 경험치.</summary>
        public int ExperienceToNext(int level) => progression.ToNext(level);

        /// <summary>지금 탄이 나올 자리(플레이어 위치 + 총구 오프셋).</summary>
        public Vec2 Muzzle(World world) => weaponRules.Muzzle(world);

        public World CreateWorld(int seed)
        {
            var world = new World
            {
                Seed = seed,
                SpawnRandom = Rng.ForStream(seed, RandomStream.Spawn),
                LevelUpRandom = Rng.ForStream(seed, RandomStream.LevelUp),
                Enemies = new EnemyState[enemies.MaxAlive],
                EnemiesSpawnedByKind = new long[enemies.Kinds.Length],
                EnemiesKilledByKind = new long[enemies.Kinds.Length],
            };
            world.Player.Id = world.AllocateEntityId();
            world.Player.FacingX = Facing.Right;
            weaponRules.InitWorld(world);
            progression.InitWorld(world, weapons.Kinds.Length);
            health.InitWorld(world);
            return world;
        }

        /// <summary>판이 시작된 뒤 지난 시간(초) = 틱 수 × 틱 길이.</summary>
        public float ElapsedSeconds(World world) => world.Tick * TickSeconds;

        /// <summary>최대 체력(PlayerTuning).</summary>
        public float MaxHealth => health.MaxHealth;

        /// <summary>맞은 뒤 무적 시간(틱).</summary>
        public int HurtInvulnerableTicks => health.InvulnerableTicks;

        /// <summary>판이 끝날 때까지 버틴 시간(초) = RunTicks × 틱 길이. 진행 중이면 지금까지, 목표에 닿았으면 목표 시간.</summary>
        public float SurvivedSeconds(World world) => RunTicks(world) * TickSeconds;

        /// <summary>
        /// 판 시간(틱): 진행 중이면 지금 틱, 끝났으면 끝난 틱. HUD 타이머가 읽는다(레벨업 선택 중에는 틱이 멈추므로 타이머도 멈춘다).
        /// 생존 목표에 닿은 뒤에는 목표 틱에서 멈춘다(마무리 중에도 30:00).
        /// </summary>
        public long RunTicks(World world)
        {
            if (world.GoalReached && survivalTicks > 0) return survivalTicks;
            return world.IsOver ? world.Result.Ticks : world.Tick;
        }

        /// <summary>목표에 닿은 뒤 판이 끝날 때까지 길어야(틱, RunTuning). 0 = 목표에서 바로 판 끝.</summary>
        public long FinaleTicks => finaleTicks;

        /// <summary>마무리가 끝날 때까지 남은 틱(목표 전이면 마무리 길이 전부, 끝났으면 0).</summary>
        public long FinaleTicksLeft(World world)
        {
            if (world.IsOver) return 0;
            if (!world.GoalReached) return finaleTicks;
            return Math.Max(0L, finaleTicks - (world.Tick - world.GoalReachedTick));
        }

        /// <summary>마무리 웨이브(목표 이후에 시작)인지.</summary>
        public bool IsFinaleWave(int wave) => wave >= 0 && wave < waveIsFinale.Length && waveIsFinale[wave];

        /// <summary>생존 목표(틱). 이 틱에 닿으면 승리. 0 = 시간 승리 없음.</summary>
        public long SurvivalTicks => survivalTicks;

        /// <summary>생존 목표(초). 0 = 시간 승리 없음.</summary>
        public float SurvivalSeconds => survivalTicks * TickSeconds;

        /// <summary>
        /// 순서: 플레이어 이동 → 적 추적 → 적끼리 밀어내기 → 적 접촉 피해 × 받는 피해 배율(체력 0이면 그 틱에서 판 끝) → 체력 재생 → 발사(사거리 안 가장 가까운 적) → 탄 이동 · 맞기
        /// → 젬 끌기 · 줍기(경험치 · 레벨) → 죽은 적 자리에 젬 떨어뜨리기 → 죽은 적 빼기 → 돌진하다 멀어진 적 빼기 → 스폰(시간대 비중 → 웨이브, 새 적 · 새 젬은 그 틱에는 움직이지 않는다)
        /// → 틱 + 1 → 생존 목표 틱에 닿았으면 승리 확정(같은 틱에 체력 0이면 패배가 먼저).
        /// 목표에 닿으면(1-11): 마무리 시간이 0이면 바로 승리로 판 끝. 아니면 남은 적이 모두 사라지고(처치 아님 · 젬 없음) 시간대 스폰 · 보통 웨이브는 멈추며
        /// 마무리 웨이브만 나온다. 그 뒤 죽거나(승리) 마무리 시간이 다 되면(체력 0 → 승리) 판이 끝난다.
        /// 레벨업 선택을 기다리는 동안(PendingLevelUps > 0)과 판이 끝난 뒤(IsOver)에는 아무것도 하지 않는다 — 틱도 멈춘다.
        /// </summary>
        public void Step(World world, in InputFrame input)
        {
            if (world.IsOver || world.PendingLevelUps > 0) return;
            // 건너뛰기(디버그)로 목표를 넘었으면 이 틱을 하기 전에 목표에 닿는다.
            if (survivalTicks > 0 && !world.GoalReached && world.Tick >= survivalTicks)
            {
                ReachGoal(world);
                if (world.IsOver) return;
            }
            StepPlayer(world, input);
            StepEnemies(world);
            if (health.StepContact(world, progression.DamageTakenScale(world)))
            {
                // 죽은 틱: 나머지(발사 · 탄 · 젬 · 스폰)는 하지 않고, 이 틱까지 센 결과를 남긴다.
                world.Tick++;
                HealthRules.EndRun(world, RunOutcome.Defeat);
                return;
            }
            progression.StepRegen(world);
            weaponRules.Fire(world);
            weaponRules.StepBullets(world);
            progression.StepGems(world);
            progression.DropGems(world);
            WeaponRules.RemoveDeadEnemies(world);
            RemoveEscapedEnemies(world);
            StepSpawning(world);
            world.Tick++;
            if (survivalTicks <= 0) return;
            if (!world.GoalReached)
            {
                if (world.Tick >= survivalTicks) ReachGoal(world);
            }
            else if (world.Tick - world.GoalReachedTick >= finaleTicks)
            {
                EndFinale(world);
            }
        }

        // 생존 목표에 닿음: 승리 확정. 마무리가 없으면 바로 판 끝(1-7 그대로), 있으면 남은 적을 모두 없앤다(처치 아님 · 젬 없음).
        void ReachGoal(World world)
        {
            world.GoalReachedTick = world.Tick;
            if (finaleTicks <= 0)
            {
                HealthRules.EndRun(world, RunOutcome.Victory);
                return;
            }
            world.EnemiesDismissedTotal += world.EnemyCount;
            world.EnemyCount = 0;
            world.SpawnBudget = 0f;
        }

        // 마무리 시간이 다 됐다: 잡히지 않았어도(디버그 무적 포함) 잡힌 것과 같이 끝낸다 — 체력 0 → 승리.
        void EndFinale(World world)
        {
            ref PlayerState p = ref world.Player;
            if (p.Health > 0f)
            {
                p.Health = 0f;
                p.LastHurtTick = world.Tick;
            }
            HealthRules.EndRun(world, RunOutcome.Victory);
        }

        void StepPlayer(World world, in InputFrame input)
        {
            ref PlayerState p = ref world.Player;
            Vec2 move = MoveRules.ClampMove(input.Move);
            p.PreviousPosition = p.Position;
            p.Velocity = move * progression.MoveSpeed(world);
            p.Position = p.Position + p.Velocity * TickSeconds;
            if (move.X > 0f) p.FacingX = Facing.Right;
            else if (move.X < 0f) p.FacingX = Facing.Left;
        }

        // 플레이어를 향해 직선으로. 다 왔으면 플레이어 자리에서 멈춘다. 돌진하는 적(Charge)은 그 방향으로 곧게.
        void StepEnemies(World world)
        {
            Vec2 target = world.Player.Position;
            EnemyState[] list = world.Enemies;
            for (int i = 0; i < world.EnemyCount; i++)
            {
                ref EnemyState e = ref list[i];
                e.PreviousPosition = e.Position;
                if (e.Charge.X != 0f || e.Charge.Y != 0f)
                {
                    e.Position = e.Position + e.Charge * TickSeconds;
                    continue;
                }
                Vec2 to = target - e.Position;
                float distance = to.Magnitude;
                float step = enemies.Kinds[e.Kind].MoveSpeed * TickSeconds;
                e.Position = distance <= step ? target : e.Position + to * (step / distance);
            }
            separation.Apply(list, world.EnemyCount, enemies.Kinds, enemies.SeparationPush);
        }

        // 돌진하는 적이 플레이어에게서 ChargeDespawnDistance보다 멀어지면 뺀다(처치 아님 · 젬 없음). 빈자리는 맨 끝 적이 채운다.
        void RemoveEscapedEnemies(World world)
        {
            float limit = enemies.ChargeDespawnDistance;
            if (limit <= 0f) return;
            float limit2 = limit * limit;
            Vec2 player = world.Player.Position;
            int i = 0;
            while (i < world.EnemyCount)
            {
                ref EnemyState e = ref world.Enemies[i];
                bool charging = e.Charge.X != 0f || e.Charge.Y != 0f;
                if (!charging || (e.Position - player).SqrMagnitude <= limit2)
                {
                    i++;
                    continue;
                }
                world.Enemies[i] = world.Enemies[world.EnemyCount - 1];
                world.EnemyCount--;
                world.EnemiesEscapedTotal++;
            }
        }

        // 시간대의 초당 마릿수만큼 몫을 쌓고 1이 될 때마다 한 마리(종류는 시간대 비중대로). 가득 찼으면 그 몫은 버린다(나중에 몰아서 나오지 않게).
        // 그 뒤 이 틱이 웨이브의 때면 웨이브를 낸다(표 순서대로).
        void StepSpawning(World world)
        {
            if (world.SpawningPaused) return;
            // 목표에 닿은 뒤에는 시간대 스폰이 없다(마무리 웨이브만).
            int phaseIndex = world.GoalReached ? -1 : enemies.PhaseAt(ElapsedSeconds(world));
            if (phaseIndex >= 0)
            {
                SpawnPhaseData phase = enemies.Phases[phaseIndex];
                world.SpawnBudget += phase.SpawnsPerSecond * TickSeconds;
                while (world.SpawnBudget >= 1f)
                {
                    world.SpawnBudget -= 1f;
                    if (world.EnemyCount >= world.Enemies.Length) continue;
                    TrySpawn(world, PickKind(world, phase));
                }
            }
            for (int w = 0; w < waveStartTicks.Length; w++)
            {
                if (WaveDue(w, world)) SpawnWave(world, enemies.Waves[w]);
            }
        }

        // 마무리 웨이브는 목표에 닿은 뒤에만, 나머지는 닿기 전에만. 마무리 웨이브의 시작은 목표에 닿은 틱보다 이르지 않다(건너뛰기로 목표를 넘어도 나온다).
        bool WaveDue(int wave, World world)
        {
            bool finale = waveIsFinale[wave];
            if (finale != world.GoalReached) return false;
            long start = waveStartTicks[wave];
            if (finale && world.GoalReachedTick > start) start = world.GoalReachedTick;
            return Due(start, waveIntervalTicks[wave], world.Tick);
        }

        static bool Due(long start, long interval, long tick)
        {
            if (tick < start) return false;
            return interval <= 0 ? tick == start : (tick - start) % interval == 0;
        }

        /// <summary>이 틱에 웨이브 w가 나오는 때인지(표의 시작 · 간격만 봄, 확인용).</summary>
        public bool IsWaveTick(int wave, long tick) => wave >= 0 && wave < waveStartTicks.Length && Due(waveStartTicks[wave], waveIntervalTicks[wave], tick);

        /// <summary>tick 뒤(포함) 웨이브 w가 처음 나오는 틱. 더 없으면 -1.</summary>
        public long NextWaveTick(int wave, long tick)
        {
            if (wave < 0 || wave >= waveStartTicks.Length) return -1;
            long start = waveStartTicks[wave];
            if (tick <= start) return start;
            long interval = waveIntervalTicks[wave];
            if (interval <= 0) return -1;
            long k = (tick - start + interval - 1) / interval;
            return start + k * interval;
        }

        // 한 종류 시간대는 난수를 뽑지 않는다(섞기 전과 같은 수열).
        int PickKind(World world, in SpawnPhaseData phase)
        {
            return phase.Mix.Length == 1 ? phase.Mix[0].Kind : phase.KindAt(world.SpawnRandom.NextFloat());
        }

        void SpawnWave(World world, in SpawnWaveData wave)
        {
            if (wave.Pattern == SpawnPattern.Swarm)
            {
                SpawnSwarm(world, wave);
                return;
            }
            float distance = wave.Distance > 0f ? wave.Distance : enemies.SpawnRadius;
            for (int i = 0; i < wave.Count; i++)
            {
                if (!TrySpawn(world, wave.Kind, distance)) break;
            }
        }

        // 한 각도(스폰 난수 하나)에 떼를 거의 정사각형으로 놓고, 모두 나온 순간의 플레이어 쪽으로 같은 속도로 돌진한다.
        void SpawnSwarm(World world, in SpawnWaveData wave)
        {
            if (wave.Count <= 0 || world.EnemyCount >= world.Enemies.Length) return;
            float angle = world.SpawnRandom.Range(0f, FullTurn);
            Vec2 outward = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle));
            Vec2 across = new Vec2(-outward.Y, outward.X);
            Vec2 center = world.Player.Position + outward * enemies.SpawnRadius;
            Vec2 charge = outward * -(enemies.Kinds[wave.Kind].MoveSpeed * wave.SpeedScale);
            int width = wave.SwarmRowWidth;
            for (int i = 0; i < wave.Count; i++)
            {
                float lateral = (i % width - (width - 1) * 0.5f) * wave.Spacing;
                float back = (i / width) * wave.Spacing;
                Vec2 position = center + across * lateral + outward * back;
                if (!TrySpawnAt(world, wave.Kind, position, charge)) break;
            }
        }

        bool TrySpawn(World world, int kind) => TrySpawn(world, kind, enemies.SpawnRadius);

        bool TrySpawn(World world, int kind, float distance)
        {
            if (world.EnemyCount >= world.Enemies.Length) return false;
            float angle = world.SpawnRandom.Range(0f, FullTurn);
            Vec2 offset = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
            return TrySpawnAt(world, kind, world.Player.Position + offset, Vec2.Zero);
        }

        bool TrySpawnAt(World world, int kind, Vec2 position, Vec2 charge)
        {
            if (world.EnemyCount >= world.Enemies.Length) return false;
            world.Enemies[world.EnemyCount] = new EnemyState
            {
                Id = world.AllocateEntityId(),
                Kind = kind,
                Position = position,
                PreviousPosition = position,
                Health = enemies.Kinds[kind].MaxHealth,
                LastHitTick = EnemyState.NeverHit,
                Charge = charge,
            };
            world.EnemyCount++;
            world.EnemiesSpawnedTotal++;
            if (kind < world.EnemiesSpawnedByKind.Length) world.EnemiesSpawnedByKind[kind]++;
            return true;
        }

        /// <summary>지금 남은 자리만큼 count마리를 바로 스폰(디버그 · 나중의 이벤트). 종류는 지금 시간대의 비중대로(첫 시간대 전이면 첫 시간대). 스폰한 수를 돌려준다.</summary>
        public int SpawnEnemies(World world, int count)
        {
            if (enemies.Phases.Length == 0) return 0;
            int phaseIndex = Math.Max(0, enemies.PhaseAt(ElapsedSeconds(world)));
            SpawnPhaseData phase = enemies.Phases[phaseIndex];
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (world.EnemyCount >= world.Enemies.Length) break;
                if (!TrySpawn(world, PickKind(world, phase))) break;
                spawned++;
            }
            return spawned;
        }

        /// <summary>종류를 정해 count마리를 바로 스폰(디버그). 스폰 원 위 아무 각도, 플레이어를 쫓는다. 없는 종류면 0.</summary>
        public int SpawnEnemies(World world, int count, int kind)
        {
            if (kind < 0 || kind >= enemies.Kinds.Length) return 0;
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TrySpawn(world, kind)) break;
                spawned++;
            }
            return spawned;
        }

        /// <summary>웨이브 w를 지금 바로 낸다(디버그). 없는 웨이브면 false.</summary>
        public bool SpawnWaveNow(World world, int wave)
        {
            if (wave < 0 || wave >= enemies.Waves.Length) return false;
            SpawnWave(world, enemies.Waves[wave]);
            return true;
        }

        /// <summary>적을 모두 없앤다(디버그). ID는 되돌리지 않는다 — 새 적은 새 ID.</summary>
        public void ClearEnemies(World world)
        {
            world.EnemyCount = 0;
            world.SpawnBudget = 0f;
        }

        /// <summary>날아가는 탄을 모두 없앤다(디버그). ID는 되돌리지 않는다.</summary>
        public void ClearBullets(World world)
        {
            WeaponRules.ClearBullets(world);
        }

        /// <summary>보간 없이 바로 옮긴다(디버그 순간이동 · 나중의 재배치).</summary>
        public void TeleportPlayer(World world, Vec2 position)
        {
            world.Player.Position = position;
            world.Player.PreviousPosition = position;
        }

        // ---- 경험치 · 레벨업 ----

        /// <summary>경험치를 바로 더한다(디버그 · 나중의 보상). 레벨이 오르면 대기열에 쌓이고 선택지를 뽑는다.</summary>
        public void AddExperience(World world, int amount)
        {
            progression.GainExperience(world, amount);
        }

        /// <summary>지금 보이는 선택지 중 choice번째(0부터)를 고른다. 대기열이 남으면 다음 선택지가 뽑힌다. 고를 수 없으면 false.</summary>
        public bool ChooseUpgrade(World world, int choice)
        {
            return progression.Choose(world, choice);
        }

        /// <summary>강화 표의 row번째 줄을 한 단계 건다(디버그). 최대 단계면 false.</summary>
        public bool ApplyUpgrade(World world, int row)
        {
            return progression.ApplyUpgrade(world, row);
        }

        // ---- 체력 · 판 끝 ----

        /// <summary>
        /// 판 시간을 seconds(초, 틱으로 반올림)로 건너뛴다(디버그). 앞으로만 — 지금보다 이르거나 같으면, 또는 판이 끝났으면 false.
        /// 틱 수만 바꾼다(적 · 탄 · 젬 · 체력은 그대로, 시간대 스폰은 그 시간의 것). 생존 목표 이상으로 건너뛰면 다음 Step에서 승리.
        /// </summary>
        public bool SkipTo(World world, float seconds)
        {
            long target = RunClock.TicksOf(seconds, TicksPerSecond);
            if (world.IsOver || target <= world.Tick) return false;
            world.Tick = target;
            return true;
        }

        /// <summary>판을 끝낸다(디버그 · 30분 승리도 같은 길). 결과 값을 남기고, 그 뒤 Step은 아무것도 하지 않는다.</summary>
        public void EndRun(World world, RunOutcome outcome)
        {
            HealthRules.EndRun(world, outcome);
        }

        /// <summary>플레이어에게 바로 피해(디버그). 무적 시간은 걸지 않는다. 0이 되면 판이 끝난다.</summary>
        public void DamagePlayer(World world, float damage)
        {
            if (world.IsOver || damage <= 0f) return;
            HealthRules.Hurt(world, damage);
            if (world.Player.Health <= 0f) HealthRules.EndRun(world, RunOutcome.Defeat);
        }

        /// <summary>체력을 최대로(디버그).</summary>
        public void FillHealth(World world)
        {
            if (world.IsOver) return;
            world.Player.Health = progression.MaxHealth(world);
        }

        /// <summary>걸린 강화를 모두 푼다(디버그). 레벨 · 경험치는 그대로.</summary>
        public void ResetUpgrades(World world)
        {
            Stats.Reset(world);
            float max = progression.MaxHealth(world);
            if (world.Player.Health > max) world.Player.Health = max;
        }

        // ---- 패시브 · 레벨업 선택지 ----

        /// <summary>레벨업 선택지 값(World.LevelUpChoices)이 무엇인지: 강화 줄 · 패시브 줄 · 대체 보상.</summary>
        public LevelUpOption DescribeChoice(int option) => progression.Describe(option);

        /// <summary>패시브 표 row번째 줄의 선택지 값.</summary>
        public int PassiveOption(int row) => progression.PassiveOption(row);

        /// <summary>대체 보상의 선택지 값.</summary>
        public int FallbackOption => progression.FallbackOption;

        /// <summary>패시브 표의 row번째 줄을 한 단계 건다(없으면 얻음, 디버그). 최대 단계거나 새 패시브인데 칸이 다 찼으면 false.</summary>
        public bool ApplyPassive(World world, int row) => progression.ApplyPassive(world, row);

        /// <summary>지금 선택지에 나올 수 있는 패시브인지(최대 단계 전 · 새 것이면 칸이 남음).</summary>
        public bool PassiveOfferable(World world, int row) => row >= 0 && row < passives.Rows.Length && progression.PassiveOfferable(world, row);

        /// <summary>강화 · 패시브가 걸린 최대 체력.</summary>
        public float MaxHealthOf(World world) => progression.MaxHealth(world);

        /// <summary>초당 체력 재생.</summary>
        public float HealthRegen(World world) => progression.HealthRegen(world);

        /// <summary>받는 피해 배율(1 = 그대로).</summary>
        public float DamageTakenScale(World world) => progression.DamageTakenScale(world);

        /// <summary>젬 경험치 배율(1 = 그대로).</summary>
        public float ExperienceGain(World world) => progression.ExperienceGain(world);

        /// <summary>체력을 채운다(최대 체력까지, 디버그 · 나중의 보상).</summary>
        public void Heal(World world, float amount) => progression.Heal(world, amount);
    }
}
