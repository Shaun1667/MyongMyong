using System.Collections.Generic;
using Myong.Core;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;

namespace Myong.Tests
{
    /// <summary>적 스폰 · 추적(1-2)과 시드 난수. 여기의 숫자는 테스트용 값이다(게임 수치는 EnemyTable · SpawnTable 에셋).</summary>
    public sealed class EnemySpawnTests
    {
        const int TicksPerSecond = 60;
        const float PlayerSpeed = 5f;
        const float EnemySpeed = 2f;
        const float EnemyRadius = 0.3f;
        const float SpawnRadius = 20f;
        const float Tolerance = 0.001f;

        static EnemyRulesData Rules(float perSecond = 10f, int maxAlive = 50, float push = 0f, float enemySpeed = EnemySpeed)
        {
            return new EnemyRulesData(
                new[] { new EnemyKindData(enemySpeed, EnemyRadius) },
                new[] { new SpawnPhaseData(0f, perSecond, 0) },
                SpawnRadius, maxAlive, push);
        }

        static Simulation NewSimulation(EnemyRulesData rules) => new Simulation(TicksPerSecond, new PlayerTuningData(PlayerSpeed), rules);

        static void Run(Simulation simulation, World world, Vec2 move, int ticks)
        {
            var input = new InputFrame { Move = move };
            for (int i = 0; i < ticks; i++) simulation.Step(world, input);
        }

        static float DistanceToPlayer(World world, int index) => (world.Enemies[index].Position - world.Player.Position).Magnitude;

        // ---- 시드 난수 ----

        [Test]
        public void Rng_SameSeedAndStream_GivesTheSameSequence()
        {
            var a = Rng.ForStream(123, RandomStream.Spawn);
            var b = Rng.ForStream(123, RandomStream.Spawn);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void Rng_DifferentSeeds_GiveDifferentSequences()
        {
            var a = Rng.ForStream(1, RandomStream.Spawn);
            var b = Rng.ForStream(2, RandomStream.Spawn);
            Assert.AreNotEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void Rng_FloatsStayInRange()
        {
            var rng = Rng.ForStream(9, RandomStream.Spawn);
            for (int i = 0; i < 10000; i++)
            {
                float f = rng.NextFloat();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);
                int n = rng.Range(3, 7);
                Assert.GreaterOrEqual(n, 3);
                Assert.Less(n, 7);
            }
        }

        // ---- 스폰 ----

        [Test]
        public void SameSeed_GivesTheSameSpawns()
        {
            var simulation = NewSimulation(Rules());
            var a = simulation.CreateWorld(42);
            var b = simulation.CreateWorld(42);
            var moves = new[] { new Vec2(1f, 0f), new Vec2(0.5f, -1f), Vec2.Zero, new Vec2(-1f, 1f) };
            foreach (var move in moves)
            {
                Run(simulation, a, move, 45);
                Run(simulation, b, move, 45);
            }

            Assert.Greater(a.EnemyCount, 0);
            Assert.AreEqual(a.EnemyCount, b.EnemyCount);
            Assert.AreEqual(a.EnemiesSpawnedTotal, b.EnemiesSpawnedTotal);
            for (int i = 0; i < a.EnemyCount; i++)
            {
                Assert.AreEqual(a.Enemies[i].Id, b.Enemies[i].Id);
                Assert.AreEqual(a.Enemies[i].Position, b.Enemies[i].Position, $"enemy {i}");
            }
            Assert.AreEqual(a.SpawnRandom.State, b.SpawnRandom.State);
        }

        [Test]
        public void DifferentSeed_GivesDifferentSpawns()
        {
            var simulation = NewSimulation(Rules());
            var a = simulation.CreateWorld(1);
            var b = simulation.CreateWorld(2);
            Run(simulation, a, Vec2.Zero, TicksPerSecond);
            Run(simulation, b, Vec2.Zero, TicksPerSecond);
            Assert.AreNotEqual(a.Enemies[0].Position, b.Enemies[0].Position);
        }

        [Test]
        public void SpawnedEnemy_IsOnTheRadiusAroundThePlayer()
        {
            // 초당 60마리 = 틱마다 한 마리. 새 적은 그 틱에는 움직이지 않으니 스폰 직후 거리 = 반경.
            var simulation = NewSimulation(Rules(perSecond: TicksPerSecond, maxAlive: 100));
            var world = simulation.CreateWorld(5);
            simulation.TeleportPlayer(world, new Vec2(3f, -2f));
            var input = new InputFrame { Move = new Vec2(1f, 1f) };
            for (int t = 0; t < 60; t++)
            {
                int before = world.EnemyCount;
                simulation.Step(world, input);
                Assert.AreEqual(before + 1, world.EnemyCount, $"tick {t}");
                Assert.AreEqual(SpawnRadius, DistanceToPlayer(world, world.EnemyCount - 1), Tolerance, $"tick {t}");
            }
        }

        [Test]
        public void DebugSpawn_IsOnTheRadiusAroundThePlayer()
        {
            var simulation = NewSimulation(Rules(perSecond: 0f, maxAlive: 100));
            var world = simulation.CreateWorld(8);
            simulation.TeleportPlayer(world, new Vec2(-10f, 4f));
            Assert.AreEqual(30, simulation.SpawnEnemies(world, 30));
            for (int i = 0; i < world.EnemyCount; i++) Assert.AreEqual(SpawnRadius, DistanceToPlayer(world, i), Tolerance);
        }

        [Test]
        public void SpawnRate_FollowsThePhase()
        {
            var simulation = NewSimulation(Rules(perSecond: 3f, maxAlive: 100));
            var world = simulation.CreateWorld(0);
            Run(simulation, world, Vec2.Zero, TicksPerSecond * 4);
            Assert.AreEqual(12, world.EnemiesSpawnedTotal, 1);
        }

        [Test]
        public void PausedSpawning_SpawnsNothing()
        {
            var simulation = NewSimulation(Rules(perSecond: 30f));
            var world = simulation.CreateWorld(0);
            world.SpawningPaused = true;
            Run(simulation, world, Vec2.Zero, TicksPerSecond * 2);
            Assert.AreEqual(0, world.EnemyCount);
            world.SpawningPaused = false;
            Run(simulation, world, Vec2.Zero, TicksPerSecond);
            Assert.Greater(world.EnemyCount, 0);
        }

        [Test]
        public void AliveCount_NeverExceedsTheMaximum()
        {
            const int max = 25;
            var simulation = NewSimulation(Rules(perSecond: 600f, maxAlive: max));
            var world = simulation.CreateWorld(3);
            for (int t = 0; t < 300; t++)
            {
                simulation.Step(world, default);
                Assert.LessOrEqual(world.EnemyCount, max);
            }
            Assert.AreEqual(max, world.EnemyCount);
            Assert.AreEqual(0, simulation.SpawnEnemies(world, 1000));
            Assert.AreEqual(max, world.EnemyCount);
            Assert.AreEqual(max, world.Enemies.Length);
        }

        [Test]
        public void EntityIds_AreNeverReused()
        {
            var simulation = NewSimulation(Rules(perSecond: 120f, maxAlive: 100));
            var world = simulation.CreateWorld(11);
            var seen = new HashSet<int> { world.Player.Id };

            Run(simulation, world, Vec2.Zero, TicksPerSecond);
            for (int i = 0; i < world.EnemyCount; i++) Assert.IsTrue(seen.Add(world.Enemies[i].Id), $"duplicate id {world.Enemies[i].Id}");

            simulation.ClearEnemies(world);
            Assert.AreEqual(0, world.EnemyCount);
            simulation.SpawnEnemies(world, 50);
            Run(simulation, world, Vec2.Zero, 10);
            for (int i = 0; i < world.EnemyCount; i++) Assert.IsTrue(seen.Add(world.Enemies[i].Id), $"duplicate id {world.Enemies[i].Id}");
        }

        // ---- 추적 ----

        [Test]
        public void Chasing_ReducesTheDistanceEveryTick_AndReachesThePlayer()
        {
            var simulation = NewSimulation(Rules(perSecond: 0f));
            var world = simulation.CreateWorld(4);
            simulation.SpawnEnemies(world, 1);
            float previous = DistanceToPlayer(world, 0);
            float stepLength = EnemySpeed / TicksPerSecond;

            int ticks = (int)(SpawnRadius / stepLength) + 10;
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step(world, default);
                float now = DistanceToPlayer(world, 0);
                if (previous > 0f)
                {
                    Assert.Less(now, previous, $"tick {t}");
                    Assert.AreEqual(System.Math.Max(0f, previous - stepLength), now, Tolerance, $"tick {t}");
                }
                previous = now;
            }
            Assert.AreEqual(0f, previous, Tolerance);
        }

