using Myong.Core;
using Myong.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools.Constraints;

namespace Myong.Tests
{
    /// <summary>
    /// 30분 마무리(1-11): 목표에서 승리 확정 · 남은 적 사라짐(처치 · 젬 아님) · 마무리 웨이브만 · 무적 몸 · 닿으면 즉사 · 잡혀도 승리 · 마무리 시간 끝 · 타이머 멈춤 ·
    /// 마무리 0 = 1-7 그대로 · 건너뛰기 · 같은 시드 · 할당 · 목표 글 · 에셋 검사. 여기의 숫자는 테스트용 값이다(게임 수치는 에셋).
    /// </summary>
    public sealed class FinaleTests
    {
        const int TicksPerSecond = 60;
        const float SurvivalSeconds = 10f; // 600틱
        const long SurvivalTicks = 600;
        const float FinaleSeconds = 5f; // 300틱
        const long FinaleTicks = 300;
        const float PlayerSpeed = 5f;
        const float PlayerRadius = 0.4f;
        const float MaxHealth = 30f;
        const int Basic = 0;
        const int Reaper = 1;
        const float ReaperSpeed = 8f;
        const float ReaperRadius = 1f;
        const float ReaperHealth = 99999f;
        const float ReaperDistance = 22f;
        const float Tolerance = 1e-3f;

        static readonly EnemyKindData BasicKind = new EnemyKindData(1f, 0.3f, 10f, 1, 10f);
        static readonly EnemyKindData ReaperKind = new EnemyKindData(ReaperSpeed, ReaperRadius, ReaperHealth, 0, 0f, invulnerable: true, lethalContact: true);

        // basic: 초당 3마리 + 2초부터 2초마다 1마리 웨이브(보통 웨이브). 마무리 적: 목표 시간(10초)에 한 번, 22유닛 밖.
        static EnemyRulesData Enemies() => new EnemyRulesData(
            new[] { BasicKind, ReaperKind },
            new[] { new SpawnPhaseData(0f, 3f, Basic) },
            15f, 300, 0.5f,
            new[]
            {
                new SpawnWaveData(Basic, 2f, 2f, 1),
                new SpawnWaveData(Reaper, SurvivalSeconds, 0f, 1, SpawnPattern.Scatter, 0f, 1f, ReaperDistance),
            },
            0f);

        static Simulation NewSimulation(float finaleSeconds = FinaleSeconds, WeaponRulesData weapons = null, PassiveRulesData passives = null, float survivalSeconds = SurvivalSeconds, EnemyRulesData enemies = null)
        {
            return new Simulation(TicksPerSecond, new PlayerTuningData(PlayerSpeed, 1f, MaxHealth, PlayerRadius, 0.5f),
                enemies ?? Enemies(), weapons ?? WeaponRulesData.None, new ExperienceRulesData(new[] { 1000 }, 0, 64, 5f, 0.4f), UpgradeRulesData.None,
                new RunRulesData(survivalSeconds, finaleSeconds), passives ?? PassiveRulesData.None);
        }

        static void Step(Simulation simulation, World world, long ticks, Vec2 move = default)
        {
            var input = new InputFrame { Move = move };
            for (long i = 0; i < ticks; i++) simulation.Step(world, input);
        }

        static int CountOfKind(World world, int kind)
        {
            int n = 0;
            for (int i = 0; i < world.EnemyCount; i++)
            {
                if (world.Enemies[i].Kind == kind) n++;
            }
            return n;
        }

        static void Place(World world, int kind, Vec2 position, float health)
        {
            world.Enemies[world.EnemyCount] = new EnemyState
            {
                Id = world.AllocateEntityId(),
                Kind = kind,
                Position = position,
                PreviousPosition = position,
                Health = health,
                LastHitTick = EnemyState.NeverHit,
            };
            world.EnemyCount++;
        }

        [Test]
        public void ReachingTheGoal_ConfirmsVictory_AndClearsTheEnemies_WithoutKillsOrGems()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(3);
            Step(simulation, world, SurvivalTicks - 1);
            Assert.IsFalse(world.GoalReached);
            int alive = world.EnemyCount;
            Assert.Greater(alive, 0, "enemies should be on the field before the goal");

