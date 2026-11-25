using Myong.Core;
using NUnit.Framework;

namespace Myong.Tests
{
    /// <summary>판 시간 · 생존 승리(1-7): 목표 틱에 승리 · 한 틱 전은 진행 · 목표 없음 · 레벨업 중 멈춤 · 같은 틱 사망은 패배 · 건너뛰기 · 분초 표시. 여기의 숫자는 테스트용 값이다(게임 수치는 에셋).</summary>
    public sealed class RunTimerTests
    {
        const int TicksPerSecond = 60;
        const float SurvivalSeconds = 10f; // 600틱
        const long SurvivalTicks = 600;

        static Simulation NewSimulation(float survivalSeconds = SurvivalSeconds, EnemyRulesData enemies = null, ExperienceRulesData experience = null, UpgradeRulesData upgrades = null)
        {
            return new Simulation(TicksPerSecond, new PlayerTuningData(5f, 1f, 30f, 0.4f, 0.5f),
                enemies ?? EnemyRulesData.None, WeaponRulesData.None, experience ?? ExperienceRulesData.None, upgrades ?? UpgradeRulesData.None,
                new RunRulesData(survivalSeconds));
        }

        static void Step(Simulation simulation, World world, long ticks)
        {
            var input = new InputFrame();
            for (long i = 0; i < ticks; i++) simulation.Step(world, input);
        }

        [Test]
        public void SurvivalSeconds_BecomeTicks()
        {
            var simulation = NewSimulation();
            Assert.AreEqual(SurvivalTicks, simulation.SurvivalTicks);
            Assert.AreEqual(SurvivalSeconds, simulation.SurvivalSeconds, 1e-4f);
            Assert.AreEqual(108000L, RunClock.TicksOf(1800f, 60), "30 minutes");
            Assert.AreEqual(0L, RunClock.TicksOf(0f, 60));
            Assert.AreEqual(0L, RunClock.TicksOf(-5f, 60));
        }

        [Test]
        public void OneTickBeforeTheGoal_IsStillPlaying()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            Step(simulation, world, SurvivalTicks - 1);
            Assert.AreEqual(RunOutcome.Playing, world.Outcome);
            Assert.AreEqual(SurvivalTicks - 1, world.Tick);
            Assert.AreEqual(SurvivalTicks - 1, simulation.RunTicks(world));
        }

        [Test]
        public void ReachingTheGoal_IsVictory_WithResultValues()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            world.EnemiesKilledTotal = 42;
            Step(simulation, world, SurvivalTicks);
            Assert.AreEqual(RunOutcome.Victory, world.Outcome);
            Assert.IsTrue(world.IsOver);
            Assert.AreEqual(RunOutcome.Victory, world.Result.Outcome);
            Assert.AreEqual(SurvivalTicks, world.Result.Ticks);
            Assert.AreEqual(world.Level, world.Result.Level);
            Assert.AreEqual(42, world.Result.EnemiesKilled);
            Assert.AreEqual(SurvivalSeconds, simulation.SurvivedSeconds(world), 1e-4f);

