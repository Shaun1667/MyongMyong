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
        readonly EnemyGrid enemyGrid;
        readonly EnemySeparation separation;
        readonly WeaponRules weaponRules;
        readonly ProgressionRules progression;
        readonly HealthRules health;
        readonly long survivalTicks;

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
        {
            if (ticksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            TicksPerSecond = ticksPerSecond;
            TickSeconds = 1f / ticksPerSecond;
            this.player = player;
            this.enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            this.weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            this.experience = experience ?? throw new ArgumentNullException(nameof(experience));
            this.upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
            foreach (var row in upgrades.Rows)
            {
                if (row.WeaponKind >= weapons.Kinds.Length) throw new ArgumentOutOfRangeException(nameof(upgrades), "upgrade refers to a missing weapon kind");
            }
            enemyGrid = new EnemyGrid(enemies.MaxAlive, enemies.MaxRadius);
            separation = new EnemySeparation(enemyGrid, enemies.MaxAlive);
            weaponRules = new WeaponRules(weapons, upgrades, enemies, enemyGrid, ticksPerSecond);
            progression = new ProgressionRules(experience, upgrades, enemies, player, ticksPerSecond);
            health = new HealthRules(enemies, player, ticksPerSecond);
            survivalTicks = RunClock.TicksOf(run.SurvivalSeconds, ticksPerSecond);
        }

        public int TicksPerSecond { get; }

        public float TickSeconds { get; }

        public EnemyRulesData Enemies => enemies;

        public WeaponRulesData Weapons => weapons;

        public ExperienceRulesData Experience => experience;

        public UpgradeRulesData Upgrades => upgrades;

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

        /// <summary>판이 끝날 때까지 버틴 시간(초). 진행 중이면 지금까지.</summary>
        public float SurvivedSeconds(World world) => (world.IsOver ? world.Result.Ticks : world.Tick) * TickSeconds;

        /// <summary>판 시간(틱): 진행 중이면 지금 틱, 끝났으면 끝난 틱. HUD 타이머가 읽는다(레벨업 선택 중에는 틱이 멈추므로 타이머도 멈춘다).</summary>
        public long RunTicks(World world) => world.IsOver ? world.Result.Ticks : world.Tick;

        /// <summary>생존 목표(틱). 이 틱에 닿으면 승리. 0 = 시간 승리 없음.</summary>
        public long SurvivalTicks => survivalTicks;

        /// <summary>생존 목표(초). 0 = 시간 승리 없음.</summary>
        public float SurvivalSeconds => survivalTicks * TickSeconds;

        /// <summary>
        /// 순서: 플레이어 이동 → 적 추적 → 적끼리 밀어내기 → 적 접촉 피해(체력 0이면 그 틱에서 판 끝) → 발사(사거리 안 가장 가까운 적) → 탄 이동 · 맞기
        /// → 젬 끌기 · 줍기(경험치 · 레벨) → 죽은 적 자리에 젬 떨어뜨리기 → 죽은 적 빼기 → 스폰(새 적 · 새 젬은 그 틱에는 움직이지 않는다)
        /// → 틱 + 1 → 생존 목표 틱에 닿았으면 승리(같은 틱에 체력 0이면 패배가 먼저).
        /// 레벨업 선택을 기다리는 동안(PendingLevelUps > 0)과 판이 끝난 뒤(IsOver)에는 아무것도 하지 않는다 — 틱도 멈춘다.
        /// </summary>
        public void Step(World world, in InputFrame input)
        {
            if (world.IsOver || world.PendingLevelUps > 0) return;
            StepPlayer(world, input);
            StepEnemies(world);
            if (health.StepContact(world))
            {
                // 죽은 틱: 나머지(발사 · 탄 · 젬 · 스폰)는 하지 않고, 이 틱까지 센 결과를 남긴다.
                world.Tick++;
                HealthRules.EndRun(world, RunOutcome.Defeat);
                return;
            }
            weaponRules.Fire(world);
            weaponRules.StepBullets(world);
            progression.StepGems(world);
            progression.DropGems(world);
            WeaponRules.RemoveDeadEnemies(world);
            StepSpawning(world);
            world.Tick++;
            if (survivalTicks > 0 && world.Tick >= survivalTicks) HealthRules.EndRun(world, RunOutcome.Victory);
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

        // 플레이어를 향해 직선으로. 다 왔으면 플레이어 자리에서 멈춘다.
        void StepEnemies(World world)
        {
            Vec2 target = world.Player.Position;
            EnemyState[] list = world.Enemies;
            for (int i = 0; i < world.EnemyCount; i++)
            {
                ref EnemyState e = ref list[i];
                e.PreviousPosition = e.Position;
                Vec2 to = target - e.Position;
                float distance = to.Magnitude;
                float step = enemies.Kinds[e.Kind].MoveSpeed * TickSeconds;
                e.Position = distance <= step ? target : e.Position + to * (step / distance);
            }
            separation.Apply(list, world.EnemyCount, enemies.Kinds, enemies.SeparationPush);
        }

        // 시간대의 초당 마릿수만큼 몫을 쌓고 1이 될 때마다 한 마리. 가득 찼으면 그 몫은 버린다(나중에 몰아서 나오지 않게).
        void StepSpawning(World world)
        {
            if (world.SpawningPaused) return;
            int phaseIndex = enemies.PhaseAt(ElapsedSeconds(world));
            if (phaseIndex < 0) return;
            SpawnPhaseData phase = enemies.Phases[phaseIndex];
            world.SpawnBudget += phase.SpawnsPerSecond * TickSeconds;
            while (world.SpawnBudget >= 1f)
            {
                world.SpawnBudget -= 1f;
                TrySpawn(world, phase.Kind);
            }
        }

        bool TrySpawn(World world, int kind)
        {
            if (world.EnemyCount >= world.Enemies.Length) return false;
            float angle = world.SpawnRandom.Range(0f, FullTurn);
            Vec2 offset = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle)) * enemies.SpawnRadius;
            Vec2 position = world.Player.Position + offset;
            world.Enemies[world.EnemyCount] = new EnemyState
            {
                Id = world.AllocateEntityId(),
                Kind = kind,
                Position = position,
                PreviousPosition = position,
                Health = enemies.Kinds[kind].MaxHealth,
                LastHitTick = EnemyState.NeverHit,
            };
            world.EnemyCount++;
            world.EnemiesSpawnedTotal++;
            return true;
        }

        /// <summary>지금 남은 자리만큼 count마리를 바로 스폰(디버그 · 나중의 이벤트). 종류는 지금 시간대의 것(첫 시간대 전이면 첫 시간대). 스폰한 수를 돌려준다.</summary>
        public int SpawnEnemies(World world, int count)
        {
            if (enemies.Phases.Length == 0) return 0;
            int phaseIndex = Math.Max(0, enemies.PhaseAt(ElapsedSeconds(world)));
            int kind = enemies.Phases[phaseIndex].Kind;
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TrySpawn(world, kind)) break;
                spawned++;
            }
            return spawned;
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
            world.Player.Health = health.MaxHealth;
        }

        /// <summary>걸린 강화를 모두 푼다(디버그). 레벨 · 경험치는 그대로.</summary>
        public void ResetUpgrades(World world)
        {
            Stats.Reset(world);
        }
    }
}
