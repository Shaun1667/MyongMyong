using System.Collections.Generic;
using Myong.Core;
using Myong.Unity;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;

namespace Myong.Tests
{
    /// <summary>경험치 · 레벨업(1-4): 젬 드롭 · 끌기 · 줍기 · 레벨 곡선 · 대기열 · 선택지(시드 · 최대 단계) · 강화 적용 · 멈춤 · 할당. 여기의 숫자는 테스트용 값이다(게임 수치는 에셋).</summary>
    public sealed class ExperienceTests
    {
        const int TicksPerSecond = 60;
        const float PlayerSpeed = 5f;
        const float PickupRadius = 2f;
        const float PullSpeed = 6f;
        const float CollectDistance = 0.3f;
        const float Tolerance = 0.001f;
        static readonly int[] Curve = { 3, 5 };
        const int Extra = 2;

        static ExperienceRulesData Experience(int maxGems = 64) => new ExperienceRulesData(Curve, Extra, maxGems, PullSpeed, CollectDistance);

        static EnemyRulesData StillEnemies(int experience = 2, float health = 1f) => new EnemyRulesData(
            new[] { new EnemyKindData(0f, 0.3f, health, experience) },
            new[] { new SpawnPhaseData(0f, 0f, 0) },
            20f, 50, 0f);

        static WeaponKindData Gun(float interval = 0.5f, int shots = 1, int pierce = 0) => new WeaponKindData(interval, 10f, 5f, 10f, shots, pierce, 0.1f);

        static WeaponRulesData Weapons(params WeaponKindData[] kinds)
        {
            var starting = new int[kinds.Length];
            for (int i = 0; i < starting.Length; i++) starting[i] = i;
            return new WeaponRulesData(kinds, starting, 64, Vec2.Zero);
        }

        // 강화 7종(무기 0 대상 5줄 + 플레이어 2줄), 선택지 3개.
        static UpgradeRulesData SevenUpgrades(int maxStage = 5) => new UpgradeRulesData(new[]
        {
            new UpgradeData(StatTarget.FireInterval, StatOp.Multiply, 0.5f, maxStage, 0),
            new UpgradeData(StatTarget.Damage, StatOp.Add, 5f, maxStage, 0),
            new UpgradeData(StatTarget.ShotsPerVolley, StatOp.Add, 1f, maxStage, 0),
            new UpgradeData(StatTarget.Pierce, StatOp.Add, 1f, maxStage, 0),
            new UpgradeData(StatTarget.BulletSpeed, StatOp.Multiply, 2f, maxStage, 0),
            new UpgradeData(StatTarget.MoveSpeed, StatOp.Multiply, 1.5f, maxStage),
            new UpgradeData(StatTarget.PickupRadius, StatOp.Add, 1f, maxStage),
        }, 3);

        static Simulation NewSimulation(EnemyRulesData enemies = null, WeaponRulesData weapons = null, UpgradeRulesData upgrades = null, ExperienceRulesData experience = null)
        {
            return new Simulation(TicksPerSecond, new PlayerTuningData(PlayerSpeed, PickupRadius),
                enemies ?? StillEnemies(), weapons ?? Weapons(Gun()), experience ?? Experience(), upgrades ?? SevenUpgrades());
        }

        static void PlaceEnemy(World world, Vec2 position, float health = 1f)
        {
            world.Enemies[world.EnemyCount] = new EnemyState
            {
                Id = world.AllocateEntityId(),
                Kind = 0,
                Position = position,
                PreviousPosition = position,
                Health = health,
                LastHitTick = EnemyState.NeverHit,
            };
            world.EnemyCount++;
        }

        static void PlaceGem(World world, Vec2 position, int value)
        {
            world.Gems[world.GemCount] = new GemState { Id = world.AllocateEntityId(), Position = position, PreviousPosition = position, Value = value };
            world.GemCount++;
        }

        // ---- 젬 ----

