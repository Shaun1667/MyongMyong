using Myong.Core;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;

namespace Myong.Tests
{
    /// <summary>접촉 피해 · 체력 · 판 끝(1-5): 겹치면 피해 · 무적 시간 · 0이면 GameOver와 Step 멈춤 · 결과 값 · 다시 시작 · 같은 시드 같은 사망 틱 · 할당. 여기의 숫자는 테스트용 값이다(게임 수치는 에셋).</summary>
    public sealed class HealthTests
    {
        const int TicksPerSecond = 60;
        const float PlayerSpeed = 5f;
        const float MaxHealth = 30f;
        const float PlayerRadius = 0.4f;
        const float InvulnerableSeconds = 0.5f; // 30틱
        const int InvulnerableTicks = 30;
        const float EnemyRadius = 0.3f;
        const float ContactDamage = 10f;

        static EnemyRulesData Enemies(float contactDamage = ContactDamage, float speed = 0f, float spawnsPerSecond = 0f, float health = 1000f) => new EnemyRulesData(
            new[] { new EnemyKindData(speed, EnemyRadius, health, 1, contactDamage) },
            new[] { new SpawnPhaseData(0f, spawnsPerSecond, 0) },
            15f, 300, 0.5f);

        static Simulation NewSimulation(EnemyRulesData enemies = null, float invulnerableSeconds = InvulnerableSeconds, WeaponRulesData weapons = null)
        {
            return new Simulation(TicksPerSecond, new PlayerTuningData(PlayerSpeed, 1f, MaxHealth, PlayerRadius, invulnerableSeconds),
                enemies ?? Enemies(), weapons ?? WeaponRulesData.None, ExperienceRulesData.None, UpgradeRulesData.None);
        }

        static void PlaceEnemy(World world, Vec2 position)
        {
            world.Enemies[world.EnemyCount] = new EnemyState
            {
                Id = world.AllocateEntityId(),
                Kind = 0,
                Position = position,
                PreviousPosition = position,
                Health = 1000f,
                LastHitTick = EnemyState.NeverHit,
            };
            world.EnemyCount++;
        }

        static void Step(Simulation simulation, World world, int ticks)
        {
            var input = new InputFrame();
            for (int i = 0; i < ticks; i++) simulation.Step(world, input);
        }

        [Test]
        public void NewRun_StartsAtFullHealth_AndPlaying()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            Assert.AreEqual(MaxHealth, world.Player.Health);
            Assert.AreEqual(MaxHealth, simulation.MaxHealth);
            Assert.AreEqual(RunOutcome.Playing, world.Outcome);
            Assert.IsFalse(world.IsOver);
            Assert.AreEqual(PlayerState.NeverHurt, world.Player.LastHurtTick);
            Assert.AreEqual(InvulnerableTicks, simulation.HurtInvulnerableTicks);
        }

