using System.Collections.Generic;
using Myong.Core;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;

namespace Myong.Tests
{
    /// <summary>자동 사격(1-3): 조준 · 발사 간격 · 피해 · 관통 · 사거리 · 탄 최대 수 · 결정성 · 할당. 여기의 숫자는 테스트용 값이다(게임 수치는 WeaponTable 에셋).</summary>
    public sealed class WeaponTests
    {
        const int TicksPerSecond = 60;
        const float PlayerSpeed = 5f;
        const float EnemyRadius = 0.3f;
        const float Tolerance = 0.001f;

        static WeaponKindData Gun(float interval = 0.5f, float speed = 10f, float damage = 5f, float range = 10f, int shots = 1, int pierce = 0, float radius = 0.1f)
        {
            return new WeaponKindData(interval, speed, damage, range, shots, pierce, radius);
        }

        static WeaponRulesData Weapons(WeaponKindData gun, int maxBullets = 64)
        {
            return new WeaponRulesData(new[] { gun }, new[] { 0 }, maxBullets, Vec2.Zero);
        }

        // 움직이지 않고 스폰하지 않는 적(위치를 손으로 놓는다).
        static EnemyRulesData StillEnemies(int maxAlive = 50, float health = 10f)
        {
            return new EnemyRulesData(
                new[] { new EnemyKindData(0f, EnemyRadius, health) },
                new[] { new SpawnPhaseData(0f, 0f, 0) },
                20f, maxAlive, 0f);
        }

        static Simulation NewSimulation(EnemyRulesData enemies, WeaponRulesData weapons) => new Simulation(TicksPerSecond, new PlayerTuningData(PlayerSpeed), enemies, weapons);

        static int Place(World world, Vec2 position, float health)
        {
            int index = world.EnemyCount;
            world.Enemies[index] = new EnemyState
            {
                Id = world.AllocateEntityId(),
                Kind = 0,
                Position = position,
                PreviousPosition = position,
                Health = health,
                LastHitTick = EnemyState.NeverHit,
            };
            world.EnemyCount++;
            return index;
        }

        static int CountHit(World world)
        {
            int hit = 0;
            for (int i = 0; i < world.EnemyCount; i++)
            {
                if (world.Enemies[i].LastHitTick != EnemyState.NeverHit) hit++;
            }
            return hit;
        }

        // ---- 조준 ----

        [Test]
        public void Aims_AtTheNearestEnemy()
        {
            var simulation = NewSimulation(StillEnemies(), Weapons(Gun()));
            var world = simulation.CreateWorld(1);
            Place(world, new Vec2(5f, 0f), 10f);
            Place(world, new Vec2(-2f, 0f), 10f); // 가장 가까움
            Place(world, new Vec2(0f, 3f), 10f);

            simulation.Step(world, default);

            Assert.AreEqual(1, world.BulletCount);
            Assert.AreEqual(-1f, world.Bullets[0].Direction.X, Tolerance);
            Assert.AreEqual(0f, world.Bullets[0].Direction.Y, Tolerance);
        }

        [Test]
        public void Volley_AimsAtTheNearestEnemies_OneEach()
        {
            var simulation = NewSimulation(StillEnemies(), Weapons(Gun(shots: 2)));
            var world = simulation.CreateWorld(1);
            Place(world, new Vec2(5f, 0f), 10f);
            Place(world, new Vec2(-2f, 0f), 10f);
            Place(world, new Vec2(0f, 3f), 10f);

            simulation.Step(world, default);

            Assert.AreEqual(2, world.BulletCount);
            var directions = new List<Vec2> { world.Bullets[0].Direction, world.Bullets[1].Direction };
            Assert.IsTrue(directions.Exists(d => System.Math.Abs(d.X + 1f) < Tolerance), "no bullet toward (-2, 0)");
            Assert.IsTrue(directions.Exists(d => System.Math.Abs(d.Y - 1f) < Tolerance), "no bullet toward (0, 3)");
        }

