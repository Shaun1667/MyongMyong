using System;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 컴포지션 루트. 씬의 모든 것을 여기서 잇는다: 텍스트 표 읽기, 한 판(World) 만들기,
    /// 고정 틱으로 시뮬레이션 돌리기, 표시 쪽에 상태 넘기기. 디버그 진입점(DebugXxx)도 여기 모은다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameRoot : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] TextAsset stringsCsv;
        [SerializeField] PlayerTuning playerTuning;
        [SerializeField] SimulationTuning simulationTuning;
        [SerializeField] EnemyTable enemyTable;
        [SerializeField] SpawnTable spawnTable;
        [SerializeField] WeaponTable weaponTable;
        [SerializeField] ExperienceTable experienceTable;
        [SerializeField] UpgradeTable upgradeTable;
        [SerializeField] PassiveTable passiveTable;
        [SerializeField] RunTuning runTuning;
        [SerializeField] FeedbackTuning feedbackTuning;

        [Header("Scene")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] VirtualJoystick joystick;
        [SerializeField] PlayerView playerView;
        [SerializeField] EnemyRenderPool enemyView;
        [SerializeField] BulletRenderPool bulletView;
        [SerializeField] GemRenderPool gemView;
        [SerializeField] ExperienceHud experienceHud;
        [SerializeField] LevelUpView levelUpView;
        [SerializeField] PlayerHealthBar healthBar;
        [SerializeField] GameOverView gameOverView;
        [SerializeField] RunTimerHud timerHud;
        [SerializeField] PassiveHud passiveHud;

        [Header("Seed")]
        [Tooltip("Off: a new seed every run. On: always Fixed Seed.")]
        [SerializeField] bool useFixedSeed;
        [SerializeField] int fixedSeed;

        Simulation simulation;
        World world;
        float accumulator;
        readonly System.Diagnostics.Stopwatch stepTimer = new System.Diagnostics.Stopwatch();
        double lastStepMilliseconds;
        float frameMillisecondsAverage;
        bool debugAutoChooseFirst;
        Func<int, LevelUpOption> describeChoice;
        // 마무리(1-11): 목표 글을 보일 틱 수 · 마무리 웨이브 적의 이름 글 키(없으면 null).
        int goalBannerTicks;
        string finaleNameKey;

        void Awake()
        {
            if (stringsCsv != null) Text.Load(TextTable.ParseCsv(stringsCsv.text));
            if (levelUpView != null) levelUpView.Chosen += OnCardChosen;
            if (gameOverView != null) gameOverView.RestartPressed += OnRestartPressed;
            StartRun(NextSeed());
        }

        // 고정 시드가 켜져 있으면 그 시드, 아니면 매번 새 시드.
        int NextSeed() => useFixedSeed ? fixedSeed : Environment.TickCount;

        void StartRun(int seed)
        {
            EnemyRulesData enemyRules = enemyTable != null && spawnTable != null ? spawnTable.ToRules(enemyTable) : EnemyRulesData.None;
            WeaponRulesData weaponRules = weaponTable != null ? weaponTable.ToRules() : WeaponRulesData.None;
            ExperienceRulesData experienceRules = experienceTable != null ? experienceTable.ToRules() : ExperienceRulesData.None;
            UpgradeRulesData upgradeRules = upgradeTable != null ? upgradeTable.ToRules(weaponTable) : UpgradeRulesData.None;
            RunRulesData runRules = runTuning != null ? runTuning.ToRules() : RunRulesData.None;
            PassiveRulesData passiveRules = passiveTable != null ? passiveTable.ToRules() : PassiveRulesData.None;
            simulation = new Simulation(simulationTuning.TicksPerSecond, playerTuning.ToData(), enemyRules, weaponRules, experienceRules, upgradeRules, runRules, passiveRules);
            describeChoice = simulation.DescribeChoice;
            world = simulation.CreateWorld(seed);
            goalBannerTicks = feedbackTuning != null ? FeedbackRules.TicksOf(feedbackTuning.GoalBannerSeconds, simulation.TicksPerSecond) : 0;
            finaleNameKey = FindFinaleNameKey(enemyRules);
            accumulator = 0f;
            if (enemyView != null) enemyView.Prepare(enemyRules.MaxAlive, enemyTable, feedbackTuning, simulation.TicksPerSecond);
            if (bulletView != null) bulletView.Prepare(weaponRules.MaxBullets, weaponTable);
            if (gemView != null) gemView.Prepare(experienceRules.MaxGems, experienceTable != null ? experienceTable.GemVisualId : string.Empty);
            if (playerView != null)
            {
                playerView.Configure(feedbackTuning, simulation.TicksPerSecond);
                playerView.ResetView();
            }
        }

        // 마무리 웨이브(목표 이후에 시작) 중 처음 것의 적 이름 글 키(EnemyTable nameKey). 없으면 null.
        string FindFinaleNameKey(EnemyRulesData enemyRules)
        {
            if (enemyTable == null) return null;
            for (int w = 0; w < enemyRules.Waves.Length; w++)
            {
                if (!simulation.IsFinaleWave(w)) continue;
                int kind = enemyRules.Waves[w].Kind;
                if (kind < 0 || kind >= enemyTable.Rows.Count) continue;
                string key = enemyTable.Rows[kind].nameKey;
                if (!string.IsNullOrEmpty(key)) return key;
            }
            return null;
        }

        void OnDestroy()
        {
            if (levelUpView != null) levelUpView.Chosen -= OnCardChosen;
            if (gameOverView != null) gameOverView.RestartPressed -= OnRestartPressed;
        }

        // 판 끝 창의 [다시 시작](클릭 · 패드 · Enter). 디버그 재시작과 같은 길(StartRun), 새 시드.
        void OnRestartPressed()
        {
            if (world != null && world.IsOver) StartRun(NextSeed());
        }

        // 레벨업 창의 카드를 눌렀다(마우스 · 패드 확인). 적용은 Core.
        void OnCardChosen(int card)
        {
            if (world != null && world.IsChoosingUpgrade) simulation.ChooseUpgrade(world, card);
        }

        void Update()
        {
            float tick = simulation.TickSeconds;
            int budget = simulationTuning.MaxTicksPerFrame;
            // 레벨업 · 판 끝 창이 떠 있으면 가상 조이스틱을 막는다(누르던 것도 놓아 입력 0 — 창 뒤에서 이동으로 새지 않게).
            if (joystick != null) joystick.SetBlocked(world.IsChoosingUpgrade || world.IsOver);
            var frame = new InputFrame { Move = input != null ? input.ReadMove() : Vec2.Zero };

            // 레벨업 선택: 숫자키(입력 액션) 또는 디버그 자동 선택. 클릭 · 패드는 OnCardChosen.
            int card = input != null ? input.ReadChosenCard() : -1;
            if (world.IsChoosingUpgrade)
            {
                if (debugAutoChooseFirst) card = 0;
                if (card >= 0) simulation.ChooseUpgrade(world, card);
            }

            stepTimer.Restart();
            accumulator += Time.deltaTime;
            // 선택을 기다리는 동안 · 판이 끝난 뒤에는 틱을 돌리지 않고 쌓인 시간도 버린다(창을 닫아도 몰아서 가지 않게).
            bool paused = world.IsChoosingUpgrade || world.IsOver;
            if (paused) accumulator = 0f;
            while (accumulator >= tick && budget > 0 && !world.IsChoosingUpgrade && !world.IsOver)
            {
                simulation.Step(world, frame);
                accumulator -= tick;
                budget--;
            }
            // 한 프레임에 낼 수 있는 틱을 다 쓰고도 시간이 남으면 버린다(멈추는 대신 느려진다).
            if (accumulator >= tick || world.IsChoosingUpgrade || world.IsOver) accumulator = 0f;
            stepTimer.Stop();
            lastStepMilliseconds = stepTimer.Elapsed.TotalMilliseconds;
            frameMillisecondsAverage += (Time.unscaledDeltaTime * 1000f - frameMillisecondsAverage) * DebugFrameAverageWeight;

            // 멈춰 있을 때는 마지막 틱 그대로(보간 없이) 그린다.
            float alpha = world.IsChoosingUpgrade || world.IsOver ? 1f : accumulator / tick;
            // 사망: 시뮬레이션은 이미 멈춰 있다. 사망 애니메이션이 끝난 뒤에 판 끝 창을 띄운다.
            // 패배는 모두, 승리는 목표 뒤 잡혀(체력 0) 끝난 때만(1-11) — 창은 승리 창. 잡히지 않은 승리(디버그 바로 승리)는 바로.
            bool dead = world.Outcome == RunOutcome.Defeat || (world.IsOver && world.Result.PlayerDied);
            if (playerView != null) playerView.Render(world.Player, alpha, simulation.MoveSpeed(world), world.Tick, dead);
            if (healthBar != null) healthBar.Render(world.Player, alpha, simulation.MaxHealthOf(world));
            if (enemyView != null) enemyView.Render(world.Enemies, world.EnemyCount, alpha, world.Tick, world.EnemiesKilledTotal);
            if (bulletView != null) bulletView.Render(world.Bullets, world.BulletCount, alpha);
            if (gemView != null) gemView.Render(world.Gems, world.GemCount, alpha);
            if (experienceHud != null) experienceHud.Render(world.Level, world.Experience, simulation.ExperienceToNext(world.Level));
            if (levelUpView != null) levelUpView.Render(world.PendingLevelUps, world.LevelUpChoices, world.LevelUpChoiceCount, describeChoice, world.UpgradeStages, upgradeTable, world.PassiveStages, passiveTable);
            if (passiveHud != null) passiveHud.Render(world.PassiveSlots, world.PassiveSlotCount, world.PassiveStages, passiveTable);
            if (timerHud != null)
            {
                bool banner = !world.IsOver && FeedbackRules.GoalBannerVisible(world.Tick, world.GoalReachedTick, goalBannerTicks);
                timerHud.Render(simulation.RunTicks(world), simulation.TicksPerSecond, world.GoalReached, banner, finaleNameKey);
            }
            bool showResult = world.IsOver && (!dead || playerView == null || playerView.DeathPresentationDone);
            RunResult shownResult = showResult ? world.Result : default;
            if (gameOverView != null) gameOverView.Render(shownResult, simulation.SurvivedSeconds(world));
        }

        // ---- 디버그 진입점 (에디터 디버그 창 Myong/Debug/Debug Window 가 부른다) ----

        const float DebugFrameAverageWeight = 0.05f;

        public bool DebugIsRunning => world != null;

        public int DebugSeed => world.Seed;

        public long DebugTick => world.Tick;

        public float DebugElapsedSeconds => simulation.ElapsedSeconds(world);

        /// <summary>이번 프레임에 시뮬레이션(Step 여러 번)에 든 시간(ms).</summary>
        public double DebugStepMilliseconds => lastStepMilliseconds;

        /// <summary>프레임 시간(ms)의 이동 평균.</summary>
        public float DebugFrameMilliseconds => frameMillisecondsAverage;

        /// <summary>살아 있는 적 수.</summary>
        public int DebugEnemyCount => world.EnemyCount;

        public int DebugEnemyCapacity => world.Enemies.Length;

        public long DebugEnemiesSpawnedTotal => world.EnemiesSpawnedTotal;

        public bool DebugSpawningPaused => world.SpawningPaused;

        /// <summary>적 하나의 상태(확인용, 복사본).</summary>
        public EnemyState DebugEnemy(int index) => world.Enemies[index];

        /// <summary>[상태 변경] 지금 적 count마리를 바로 스폰(최대 수까지). 스폰한 수를 돌려준다.</summary>
        public int DebugSpawnEnemies(int count)
        {
            return simulation.SpawnEnemies(world, count);
        }

        /// <summary>적 종류 수(EnemyTable 줄 수).</summary>
        public int DebugEnemyKindCount => world.EnemiesSpawnedByKind.Length;

        /// <summary>적 종류의 표 id(없으면 빈 문자열).</summary>
        public string DebugEnemyKindId(int kind) => enemyTable != null && kind >= 0 && kind < enemyTable.Rows.Count ? enemyTable.Rows[kind].id : string.Empty;

        /// <summary>지금 살아 있는 그 종류의 수(디버그용으로 세어 본다).</summary>
        public int DebugAliveOfKind(int kind)
        {
            int n = 0;
            for (int i = 0; i < world.EnemyCount; i++)
            {
                if (world.Enemies[i].Kind == kind) n++;
            }
            return n;
        }

        /// <summary>지금 돌진 중인 적 수.</summary>
        public int DebugChargingCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < world.EnemyCount; i++)
                {
                    if (world.Enemies[i].Charge.X != 0f || world.Enemies[i].Charge.Y != 0f) n++;
                }
                return n;
            }
        }

        public long DebugSpawnedOfKind(int kind) => kind >= 0 && kind < world.EnemiesSpawnedByKind.Length ? world.EnemiesSpawnedByKind[kind] : 0;

        public long DebugKilledOfKind(int kind) => kind >= 0 && kind < world.EnemiesKilledByKind.Length ? world.EnemiesKilledByKind[kind] : 0;

        /// <summary>돌진하다 멀어져 사라진 적 누계.</summary>
        public long DebugEnemiesEscapedTotal => world.EnemiesEscapedTotal;

        /// <summary>지금 시간대 번호(첫 시간대 전 -1) · 그 시간대.</summary>
        public int DebugPhaseIndex => simulation.Enemies.PhaseAt(simulation.ElapsedSeconds(world));

        public SpawnPhaseData DebugPhase(int index) => simulation.Enemies.Phases[index];

        public int DebugWaveCount => simulation.Enemies.Waves.Length;

        public SpawnWaveData DebugWave(int index) => simulation.Enemies.Waves[index];

        /// <summary>웨이브 w가 다음에 나오는 틱(지금 틱 포함). 더 없으면 -1.</summary>
        public long DebugNextWaveTick(int wave) => simulation.NextWaveTick(wave, world.Tick);

        /// <summary>지난 프레임에 크게 그린 젬 수(정예의 젬, 표시 확인용).</summary>
        public int DebugBigGemsShown => gemView != null ? gemView.BigShownCount : 0;

        /// <summary>[상태 변경] 종류를 골라 count마리를 바로 소환(스폰 원 위, 쫓아옴). 스폰한 수를 돌려준다.</summary>
        public int DebugSpawnEnemiesOfKind(int kind, int count)
        {
            return simulation.SpawnEnemies(world, count, kind);
        }

        /// <summary>[상태 변경] 웨이브 w를 지금 바로(떼 돌진 · 정예 확인용).</summary>
        public bool DebugSpawnWaveNow(int wave)
        {
            return simulation.SpawnWaveNow(world, wave);
        }

        /// <summary>[상태 변경] 적 모두 제거.</summary>
        public void DebugClearEnemies()
        {
            simulation.ClearEnemies(world);
        }

        /// <summary>[상태 변경] 시간대 스폰 멈춤 / 재개.</summary>
        public void DebugSetSpawningPaused(bool paused)
        {
            world.SpawningPaused = paused;
        }

        /// <summary>날아가는 탄 수.</summary>
        public int DebugBulletCount => world.BulletCount;

        public int DebugBulletCapacity => world.Bullets.Length;

        public long DebugBulletsFiredTotal => world.BulletsFiredTotal;

        /// <summary>이 판의 처치 수.</summary>
        public long DebugEnemiesKilledTotal => world.EnemiesKilledTotal;

        public bool DebugFiringPaused => world.FiringPaused;

        public bool DebugEnemiesInvincible => world.EnemiesInvincible;

        /// <summary>가진 무기 수.</summary>
        public int DebugWeaponCount => world.Weapons.Length;

        /// <summary>가진 무기 하나(확인용, 복사본).</summary>
        public WeaponState DebugWeapon(int index) => world.Weapons[index];

        /// <summary>무기 종류의 표 id(없으면 빈 문자열).</summary>
        public string DebugWeaponId(int kind) => weaponTable != null && kind >= 0 && kind < weaponTable.Rows.Count ? weaponTable.Rows[kind].id : string.Empty;

        /// <summary>무기 종류의 발사 간격(틱).</summary>
        public int DebugFireIntervalTicks(int kind) => simulation.FireIntervalTicks(kind);

        /// <summary>탄 하나의 상태(확인용, 복사본).</summary>
        public BulletState DebugBullet(int index) => world.Bullets[index];

        /// <summary>지난 프레임에 흰색으로 번쩍인 적 수(표시 확인용).</summary>
        public int DebugFlashingEnemies => enemyView != null ? enemyView.FlashingCount : 0;

        /// <summary>[상태 변경] 자동 사격 멈춤 / 재개.</summary>
        public void DebugSetFiringPaused(bool paused)
        {
            world.FiringPaused = paused;
        }

        /// <summary>[상태 변경] 적 무적 켜기 / 끄기(탄은 맞고 사라지지만 체력이 줄지 않는다).</summary>
        public void DebugSetEnemiesInvincible(bool invincible)
        {
            world.EnemiesInvincible = invincible;
        }

        /// <summary>[상태 변경] 날아가는 탄 모두 제거.</summary>
        public void DebugClearBullets()
        {
            simulation.ClearBullets(world);
        }

        // ---- 경험치 · 레벨 ----

        public int DebugLevel => world.Level;

        public int DebugExperience => world.Experience;

        public int DebugExperienceToNext => simulation.ExperienceToNext(world.Level);

        public long DebugExperienceTotal => world.ExperienceTotal;

        public int DebugGemCount => world.GemCount;

        public int DebugGemCapacity => world.Gems.Length;

        public long DebugGemsDroppedTotal => world.GemsDroppedTotal;

        public long DebugGemsCollectedTotal => world.GemsCollectedTotal;

        /// <summary>젬 하나의 상태(확인용, 복사본).</summary>
        public GemState DebugGem(int index) => world.Gems[index];

        public int DebugPendingLevelUps => world.PendingLevelUps;

        public int DebugChoiceCount => world.LevelUpChoiceCount;

        /// <summary>지금 보이는 선택지 i의 강화 표 줄 번호.</summary>
        public int DebugChoiceRow(int index) => world.LevelUpChoices[index];

        public int DebugUpgradeRowCount => upgradeTable != null ? upgradeTable.Rows.Count : 0;

        public string DebugUpgradeId(int row) => upgradeTable != null && row >= 0 && row < upgradeTable.Rows.Count ? upgradeTable.Rows[row].id : string.Empty;

        public int DebugUpgradeStage(int row) => row >= 0 && row < world.UpgradeStages.Length ? world.UpgradeStages[row] : 0;

        public int DebugUpgradeMaxStage(int row) => upgradeTable != null && row >= 0 && row < upgradeTable.Rows.Count ? upgradeTable.Rows[row].maxStage : 0;

        /// <summary>강화가 걸린 플레이어 값.</summary>
        public float DebugMoveSpeed => simulation.MoveSpeed(world);

        public float DebugPickupRadius => simulation.PickupRadius(world);

        /// <summary>강화가 걸린 무기 값.</summary>
        public float DebugWeaponValue(int kind, StatTarget target) => simulation.WeaponValue(world, kind, target);

        /// <summary>강화가 걸린 발사 간격(틱).</summary>
        public int DebugFireIntervalTicksNow(int kind) => simulation.FireIntervalTicks(world, kind);

        /// <summary>레벨업 창이 뜨면 처음 카드를 바로 고를지(디버그 · 긴 시뮬레이션 확인용).</summary>
        public bool DebugAutoChooseFirst
        {
            get => debugAutoChooseFirst;
            set => debugAutoChooseFirst = value;
        }

        /// <summary>[상태 변경] 경험치 +amount(레벨이 오르면 레벨업 창이 뜬다).</summary>
        public void DebugAddExperience(int amount)
        {
            simulation.AddExperience(world, amount);
        }

        /// <summary>[상태 변경] 다음 레벨까지 남은 경험치를 바로 넣어 레벨업.</summary>
        public void DebugLevelUpNow()
        {
            int need = simulation.ExperienceToNext(world.Level);
            if (need == int.MaxValue) return;
            simulation.AddExperience(world, need - world.Experience);
        }

        /// <summary>[상태 변경] 레벨업 창의 card번째(0부터)를 고른다 — 숫자키를 누른 것과 같은 길(다음 프레임에 적용).</summary>
        public void DebugPressCard(int card)
        {
            if (input != null) input.DebugPressCard(card);
        }

        /// <summary>[상태 변경] 강화 표의 row번째 줄을 한 단계 건다. 최대 단계면 false.</summary>
        public bool DebugApplyUpgrade(int row)
        {
            return simulation.ApplyUpgrade(world, row);
        }

        /// <summary>[상태 변경] 걸린 강화를 모두 푼다(레벨 · 경험치는 그대로).</summary>
        public void DebugResetUpgrades()
        {
            simulation.ResetUpgrades(world);
        }

        // ---- 패시브 ----

        /// <summary>지금 선택지 i가 무엇인지(강화 줄 · 패시브 줄 · 대체 보상).</summary>
        public LevelUpOption DebugChoiceOption(int index) => simulation.DescribeChoice(world.LevelUpChoices[index]);

        public int DebugPassiveRowCount => passiveTable != null ? passiveTable.Rows.Count : 0;

        public string DebugPassiveId(int row) => passiveTable != null && row >= 0 && row < passiveTable.Rows.Count ? passiveTable.Rows[row].id : string.Empty;

        public int DebugPassiveStage(int row) => row >= 0 && row < world.PassiveStages.Length ? world.PassiveStages[row] : 0;

        public int DebugPassiveMaxStage(int row) => passiveTable != null && row >= 0 && row < passiveTable.Rows.Count ? passiveTable.Rows[row].maxStage : 0;

        /// <summary>가진 패시브 수 / 최대(PassiveTable).</summary>
        public int DebugPassiveOwnedCount => world.PassiveSlotCount;

        public int DebugPassiveMaxOwned => simulation.Passives.MaxOwned;

        /// <summary>얻은 순서 i번째 패시브의 표 줄 번호.</summary>
        public int DebugPassiveOwnedRow(int index) => world.PassiveSlots[index];

        /// <summary>지금 선택지에 나올 수 있는 패시브인지.</summary>
        public bool DebugPassiveOfferable(int row) => simulation.PassiveOfferable(world, row);

        /// <summary>대체 보상을 받은 수 · 회복한 체력 합.</summary>
        public int DebugFallbackRewardsTaken => world.FallbackRewardsTaken;

        public float DebugHealedTotal => world.HealedTotal;

        /// <summary>강화 · 패시브가 걸린 플레이어 값.</summary>
        public float DebugHealthRegen => simulation.HealthRegen(world);

        public float DebugDamageTakenScale => simulation.DamageTakenScale(world);

        public float DebugExperienceGain => simulation.ExperienceGain(world);

        /// <summary>HUD 패시브 줄에 지금 보이는 아이콘 수(표시 확인용).</summary>
        public int DebugPassiveHudShown => passiveHud != null ? passiveHud.ShownCount : -1;

        /// <summary>[상태 변경] 패시브 표의 row번째 줄을 주거나(없으면) 한 단계 올린다. 최대 단계 · 칸이 다 찼으면 false.</summary>
        public bool DebugApplyPassive(int row)
        {
            return simulation.ApplyPassive(world, row);
        }

        // ---- 체력 · 판 끝 ----

        public float DebugPlayerHealth => world.Player.Health;

        public float DebugPlayerMaxHealth => simulation.MaxHealthOf(world);

        public bool DebugPlayerInvincible => world.PlayerInvincible;

        /// <summary>맞은 뒤 무적 시간이 남은 틱(0 = 없음).</summary>
        public long DebugInvulnerableTicksLeft => Math.Max(0L, world.Player.InvulnerableUntilTick - world.Tick);

        public int DebugHurtInvulnerableTicks => simulation.HurtInvulnerableTicks;

        public long DebugHitsTaken => world.HitsTaken;

        public float DebugDamageTakenTotal => world.DamageTakenTotal;

        public long DebugLastHurtTick => world.Player.LastHurtTick;

        public bool DebugIsOver => world.IsOver;

        public RunResult DebugResult => world.Result;

        public float DebugSurvivedSeconds => simulation.SurvivedSeconds(world);

        /// <summary>표시 확인용: 플레이어가 흰색으로 번쩍이는 중 / 깜빡임의 어두운 쪽 / 체력 바 도트 수 / 판 끝 창이 떠 있음.</summary>
        public bool DebugPlayerFlashing => playerView != null && playerView.IsFlashing;

        public bool DebugPlayerBlinkDim => playerView != null && playerView.IsBlinkDim;

        public int DebugHealthBarDots => healthBar != null ? healthBar.FillDots : -1;

        public bool DebugGameOverOpen => gameOverView != null && gameOverView.IsOpen;

        /// <summary>[상태 변경] 플레이어 무적 켜기 / 끄기.</summary>
        public void DebugSetPlayerInvincible(bool invincible)
        {
            world.PlayerInvincible = invincible;
        }

        /// <summary>[상태 변경] 체력 채우기.</summary>
        public void DebugFillHealth()
        {
            simulation.FillHealth(world);
        }

        /// <summary>[상태 변경] 피해 amount(0이 되면 판 끝).</summary>
        public void DebugDamagePlayer(float amount)
        {
            simulation.DamagePlayer(world, amount);
        }

        // ---- 판 시간 · 승리 ----

        /// <summary>생존 목표(초, RunTuning). 0 = 시간 승리 없음.</summary>
        public float DebugSurvivalSeconds => simulation.SurvivalSeconds;

        /// <summary>판 시간(틱, 끝났으면 끝난 틱).</summary>
        public long DebugRunTicks => simulation.RunTicks(world);

        /// <summary>HUD 타이머에 지금 보이는 글 · 판 끝 창의 제목 · 시간 글(표시 확인용).</summary>
        public string DebugTimerText => timerHud != null ? timerHud.Shown : string.Empty;

        public string DebugGameOverTitle => gameOverView != null ? gameOverView.ShownTitle : string.Empty;

        public string DebugGameOverTime => gameOverView != null ? gameOverView.ShownTime : string.Empty;

        /// <summary>[상태 변경] 판 시간을 seconds로 건너뛴다(앞으로만, 틱 수만 바뀜). 건너뛰었으면 true.</summary>
        public bool DebugSkipToSeconds(float seconds)
        {
            accumulator = 0f;
            return simulation.SkipTo(world, seconds);
        }

        /// <summary>[상태 변경] 생존 목표 secondsBefore초 전으로 건너뛴다(예: 30분 목표에 10초 = 29:50). 목표가 없으면 false.</summary>
        public bool DebugSkipToBeforeGoal(float secondsBefore)
        {
            if (simulation.SurvivalTicks <= 0) return false;
            return DebugSkipToSeconds(Math.Max(0f, simulation.SurvivalSeconds - secondsBefore));
        }

        /// <summary>생존 목표에 닿았는지 · 닿은 틱(-1 = 아직) · 마무리 남은 틱 · 마무리 길이(틱) · 목표에서 사라진 적 수 (1-11).</summary>
        public bool DebugGoalReached => world.GoalReached;

        public long DebugGoalReachedTick => world.GoalReachedTick;

        public long DebugFinaleTicksLeft => simulation.FinaleTicksLeft(world);

        public long DebugFinaleTicks => simulation.FinaleTicks;

        public long DebugEnemiesDismissedTotal => world.EnemiesDismissedTotal;

        /// <summary>마무리 적 이름 글 키 · 목표 글이 떠 있는지 · 그 글 · 타이머가 목표 색인지(표시 확인용).</summary>
        public string DebugFinaleNameKey => finaleNameKey ?? string.Empty;

        public bool DebugGoalBannerShown => timerHud != null && timerHud.BannerShown;

        public string DebugGoalBannerText => timerHud != null ? timerHud.ShownGoal + " / " + timerHud.ShownFinale : string.Empty;

        public bool DebugTimerInGoalColor => timerHud != null && timerHud.TimerInGoalColor;

        /// <summary>마무리 웨이브인지(디버그 창 표시용).</summary>
        public bool DebugIsFinaleWave(int wave) => simulation.IsFinaleWave(wave);

        /// <summary>[상태 변경] 마무리 바로: 생존 목표 시간으로 건너뛴다 — 다음 틱에 목표(생존 성공) → 남은 적이 사라지고 마무리 적이 나온다. 목표가 없거나 이미 닿았으면 false.</summary>
        public bool DebugFinaleNow()
        {
            if (simulation.SurvivalTicks <= 0 || world.GoalReached) return false;
            return DebugSkipToSeconds(simulation.SurvivalSeconds);
        }

        /// <summary>[상태 변경] 바로 승리(시간을 건너뛰지 않고 지금 틱으로 판 끝 — 창 모양 확인용).</summary>
        public void DebugVictoryNow()
        {
            simulation.EndRun(world, RunOutcome.Victory);
        }

        /// <summary>[상태 변경] 바로 게임 오버(패배로 판 끝).</summary>
        public void DebugGameOverNow()
        {
            simulation.EndRun(world, RunOutcome.Defeat);
        }

        /// <summary>[상태 변경] 판 끝 창의 [다시 시작]을 누른 것과 같은 길(버튼 onClick). 창이 없으면 바로.</summary>
        public void DebugPressRestart()
        {
            if (gameOverView != null && gameOverView.RestartButton != null) gameOverView.RestartButton.onClick.Invoke();
            else OnRestartPressed();
        }

        public Vec2 DebugPlayerPosition => world.Player.Position;

        public Vec2 DebugPlayerVelocity => world.Player.Velocity;

        public int DebugPlayerFacingX => world.Player.FacingX;

        /// <summary>[상태 변경] 같은 시드로 판을 처음부터. 수치 에셋도 다시 읽는다.</summary>
        public void DebugRestart()
        {
            StartRun(world.Seed);
        }

        /// <summary>[상태 변경] 시드를 정해서 판을 처음부터.</summary>
        public void DebugRestartWithSeed(int seed)
        {
            StartRun(seed);
        }

        /// <summary>[상태 변경] 플레이어를 그 자리로.</summary>
        public void DebugTeleportPlayer(float x, float y)
        {
            simulation.TeleportPlayer(world, new Vec2(x, y));
        }

        /// <summary>[상태 변경] 실제 입력 대신 이 이동 벡터를 넣는다. DebugClearMoveInput으로 되돌린다.</summary>
        public void DebugSetMoveInput(float x, float y)
        {
            if (input != null) input.DebugSetMove(new Vec2(x, y));
        }

        public void DebugClearMoveInput()
        {
            if (input != null) input.DebugClearMove();
        }

        // ---- 가상 조이스틱 ----

        /// <summary>이번 프레임에 Move 액션에서 읽은 이동 입력(디버그 대신 넣기가 켜져 있으면 그 값).</summary>
        public Vec2 DebugMoveInput => input != null ? input.ReadMove() : Vec2.Zero;

        public bool DebugHasJoystick => joystick != null;

        public bool DebugJoystickActive => joystick != null && joystick.IsActive;

        public bool DebugJoystickBlocked => joystick != null && joystick.IsBlocked;

        /// <summary>조이스틱이 Move 액션에 넣고 있는 값.</summary>
        public Vec2 DebugJoystickValue => joystick != null ? joystick.Value : Vec2.Zero;

        /// <summary>[상태 변경] 손가락 흉내: 화면 픽셀 자리를 누른다(uGUI 포인터 이벤트와 같은 길).</summary>
        public void DebugJoystickPress(float screenX, float screenY)
        {
            if (joystick != null) joystick.DebugPress(new Vector2(screenX, screenY));
        }

        /// <summary>[상태 변경] 손가락 흉내: 누른 채 화면 픽셀 자리로 끈다.</summary>
        public void DebugJoystickDrag(float screenX, float screenY)
        {
            if (joystick != null) joystick.DebugDrag(new Vector2(screenX, screenY));
        }

        /// <summary>[상태 변경] 손가락 흉내: 뗀다.</summary>
        public void DebugJoystickRelease()
        {
            if (joystick != null) joystick.DebugRelease();
        }

        // ---- 피격 · 사망 연출 ----

        const float DebugEnemyFlashPreviewSeconds = 0.5f;

        /// <summary>사망 연출 중인지 · 시작한 뒤 몇 초인지 · 사망 애니메이션이 끝까지 갔는지 · 연출이 끝나 창을 띄울 때인지(표시 확인용).</summary>
        public bool DebugPlayerDead => playerView != null && playerView.IsDead;

        public float DebugDeathSeconds => playerView != null ? playerView.DeathSeconds : 0f;

        public bool DebugDeathAnimationEnded => playerView != null && playerView.DeathAnimationEnded;

        public bool DebugDeathPresentationDone => playerView != null && playerView.DeathPresentationDone;

        /// <summary>지난 프레임에 보인 적 소멸 연출 수 · 이 판의 누계 · 연출 렌더러 수(표시 확인용).</summary>
        public int DebugDeathEffectsShown => enemyView != null ? enemyView.DeathEffectsShown : 0;

        public long DebugDeathEffectsStarted => enemyView != null ? enemyView.DeathEffectsStarted : 0;

        public int DebugDeathEffectCapacity => enemyView != null ? enemyView.DeathEffectCapacity : 0;

        /// <summary>[상태 변경] 피격 흉내: 적에게 닿은 것처럼 피해 amount + 맞은 뒤 무적 시간(번쩍임 · 깜빡임 · 피격 애니메이션이 실제 피격과 같은 길로 나온다). 0이 되면 사망.</summary>
        public void DebugSimulateHit(float amount)
        {
            if (world.IsOver || amount <= 0f) return;
            simulation.DamagePlayer(world, amount);
            if (!world.IsOver) world.Player.InvulnerableUntilTick = world.Tick + simulation.HurtInvulnerableTicks;
        }

        /// <summary>[상태 변경] 사망 흉내: 남은 체력만큼 피해를 줘 체력 0으로 판을 끝낸다(사망 애니메이션 → 판 끝 창).</summary>
        public void DebugKillPlayer()
        {
            if (world.IsOver) return;
            simulation.DamagePlayer(world, Math.Max(1f, world.Player.Health));
        }

        /// <summary>적 번쩍임 보기(보기만): 잠깐 화면의 모든 적을 흰색으로.</summary>
        public void DebugPreviewEnemyFlash()
        {
            if (enemyView != null) enemyView.DebugPreviewFlash(DebugEnemyFlashPreviewSeconds);
        }

        /// <summary>적 소멸 연출 보기(보기만): 살아 있는 적 자리에 소멸 연출을 낸다(적은 그대로). 낸 수를 돌려준다.</summary>
        public int DebugPreviewEnemyDeaths()
        {
            return enemyView != null ? enemyView.DebugPreviewDeaths(world.Enemies, world.EnemyCount, world.Tick) : 0;
        }

        /// <summary>피격 애니메이션만 재생(보기만).</summary>
        public void DebugPlayHit()
        {
            if (playerView != null) playerView.PlayHit();
        }

        /// <summary>표시 언어 바꾸기(보기만).</summary>
        public void DebugSetLanguage(Language language)
        {
            Text.SetLanguage(language);
        }
    }
}