        [Test]
        public void KilledEnemy_DropsAGem_WhereItDied()
        {
            var simulation = NewSimulation(weapons: Weapons(Gun()));
            var world = simulation.CreateWorld(1);
            PlaceEnemy(world, new Vec2(6f, 0f));
            int enemyId = world.Enemies[0].Id;

            for (int t = 0; t < TicksPerSecond && world.EnemyCount > 0; t++) simulation.Step(world, default);

            Assert.AreEqual(0, world.EnemyCount);
            Assert.AreEqual(1, world.GemCount);
            Assert.AreEqual(1, world.GemsDroppedTotal);
            Assert.AreEqual(2, world.Gems[0].Value, "gem value = EnemyKindData.ExperienceValue");
            Assert.AreEqual(6f, world.Gems[0].Position.X, Tolerance);
            Assert.AreEqual(0f, world.Gems[0].Position.Y, Tolerance);
            Assert.AreNotEqual(enemyId, world.Gems[0].Id, "a gem gets its own entity id");
        }

        [Test]
        public void EnemyWithNoExperience_DropsNothing()
        {
            var simulation = NewSimulation(enemies: StillEnemies(experience: 0), weapons: Weapons(Gun()));
            var world = simulation.CreateWorld(1);
            PlaceEnemy(world, new Vec2(6f, 0f));
            for (int t = 0; t < TicksPerSecond && world.EnemyCount > 0; t++) simulation.Step(world, default);
            Assert.AreEqual(1, world.EnemiesKilledTotal);
            Assert.AreEqual(0, world.GemCount);
        }

        [Test]
        public void FullGemPool_AddsTheExperienceToTheLastGem()
        {
            var simulation = NewSimulation(weapons: Weapons(Gun(interval: 0.05f, shots: 3)), experience: Experience(maxGems: 2));
            var world = simulation.CreateWorld(1);
            for (int i = 0; i < 4; i++) PlaceEnemy(world, new Vec2(5f + i, 0f));
            for (int t = 0; t < TicksPerSecond * 2 && world.EnemyCount > 0; t++) simulation.Step(world, default);

            Assert.AreEqual(0, world.EnemyCount);
            Assert.AreEqual(2, world.GemCount);
            Assert.AreEqual(4, world.GemsDroppedTotal);
            Assert.AreEqual(8, world.Gems[0].Value + world.Gems[1].Value, "no experience is lost when the pool is full");
        }

        [Test]
        public void GemInsidePickupRadius_IsPulled_GemOutsideStays()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            PlaceGem(world, new Vec2(1.5f, 0f), 1); // 반경 2 안
            PlaceGem(world, new Vec2(0f, 5f), 1);   // 밖

            simulation.Step(world, default);

            Assert.AreEqual(1.5f - PullSpeed / TicksPerSecond, world.Gems[0].Position.X, Tolerance);
            Assert.IsTrue(world.Gems[0].Attracted);
            Assert.AreEqual(5f, world.Gems[1].Position.Y, Tolerance);
            Assert.IsFalse(world.Gems[1].Attracted);

            // 한 번 끌리면 플레이어가 멀어져도 따라온다.
            simulation.TeleportPlayer(world, new Vec2(-10f, 0f));
            float before = (world.Gems[0].Position - world.Player.Position).Magnitude;
            simulation.Step(world, default);
            float after = (world.Gems[0].Position - world.Player.Position).Magnitude;
            Assert.AreEqual(before - PullSpeed / TicksPerSecond, after, Tolerance);
        }