            Step(simulation, world, 1);
            Assert.IsTrue(world.GoalReached);
            Assert.AreEqual(SurvivalTicks, world.GoalReachedTick);
            Assert.AreEqual(RunOutcome.Playing, world.Outcome, "the run goes on into the finale");
            Assert.AreEqual(0, world.EnemyCount, "remaining enemies vanish");
            Assert.GreaterOrEqual(world.EnemiesDismissedTotal, alive);
            Assert.AreEqual(world.EnemiesSpawnedTotal, world.EnemiesDismissedTotal, "every enemy that was on the field (nothing killed) vanished");
            Assert.AreEqual(0, world.EnemiesKilledTotal, "vanishing is not a kill");
            Assert.AreEqual(0, world.GemsDroppedTotal, "no gems for vanished enemies");
            Assert.AreEqual(SurvivalTicks, simulation.RunTicks(world));
            Assert.AreEqual(FinaleTicks, simulation.FinaleTicks);
            Assert.AreEqual(FinaleTicks, simulation.FinaleTicksLeft(world));
        }

        [Test]
        public void AfterTheGoal_OnlyTheFinaleWaveSpawns_OnceAndOffScreen()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(3);
            world.PlayerInvincible = true;
            Assert.IsFalse(simulation.IsFinaleWave(0));
            Assert.IsTrue(simulation.IsFinaleWave(1));
            Step(simulation, world, SurvivalTicks);
            long basicsAtGoal = world.EnemiesSpawnedByKind[Basic];

            Step(simulation, world, 1);
            Assert.AreEqual(1, world.EnemyCount);
            Assert.AreEqual(Reaper, world.Enemies[0].Kind);
            Assert.AreEqual(ReaperDistance, (world.Enemies[0].Position - world.Player.Position).Magnitude, Tolerance, "finale wave uses its own distance");
            Assert.AreEqual(ReaperHealth, world.Enemies[0].Health);

