using System.Collections.Generic;
using Myong.Core;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;

namespace Myong.Tests
{
    /// <summary>적 종류(1-8): 시간대 종류 비중 · 웨이브(정예 Scatter · 떼 돌진 Swarm) · 종류별 누계 · 큰 젬 · 결정성 · 할당. 여기의 숫자는 테스트용 값이다(게임 수치는 EnemyTable · SpawnTable 에셋).</summary>
    public sealed class EnemyKindTests
    {
        const int TicksPerSecond = 60;
        const float PlayerSpeed = 5f;
        const float SpawnRadius = 20f;
        const float DespawnDistance = 26f;
        const float Tolerance = 0.001f;

        // 0 = 보통 · 1 = 빠름 · 2 = 정예(경험치 20)
        static readonly EnemyKindData[] Kinds =
        {
            new EnemyKindData(2f, 0.3f, 10f, 1, 10f),
            new EnemyKindData(3f, 0.2f, 5f, 1, 5f),
            new EnemyKindData(1f, 0.7f, 200f, 20, 20f),
        };

        static EnemyRulesData Rules(SpawnPhaseData[] phases, SpawnWaveData[] waves = null, int maxAlive = 300, float despawn = DespawnDistance)
        {
            return new EnemyRulesData(Kinds, phases, SpawnRadius, maxAlive, 0f, waves ?? new SpawnWaveData[0], despawn);
        }

        static SpawnPhaseData[] NoPhaseSpawns => new[] { new SpawnPhaseData(0f, 0f, 0) };

        static Simulation NewSimulation(EnemyRulesData rules) => new Simulation(TicksPerSecond, new PlayerTuningData(PlayerSpeed), rules);

        static void Run(Simulation simulation, World world, int ticks, Vec2 move = default)
        {
            var input = new InputFrame { Move = move };
            for (int i = 0; i < ticks; i++) simulation.Step(world, input);
        }

        static int CountKind(World world, int kind)
        {
            int n = 0;
            for (int i = 0; i < world.EnemyCount; i++) if (world.Enemies[i].Kind == kind) n++;
            return n;
        }

        // ---- 시간대 비중 ----

        [Test]
        public void OneKindPhase_IsTheSameAsBeforeMixing()
        {
            // 한 종류 시간대(int 생성자)와 Mix 하나짜리는 같은 결과 — 종류를 고르느라 난수를 더 뽑지 않는다.
            var a = NewSimulation(Rules(new[] { new SpawnPhaseData(0f, 6f, 0) }));
            var b = NewSimulation(Rules(new[] { new SpawnPhaseData(0f, 6f, new[] { new SpawnWeight(0, 2.5f) }) }));
            var wa = a.CreateWorld(77);
            var wb = b.CreateWorld(77);
            Run(a, wa, 300);
            Run(b, wb, 300);
            Assert.AreEqual(wa.EnemyCount, wb.EnemyCount);
            Assert.AreEqual(wa.SpawnRandom.State, wb.SpawnRandom.State);
            for (int i = 0; i < wa.EnemyCount; i++) Assert.AreEqual(wa.Enemies[i].Position.X, wb.Enemies[i].Position.X, Tolerance);

            // 스폰 한 번에 난수 하나(각도)만.
            var rng = Rng.ForStream(77, RandomStream.Spawn);
            for (long i = 0; i < wa.EnemiesSpawnedTotal; i++) rng.NextFloat();
            Assert.AreEqual(rng.State, wa.SpawnRandom.State);
        }

        [Test]
        public void MixedPhase_SpawnsKindsByWeight()
        {
            var simulation = NewSimulation(Rules(new[] { new SpawnPhaseData(0f, 60f, new[] { new SpawnWeight(0, 3f), new SpawnWeight(1, 1f) }) }, maxAlive: 5000));
            var world = simulation.CreateWorld(5);
            simulation.SpawnEnemies(world, 4000);
            float fast = CountKind(world, 1) / (float)world.EnemyCount;
            Assert.AreEqual(4000, world.EnemyCount);
            Assert.AreEqual(0.25f, fast, 0.03f, "weight 3:1 = 25% fast");
            Assert.AreEqual(world.EnemyCount, world.EnemiesSpawnedByKind[0] + world.EnemiesSpawnedByKind[1]);
            Assert.AreEqual(0, world.EnemiesSpawnedByKind[2]);
        }

