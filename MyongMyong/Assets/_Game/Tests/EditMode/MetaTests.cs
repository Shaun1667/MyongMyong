using System.IO;
using Myong.Core;
using Myong.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;

namespace Myong.Tests
{
    /// <summary>
    /// 코인 · 영구 강화(1-12): 코인 드롭 · 줍기 · 판 끝 지갑 · 사기 · 되돌리기 · 판 시작 적용 순서 · 0단계면 그대로 · 결정성 · 저장 형식 · 파일(임시 경로만) · 표 에셋.
    /// 여기의 숫자는 테스트용 값이다(게임 수치는 에셋). 사용자의 실제 저장 파일(persistentDataPath/meta.json)은 건드리지 않는다.
    /// </summary>
    public sealed class MetaTests
    {
        const int TicksPerSecond = 60;
        const float PlayerSpeed = 5f;
        const float PickupRadius = 2f;
        const float MaxHealth = 100f;
        const float Tolerance = 0.0001f;

        static readonly string[] Ids = { "max_health", "damage", "move_speed", "regen" };

        static PermanentRulesData Rules() => new PermanentRulesData(new[]
        {
            new PermanentUpgradeData(new StatEffect(StatTarget.MaxHealth, StatOp.Add, 10f), new[] { 10, 20, 40 }),
            new PermanentUpgradeData(new StatEffect(StatTarget.Damage, StatOp.Multiply, 1.1f), new[] { 15, 30 }),
            new PermanentUpgradeData(new StatEffect(StatTarget.MoveSpeed, StatOp.Multiply, 1.2f), new[] { 5, 5 }),
            new PermanentUpgradeData(new StatEffect(StatTarget.HealthRegen, StatOp.Add, 0.5f), new[] { 1 }),
        });

        static WeaponRulesData Gun() => new WeaponRulesData(new[] { new WeaponKindData(0.2f, 12f, 5f, 10f, 1, 0, 0.1f) }, new[] { 0 }, 128, Vec2.Zero);

        static EnemyRulesData Enemies(float coinChance, int coinValue, float perSecond = 3f) => new EnemyRulesData(
            new[] { new EnemyKindData(1.5f, 0.3f, 5f, 1, 10f, false, false, coinChance, coinValue) },
            new[] { new SpawnPhaseData(0f, perSecond, 0) },
            12f, 200, 0.5f);

        static ExperienceRulesData Experience(int maxCoins = 64) => new ExperienceRulesData(new[] { 3, 5, 8 }, 3, 128, 8f, 0.4f, maxCoins);

        static Simulation NewSimulation(EnemyRulesData enemies = null, ExperienceRulesData experience = null, PassiveRulesData passives = null)
        {
            return new Simulation(TicksPerSecond, new PlayerTuningData(PlayerSpeed, PickupRadius, MaxHealth, 0.35f, 0.5f),
                enemies ?? Enemies(0.5f, 1), Gun(), experience ?? Experience(), new UpgradeRulesData(new UpgradeData[0], 3), RunRulesData.None,
                passives ?? PassiveRulesData.None);
        }

        static MetaProgress Progress(long coins, params int[] stages)
        {
            var p = new MetaProgress(Ids.Length) { Coins = coins };
            for (int i = 0; i < stages.Length; i++) p.Stages[i] = stages[i];
            return p;
        }

        // 서서 돌며 쏘는 판: 플레이어 무적, 레벨업은 첫 카드.
        static void Run(Simulation simulation, World world, int ticks)
        {
            world.PlayerInvincible = true;
            var input = new InputFrame { Move = new Vec2(0.6f, 0.3f) };
            for (int i = 0; i < ticks; i++)
            {
                if (i % 120 == 60) input.Move = new Vec2(-input.Move.Y, input.Move.X);
                simulation.Step(world, input);
                while (world.IsChoosingUpgrade) simulation.ChooseUpgrade(world, 0);
            }
        }