        [Test]
        public void PickedUpGem_BecomesExperience()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);
            PlaceGem(world, new Vec2(1f, 0f), 2);
            for (int t = 0; t < TicksPerSecond && world.GemCount > 0; t++) simulation.Step(world, default);

            Assert.AreEqual(0, world.GemCount);
            Assert.AreEqual(1, world.GemsCollectedTotal);
            Assert.AreEqual(2, world.Experience);
            Assert.AreEqual(2, world.ExperienceTotal);
            Assert.AreEqual(1, world.Level);
        }

        // ---- 레벨 ----

        [Test]
        public void ExperienceToNext_FollowsTheTable_ThenAddsExtraPerLevel()
        {
            var rules = Experience();
            Assert.AreEqual(3, rules.ToNext(1));
            Assert.AreEqual(5, rules.ToNext(2));
            Assert.AreEqual(5 + Extra, rules.ToNext(3));
            Assert.AreEqual(5 + Extra * 2, rules.ToNext(4));
            Assert.AreEqual(int.MaxValue, ExperienceRulesData.None.ToNext(1), "an empty curve never levels up");
        }

        [Test]
        public void Level_RisesByTheCurve_AndKeepsTheRemainder()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);

            simulation.AddExperience(world, 2);
            Assert.AreEqual(1, world.Level);
            Assert.AreEqual(0, world.PendingLevelUps);

            simulation.AddExperience(world, 2); // 4 = 3 + 1
            Assert.AreEqual(2, world.Level);
            Assert.AreEqual(1, world.Experience);
            Assert.AreEqual(1, world.PendingLevelUps);
            Assert.AreEqual(3, world.LevelUpChoiceCount);

            Assert.IsTrue(simulation.ChooseUpgrade(world, 0));
            Assert.AreEqual(0, world.PendingLevelUps);
            Assert.AreEqual(0, world.LevelUpChoiceCount);
        }

        [Test]
        public void SeveralLevelsAtOnce_AreQueued_AndShownOneAfterAnother()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(1);

            simulation.AddExperience(world, 3 + 5 + 7); // 레벨 1 → 4
            Assert.AreEqual(4, world.Level);
            Assert.AreEqual(0, world.Experience);
            Assert.AreEqual(3, world.PendingLevelUps);

            for (int left = 3; left > 0; left--)
            {
                Assert.AreEqual(left, world.PendingLevelUps);
                Assert.AreEqual(3, world.LevelUpChoiceCount, "each queued level-up shows its own choices");
                Assert.IsTrue(simulation.ChooseUpgrade(world, 0));
            }
            Assert.AreEqual(0, world.PendingLevelUps);
            Assert.IsFalse(simulation.ChooseUpgrade(world, 0), "nothing to choose");
            int taken = 0;
            foreach (int stage in world.UpgradeStages) taken += stage;
            Assert.AreEqual(3, taken);
        }

        // ---- 선택지 ----

        static List<int> ChoiceSequence(Simulation simulation, int seed, int levels, int pick)
        {
            var world = simulation.CreateWorld(seed);
            var seen = new List<int>();
            for (int level = 0; level < levels; level++)
            {
                simulation.AddExperience(world, simulation.ExperienceToNext(world.Level) - world.Experience);
                for (int i = 0; i < world.LevelUpChoiceCount; i++) seen.Add(world.LevelUpChoices[i]);
                simulation.ChooseUpgrade(world, pick % world.LevelUpChoiceCount);
            }
            return seen;
        }

        [Test]
        public void SameSeedAndSameChoices_GiveTheSameOptions()
        {
            var simulation = NewSimulation();
            var a = ChoiceSequence(simulation, 42, 8, 1);
            var b = ChoiceSequence(simulation, 42, 8, 1);
            CollectionAssert.AreEqual(a, b);
            Assert.AreEqual(8 * 3, a.Count);

            var other = ChoiceSequence(simulation, 43, 8, 1);
            CollectionAssert.AreNotEqual(a, other, "another seed gives other options");
        }

        [Test]
        public void Choices_AreDistinct()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(9);
            for (int level = 0; level < 10; level++)
            {
                simulation.AddExperience(world, simulation.ExperienceToNext(world.Level) - world.Experience);
                var set = new HashSet<int>();
                for (int i = 0; i < world.LevelUpChoiceCount; i++) Assert.IsTrue(set.Add(world.LevelUpChoices[i]), "the same upgrade twice in one level-up");
                simulation.ChooseUpgrade(world, 0);
            }
        }

        [Test]
        public void LevelUpStream_DoesNotChangeSpawns()
        {
            var enemies = new EnemyRulesData(new[] { new EnemyKindData(1f, 0.3f, 10f, 1) }, new[] { new SpawnPhaseData(0f, 3f, 0) }, 12f, 100, 0.5f);
            var simulation = NewSimulation(enemies: enemies);
            var a = simulation.CreateWorld(5);
            var b = simulation.CreateWorld(5);
            simulation.AddExperience(b, 3); // b만 선택지를 뽑는다(레벨업 스트림 사용)
            simulation.ChooseUpgrade(b, 0);
            for (int t = 0; t < TicksPerSecond * 2; t++)
            {
                simulation.Step(a, default);
                simulation.Step(b, default);
            }
            Assert.Greater(a.EnemiesSpawnedTotal, 0);
            Assert.AreEqual(a.EnemiesSpawnedTotal, b.EnemiesSpawnedTotal);
            Assert.AreEqual(a.SpawnRandom.State, b.SpawnRandom.State, "drawing level-up choices must not move the spawn stream");
        }

        [Test]
        public void MaxedUpgrades_AreLeftOut_AndFewerChoicesShowWhenFewRemain()
        {
            var upgrades = new UpgradeRulesData(new[]
            {
                new UpgradeData(StatTarget.Damage, StatOp.Add, 1f, 1, 0),
                new UpgradeData(StatTarget.Pierce, StatOp.Add, 1f, 1, 0),
                new UpgradeData(StatTarget.MoveSpeed, StatOp.Multiply, 1.1f, 1),
                new UpgradeData(StatTarget.PickupRadius, StatOp.Add, 1f, 2),
            }, 3);
            var simulation = NewSimulation(weapons: Weapons(Gun()), upgrades: upgrades);
            var world = simulation.CreateWorld(3);

            // 남은 단계 합 5 → 다섯 번 고르면 모두 최대. 네 번째는 남은 단계 2(선택지 2개 이하), 다섯 번째는 1.
            for (int k = 0; k < 5; k++)
            {
                simulation.AddExperience(world, simulation.ExperienceToNext(world.Level) - world.Experience);
                int offerable = 0;
                for (int r = 0; r < upgrades.Rows.Length; r++)
                {
                    if (world.UpgradeStages[r] < upgrades.Rows[r].MaxStage) offerable++;
                }
                Assert.AreEqual(System.Math.Min(3, offerable), world.LevelUpChoiceCount);
                if (k == 0) Assert.AreEqual(3, world.LevelUpChoiceCount);
                if (k == 3) Assert.LessOrEqual(world.LevelUpChoiceCount, 2);
                if (k == 4) Assert.AreEqual(1, world.LevelUpChoiceCount);
                for (int i = 0; i < world.LevelUpChoiceCount; i++)
                {
                    int row = world.LevelUpChoices[i];
                    Assert.Less(world.UpgradeStages[row], upgrades.Rows[row].MaxStage, "a maxed upgrade was offered");
                }
                simulation.ChooseUpgrade(world, 0);
            }

            // 모두 최대: 레벨은 오르지만 고를 것이 없어 멈추지 않는다.
            int level = world.Level;
            simulation.AddExperience(world, simulation.ExperienceToNext(world.Level) - world.Experience);
            Assert.AreEqual(level + 1, world.Level);
            Assert.AreEqual(0, world.LevelUpChoiceCount);
            Assert.AreEqual(0, world.PendingLevelUps);
        }

        [Test]
        public void WeaponOnlyUpgrade_IsNotOffered_WithoutThatWeapon()
        {
            var upgrades = new UpgradeRulesData(new[]
            {
                new UpgradeData(StatTarget.Damage, StatOp.Add, 1f, 5, 1), // 무기 1(가지지 않음)
                new UpgradeData(StatTarget.MoveSpeed, StatOp.Multiply, 1.1f, 5),
            }, 3);
            var weapons = new WeaponRulesData(new[] { Gun(), Gun() }, new[] { 0 }, 16, Vec2.Zero);
            var simulation = NewSimulation(weapons: weapons, upgrades: upgrades);
            var world = simulation.CreateWorld(3);
            simulation.AddExperience(world, 3);
            Assert.AreEqual(1, world.LevelUpChoiceCount);
            Assert.AreEqual(1, world.LevelUpChoices[0]);
        }

        // ---- 강화 적용 ----

        [Test]
        public void Upgrades_ChangeWeaponAndPlayerValues_ByData()
        {
            var simulation = NewSimulation(weapons: Weapons(Gun(interval: 0.5f)));
            var world = simulation.CreateWorld(1);
            Assert.AreEqual(30, simulation.FireIntervalTicks(world, 0));
            Assert.AreEqual(5f, simulation.WeaponValue(world, 0, StatTarget.Damage), Tolerance);

            for (int row = 0; row < 7; row++) Assert.IsTrue(simulation.ApplyUpgrade(world, row));

            Assert.AreEqual(15, simulation.FireIntervalTicks(world, 0), "0.5s x 0.5");
            Assert.AreEqual(10f, simulation.WeaponValue(world, 0, StatTarget.Damage), Tolerance);
            Assert.AreEqual(2f, simulation.WeaponValue(world, 0, StatTarget.ShotsPerVolley), Tolerance);
            Assert.AreEqual(1f, simulation.WeaponValue(world, 0, StatTarget.Pierce), Tolerance);
            Assert.AreEqual(20f, simulation.WeaponValue(world, 0, StatTarget.BulletSpeed), Tolerance);
            Assert.AreEqual(10f, simulation.WeaponValue(world, 0, StatTarget.Range), Tolerance, "no upgrade on range");
            Assert.AreEqual(PlayerSpeed * 1.5f, simulation.MoveSpeed(world), Tolerance);
            Assert.AreEqual(PickupRadius + 1f, simulation.PickupRadius(world), Tolerance);

            // 단계는 겹쳐 걸린다: 곱하기는 곱해지고 더하기는 더해진다.
            simulation.ApplyUpgrade(world, 1);
            simulation.ApplyUpgrade(world, 5);
            Assert.AreEqual(15f, simulation.WeaponValue(world, 0, StatTarget.Damage), Tolerance);
            Assert.AreEqual(PlayerSpeed * 1.5f * 1.5f, simulation.MoveSpeed(world), Tolerance);

            simulation.ResetUpgrades(world);
            Assert.AreEqual(30, simulation.FireIntervalTicks(world, 0));
            Assert.AreEqual(PlayerSpeed, simulation.MoveSpeed(world), Tolerance);
            foreach (int stage in world.UpgradeStages) Assert.AreEqual(0, stage);
        }

        [Test]
        public void Upgrades_AreUsedByTheSimulation()
        {
            var simulation = NewSimulation(enemies: StillEnemies(health: 1000f), weapons: Weapons(Gun()));
            var world = simulation.CreateWorld(1);
            PlaceEnemy(world, new Vec2(4f, 0f), 1000f);
            PlaceEnemy(world, new Vec2(-4f, 0f), 1000f);

            simulation.ApplyUpgrade(world, 2); // 한 번에 +1
            simulation.ApplyUpgrade(world, 1); // 피해 +5
            simulation.Step(world, default);
            Assert.AreEqual(2, world.BulletsFiredTotal, "one more bullet per volley");
            for (int t = 0; t < 30; t++) simulation.Step(world, default);
            Assert.AreEqual(990f, world.Enemies[0].Health, Tolerance, "damage 5 + 5");

            simulation.ApplyUpgrade(world, 5); // 이동 속도 x1.5
            var input = new InputFrame { Move = new Vec2(1f, 0f) };
            Vec2 before = world.Player.Position;
            simulation.Step(world, input);
            Assert.AreEqual(PlayerSpeed * 1.5f / TicksPerSecond, world.Player.Position.X - before.X, Tolerance);
        }

        [Test]
        public void GlobalWeaponUpgrade_AppliesToEveryWeapon()
        {
            var upgrades = new UpgradeRulesData(new[] { new UpgradeData(StatTarget.Damage, StatOp.Multiply, 2f, 3) }, 3);
            var weapons = new WeaponRulesData(new[] { Gun(), new WeaponKindData(1f, 10f, 3f, 10f, 1, 0, 0.1f) }, new[] { 0, 1 }, 16, Vec2.Zero);
            var simulation = NewSimulation(weapons: weapons, upgrades: upgrades);
            var world = simulation.CreateWorld(1);
            simulation.ApplyUpgrade(world, 0);
            Assert.AreEqual(10f, simulation.WeaponValue(world, 0, StatTarget.Damage), Tolerance);
            Assert.AreEqual(6f, simulation.WeaponValue(world, 1, StatTarget.Damage), Tolerance);
        }

        [Test]
        public void MaxStage_CannotBeExceeded()
        {
            var simulation = NewSimulation(weapons: Weapons(Gun()), upgrades: SevenUpgrades(maxStage: 2));
            var world = simulation.CreateWorld(1);
            Assert.IsTrue(simulation.ApplyUpgrade(world, 3));
            Assert.IsTrue(simulation.ApplyUpgrade(world, 3));
            Assert.IsFalse(simulation.ApplyUpgrade(world, 3));
            Assert.AreEqual(2, world.UpgradeStages[3]);
            Assert.AreEqual(2f, simulation.WeaponValue(world, 0, StatTarget.Pierce), Tolerance);
        }

        // ---- 멈춤 ----

        [Test]
        public void Simulation_IsFrozen_WhileALevelUpWaits()
        {
            var enemies = new EnemyRulesData(new[] { new EnemyKindData(2f, 0.3f, 10f, 1) }, new[] { new SpawnPhaseData(0f, 4f, 0) }, 12f, 100, 0.5f);
            var simulation = NewSimulation(enemies: enemies, weapons: Weapons(Gun()));
            var world = simulation.CreateWorld(2);
            for (int t = 0; t < TicksPerSecond; t++) simulation.Step(world, default);
            Assert.Greater(world.EnemyCount, 0);

            while (world.IsChoosingUpgrade) simulation.ChooseUpgrade(world, 0);
            simulation.AddExperience(world, simulation.ExperienceToNext(world.Level) - world.Experience);
            Assert.IsTrue(world.IsChoosingUpgrade);
            long tick = world.Tick;
            Vec2 enemy = world.Enemies[0].Position;
            long spawned = world.EnemiesSpawnedTotal;
            var input = new InputFrame { Move = new Vec2(1f, 0f) };
            for (int t = 0; t < 30; t++) simulation.Step(world, input);

            Assert.AreEqual(tick, world.Tick);
            Assert.AreEqual(enemy, world.Enemies[0].Position);
            Assert.AreEqual(spawned, world.EnemiesSpawnedTotal);
            Assert.AreEqual(Vec2.Zero, world.Player.Position);

            simulation.ChooseUpgrade(world, 0);
            simulation.Step(world, input);
            Assert.AreEqual(tick + 1, world.Tick);
        }

        // ---- 할당 ----

        [Test]
        public void Step_DoesNotAllocate_WithGemsAndLevelUps()
        {
            var enemies = new EnemyRulesData(
                new[] { new EnemyKindData(2f, 0.3f, 5f, 1) },
                new[] { new SpawnPhaseData(0f, 30f, 0) },
                12f, 300, 0.5f);
            var experience = new ExperienceRulesData(new[] { 2, 3 }, 1, 256, 8f, 0.4f);
            var simulation = new Simulation(TicksPerSecond, new PlayerTuningData(PlayerSpeed, 4f), enemies, Weapons(Gun(interval: 0.05f, shots: 3, pierce: 2)), experience, SevenUpgrades(maxStage: 3));
            var world = simulation.CreateWorld(21);
            simulation.SpawnEnemies(world, 250);
            var input = new InputFrame { Move = new Vec2(1f, 0.5f) };
            for (int i = 0; i < 60; i++)
            {
                simulation.Step(world, input);
                while (world.IsChoosingUpgrade) simulation.ChooseUpgrade(world, 0);
            }
            Assert.Greater(world.GemsDroppedTotal, 0);

            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                {
                    simulation.Step(world, input);
                    if (i % 20 == 0) simulation.AddExperience(world, 1);
                    while (world.IsChoosingUpgrade) simulation.ChooseUpgrade(world, 0);
                }
            }, NUnit.Framework.Is.Not.AllocatingGCMemory());
            Assert.Greater(world.GemsCollectedTotal, 0);
            Assert.Greater(world.Level, 2);
        }

        // ---- 표시 ----

        [Test]
        public void CardValue_IsFormattedFromTheData()
        {
            Assert.AreEqual("-12%", LevelUpView.FormatValue(StatOp.Multiply, 0.88f));
            Assert.AreEqual("+30%", LevelUpView.FormatValue(StatOp.Multiply, 1.3f));
            Assert.AreEqual("+2.5", LevelUpView.FormatValue(StatOp.Add, 2.5f));
            Assert.AreEqual("+1", LevelUpView.FormatValue(StatOp.Add, 1f));
        }
    }
}