        [Test]
        public void KindAt_FollowsCumulativeWeights()
        {
            var phase = new SpawnPhaseData(0f, 1f, new[] { new SpawnWeight(0, 1f), new SpawnWeight(1, 2f), new SpawnWeight(2, 1f) });
            Assert.AreEqual(4f, phase.TotalWeight, Tolerance);
            Assert.AreEqual(0, phase.KindAt(0f));
            Assert.AreEqual(0, phase.KindAt(0.24f));
            Assert.AreEqual(1, phase.KindAt(0.26f));
            Assert.AreEqual(1, phase.KindAt(0.74f));
            Assert.AreEqual(2, phase.KindAt(0.76f));
            Assert.AreEqual(2, phase.KindAt(0.9999f));
        }

        [Test]
        public void Phases_ChangeTheMixOverTime()
        {
            var phases = new[]
            {
                new SpawnPhaseData(0f, 10f, 0),
                new SpawnPhaseData(2f, 10f, new[] { new SpawnWeight(0, 1f), new SpawnWeight(1, 1f) }),
            };
            var simulation = NewSimulation(Rules(phases));
            var world = simulation.CreateWorld(3);
            Run(simulation, world, 2 * TicksPerSecond);
            Assert.Greater(world.EnemiesSpawnedByKind[0], 0);
            Assert.AreEqual(0, world.EnemiesSpawnedByKind[1], "first phase is the base kind only");
            Run(simulation, world, 4 * TicksPerSecond);
            Assert.Greater(world.EnemiesSpawnedByKind[1], 0, "the second phase mixes in the fast kind");
        }

        [Test]
        public void SameSeed_SameKinds_DifferentSeed_Differs()
        {
            var rules = Rules(new[] { new SpawnPhaseData(0f, 20f, new[] { new SpawnWeight(0, 1f), new SpawnWeight(1, 1f), new SpawnWeight(2, 1f) }) });
            var kindsA = new List<int>();
            var kindsB = new List<int>();
            var kindsC = new List<int>();
            foreach (var (seed, list) in new[] { (11, kindsA), (11, kindsB), (12, kindsC) })
            {
                var simulation = NewSimulation(rules);
                var world = simulation.CreateWorld(seed);
                Run(simulation, world, 120);
                for (int i = 0; i < world.EnemyCount; i++) list.Add(world.Enemies[i].Kind);
            }
            CollectionAssert.AreEqual(kindsA, kindsB);
            CollectionAssert.AreNotEqual(kindsA, kindsC);
        }

        // ---- 웨이브 ----

        [Test]
        public void ScatterWave_ComesAtItsTicks_AndChases()
        {
            var waves = new[] { new SpawnWaveData(2, 1f, 2f, 2) }; // 1초부터 2초마다 정예 2마리
            var simulation = NewSimulation(Rules(NoPhaseSpawns, waves));
            var world = simulation.CreateWorld(9);

            Run(simulation, world, TicksPerSecond); // 틱 0 ~ 59
            Assert.AreEqual(0, world.EnemyCount);
            Run(simulation, world, 1); // 틱 60
            Assert.AreEqual(2, CountKind(world, 2));
            for (int i = 0; i < world.EnemyCount; i++)
            {
                Assert.AreEqual(SpawnRadius, (world.Enemies[i].Position - world.Player.Position).Magnitude, 0.01f);
                Assert.AreEqual(0f, world.Enemies[i].Charge.SqrMagnitude, "scatter enemies chase");
            }
            Run(simulation, world, 2 * TicksPerSecond - 1);
            Assert.AreEqual(2, world.EnemiesSpawnedByKind[2]);
            Run(simulation, world, 1); // 틱 180
            Assert.AreEqual(4, world.EnemiesSpawnedByKind[2]);
            Assert.IsTrue(simulation.IsWaveTick(0, 60));
            Assert.IsTrue(simulation.IsWaveTick(0, 180));
            Assert.IsFalse(simulation.IsWaveTick(0, 120));
            Assert.AreEqual(300, simulation.NextWaveTick(0, 181));
        }

        [Test]
        public void OnceWave_ComesOnlyOnce()
        {
            var simulation = NewSimulation(Rules(NoPhaseSpawns, new[] { new SpawnWaveData(2, 0.5f, 0f, 1) }));
            var world = simulation.CreateWorld(1);
            Run(simulation, world, 10 * TicksPerSecond);
            Assert.AreEqual(1, world.EnemiesSpawnedByKind[2]);
            Assert.AreEqual(-1, simulation.NextWaveTick(0, 31));
        }