            // 끝난 뒤 Step은 아무것도 하지 않는다(타이머도 그대로).
            Step(simulation, world, 120);
            Assert.AreEqual(SurvivalTicks, world.Tick);
            Assert.AreEqual(SurvivalTicks, simulation.RunTicks(world));
            Assert.AreEqual(RunOutcome.Victory, world.Outcome);
        }

        [Test]
        public void NoSurvivalGoal_NeverWins()
        {
            var simulation = NewSimulation(0f);
            var world = simulation.CreateWorld(1);
            Assert.AreEqual(0L, simulation.SurvivalTicks);
            Step(simulation, world, 1200);
            Assert.AreEqual(RunOutcome.Playing, world.Outcome);
            Assert.AreEqual(1200L, world.Tick);
        }

        [Test]
        public void OldConstructor_HasNoTimeVictory()
        {
            var simulation = new Simulation(TicksPerSecond, new PlayerTuningData(5f, 1f, 30f, 0.4f, 0.5f), EnemyRulesData.None, WeaponRulesData.None, ExperienceRulesData.None, UpgradeRulesData.None);
            Assert.AreEqual(0L, simulation.SurvivalTicks);
        }

        [Test]
        public void TimerStops_WhileChoosingALevelUp()
        {
            var experience = new ExperienceRulesData(new[] { 5 }, 0, 10, 5f, 0.4f);
            var upgrades = new UpgradeRulesData(new[] { new UpgradeData(StatTarget.MoveSpeed, StatOp.Multiply, 1.1f, 5) }, 1);
            var simulation = NewSimulation(experience: experience, upgrades: upgrades);
            var world = simulation.CreateWorld(1);
            Step(simulation, world, 100);
            simulation.AddExperience(world, 5);
            Assert.Greater(world.PendingLevelUps, 0, "level-up window should be waiting");

            Step(simulation, world, 1000);
            Assert.AreEqual(100L, world.Tick, "timer runs during the level-up choice");
            Assert.AreEqual(100L, simulation.RunTicks(world));
            Assert.AreEqual(RunOutcome.Playing, world.Outcome, "won while choosing");

            Assert.IsTrue(simulation.ChooseUpgrade(world, 0));
            Step(simulation, world, SurvivalTicks - 100);
            Assert.AreEqual(RunOutcome.Victory, world.Outcome);
            Assert.AreEqual(SurvivalTicks, world.Result.Ticks);
        }

        [Test]
        public void DyingOnTheGoalTick_IsDefeat()
        {
            const float radius = 0.3f;
            var enemies = new EnemyRulesData(new[] { new EnemyKindData(0f, radius, 1000f, 1, 10f) }, new[] { new SpawnPhaseData(0f, 0f, 0) }, 15f, 10, 0.5f);
            var simulation = NewSimulation(enemies: enemies);
            var world = simulation.CreateWorld(1);
            Step(simulation, world, SurvivalTicks - 1);
            world.Player.Health = 5f; // 다음 접촉 한 번에 0
            world.Enemies[0] = new EnemyState { Id = world.AllocateEntityId(), Kind = 0, Position = Vec2.Zero, PreviousPosition = Vec2.Zero, Health = 1000f, LastHitTick = EnemyState.NeverHit };
            world.EnemyCount = 1;
            Step(simulation, world, 1);
            Assert.AreEqual(RunOutcome.Defeat, world.Outcome);
            Assert.AreEqual(SurvivalTicks, world.Result.Ticks);
        }

        [Test]
        public void SkipTo_MovesForwardOnly_AndVictoryFollows()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            Step(simulation, world, 30);
            Assert.IsFalse(simulation.SkipTo(world, 0.25f), "backwards");
            Assert.AreEqual(30L, world.Tick);
            Assert.IsFalse(simulation.SkipTo(world, 0.5f), "same tick");

            Assert.IsTrue(simulation.SkipTo(world, SurvivalSeconds - 2f));
            Assert.AreEqual(SurvivalTicks - 2 * TicksPerSecond, world.Tick);
            Assert.AreEqual(RunOutcome.Playing, world.Outcome);
            Step(simulation, world, 2 * TicksPerSecond);
            Assert.AreEqual(RunOutcome.Victory, world.Outcome);
            Assert.AreEqual(SurvivalTicks, world.Result.Ticks);
            Assert.IsFalse(simulation.SkipTo(world, 100f), "skip after the run is over");
        }

        [Test]
        public void SkipTo_PastTheGoal_WinsOnTheNextStep()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            Assert.IsTrue(simulation.SkipTo(world, SurvivalSeconds + 5f));
            Assert.AreEqual(RunOutcome.Playing, world.Outcome);
            Step(simulation, world, 1);
            Assert.AreEqual(RunOutcome.Victory, world.Outcome);
        }

        [Test]
        public void SkipTo_UsesTheSpawnPhaseOfThatTime()
        {
            var enemies = new EnemyRulesData(new[] { new EnemyKindData(0f, 0.3f, 10f, 1, 0f) },
                new[] { new SpawnPhaseData(0f, 0f, 0), new SpawnPhaseData(5f, 60f, 0) }, 15f, 300, 0.5f);
            var simulation = NewSimulation(enemies: enemies);
            var world = simulation.CreateWorld(1);
            Step(simulation, world, 60);
            Assert.AreEqual(0, world.EnemyCount, "first phase spawns nothing");
            Assert.IsTrue(simulation.SkipTo(world, 6f));
            Step(simulation, world, 60);
            Assert.AreEqual(60, world.EnemyCount, "later phase: one per tick");
        }

        [Test]
        public void SameSeed_SameVictoryTick()
        {
            var enemies = new EnemyRulesData(new[] { new EnemyKindData(1f, 0.3f, 10f, 1, 0f) }, new[] { new SpawnPhaseData(0f, 3f, 0) }, 15f, 300, 0.5f);
            var a = NewSimulation(enemies: enemies);
            var b = NewSimulation(enemies: enemies);
            var worldA = a.CreateWorld(77);
            var worldB = b.CreateWorld(77);
            Step(a, worldA, SurvivalTicks + 10);
            Step(b, worldB, SurvivalTicks + 10);
            Assert.AreEqual(worldA.Result.Ticks, worldB.Result.Ticks);
            Assert.AreEqual(worldA.EnemiesSpawnedTotal, worldB.EnemiesSpawnedTotal);
            Assert.AreEqual(RunOutcome.Victory, worldA.Outcome);
        }

        [TestCase(0L, 0L, 0)]
        [TestCase(59L, 0L, 0)]
        [TestCase(60L, 0L, 1)]
        [TestCase(3599L, 0L, 59)]
        [TestCase(3600L, 1L, 0)]
        [TestCase(107399L, 29L, 49)]
        [TestCase(107400L, 29L, 50)]
        [TestCase(107999L, 29L, 59)]
        [TestCase(108000L, 30L, 0)]
        [TestCase(-5L, 0L, 0)]
        public void Split_GivesMinutesAndFlooredSeconds(long ticks, long minutes, int seconds)
        {
            RunClock.Split(ticks, 60, out long m, out int s);
            Assert.AreEqual(minutes, m);
            Assert.AreEqual(seconds, s);
        }
    }
}