        [Test]
        public void NoEnemy_NoShot_AndOutOfRange_NoShot()
        {
            var simulation = NewSimulation(StillEnemies(), Weapons(Gun(range: 5f)));
            var world = simulation.CreateWorld(1);
            for (int t = 0; t < TicksPerSecond * 2; t++) simulation.Step(world, default);
            Assert.AreEqual(0, world.BulletsFiredTotal);

            Place(world, new Vec2(8f, 0f), 10f); // 사거리 5 + 몸 0.3 밖
            for (int t = 0; t < TicksPerSecond; t++) simulation.Step(world, default);
            Assert.AreEqual(0, world.BulletsFiredTotal);

            // 쏠 적이 생기면 기다리지 않고 바로 쏜다.
            Place(world, new Vec2(3f, 0f), 10f);
            simulation.Step(world, default);
            Assert.AreEqual(1, world.BulletsFiredTotal);
        }

        // ---- 발사 간격 ----

        [Test]
        public void FireInterval_IsKept()
        {
            const float interval = 0.5f; // = 30틱
            var simulation = NewSimulation(StillEnemies(), Weapons(Gun(interval: interval, speed: 1f)));
            Assert.AreEqual(30, simulation.FireIntervalTicks(0));
            var world = simulation.CreateWorld(1);
            world.EnemiesInvincible = true;
            Place(world, new Vec2(9f, 0f), 10f);

            var fireTicks = new List<long>();
            for (int t = 0; t < 121; t++)
            {
                long before = world.BulletsFiredTotal;
                long tick = world.Tick;
                simulation.Step(world, default);
                if (world.BulletsFiredTotal > before) fireTicks.Add(tick);
            }
            CollectionAssert.AreEqual(new long[] { 0, 30, 60, 90, 120 }, fireTicks);
        }

        [Test]
        public void FireIntervalTicks_RoundsAndIsAtLeastOne()
        {
            var simulation = NewSimulation(StillEnemies(), new WeaponRulesData(new[] { Gun(interval: 0.4f), Gun(interval: 0.001f) }, new[] { 0, 1 }, 8, Vec2.Zero));
            Assert.AreEqual(24, simulation.FireIntervalTicks(0));
            Assert.AreEqual(1, simulation.FireIntervalTicks(1));
        }

        // ---- 피해 · 처치 ----

        [Test]
        public void Damage_ReducesHealth_AndZeroKills()
        {
            const float damage = 5f;
            var simulation = NewSimulation(StillEnemies(), Weapons(Gun(damage: damage, interval: 0.2f)));
            var world = simulation.CreateWorld(1);
            int id = world.Enemies[Place(world, new Vec2(2f, 0f), 12f)].Id;

            var healths = new List<float> { 12f };
            for (int t = 0; t < TicksPerSecond * 3 && world.EnemyCount > 0; t++)
            {
                simulation.Step(world, default);
                if (world.EnemyCount > 0 && world.Enemies[0].Health != healths[healths.Count - 1]) healths.Add(world.Enemies[0].Health);
            }

            CollectionAssert.AreEqual(new[] { 12f, 7f, 2f }, healths);
            Assert.AreEqual(0, world.EnemyCount, "a dead enemy is removed");
            Assert.AreEqual(1, world.EnemiesKilledTotal);

            // 새 적은 새 ID
            int newIndex = Place(world, new Vec2(2f, 0f), 12f);
            Assert.AreNotEqual(id, world.Enemies[newIndex].Id);
        }

        [Test]
        public void Invincible_TakesNoDamage_ButBulletsStillHit()
        {
            var simulation = NewSimulation(StillEnemies(), Weapons(Gun(damage: 100f)));
            var world = simulation.CreateWorld(1);
            world.EnemiesInvincible = true;
            Place(world, new Vec2(2f, 0f), 10f);
            for (int t = 0; t < TicksPerSecond; t++) simulation.Step(world, default);

            Assert.AreEqual(1, world.EnemyCount);
            Assert.AreEqual(10f, world.Enemies[0].Health);
            Assert.AreEqual(0, world.EnemiesKilledTotal);
            Assert.AreNotEqual(EnemyState.NeverHit, world.Enemies[0].LastHitTick);
        }