        [Test]
        public void SwarmWave_PacksAtOneAngle_AndChargesStraight()
        {
            var waves = new[] { new SpawnWaveData(1, 0f, 0f, 9, SpawnPattern.Swarm, 0.6f, 2f) };
            var simulation = NewSimulation(Rules(NoPhaseSpawns, waves));
            var world = simulation.CreateWorld(4);
            Run(simulation, world, 1);
            Assert.AreEqual(9, world.EnemyCount);

            Vec2 charge = world.Enemies[0].Charge;
            Assert.AreEqual(Kinds[1].MoveSpeed * 2f, charge.Magnitude, Tolerance, "charge speed = kind speed x speed scale");
            Vec2 center = Vec2.Zero;
            for (int i = 0; i < 9; i++)
            {
                Assert.AreEqual(charge.X, world.Enemies[i].Charge.X, Tolerance, "the whole pack shares one direction");
                Assert.AreEqual(charge.Y, world.Enemies[i].Charge.Y, Tolerance);
                center = center + world.Enemies[i].Position * (1f / 9f);
            }
            // 떼 가운데에서 플레이어 쪽을 향한다(9마리 = 3 x 3, 가장 먼 줄은 반경 + 2칸).
            Vec2 toPlayer = (world.Player.Position - center).Normalized;
            Vec2 dir = charge.Normalized;
            Assert.AreEqual(1f, toPlayer.X * dir.X + toPlayer.Y * dir.Y, 0.001f);
            for (int i = 0; i < 9; i++)
            {
                float d = (world.Enemies[i].Position - world.Player.Position).Magnitude;
                Assert.GreaterOrEqual(d, SpawnRadius - 0.6f - Tolerance);
                Assert.LessOrEqual(d, SpawnRadius + waves[0].SwarmDepth + 0.6f);
            }

            // 플레이어가 옆으로 피해도 방향을 바꾸지 않는다.
            Vec2 before = world.Enemies[0].Position;
            Run(simulation, world, 30, new Vec2(dir.Y, -dir.X));
            Assert.AreEqual(charge.X, world.Enemies[0].Charge.X, Tolerance);
            Vec2 moved = world.Enemies[0].Position - before;
            Assert.AreEqual(charge.Magnitude * 0.5f, moved.Magnitude, 0.01f, "straight line at charge speed");
        }

        [Test]
        public void ChargingEnemies_Despawn_WhenFar_WithoutCountingAsKills()
        {
            var waves = new[] { new SpawnWaveData(1, 0f, 0f, 4, SpawnPattern.Swarm, 0.5f, 3f) };
            var experience = new ExperienceRulesData(new[] { 5 }, 1, 64, 5f, 0.3f);
            var simulation = new Simulation(TicksPerSecond, new PlayerTuningData(PlayerSpeed), Rules(NoPhaseSpawns, waves), WeaponRulesData.None, experience, UpgradeRulesData.None);
            var world = simulation.CreateWorld(8);
            world.PlayerInvincible = true;
            Run(simulation, world, 1);
            Assert.AreEqual(4, world.EnemyCount);
            // 9유닛/초: 반경 20에서 지나가 26 밖까지 약 5.1초.
            Run(simulation, world, 7 * TicksPerSecond);
            Assert.AreEqual(0, world.EnemyCount);
            Assert.AreEqual(4, world.EnemiesEscapedTotal);
            Assert.AreEqual(0, world.EnemiesKilledTotal);
            Assert.AreEqual(0, world.GemsDroppedTotal, "escaped enemies drop nothing");
        }

        [Test]
        public void ChasingEnemies_NeverDespawn()
        {
            var simulation = NewSimulation(Rules(NoPhaseSpawns));
            var world = simulation.CreateWorld(1);
            simulation.SpawnEnemies(world, 3, 0);
            Run(simulation, world, 10 * TicksPerSecond, new Vec2(1f, 0f)); // 플레이어가 달아나 멀어져도
            Assert.AreEqual(3, world.EnemyCount);
            Assert.AreEqual(0, world.EnemiesEscapedTotal);
        }

        // ---- 종류별 누계 · 큰 젬 ----