        static void PlaceCoin(World world, Vec2 position, int value)
        {
            world.Coins[world.CoinCount++] = new GemState { Id = world.AllocateEntityId(), Position = position, PreviousPosition = position, Value = value };
        }

        // ---- 사기 · 되돌리기 · 지갑 ----

        [Test]
        public void Buy_TakesThePrice_PricesRise_AndStopsAtMaxStage()
        {
            var rules = Rules();
            var p = Progress(65);
            Assert.AreEqual(10, MetaRules.PriceOfNext(rules, p, 0));
            Assert.IsTrue(MetaRules.Buy(rules, p, 0));
            Assert.AreEqual(55, p.Coins);
            Assert.AreEqual(20, MetaRules.PriceOfNext(rules, p, 0));
            Assert.IsTrue(MetaRules.Buy(rules, p, 0));
            Assert.IsFalse(MetaRules.CanBuy(rules, p, 0), "40 needed, 35 left");
            Assert.IsFalse(MetaRules.Buy(rules, p, 0));
            Assert.AreEqual(35, p.Coins);
            Assert.AreEqual(2, p.Stages[0]);

            Assert.IsTrue(MetaRules.Buy(rules, p, 3));
            Assert.AreEqual(-1, MetaRules.PriceOfNext(rules, p, 3), "max stage: no next price");
            Assert.IsFalse(MetaRules.Buy(rules, p, 3));
            Assert.AreEqual(1, p.Stages[3]);
            Assert.IsFalse(MetaRules.Buy(rules, p, 99), "no such row");
        }

        [Test]
        public void RefundAll_GivesBackEverythingSpent_AndClearsStages()
        {
            var rules = Rules();
            var p = Progress(100);
            MetaRules.Buy(rules, p, 0);
            MetaRules.Buy(rules, p, 0);
            MetaRules.Buy(rules, p, 1);
            MetaRules.Buy(rules, p, 2);
            Assert.AreEqual(100 - 10 - 20 - 15 - 5, p.Coins);
            Assert.AreEqual(50, MetaRules.RefundAll(rules, p));
            Assert.AreEqual(100, p.Coins);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, p.Stages);
            Assert.AreEqual(0, MetaRules.RefundAll(rules, p));
        }

        [Test]
        public void CreditRun_AddsCoinsForWinsAndLosses_ButNotForPlayingOrDebugRuns()
        {
            var p = Progress(5);
            Assert.AreEqual(7, MetaRules.CreditRun(p, new RunResult { Outcome = RunOutcome.Defeat, Coins = 7 }));
            Assert.AreEqual(3, MetaRules.CreditRun(p, new RunResult { Outcome = RunOutcome.Victory, Coins = 3 }));
            Assert.AreEqual(0, MetaRules.CreditRun(p, new RunResult { Outcome = RunOutcome.Playing, Coins = 9 }));
            Assert.AreEqual(0, MetaRules.CreditRun(p, new RunResult { Outcome = RunOutcome.Defeat, Coins = 9, DebugUsed = true }));
            Assert.AreEqual(15, p.Coins);
            MetaRules.AddCoins(p, long.MaxValue);
            Assert.AreEqual(long.MaxValue, p.Coins, "no overflow");
        }

        [Test]
        public void DebugUse_IsRecordedInTheResult_AndTheRunEarnsNoCoins()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(3);
            world.CoinsCollected = 12;
            Simulation.MarkDebugUsed(world);
            simulation.EndRun(world, RunOutcome.Defeat);
            Assert.IsTrue(world.Result.DebugUsed);
            Assert.AreEqual(12, world.Result.Coins);
            var p = Progress(0);
            Assert.AreEqual(0, MetaRules.CreditRun(p, world.Result));
            Assert.AreEqual(0, p.Coins);