        [Test]
        public void Chasing_FollowsAMovingPlayer()
        {
            var simulation = NewSimulation(Rules(perSecond: 0f, enemySpeed: PlayerSpeed * 2f));
            var world = simulation.CreateWorld(4);
            simulation.SpawnEnemies(world, 1);
            float start = DistanceToPlayer(world, 0);
            Run(simulation, world, new Vec2(1f, 0f), TicksPerSecond);
            Assert.Less(DistanceToPlayer(world, 0), start);
        }

        [Test]
        public void Separation_PushesOverlappingEnemiesApart()
        {
            var simulation = NewSimulation(Rules(perSecond: 0f, push: 1f, enemySpeed: 0f));
            var world = simulation.CreateWorld(0);
            var spot = new Vec2(5f, 5f);
            world.Enemies[0] = new EnemyState { Id = world.AllocateEntityId(), Position = spot, PreviousPosition = spot };
            world.Enemies[1] = new EnemyState { Id = world.AllocateEntityId(), Position = spot, PreviousPosition = spot };
            world.EnemyCount = 2;

            simulation.Step(world, default);
            float gap = (world.Enemies[0].Position - world.Enemies[1].Position).Magnitude;
            Assert.AreEqual(EnemyRadius * 2f, gap, Tolerance);
        }

        // ---- 할당 ----

        [Test]
        public void Step_DoesNotAllocate_WithAFullPool()
        {
            var simulation = NewSimulation(Rules(perSecond: 30f, maxAlive: 300, push: 0.5f));
            var world = simulation.CreateWorld(21);
            simulation.SpawnEnemies(world, 300);
            var input = new InputFrame { Move = new Vec2(1f, 0.5f) };
            simulation.Step(world, input); // 처음 한 번(JIT 등)은 재지 않는다

            Assert.That(() =>
            {
                for (int i = 0; i < 120; i++) simulation.Step(world, input);
            }, NUnit.Framework.Is.Not.AllocatingGCMemory());
        }
    }
}