        [Test]
        public void Kills_AreCountedPerKind_AndTheEliteDropsABigGem()
        {
            var weapons = new WeaponRulesData(new[] { new WeaponKindData(0.05f, 20f, 50f, 15f, 1, 0, 0.1f) }, new[] { 0 }, 64, Vec2.Zero);
            var experience = new ExperienceRulesData(new[] { 1000 }, 0, 64, 5f, 0.3f);
            var simulation = new Simulation(TicksPerSecond, new PlayerTuningData(PlayerSpeed), Rules(NoPhaseSpawns), weapons, experience, UpgradeRulesData.None);
            var world = simulation.CreateWorld(2);
            world.PlayerInvincible = true;
            simulation.SpawnEnemies(world, 1, 2);
            simulation.SpawnEnemies(world, 2, 1);
            for (int t = 0; t < 20 * TicksPerSecond && world.EnemyCount > 0; t++) simulation.Step(world, default);

            Assert.AreEqual(0, world.EnemyCount);
            Assert.AreEqual(0, world.EnemiesKilledByKind[0]);
            Assert.AreEqual(2, world.EnemiesKilledByKind[1]);
            Assert.AreEqual(1, world.EnemiesKilledByKind[2]);
            Assert.AreEqual(3, world.EnemiesKilledTotal);
            int biggest = 0;
            for (int g = 0; g < world.GemCount; g++) biggest = System.Math.Max(biggest, world.Gems[g].Value);
            Assert.AreEqual(Kinds[2].ExperienceValue, biggest, "the elite's gem carries its experience");
        }

        [Test]
        public void DebugSpawn_OfAKind_SpawnsOnlyThatKind()
        {
            var simulation = NewSimulation(Rules(new[] { new SpawnPhaseData(0f, 1f, 0) }));
            var world = simulation.CreateWorld(6);
            Assert.AreEqual(5, simulation.SpawnEnemies(world, 5, 2));
            Assert.AreEqual(5, CountKind(world, 2));
            Assert.AreEqual(5, world.EnemiesSpawnedByKind[2]);
            Assert.AreEqual(0, simulation.SpawnEnemies(world, 5, 7), "unknown kind");
            Assert.AreEqual(0, simulation.SpawnEnemies(world, 5, -1));
            Assert.IsTrue(simulation.SpawnWaveNow(world, -1) == false);
        }

        // ---- 검사 ----

        [Test]
        public void Rules_RejectMissingKinds_AndBadWeights()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Rules(new[] { new SpawnPhaseData(0f, 1f, new[] { new SpawnWeight(0, 1f), new SpawnWeight(5, 1f) }) }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SpawnPhaseData(0f, 1f, new[] { new SpawnWeight(0, 0f) }));
            Assert.Throws<System.ArgumentException>(() => new SpawnPhaseData(0f, 1f, new SpawnWeight[0]));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Rules(NoPhaseSpawns, new[] { new SpawnWaveData(9, 0f, 1f, 1) }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Rules(NoPhaseSpawns, new[] { new SpawnWaveData(0, 0f, -1f, 1) }));
        }

        [Test]
        public void MaxRadius_GrowsWithTheBiggestKind()
        {
            Assert.AreEqual(0.7f, Rules(NoPhaseSpawns).MaxRadius, Tolerance);
        }

        // ---- 수백 마리 ----

        [Test]
        public void Step_DoesNotAllocate_WithMixedKindsAndWaves()
        {
            var phases = new[] { new SpawnPhaseData(0f, 30f, new[] { new SpawnWeight(0, 2f), new SpawnWeight(1, 1f), new SpawnWeight(2, 1f) }) };
            var waves = new[]
            {
                new SpawnWaveData(2, 0f, 0.5f, 2),
                new SpawnWaveData(1, 0f, 0.25f, 9, SpawnPattern.Swarm, 0.6f, 2f),
            };
            var simulation = NewSimulation(Rules(phases, waves, maxAlive: 300));
            var world = simulation.CreateWorld(21);
            world.PlayerInvincible = true;
            simulation.SpawnEnemies(world, 250);
            var input = new InputFrame { Move = new Vec2(1f, 0.5f) };
            simulation.Step(world, input); // 처음 한 번(JIT 등)은 재지 않는다

            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++) simulation.Step(world, input);
            }, NUnit.Framework.Is.Not.AllocatingGCMemory());
            Assert.LessOrEqual(world.EnemyCount, 300);
            Assert.Greater(world.EnemiesSpawnedByKind[2], 0);
        }
    }
}