        [Test]
        public void KilledEnemy_IsReplacedByTheLastOne()
        {
            var simulation = NewSimulation(StillEnemies(), Weapons(Gun(damage: 100f, interval: 10f)));
            var world = simulation.CreateWorld(1);
            Place(world, new Vec2(1f, 0f), 1f); // 가장 가까움 → 죽음
            Place(world, new Vec2(0f, 6f), 1f);
            int lastId = world.Enemies[Place(world, new Vec2(0f, -7f), 1f)].Id;

            for (int t = 0; t < 20; t++) simulation.Step(world, default);

            Assert.AreEqual(2, world.EnemyCount);
            Assert.AreEqual(lastId, world.Enemies[0].Id, "the last enemy fills the empty slot");
        }

        // ---- 관통 ----

        [TestCase(0, 1)]
        [TestCase(2, 3)]
        public void Pierce_HitsThatManyMoreEnemies(int pierce, int expectedHits)
        {
            var simulation = NewSimulation(StillEnemies(), Weapons(Gun(pierce: pierce, range: 20f)));
            var world = simulation.CreateWorld(1);
            world.EnemiesInvincible = true;
            for (int i = 0; i < 6; i++) Place(world, new Vec2(2f + i, 0f), 10f);

            simulation.Step(world, default);
            world.FiringPaused = true; // 한 발만
            for (int t = 0; t < TicksPerSecond * 3 && world.BulletCount > 0; t++) simulation.Step(world, default);

            Assert.AreEqual(1, world.BulletsFiredTotal);
            Assert.AreEqual(0, world.BulletCount);
            Assert.AreEqual(expectedHits, CountHit(world));
            for (int i = 0; i < expectedHits; i++) Assert.AreNotEqual(EnemyState.NeverHit, world.Enemies[i].LastHitTick, $"enemy at x={2 + i} should be hit first");
        }

        // ---- 사거리 ----

        [Test]
        public void Bullet_DisappearsAtTheEndOfItsRange()
        {
            const float range = 3f;
            const float speed = 6f; // 한 틱 0.1 → 30틱
            var simulation = NewSimulation(StillEnemies(), Weapons(Gun(range: range, speed: speed)));
            var world = simulation.CreateWorld(1);
            Place(world, new Vec2(0f, 2f), 10f);
            simulation.Step(world, default); // 쏘고 한 틱 날아감
            Assert.AreEqual(1, world.BulletCount);
            world.FiringPaused = true;
            simulation.ClearEnemies(world);

            int aliveTicks = 1;
            float farthest = 0f;
            while (world.BulletCount > 0 && aliveTicks < 100)
            {
                farthest = System.Math.Max(farthest, world.Bullets[0].Position.Magnitude);
                simulation.Step(world, default);
                aliveTicks++;
            }

            Assert.AreEqual(0, world.BulletCount);
            Assert.AreEqual(range / (speed / TicksPerSecond), aliveTicks, 1f);
            Assert.AreEqual(range, farthest, 0.11f);
            Assert.LessOrEqual(farthest, range + Tolerance);
        }

        // ---- 탄 최대 수 ----

        [Test]
        public void BulletCount_NeverExceedsTheMaximum()
        {
            const int max = 5;
            var simulation = NewSimulation(StillEnemies(), Weapons(Gun(interval: 0.001f, speed: 0.5f, range: 20f, shots: 3), max));
            var world = simulation.CreateWorld(1);
            world.EnemiesInvincible = true;
            Place(world, new Vec2(15f, 0f), 10f);
            for (int t = 0; t < 60; t++)
            {
                simulation.Step(world, default);
                Assert.LessOrEqual(world.BulletCount, max);
            }
            Assert.AreEqual(max, world.BulletCount);
            Assert.AreEqual(max, world.Bullets.Length);
        }

        // ---- 결정성 ----

        static EnemyRulesData MovingEnemies() => new EnemyRulesData(
            new[] { new EnemyKindData(2f, EnemyRadius, 10f) },
            new[] { new SpawnPhaseData(0f, 6f, 0) },
            12f, 120, 0.5f);