        [Test]
        public void OverlappingEnemy_DealsContactDamage()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            PlaceEnemy(world, new Vec2(PlayerRadius + EnemyRadius - 0.05f, 0f));
            Step(simulation, world, 1);
            Assert.AreEqual(MaxHealth - ContactDamage, world.Player.Health, 1e-4f);
            Assert.AreEqual(1, world.HitsTaken);
            Assert.AreEqual(0, world.Player.LastHurtTick);
            Assert.AreEqual(InvulnerableTicks, world.Player.InvulnerableUntilTick);
        }

        [Test]
        public void EnemyJustOutsideTheRadiusSum_DoesNoDamage()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            PlaceEnemy(world, new Vec2(PlayerRadius + EnemyRadius + 0.05f, 0f));
            Step(simulation, world, 10);
            Assert.AreEqual(MaxHealth, world.Player.Health);
            Assert.AreEqual(0, world.HitsTaken);
        }

        [Test]
        public void HarmlessEnemy_DoesNoDamage()
        {
            var simulation = NewSimulation(Enemies(contactDamage: 0f));
            var world = simulation.CreateWorld(1);
            PlaceEnemy(world, Vec2.Zero);
            Step(simulation, world, 120);
            Assert.AreEqual(MaxHealth, world.Player.Health);
        }

        [Test]
        public void DuringInvulnerability_NoFurtherDamage_ThenHitAgain()
        {
            var simulation = NewSimulation(Enemies(contactDamage: 1f));
            var world = simulation.CreateWorld(1);
            PlaceEnemy(world, Vec2.Zero);
            PlaceEnemy(world, new Vec2(0.1f, 0f));
            Step(simulation, world, 1);
            Assert.AreEqual(MaxHealth - 1f, world.Player.Health, 1e-4f, "two touching enemies = one hit");
            Step(simulation, world, InvulnerableTicks - 1); // 틱 1 ~ 29: 무적
            Assert.AreEqual(MaxHealth - 1f, world.Player.Health, 1e-4f);
            Assert.AreEqual(1, world.HitsTaken);
            Step(simulation, world, 1); // 틱 30: 다시 맞음
            Assert.AreEqual(MaxHealth - 2f, world.Player.Health, 1e-4f);
            Assert.AreEqual(2, world.HitsTaken);
            Assert.AreEqual(InvulnerableTicks, world.Player.LastHurtTick);
        }

        [Test]
        public void ZeroInvulnerability_HitsEveryTick()
        {
            var simulation = NewSimulation(Enemies(contactDamage: 1f), invulnerableSeconds: 0f);
            var world = simulation.CreateWorld(1);
            PlaceEnemy(world, Vec2.Zero);
            Step(simulation, world, 5);
            Assert.AreEqual(5, world.HitsTaken);
        }

        [Test]
        public void StrongestTouchingEnemy_SetsTheDamage()
        {
            var enemies = new EnemyRulesData(
                new[] { new EnemyKindData(0f, EnemyRadius, 1000f, 0, 3f), new EnemyKindData(0f, EnemyRadius, 1000f, 0, 7f) },
                new[] { new SpawnPhaseData(0f, 0f, 0) }, 15f, 10, 0f);
            var simulation = NewSimulation(enemies);
            var world = simulation.CreateWorld(1);
            PlaceEnemy(world, Vec2.Zero);
            world.Enemies[world.EnemyCount] = world.Enemies[0];
            world.Enemies[world.EnemyCount].Id = world.AllocateEntityId();
            world.Enemies[world.EnemyCount].Kind = 1;
            world.EnemyCount++;
            Step(simulation, world, 1);
            Assert.AreEqual(MaxHealth - 7f, world.Player.Health, 1e-4f);
        }

        [Test]
        public void DebugInvincible_TakesNoDamage()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            world.PlayerInvincible = true;
            PlaceEnemy(world, Vec2.Zero);
            Step(simulation, world, 200);
            Assert.AreEqual(MaxHealth, world.Player.Health);
            Assert.IsFalse(world.IsOver);
        }

        [Test]
        public void HealthZero_EndsTheRun_AndStepStops()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            PlaceEnemy(world, Vec2.Zero);
            // 30 / 10 = 세 번: 틱 0 · 30 · 60
            Step(simulation, world, 60);
            Assert.IsFalse(world.IsOver);
            Step(simulation, world, 1);
            Assert.IsTrue(world.IsOver);
            Assert.AreEqual(RunOutcome.Defeat, world.Outcome);
            Assert.AreEqual(0f, world.Player.Health);
            Assert.AreEqual(61, world.Tick, "the death tick is counted");

            Vec2 position = world.Player.Position;
            long kills = world.EnemiesKilledTotal;
            var input = new InputFrame { Move = new Vec2(1f, 0f) };
            for (int i = 0; i < 100; i++) simulation.Step(world, input);
            Assert.AreEqual(61, world.Tick, "Step does nothing after the run is over");
            Assert.AreEqual(position, world.Player.Position);
            Assert.AreEqual(kills, world.EnemiesKilledTotal);
            Assert.AreEqual(3, world.HitsTaken);
        }

        [Test]
        public void Result_RecordsSurvivalLevelAndKills()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            world.Level = 4;
            world.EnemiesKilledTotal = 17;
            PlaceEnemy(world, Vec2.Zero);
            Step(simulation, world, 61);
            Assert.IsTrue(world.IsOver);
            Assert.AreEqual(RunOutcome.Defeat, world.Result.Outcome);
            Assert.AreEqual(61, world.Result.Ticks);
            Assert.AreEqual(4, world.Result.Level);
            Assert.AreEqual(17, world.Result.EnemiesKilled);
            Assert.AreEqual(61f / TicksPerSecond, simulation.SurvivedSeconds(world), 1e-4f);
        }

        [Test]
        public void DebugDamage_And_EndRun_UseTheSameEnd()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            Step(simulation, world, 10);
            simulation.DamagePlayer(world, 12f);
            Assert.AreEqual(MaxHealth - 12f, world.Player.Health, 1e-4f);
            Assert.AreEqual(10, world.Player.LastHurtTick);
            simulation.FillHealth(world);
            Assert.AreEqual(MaxHealth, world.Player.Health);
            simulation.DamagePlayer(world, 1000f);
            Assert.IsTrue(world.IsOver);
            Assert.AreEqual(0f, world.Player.Health);
            Assert.AreEqual(10, world.Result.Ticks);

            var other = simulation.CreateWorld(2);
            Step(simulation, other, 5);
            simulation.EndRun(other, RunOutcome.Defeat);
            Assert.IsTrue(other.IsOver);
            Assert.AreEqual(5, other.Result.Ticks);
            // 이미 끝난 판은 다시 끝나지 않는다(승리로 덮이지 않음).
            simulation.EndRun(other, RunOutcome.Victory);
            Assert.AreEqual(RunOutcome.Defeat, other.Result.Outcome);
        }

        [Test]
        public void Victory_UsesTheSameRunEnd()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            Step(simulation, world, 3);
            simulation.EndRun(world, RunOutcome.Victory);
            Assert.IsTrue(world.IsOver);
            Assert.AreEqual(RunOutcome.Victory, world.Result.Outcome);
            Step(simulation, world, 10);
            Assert.AreEqual(3, world.Tick);
        }

        [Test]
        public void EndRun_ClearsPendingLevelUps()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            world.PendingLevelUps = 2;
            world.LevelUpChoiceCount = 1;
            simulation.EndRun(world, RunOutcome.Defeat);
            Assert.AreEqual(0, world.PendingLevelUps);
            Assert.AreEqual(0, world.LevelUpChoiceCount);
        }

        [Test]
        public void Restart_IsANewWorld_FullyReset()
        {
            var simulation = NewSimulation(Enemies(speed: 2f, spawnsPerSecond: 5f));
            var world = simulation.CreateWorld(9);
            Step(simulation, world, 2000);
            Assert.IsTrue(world.IsOver, "the run should end with no weapon");

            var fresh = simulation.CreateWorld(10);
            Assert.IsFalse(fresh.IsOver);
            Assert.AreEqual(RunOutcome.Playing, fresh.Result.Outcome);
            Assert.AreEqual(MaxHealth, fresh.Player.Health);
            Assert.AreEqual(0, fresh.Tick);
            Assert.AreEqual(0, fresh.HitsTaken);
            Assert.AreEqual(0, fresh.EnemyCount);
            Assert.AreEqual(PlayerState.NeverHurt, fresh.Player.LastHurtTick);
            Assert.AreEqual(0, fresh.Player.InvulnerableUntilTick);
            Step(simulation, fresh, 10);
            Assert.AreEqual(10, fresh.Tick, "a fresh run steps again");
        }

        [Test]
        public void SameSeed_SameDeathTick_DifferentSeedDiffers()
        {
            var simulation = NewSimulation(Enemies(speed: 2f, spawnsPerSecond: 3f, health: 6f), weapons: OneGun());
            long a = DeathTick(simulation, 1234);
            long b = DeathTick(simulation, 1234);
            Assert.Greater(a, 0);
            Assert.AreEqual(a, b);
            bool anyDiffers = false;
            foreach (int seed in new[] { 1, 2, 3, 4, 5 })
            {
                if (DeathTick(simulation, seed) != a) anyDiffers = true;
            }
            Assert.IsTrue(anyDiffers, "every seed died on the same tick");
        }

        static WeaponRulesData OneGun() => new WeaponRulesData(new[] { new WeaponKindData(0.5f, 10f, 2f, 6f, 1, 0, 0.1f) }, new[] { 0 }, 64, Vec2.Zero);

        static long DeathTick(Simulation simulation, int seed)
        {
            var world = simulation.CreateWorld(seed);
            var input = new InputFrame { Move = new Vec2(0.3f, 0.1f) };
            for (int i = 0; i < 20000 && !world.IsOver; i++) simulation.Step(world, input);
            Assert.IsTrue(world.IsOver, "seed " + seed + " did not die");
            return world.Result.Ticks;
        }

        [Test]
        public void Step_DoesNotAllocate_WithContactDamage()
        {
            var simulation = NewSimulation(Enemies(contactDamage: 0.01f, speed: 2f, spawnsPerSecond: 30f, health: 6f), weapons: OneGun());
            var world = simulation.CreateWorld(5);
            simulation.SpawnEnemies(world, 250);
            var input = new InputFrame { Move = new Vec2(1f, 0.5f) };
            for (int i = 0; i < 60; i++) simulation.Step(world, input);

            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++) simulation.Step(world, input);
            }, NUnit.Framework.Is.Not.AllocatingGCMemory());
            Assert.Greater(world.HitsTaken, 0);
            Assert.IsFalse(world.IsOver);
        }
    }
}