            Step(simulation, world, 200);
            Assert.AreEqual(basicsAtGoal, world.EnemiesSpawnedByKind[Basic], "no phase or ordinary wave spawns after the goal");
            Assert.AreEqual(1, world.EnemiesSpawnedByKind[Reaper], "the finale wave comes once");
            Assert.AreEqual(1, CountOfKind(world, Reaper));
        }

        [Test]
        public void FinaleEnemy_KillsAtOnce_ThroughInvulnerabilityAndArmor_AndTheRunIsAVictory()
        {
            var armor = new PassiveRulesData(new[] { new PassiveData(new[] { new StatEffect(StatTarget.DamageTaken, StatOp.Multiply, 0.1f) }, 1) }, 6, 0f);
            var simulation = NewSimulation(passives: armor);
            var world = simulation.CreateWorld(3);
            Assert.IsTrue(simulation.ApplyPassive(world, 0));
            Step(simulation, world, SurvivalTicks);
            world.Player.InvulnerableUntilTick = long.MaxValue / 2; // 맞은 뒤 무적이 끝없이 남아 있어도

            for (int i = 0; i < FinaleTicks && !world.IsOver; i++) simulation.Step(world, new InputFrame());
            Assert.IsTrue(world.IsOver, "the finale enemy should catch a standing player");
            Assert.AreEqual(RunOutcome.Victory, world.Outcome);
            Assert.IsTrue(world.Result.PlayerDied);
            Assert.AreEqual(0f, world.Player.Health);
            Assert.Less(world.Result.Ticks, SurvivalTicks + FinaleTicks, "caught before the finale ran out");
            Assert.AreEqual(SurvivalSeconds, simulation.SurvivedSeconds(world), 1e-4f, "survived time shows the goal");
        }

        [Test]
        public void FinaleEnemy_BeforeTheGoal_IsAnOrdinaryDefeat()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(3);
            world.SpawningPaused = true;
            Assert.AreEqual(1, simulation.SpawnEnemies(world, 1, Reaper));
            for (int i = 0; i < 300 && !world.IsOver; i++) simulation.Step(world, new InputFrame());
            Assert.AreEqual(RunOutcome.Defeat, world.Outcome);
            Assert.IsTrue(world.Result.PlayerDied);
            Assert.IsFalse(world.GoalReached);
        }

        [Test]
        public void DebugInvinciblePlayer_StillEnds_WhenTheFinaleRunsOut()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(3);
            world.PlayerInvincible = true;
            Step(simulation, world, SurvivalTicks + FinaleTicks - 1);
            Assert.IsFalse(world.IsOver);
            Assert.AreEqual(1, simulation.FinaleTicksLeft(world));

            Step(simulation, world, 1);
            Assert.AreEqual(RunOutcome.Victory, world.Outcome);
            Assert.IsTrue(world.Result.PlayerDied, "the finale ends like being caught");
            Assert.AreEqual(SurvivalTicks + FinaleTicks, world.Result.Ticks);
            Assert.AreEqual(0, simulation.FinaleTicksLeft(world));
        }

        [Test]
        public void FinaleEnemy_IsNotAimedAt_AndBulletsPassThrough()
        {
            var gun = new WeaponRulesData(new[] { new WeaponKindData(0.05f, 20f, 50f, 30f, 1, 0, 0.1f) }, new[] { 0 }, 64, Vec2.Zero);
            var simulation = NewSimulation(weapons: gun, survivalSeconds: 0f);
            var world = simulation.CreateWorld(3);
            world.SpawningPaused = true;
            world.PlayerInvincible = true;

            // 무적 몸 하나만: 쏘지 않는다.
            Place(world, Reaper, new Vec2(4f, 0f), ReaperHealth);
            Step(simulation, world, 30);
            Assert.AreEqual(0, world.BulletsFiredTotal, "nothing to aim at");

            // 그 뒤에 보통 적: 탄이 무적 몸을 지나 보통 적을 맞힌다.
            Place(world, Basic, new Vec2(8f, 0f), 10f);
            for (int i = 0; i < 120 && world.EnemiesKilledTotal == 0; i++) simulation.Step(world, new InputFrame());
            Assert.AreEqual(1, world.EnemiesKilledTotal);
            Assert.Greater(world.BulletsFiredTotal, 0);
            Assert.AreEqual(1, CountOfKind(world, Reaper));
            for (int i = 0; i < world.EnemyCount; i++)
            {
                if (world.Enemies[i].Kind != Reaper) continue;
                Assert.AreEqual(ReaperHealth, world.Enemies[i].Health);
                Assert.AreEqual(EnemyState.NeverHit, world.Enemies[i].LastHitTick, "bullets pass through the untouchable body");
            }
        }

        [Test]
        public void FinaleEnemy_DoesNotPush_AndDoesNotWidenTheGrid()
        {
            Assert.AreEqual(BasicKind.Radius, Enemies().MaxRadius, Tolerance, "an untouchable body does not change the grid");

            var still = new EnemyRulesData(new[] { new EnemyKindData(0f, 0.3f, 10f, 1, 0f), new EnemyKindData(0f, ReaperRadius, ReaperHealth, 0, 0f, true, true) },
                new[] { new SpawnPhaseData(0f, 0f, Basic) }, 15f, 10, 1f);
            var simulation = NewSimulation(enemies: still, survivalSeconds: 0f);
            var world = simulation.CreateWorld(3);
            var at = new Vec2(6f, 0f);
            Place(world, Reaper, at, ReaperHealth);
            Place(world, Basic, at + new Vec2(0.2f, 0f), 10f);
            Step(simulation, world, 5);
            Assert.AreEqual(at.X, world.Enemies[0].Position.X, Tolerance);
            Assert.AreEqual(at.X + 0.2f, world.Enemies[1].Position.X, Tolerance, "overlapping the untouchable body pushes nothing");
        }

        [Test]
        public void TimerStopsAtTheGoal_DuringTheFinale()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(3);
            world.PlayerInvincible = true;
            Step(simulation, world, SurvivalTicks + 120);
            Assert.AreEqual(SurvivalTicks + 120, world.Tick, "the simulation keeps going");
            Assert.AreEqual(SurvivalTicks, simulation.RunTicks(world), "the timer shows the goal");
            Assert.AreEqual(SurvivalSeconds, simulation.SurvivedSeconds(world), 1e-4f);
            RunClock.Split(simulation.RunTicks(world), TicksPerSecond, out long minutes, out int seconds);
            Assert.AreEqual(0L, minutes);
            Assert.AreEqual(10, seconds);
        }

        [Test]
        public void NoFinale_KeepsTheImmediateVictory()
        {
            var simulation = NewSimulation(finaleSeconds: 0f);
            var world = simulation.CreateWorld(3);
            Step(simulation, world, SurvivalTicks);
            Assert.AreEqual(RunOutcome.Victory, world.Outcome);
            Assert.AreEqual(SurvivalTicks, world.Result.Ticks);
            Assert.IsFalse(world.Result.PlayerDied);
            Assert.IsTrue(world.GoalReached);
            Assert.Greater(world.EnemyCount, 0, "without a finale the enemies stay as they were (1-7)");
            Assert.AreEqual(0, world.EnemiesDismissedTotal);
        }

        [TestCase(10f)]
        [TestCase(12f)]
        public void SkippingToOrPastTheGoal_StillBringsTheFinaleEnemy(float seconds)
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(3);
            world.PlayerInvincible = true;
            Step(simulation, world, 60);
            Assert.IsTrue(simulation.SkipTo(world, seconds));
            Step(simulation, world, 1);
            long target = RunClock.TicksOf(seconds, TicksPerSecond);
            Assert.IsTrue(world.GoalReached);
            Assert.AreEqual(target, world.GoalReachedTick);
            Assert.AreEqual(1, world.EnemyCount);
            Assert.AreEqual(Reaper, world.Enemies[0].Kind);
            Assert.AreEqual(SurvivalTicks, simulation.RunTicks(world));
        }

        [Test]
        public void SameSeed_SameFinale_AndARunningPlayerIsCaught()
        {
            const float longFinale = 30f;
            var a = NewSimulation(finaleSeconds: longFinale);
            var b = NewSimulation(finaleSeconds: longFinale);
            var worldA = a.CreateWorld(33);
            var worldB = b.CreateWorld(33);
            var run = new Vec2(1f, 0f);
            foreach (var (simulation, world) in new[] { (a, worldA), (b, worldB) })
            {
                // 목표까지는 스폰을 멈춰 두고(보통 적에게 죽지 않게), 목표 뒤에는 최대 속도로 도망간다.
                world.SpawningPaused = true;
                Step(simulation, world, SurvivalTicks);
                world.SpawningPaused = false;
                for (int i = 0; i < 4000 && !world.IsOver; i++) simulation.Step(world, new InputFrame { Move = run });
            }
            Assert.AreEqual(RunOutcome.Victory, worldA.Outcome);
            Assert.IsTrue(worldA.Result.PlayerDied);
            Assert.Less(worldA.Result.Ticks, SurvivalTicks + RunClock.TicksOf(longFinale, TicksPerSecond), "faster than the player: caught before the finale runs out");
            Assert.AreEqual(worldA.Result.Ticks, worldB.Result.Ticks);
            Assert.AreEqual(worldA.Player.Position.X, worldB.Player.Position.X);
            Assert.AreEqual(worldA.EnemiesDismissedTotal, worldB.EnemiesDismissedTotal);
            Assert.AreEqual(worldA.NextEntityId, worldB.NextEntityId);
        }

        [Test]
        public void EndingTheRunAsDefeat_AfterTheGoal_IsRecordedAsVictory()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(3);
            Step(simulation, world, SurvivalTicks + 1);
            simulation.EndRun(world, RunOutcome.Defeat);
            Assert.AreEqual(RunOutcome.Victory, world.Outcome);
            Assert.IsFalse(world.Result.PlayerDied);
        }

        [Test]
        public void FinaleSteps_DoNotAllocate()
        {
            var simulation = NewSimulation(finaleSeconds: 30f);
            var world = simulation.CreateWorld(3);
            world.PlayerInvincible = true;
            Step(simulation, world, SurvivalTicks - 30);
            var input = new InputFrame { Move = new Vec2(1f, 0.5f) };
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++) simulation.Step(world, input);
            }, NUnit.Framework.Is.Not.AllocatingGCMemory());
            Assert.IsTrue(world.GoalReached);
            Assert.IsFalse(world.IsOver);
        }

        [Test]
        public void GoalBanner_ShowsForItsTicks_AfterTheGoal()
        {
            Assert.IsFalse(FeedbackRules.GoalBannerVisible(100, World.GoalNotReached, 180), "before the goal");
            Assert.IsTrue(FeedbackRules.GoalBannerVisible(600, 600, 180));
            Assert.IsTrue(FeedbackRules.GoalBannerVisible(779, 600, 180));
            Assert.IsFalse(FeedbackRules.GoalBannerVisible(780, 600, 180));
            Assert.IsFalse(FeedbackRules.GoalBannerVisible(599, 600, 180));
            Assert.IsFalse(FeedbackRules.GoalBannerVisible(600, 600, 0), "banner off");
            Assert.AreEqual(180, FeedbackRules.TicksOf(3f, 60));
        }

        [Test]
        public void FinaleData_IsValid()
        {
            var run = AssetDatabase.LoadAssetAtPath<RunTuning>("Assets/_Game/Data/RunTuning.asset");
            var enemies = AssetDatabase.LoadAssetAtPath<EnemyTable>("Assets/_Game/Data/EnemyTable.asset");
            var spawn = AssetDatabase.LoadAssetAtPath<SpawnTable>("Assets/_Game/Data/SpawnTable.asset");
            var player = AssetDatabase.LoadAssetAtPath<PlayerTuning>("Assets/_Game/Data/PlayerTuning.asset");
            var passives = AssetDatabase.LoadAssetAtPath<PassiveTable>("Assets/_Game/Data/PassiveTable.asset");
            var feedback = AssetDatabase.LoadAssetAtPath<FeedbackTuning>("Assets/_Game/Data/FeedbackTuning.asset");
            var text = TextTable.ParseCsv(System.IO.File.ReadAllText(UnityEngine.Application.dataPath + "/_Game/Text/strings.csv"));
            Assert.Greater(run.FinaleSeconds, 0f, "no finale: the run would end right at the goal");
            Assert.AreEqual(run.FinaleSeconds, run.ToRules().FinaleSeconds);
            Assert.Greater(feedback.GoalBannerSeconds, 0f, "no survival message");

            // 플레이어가 가장 빠를 때(이동 패시브를 모두 최대로)
            float add = 0f, mul = 1f;
            foreach (var row in passives.Rows)
            {
                foreach (var e in row.effects)
                {
                    if (e.target != StatTarget.MoveSpeed) continue;
                    if (e.op == StatOp.Add) add += e.value * row.maxStage;
                    else mul *= (float)System.Math.Pow(e.value, row.maxStage);
                }
            }
            float fastestPlayer = (player.MoveSpeed + add) * mul;

            float halfWidth = PixelArt.ReferenceWidth / (2f * PixelArt.PixelsPerUnit);
            float halfHeight = PixelArt.ReferenceHeight / (2f * PixelArt.PixelsPerUnit);
            float halfDiagonal = (float)System.Math.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);

            int finaleWaves = 0;
            foreach (var wave in spawn.Waves)
            {
                int kind = enemies.IndexOf(wave.enemyId);
                var row = enemies.Rows[kind];
                bool finale = wave.startSeconds >= run.SurvivalSeconds;
                if (!finale)
                {
                    Assert.IsFalse(row.lethalContact, "a killing enemy comes before the goal: " + wave.enemyId);
                    continue;
                }
                finaleWaves++;
                Assert.IsTrue(row.invulnerable && row.lethalContact, "finale enemy must be untouchable and kill on touch: " + row.id);
                Assert.Greater(row.moveSpeed, fastestPlayer, "the player could outrun the finale enemy: " + row.id);
                float distance = wave.distance > 0f ? wave.distance : spawn.SpawnRadius;
                Assert.Greater(distance, halfDiagonal + row.radius, "finale enemy appears on screen: " + row.id);
                Assert.IsFalse(string.IsNullOrEmpty(row.nameKey), "finale enemy without a name: " + row.id);
                Assert.IsTrue(text.Has(row.nameKey), "name key missing from strings.csv: " + row.nameKey);
            }
            Assert.Greater(finaleWaves, 0, "no finale wave (a wave starting at or after the survival goal)");

            foreach (var phase in spawn.Phases)
            {
                foreach (var (enemyId, _) in SpawnTable.EntriesOf(phase))
                {
                    Assert.IsFalse(enemies.Rows[enemies.IndexOf(enemyId)].lethalContact, "a killing enemy in a spawn phase: " + enemyId);
                }
            }
            var rules = spawn.ToRules(enemies);
            for (int i = 0; i < enemies.Rows.Count; i++)
            {
                Assert.AreEqual(enemies.Rows[i].invulnerable, rules.Kinds[i].Invulnerable, enemies.Rows[i].id);
                Assert.AreEqual(enemies.Rows[i].lethalContact, rules.Kinds[i].LethalContact, enemies.Rows[i].id);
            }
        }
    }
}