        [Test]
        public void SameSeed_GivesTheSameKills()
        {
            var simulation = NewSimulation(MovingEnemies(), Weapons(Gun(interval: 0.2f, pierce: 1), 128));
            var a = simulation.CreateWorld(77);
            var b = simulation.CreateWorld(77);
            var moves = new[] { new Vec2(1f, 0f), Vec2.Zero, new Vec2(-0.5f, 1f), new Vec2(0f, -1f) };
            foreach (var move in moves)
            {
                var input = new InputFrame { Move = move };
                for (int t = 0; t < 150; t++)
                {
                    simulation.Step(a, input);
                    simulation.Step(b, input);
                }
            }

            Assert.Greater(a.EnemiesKilledTotal, 0);
            Assert.AreEqual(a.EnemiesKilledTotal, b.EnemiesKilledTotal);
            Assert.AreEqual(a.BulletsFiredTotal, b.BulletsFiredTotal);
            Assert.AreEqual(a.EnemyCount, b.EnemyCount);
            Assert.AreEqual(a.BulletCount, b.BulletCount);
            Assert.AreEqual(a.NextEntityId, b.NextEntityId);
            for (int i = 0; i < a.EnemyCount; i++)
            {
                Assert.AreEqual(a.Enemies[i].Id, b.Enemies[i].Id);
                Assert.AreEqual(a.Enemies[i].Position, b.Enemies[i].Position);
                Assert.AreEqual(a.Enemies[i].Health, b.Enemies[i].Health);
            }
            for (int i = 0; i < a.BulletCount; i++)
            {
                Assert.AreEqual(a.Bullets[i].Id, b.Bullets[i].Id);
                Assert.AreEqual(a.Bullets[i].Position, b.Bullets[i].Position);
            }
        }

        [Test]
        public void EntityIds_AreNeverReused_WithBullets()
        {
            var simulation = NewSimulation(MovingEnemies(), Weapons(Gun(interval: 0.1f), 128));
            var world = simulation.CreateWorld(5);
            var seen = new HashSet<int>();
            for (int t = 0; t < TicksPerSecond * 5; t++)
            {
                simulation.Step(world, default);
                for (int i = 0; i < world.EnemyCount; i++) seen.Add(world.Enemies[i].Id);
                for (int i = 0; i < world.BulletCount; i++) seen.Add(world.Bullets[i].Id);
            }
            Assert.Greater(world.EnemiesKilledTotal, 0);
            // 같은 ID가 다른 엔티티에 다시 쓰였다면 Allocate된 수보다 본 ID가 많을 수 없다 — 모든 ID는 NextEntityId 이하이고 서로 다르다.
            foreach (int id in seen) Assert.LessOrEqual(id, world.NextEntityId);
            Assert.AreEqual(1 + world.EnemiesSpawnedTotal + world.BulletsFiredTotal, world.NextEntityId);
        }

        // ---- 할당 ----

        [Test]
        public void Step_DoesNotAllocate_WithEnemiesAndBullets()
        {
            var enemies = new EnemyRulesData(
                new[] { new EnemyKindData(2f, EnemyRadius, 1000f) },
                new[] { new SpawnPhaseData(0f, 30f, 0) },
                12f, 300, 0.5f);
            var simulation = NewSimulation(enemies, Weapons(Gun(interval: 0.05f, shots: 3, pierce: 2, range: 15f), 256));
            var world = simulation.CreateWorld(21);
            simulation.SpawnEnemies(world, 280);
            var input = new InputFrame { Move = new Vec2(1f, 0.5f) };
            for (int i = 0; i < 30; i++) simulation.Step(world, input); // 탄이 쌓이게 · 처음(JIT 등)은 재지 않는다
            Assert.Greater(world.BulletCount, 0);

            Assert.That(() =>
            {
                for (int i = 0; i < 120; i++) simulation.Step(world, input);
            }, NUnit.Framework.Is.Not.AllocatingGCMemory());
        }
    }
}