            var clean = simulation.CreateWorld(3);
            Assert.IsFalse(clean.DebugUsed, "a new run starts unmarked");
        }

        [Test]
        public void PermanentRules_RejectSizingStats_BadOps_AndBadPrices()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PermanentRulesData(new[] { new PermanentUpgradeData(new StatEffect(StatTarget.ShotsPerVolley, StatOp.Add, 1f), new[] { 1 }) }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PermanentRulesData(new[] { new PermanentUpgradeData(new StatEffect(StatTarget.Pierce, StatOp.Add, 1f), new[] { 1 }) }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PermanentRulesData(new[] { new PermanentUpgradeData(new StatEffect(StatTarget.MoveSpeed, StatOp.Multiply, 0f), new[] { 1 }) }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PermanentRulesData(new[] { new PermanentUpgradeData(new StatEffect(StatTarget.MoveSpeed, StatOp.Add, 1f), new int[0]) }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PermanentRulesData(new[] { new PermanentUpgradeData(new StatEffect(StatTarget.MoveSpeed, StatOp.Add, 1f), new[] { 5, -1 }) }));
        }

        // ---- 판 시작 적용 ----

        [Test]
        public void ZeroStages_GiveTheSameRunAsBefore()
        {
            var simulation = NewSimulation();
            var plain = simulation.CreateWorld(77);
            var zero = simulation.CreateWorld(77, Rules(), Progress(500));
            Assert.AreEqual(simulation.MaxHealthOf(plain), simulation.MaxHealthOf(zero));
            Assert.AreEqual(plain.Player.Health, zero.Player.Health);
            Assert.AreEqual(simulation.MoveSpeed(plain), simulation.MoveSpeed(zero));
            Assert.AreEqual(simulation.WeaponValue(plain, 0, StatTarget.Damage), simulation.WeaponValue(zero, 0, StatTarget.Damage));
            Run(simulation, plain, 900);
            Run(simulation, zero, 900);
            Assert.AreEqual(plain.Player.Position.X, zero.Player.Position.X);
            Assert.AreEqual(plain.Player.Position.Y, zero.Player.Position.Y);
            Assert.AreEqual(plain.EnemiesKilledTotal, zero.EnemiesKilledTotal);
            Assert.AreEqual(plain.CoinsCollected, zero.CoinsCollected);
            Assert.AreEqual(plain.Level, zero.Level);
        }

        [Test]
        public void BoughtStages_ChangeTheBaseValues_AtTheStartOfARun()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(5, Rules(), Progress(0, 2, 1, 2, 1));
            Assert.AreEqual(MaxHealth + 20f, simulation.MaxHealthOf(world), Tolerance);
            Assert.AreEqual(MaxHealth + 20f, world.Player.Health, Tolerance, "starts at the boosted max health");
            Assert.AreEqual(5f * 1.1f, simulation.WeaponValue(world, 0, StatTarget.Damage), Tolerance);
            Assert.AreEqual(PlayerSpeed * 1.2f * 1.2f, simulation.MoveSpeed(world), Tolerance);
            Assert.AreEqual(0.5f, simulation.HealthRegen(world), Tolerance);
            Assert.AreEqual(1f, world.BaseMul[(int)StatTarget.PickupRadius], "untouched stat stays");
        }

        [Test]
        public void Order_PermanentFirst_ThenPassivesAndUpgradesOnTop_AndDebugResetKeepsPermanent()
        {
            var passives = new PassiveRulesData(new[]
            {
                new PassiveData(new[] { new StatEffect(StatTarget.MoveSpeed, StatOp.Multiply, 1.5f) }, 3),
                new PassiveData(new[] { new StatEffect(StatTarget.MaxHealth, StatOp.Add, 20f) }, 3),
                new PassiveData(new[] { new StatEffect(StatTarget.MaxHealth, StatOp.Multiply, 2f) }, 3),
            }, 6, 0f);
            var simulation = NewSimulation(passives: passives);
            var world = simulation.CreateWorld(5, Rules(), Progress(0, 1, 0, 1));
            simulation.ApplyPassive(world, 0);
            simulation.ApplyPassive(world, 1);
            // 이동: (기본 × 영구 1.2) × 패시브 1.5
            Assert.AreEqual(PlayerSpeed * 1.2f * 1.5f, simulation.MoveSpeed(world), Tolerance);
            // 최대 체력: (기본 + 영구 10) + 패시브 20
            Assert.AreEqual(MaxHealth + 10f + 20f, simulation.MaxHealthOf(world), Tolerance);
            simulation.ApplyPassive(world, 2);
            // 곱하기 패시브는 영구 · 더하기를 합친 뒤에: ((100 + 10) + 20) × 2
            Assert.AreEqual((MaxHealth + 10f + 20f) * 2f, simulation.MaxHealthOf(world), Tolerance);

            simulation.ResetUpgrades(world);
            Assert.AreEqual(MaxHealth + 10f, simulation.MaxHealthOf(world), Tolerance, "debug reset clears passives only");
            Assert.AreEqual(PlayerSpeed * 1.2f, simulation.MoveSpeed(world), Tolerance);
        }

        [Test]
        public void SameSeed_AndSameStages_GiveTheSameRun()
        {
            var simulation = NewSimulation();
            var a = simulation.CreateWorld(2026, Rules(), Progress(0, 3, 2, 1, 1));
            var b = simulation.CreateWorld(2026, Rules(), Progress(0, 3, 2, 1, 1));
            Run(simulation, a, 1500);
            Run(simulation, b, 1500);
            Assert.AreEqual(a.Player.Position.X, b.Player.Position.X);
            Assert.AreEqual(a.Player.Position.Y, b.Player.Position.Y);
            Assert.AreEqual(a.EnemiesKilledTotal, b.EnemiesKilledTotal);
            Assert.AreEqual(a.CoinsDroppedTotal, b.CoinsDroppedTotal);
            Assert.AreEqual(a.CoinsCollected, b.CoinsCollected);
            Assert.AreEqual(a.Level, b.Level);
            Assert.AreEqual(a.DropRandom.State, b.DropRandom.State);
            Assert.Greater(a.CoinsDroppedTotal, 0, "coins dropped in the test run");
        }

        // ---- 코인 드롭 · 줍기 ----

        [Test]
        public void CoinDrops_UseTheirOwnStream_SoSpawnsAndKillsStayTheSame()
        {
            var with = NewSimulation(Enemies(0.5f, 1));
            var without = NewSimulation(Enemies(0f, 0));
            var a = with.CreateWorld(91);
            var b = without.CreateWorld(91);
            Run(with, a, 1200);
            Run(without, b, 1200);
            Assert.Greater(a.CoinsDroppedTotal, 0);
            Assert.AreEqual(0, b.CoinsDroppedTotal);
            Assert.AreEqual(b.EnemiesKilledTotal, a.EnemiesKilledTotal);
            Assert.AreEqual(b.SpawnRandom.State, a.SpawnRandom.State);
            Assert.AreEqual(b.LevelUpRandom.State, a.LevelUpRandom.State);
            Assert.AreEqual(Rng.ForStream(91, RandomStream.Drop).State, b.DropRandom.State, "chance 0 never draws");
        }

        [Test]
        public void SureDrops_DoNotDraw_AndCarryTheirValue()
        {
            var simulation = NewSimulation(Enemies(1f, 10));
            var world = simulation.CreateWorld(4);
            Run(simulation, world, 900);
            Assert.Greater(world.EnemiesKilledTotal, 0);
            Assert.AreEqual(world.EnemiesKilledTotal, world.CoinsDroppedTotal, "chance 1 = every kill");
            Assert.AreEqual(Rng.ForStream(4, RandomStream.Drop).State, world.DropRandom.State, "chance 1 never draws");
            Assert.AreEqual(0, world.CoinsCollected % 10);
        }

        [Test]
        public void Coins_ArePickedUpLikeGems_AndGoIntoTheResult()
        {
            var simulation = NewSimulation(Enemies(0f, 0, 0f));
            var world = simulation.CreateWorld(1);
            PlaceCoin(world, new Vec2(1f, 0f), 3);
            PlaceCoin(world, new Vec2(PickupRadius + 5f, 0f), 4);
            for (int i = 0; i < 30; i++) simulation.Step(world, default);
            Assert.AreEqual(3, world.CoinsCollected);
            Assert.AreEqual(1, world.CoinCount, "outside the pickup radius stays");
            simulation.EndRun(world, RunOutcome.Defeat);
            Assert.AreEqual(3, world.Result.Coins);
        }

        [Test]
        public void FullCoinPool_AddsToTheLastCoin()
        {
            var simulation = NewSimulation(Enemies(1f, 2, 0f), Experience(maxCoins: 1));
            var world = simulation.CreateWorld(1);
            PlaceCoin(world, new Vec2(9f, 9f), 2);
            world.Enemies[0] = new EnemyState { Id = world.AllocateEntityId(), Position = new Vec2(8f, 8f), Dead = true, LastHitTick = EnemyState.NeverHit };
            world.EnemyCount = 1;
            simulation.Step(world, default); // 틱 안에서 죽은 적 자리에 드롭
            Assert.AreEqual(1, world.CoinCount);
            Assert.AreEqual(4, world.Coins[0].Value);
        }

        [Test]
        public void Step_DoesNotAllocate_WithCoins()
        {
            var simulation = NewSimulation(Enemies(0.5f, 1, 30f));
            var world = simulation.CreateWorld(21, Rules(), Progress(0, 1, 1, 1, 1));
            world.PlayerInvincible = true;
            simulation.SpawnEnemies(world, 150);
            var input = new InputFrame { Move = new Vec2(1f, 0.5f) };
            for (int i = 0; i < 60; i++)
            {
                simulation.Step(world, input);
                while (world.IsChoosingUpgrade) simulation.ChooseUpgrade(world, 0);
            }
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                {
                    simulation.Step(world, input);
                    while (world.IsChoosingUpgrade) simulation.ChooseUpgrade(world, 0);
                }
            }, NUnit.Framework.Is.Not.AllocatingGCMemory());
            Assert.Greater(world.CoinsDroppedTotal, 0);
        }

        [Test]
        public void Coins_LandBesideTheGem_ByTheDropOffset()
        {
            var experience = new ExperienceRulesData(new[] { 3, 5, 8 }, 3, 128, 8f, 0.4f, 8, 0.35f);
            var simulation = NewSimulation(Enemies(1f, 1, 0f), experience);
            var world = simulation.CreateWorld(1);
            world.Enemies[0] = new EnemyState { Id = world.AllocateEntityId(), Position = new Vec2(8f, 8f), Dead = true, LastHitTick = EnemyState.NeverHit };
            world.EnemyCount = 1;
            simulation.Step(world, default);
            Assert.AreEqual(1, world.GemCount);
            Assert.AreEqual(1, world.CoinCount);
            Assert.AreEqual(world.Gems[0].Position.X + 0.35f, world.Coins[0].Position.X, Tolerance);
            Assert.AreEqual(world.Gems[0].Position.Y, world.Coins[0].Position.Y, Tolerance);
        }

        // ---- 저장 형식(Core) ----

        [Test]
        public void SaveFormat_RoundTripsThroughJson_WithVersion()
        {
            var rules = Rules();
            var p = Progress(321, 2, 0, 1, 1);
            MetaSaveData data = MetaSave.ToSave(p, Ids);
            Assert.AreEqual(MetaSave.CurrentVersion, data.version);
            Assert.AreEqual(3, data.upgrades.Length, "stage 0 rows are not stored");
            string json = JsonUtility.ToJson(data);
            StringAssert.Contains("\"version\":1", json);
            Assert.AreEqual(MetaLoadStatus.Loaded, MetaSave.FromSave(JsonUtility.FromJson<MetaSaveData>(json), Ids, rules, out MetaProgress back));
            Assert.AreEqual(321, back.Coins);
            CollectionAssert.AreEqual(p.Stages, back.Stages);
        }

        [Test]
        public void SaveFormat_RejectsBrokenData_AndCleansStages()
        {
            var rules = Rules();
            Assert.AreEqual(MetaLoadStatus.Corrupt, MetaSave.FromSave(null, Ids, rules, out var p0));
            Assert.AreEqual(0, p0.Coins);
            Assert.AreEqual(MetaLoadStatus.Corrupt, MetaSave.FromSave(new MetaSaveData { version = 0, coins = 5 }, Ids, rules, out _));
            Assert.AreEqual(MetaLoadStatus.Corrupt, MetaSave.FromSave(new MetaSaveData { version = 1, coins = -1 }, Ids, rules, out _));
            Assert.AreEqual(MetaLoadStatus.NewerVersion, MetaSave.FromSave(new MetaSaveData { version = MetaSave.CurrentVersion + 1, coins = 5 }, Ids, rules, out var newer));
            Assert.AreEqual(0, newer.Coins, "a newer file is not half-read");

            var data = new MetaSaveData
            {
                version = 1,
                coins = 40,
                upgrades = new[]
                {
                    new MetaSaveStage { id = "max_health", stage = 99 },
                    new MetaSaveStage { id = "max_health", stage = 1 },
                    new MetaSaveStage { id = "gone_row", stage = 2 },
                    new MetaSaveStage { id = "damage", stage = -3 },
                    null,
                },
            };
            Assert.AreEqual(MetaLoadStatus.Loaded, MetaSave.FromSave(data, Ids, rules, out var p));
            Assert.AreEqual(40, p.Coins);
            CollectionAssert.AreEqual(new[] { 3, 0, 0, 0 }, p.Stages, "clamped to max, first entry wins, unknown ids dropped");
        }

        // ---- 파일(Unity, 임시 경로만) ----

        static string TempSavePath()
        {
            string folder = Path.Combine(Path.GetTempPath(), "MyongMetaTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, MetaStore.DefaultFileName);
        }

        static void DeleteTempFolder(string path)
        {
            string folder = Path.GetDirectoryName(path);
            if (folder != null && folder.StartsWith(Path.GetTempPath()) && Path.GetFileName(folder).StartsWith("MyongMetaTests_") && Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void Store_MissingFile_StartsAtZero_ThenSavesAndLoads()
        {
            string path = TempSavePath();
            try
            {
                var store = new MetaStore(path);
                Assert.AreNotEqual(MetaStore.DefaultPath, store.Path);
                Assert.AreEqual(MetaLoadStatus.Missing, store.Load(Ids, Rules(), out var fresh));
                Assert.AreEqual(0, fresh.Coins);
                Assert.IsFalse(File.Exists(path), "loading does not create the file");

                Assert.IsTrue(store.Save(Progress(77, 1, 2), Ids));
                Assert.IsFalse(File.Exists(path + ".tmp"));
                var again = new MetaStore(path); // 에디터를 다시 연 것처럼 새로
                Assert.AreEqual(MetaLoadStatus.Loaded, again.Load(Ids, Rules(), out var loaded));
                Assert.AreEqual(77, loaded.Coins);
                CollectionAssert.AreEqual(new[] { 1, 2, 0, 0 }, loaded.Stages);
            }
            finally
            {
                DeleteTempFolder(path);
            }
        }

        [Test]
        public void Store_BrokenOrNewerFile_StartsAtZero_AndKeepsTheOriginalBesideIt()
        {
            string path = TempSavePath();
            try
            {
                const string garbage = "{ this is not json";
                File.WriteAllText(path, garbage);
                var store = new MetaStore(path);
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
                Assert.AreEqual(MetaLoadStatus.Corrupt, store.Load(Ids, Rules(), out var p));
                Assert.AreEqual(0, p.Coins);
                Assert.IsNotNull(store.LastBackupPath);
                Assert.AreEqual(garbage, File.ReadAllText(store.LastBackupPath), "backup holds the original");
                Assert.AreEqual(garbage, File.ReadAllText(path), "original not deleted by loading");

                File.WriteAllText(path, "{\"version\":99,\"coins\":5000,\"upgrades\":[]}");
                Assert.AreEqual(MetaLoadStatus.NewerVersion, store.Load(Ids, Rules(), out var q));
                Assert.AreEqual(0, q.Coins);
                Assert.IsTrue(File.Exists(store.LastBackupPath));
                Assert.AreEqual(2, Directory.GetFiles(Path.GetDirectoryName(path), MetaStore.DefaultFileName + ".broken-*").Length);

                File.WriteAllText(path, string.Empty);
                Assert.AreEqual(MetaLoadStatus.Corrupt, store.Load(Ids, Rules(), out _), "empty file");
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
                DeleteTempFolder(path);
            }
        }

        // ---- 표 에셋 ----

        [Test]
        public void PermanentUpgradeTable_Exists_RowsAreValid_PricesRise_AndTextKeysAreInTheTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<PermanentUpgradeTable>("Assets/_Game/Data/PermanentUpgradeTable.asset");
            var text = TextTable.ParseCsv(File.ReadAllText(Application.dataPath + "/_Game/Text/strings.csv"));
            Assert.IsNotNull(table);
            Assert.That(table.Rows.Count, NUnit.Framework.Is.InRange(5, 6), "first permanent upgrades: 5 ~ 6");
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var row in table.Rows)
            {
                Assert.IsFalse(string.IsNullOrEmpty(row.id), "row without id");
                Assert.IsTrue(ids.Add(row.id), "duplicate id " + row.id);
                Assert.IsTrue(PermanentRulesData.TargetAllowed(row.target), row.id);
                Assert.AreNotEqual(row.op == StatOp.Multiply ? 1f : 0f, row.value, "changes nothing: " + row.id);
                Assert.GreaterOrEqual(row.MaxStage, 1, row.id);
                for (int s = 1; s < row.prices.Count; s++) Assert.Greater(row.prices[s], row.prices[s - 1], "prices rise: " + row.id);
                Assert.Greater(row.prices[0], 0, row.id);
                Assert.IsTrue(text.Has(row.nameKey), "name key missing from strings.csv: " + row.nameKey);
                Assert.IsTrue(text.Has(row.descriptionKey), "description key missing from strings.csv: " + row.descriptionKey);
            }
            Assert.AreEqual(table.Rows.Count, table.ToRules().Rows.Length);
        }

        [Test]
        public void EnemyTable_CoinDrops_AreValid_EliteAlwaysDrops_AndExperienceTableHasCoins()
        {
            var enemies = AssetDatabase.LoadAssetAtPath<EnemyTable>("Assets/_Game/Data/EnemyTable.asset");
            var experience = AssetDatabase.LoadAssetAtPath<ExperienceTable>("Assets/_Game/Data/ExperienceTable.asset");
            Assert.Greater(experience.MaxCoins, 0);
            bool anyCoins = false;
            foreach (var row in enemies.Rows)
            {
                Assert.That(row.coinChance, NUnit.Framework.Is.InRange(0f, 1f), row.id);
                Assert.GreaterOrEqual(row.coinValue, 0, row.id);
                if (row.coinChance > 0f && row.coinValue > 0) anyCoins = true;
            }
            Assert.IsTrue(anyCoins);
            var elite = enemies.Rows[enemies.IndexOf("elite")];
            Assert.AreEqual(1f, elite.coinChance, "elite always drops");
            Assert.Greater(elite.coinValue, 1, "elite drops several coins");
            var finale = enemies.Rows[enemies.IndexOf("clockkeeper")];
            Assert.AreEqual(0, finale.coinValue, "the untouchable finale enemy drops nothing");
        }
    }
}
