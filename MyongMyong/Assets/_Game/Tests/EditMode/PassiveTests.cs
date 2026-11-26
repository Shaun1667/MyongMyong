using System.Collections.Generic;
using System.Linq;
using Myong.Core;
using Myong.Unity;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using UnityEditor;

namespace Myong.Tests
{
    /// <summary>패시브(1-9): 레벨업 선택지에 나옴 · 단계 · 최대 개수 · 대체 보상 · 결정성 · 효과(이동 · 최대 체력 · 재생 · 방어 · 경험치) · 할당 · 표 에셋. 여기의 숫자는 테스트용 값이다(게임 수치는 에셋).</summary>
    public sealed class PassiveTests
    {
        const int TicksPerSecond = 60;
        const float PlayerSpeed = 5f;
        const float PickupRadius = 2f;
        const float MaxHealth = 100f;
        const float Tolerance = 0.001f;

        static EnemyRulesData NoEnemies() => new EnemyRulesData(
            new[] { new EnemyKindData(0f, 0.3f, 1f, 1, 10f) },
            new[] { new SpawnPhaseData(0f, 0f, 0) },
            20f, 50, 0f);

        static WeaponRulesData Gun() => new WeaponRulesData(new[] { new WeaponKindData(0.5f, 10f, 5f, 10f, 1, 0, 0.1f) }, new[] { 0 }, 64, Vec2.Zero);

        static ExperienceRulesData Experience() => new ExperienceRulesData(new[] { 3, 5 }, 2, 64, 6f, 0.3f);

        static UpgradeRulesData NoUpgrades(int choices = 3) => new UpgradeRulesData(new UpgradeData[0], choices);

        static PassiveData P(StatTarget target, StatOp op, float value, int maxStage = 5) => new PassiveData(new[] { new StatEffect(target, op, value) }, maxStage);

        // 패시브 6종(이동 · 최대 체력 · 반경 · 재생 · 방어 · 경험치).
        static PassiveRulesData SixPassives(int maxOwned = 6, float fallbackHeal = 30f, int maxStage = 5) => new PassiveRulesData(new[]
        {
            P(StatTarget.MoveSpeed, StatOp.Multiply, 1.5f, maxStage),
            P(StatTarget.MaxHealth, StatOp.Add, 20f, maxStage),
            P(StatTarget.PickupRadius, StatOp.Add, 1f, maxStage),
            P(StatTarget.HealthRegen, StatOp.Add, 6f, maxStage),
            P(StatTarget.DamageTaken, StatOp.Multiply, 0.5f, maxStage),
            P(StatTarget.ExperienceGain, StatOp.Multiply, 1.5f, maxStage),
        }, maxOwned, fallbackHeal);

        static Simulation NewSimulation(PassiveRulesData passives, UpgradeRulesData upgrades = null, EnemyRulesData enemies = null, ExperienceRulesData experience = null)
        {
            return new Simulation(TicksPerSecond, new PlayerTuningData(PlayerSpeed, PickupRadius, MaxHealth, 0.35f, 0.5f),
                enemies ?? NoEnemies(), Gun(), experience ?? Experience(), upgrades ?? NoUpgrades(), RunRulesData.None, passives);
        }

        static void LevelUp(Simulation simulation, World world)
        {
            simulation.AddExperience(world, simulation.ExperienceToNext(world.Level) - world.Experience);
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

        // ---- 선택지 ----

        [Test]
        public void Passives_AreOffered_InTheLevelUpChoices_NextToWeaponUpgrades()
        {
            var upgrades = new UpgradeRulesData(new[] { new UpgradeData(StatTarget.Damage, StatOp.Add, 1f, 5, 0) }, 3);
            var simulation = NewSimulation(SixPassives(), upgrades);
            var world = simulation.CreateWorld(5);
            var seen = new HashSet<LevelUpOptionKind>();
            for (int k = 0; k < 20; k++)
            {
                LevelUp(simulation, world);
                Assert.AreEqual(3, world.LevelUpChoiceCount);
                for (int i = 0; i < world.LevelUpChoiceCount; i++) seen.Add(simulation.DescribeChoice(world.LevelUpChoices[i]).Kind);
                simulation.ChooseUpgrade(world, 0);
            }
            Assert.IsTrue(seen.Contains(LevelUpOptionKind.Passive), "no passive card");
            Assert.IsTrue(seen.Contains(LevelUpOptionKind.Upgrade), "no weapon upgrade card");
            Assert.IsFalse(seen.Contains(LevelUpOptionKind.Fallback), "stand-in offered while there is still something to pick");
        }

        [Test]
        public void ChoosingAPassive_GivesIt_ThenRaisesItsStage_UpToTheMax()
        {
            var simulation = NewSimulation(new PassiveRulesData(new[] { P(StatTarget.MoveSpeed, StatOp.Multiply, 1.5f, 2) }, 6, 0f));
            var world = simulation.CreateWorld(1);

            LevelUp(simulation, world);
            Assert.AreEqual(1, world.LevelUpChoiceCount);
            Assert.AreEqual(new LevelUpOption(LevelUpOptionKind.Passive, 0), simulation.DescribeChoice(world.LevelUpChoices[0]));
            Assert.IsTrue(simulation.ChooseUpgrade(world, 0));
            Assert.AreEqual(1, world.PassiveStages[0]);
            Assert.AreEqual(1, world.PassiveSlotCount);
            Assert.AreEqual(0, world.PassiveSlots[0]);
            Assert.AreEqual(PlayerSpeed * 1.5f, simulation.MoveSpeed(world), Tolerance);

            LevelUp(simulation, world);
            simulation.ChooseUpgrade(world, 0);
            Assert.AreEqual(2, world.PassiveStages[0]);
            Assert.AreEqual(1, world.PassiveSlotCount, "a stage-up does not take a new slot");
            Assert.AreEqual(PlayerSpeed * 1.5f * 1.5f, simulation.MoveSpeed(world), Tolerance);

            // 최대 단계: 더는 안 나오고(대체 보상 없음 = 1-4처럼 넘어감), 걸어도 안 오른다.
            LevelUp(simulation, world);
            Assert.AreEqual(0, world.LevelUpChoiceCount);
            Assert.AreEqual(0, world.PendingLevelUps);
            Assert.IsFalse(simulation.ApplyPassive(world, 0));
            Assert.AreEqual(2, world.PassiveStages[0]);
        }

        [Test]
        public void WhenTheSlotsAreFull_OnlyNextStagesOfHeldPassivesAreOffered()
        {
            var simulation = NewSimulation(SixPassives(maxOwned: 2));
            var world = simulation.CreateWorld(9);
            Assert.IsTrue(simulation.ApplyPassive(world, 4));
            Assert.IsTrue(simulation.ApplyPassive(world, 1));
            Assert.IsFalse(simulation.ApplyPassive(world, 0), "a third kind while only two are allowed");
            Assert.IsFalse(simulation.PassiveOfferable(world, 0));

            for (int k = 0; k < 6; k++)
            {
                LevelUp(simulation, world);
                Assert.LessOrEqual(world.LevelUpChoiceCount, 2);
                for (int i = 0; i < world.LevelUpChoiceCount; i++)
                {
                    LevelUpOption o = simulation.DescribeChoice(world.LevelUpChoices[i]);
                    Assert.AreEqual(LevelUpOptionKind.Passive, o.Kind);
                    Assert.IsTrue(o.Row == 4 || o.Row == 1, "a new passive was offered with full slots: " + o.Row);
                }
                simulation.ChooseUpgrade(world, 0);
            }
            Assert.AreEqual(2, world.PassiveSlotCount);
            CollectionAssert.AreEqual(new[] { 4, 1 }, world.PassiveSlots.Take(world.PassiveSlotCount).ToArray(), "slots keep the order they were taken in");
        }

        [Test]
        public void WhenEverythingIsMaxed_TheStandInHealIsOffered_AndHeals()
        {
            var simulation = NewSimulation(new PassiveRulesData(new[] { P(StatTarget.MoveSpeed, StatOp.Multiply, 1.1f, 1) }, 6, 30f));
            var world = simulation.CreateWorld(2);
            Assert.IsTrue(simulation.ApplyPassive(world, 0));
            simulation.DamagePlayer(world, 50f);
            Assert.AreEqual(50f, world.Player.Health, Tolerance);

            LevelUp(simulation, world);
            Assert.AreEqual(1, world.LevelUpChoiceCount);
            Assert.AreEqual(LevelUpOptionKind.Fallback, simulation.DescribeChoice(world.LevelUpChoices[0]).Kind);
            Assert.AreEqual(simulation.FallbackOption, world.LevelUpChoices[0]);
            Assert.IsTrue(world.IsChoosingUpgrade);
            Assert.IsTrue(simulation.ChooseUpgrade(world, 0));
            Assert.AreEqual(80f, world.Player.Health, Tolerance);
            Assert.AreEqual(1, world.FallbackRewardsTaken);
            Assert.IsFalse(world.IsChoosingUpgrade);

            // 회복은 최대 체력까지.
            LevelUp(simulation, world);
            simulation.ChooseUpgrade(world, 0);
            Assert.AreEqual(MaxHealth, world.Player.Health, Tolerance);
        }

        [Test]
        public void SameSeed_GivesTheSameChoices_WithPassives()
        {
            var upgrades = new UpgradeRulesData(new[]
            {
                new UpgradeData(StatTarget.Damage, StatOp.Add, 1f, 5, 0),
                new UpgradeData(StatTarget.Pierce, StatOp.Add, 1f, 3, 0),
            }, 3);
            List<int> Run(int seed)
            {
                var simulation = NewSimulation(SixPassives(maxOwned: 3), upgrades);
                var world = simulation.CreateWorld(seed);
                var picks = new List<int>();
                for (int k = 0; k < 15; k++)
                {
                    LevelUp(simulation, world);
                    for (int i = 0; i < world.LevelUpChoiceCount; i++) picks.Add(world.LevelUpChoices[i]);
                    simulation.ChooseUpgrade(world, k % System.Math.Max(1, world.LevelUpChoiceCount));
                }
                return picks;
            }
            CollectionAssert.AreEqual(Run(42), Run(42));
            CollectionAssert.AreNotEqual(Run(42), Run(43));
        }

        // ---- 효과 ----

        [Test]
        public void MaxHealthPassive_RaisesMaxAndCurrentHealthByTheSameAmount()
        {
            var simulation = NewSimulation(SixPassives());
            var world = simulation.CreateWorld(1);
            simulation.DamagePlayer(world, 30f);
            Assert.IsTrue(simulation.ApplyPassive(world, 1));
            Assert.AreEqual(MaxHealth + 20f, simulation.MaxHealthOf(world), Tolerance);
            Assert.AreEqual(90f, world.Player.Health, Tolerance, "missing health stays missing");
            simulation.FillHealth(world);
            Assert.AreEqual(MaxHealth + 20f, world.Player.Health, Tolerance);

            // 모두 풀면 최대 체력도 돌아오고 체력은 그 안으로.
            simulation.ResetUpgrades(world);
            Assert.AreEqual(MaxHealth, simulation.MaxHealthOf(world), Tolerance);
            Assert.AreEqual(MaxHealth, world.Player.Health, Tolerance);
            Assert.AreEqual(0, world.PassiveSlotCount);
            foreach (int stage in world.PassiveStages) Assert.AreEqual(0, stage);
        }

        [Test]
        public void RegenPassive_HealsEveryTick_UpToMaxHealth()
        {
            var simulation = NewSimulation(SixPassives());
            var world = simulation.CreateWorld(1);
            world.FiringPaused = true;
            simulation.DamagePlayer(world, 50f);
            for (int t = 0; t < TicksPerSecond; t++) simulation.Step(world, default);
            Assert.AreEqual(50f, world.Player.Health, Tolerance, "no regen without the passive");

            Assert.IsTrue(simulation.ApplyPassive(world, 3)); // +6/초
            for (int t = 0; t < TicksPerSecond; t++) simulation.Step(world, default);
            Assert.AreEqual(56f, world.Player.Health, 0.01f);
            for (int t = 0; t < TicksPerSecond * 20; t++) simulation.Step(world, default);
            Assert.AreEqual(MaxHealth, world.Player.Health, Tolerance);
        }

        [Test]
        public void ArmorPassive_ScalesContactDamage()
        {
            var simulation = NewSimulation(SixPassives());
            var world = simulation.CreateWorld(1);
            world.FiringPaused = true;
            Assert.IsTrue(simulation.ApplyPassive(world, 4)); // 받는 피해 x0.5
            PlaceEnemy(world, new Vec2(0.1f, 0f));
            simulation.Step(world, default);
            Assert.AreEqual(1, world.HitsTaken);
            Assert.AreEqual(MaxHealth - 5f, world.Player.Health, Tolerance, "contact 10 x 0.5");

            simulation.ApplyPassive(world, 4); // x0.25
            for (int t = 0; t < 30; t++) simulation.Step(world, default);
            Assert.AreEqual(2, world.HitsTaken);
            Assert.AreEqual(MaxHealth - 5f - 2.5f, world.Player.Health, Tolerance);
        }

        [Test]
        public void GrowthPassive_ScalesGemExperience_KeepingFractions()
        {
            var simulation = NewSimulation(SixPassives(), experience: new ExperienceRulesData(new[] { 1000 }, 0, 64, 6f, 0.3f));
            var world = simulation.CreateWorld(1);
            world.FiringPaused = true;
            Assert.IsTrue(simulation.ApplyPassive(world, 5)); // 경험치 x1.5
            for (int g = 0; g < 2; g++)
            {
                world.Gems[world.GemCount] = new GemState { Id = world.AllocateEntityId(), Position = Vec2.Zero, PreviousPosition = Vec2.Zero, Value = 1 };
                world.GemCount++;
                simulation.Step(world, default);
            }
            Assert.AreEqual(3, world.Experience, "1.5 + 1.5 = 3 (the half is carried)");
            Assert.AreEqual(0f, world.ExperienceCarry, Tolerance);

            // 디버그 경험치는 배율을 받지 않는다(레벨업 바로가 정확히 맞게).
            simulation.AddExperience(world, 10);
            Assert.AreEqual(13, world.Experience);
        }

        [Test]
        public void WithoutPassives_GemExperienceAndDamageAreUnchanged()
        {
            var simulation = NewSimulation(PassiveRulesData.None);
            var world = simulation.CreateWorld(1);
            Assert.AreEqual(1f, simulation.ExperienceGain(world));
            Assert.AreEqual(1f, simulation.DamageTakenScale(world));
            Assert.AreEqual(0f, simulation.HealthRegen(world));
            Assert.AreEqual(MaxHealth, simulation.MaxHealthOf(world));
            Assert.AreEqual(simulation.MaxHealth, simulation.MaxHealthOf(world));
        }

        [Test]
        public void PassiveRules_RejectWeaponStats_AndEmptyRows()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PassiveRulesData(new[] { P(StatTarget.Damage, StatOp.Add, 1f) }, 6, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PassiveRulesData(new[] { new PassiveData(new StatEffect[0], 1) }, 6, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PassiveRulesData(new[] { P(StatTarget.MoveSpeed, StatOp.Multiply, 0f) }, 6, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PassiveRulesData(new[] { P(StatTarget.MoveSpeed, StatOp.Add, 1f, 0) }, 6, 0f));
        }

        // ---- 할당 ----

        [Test]
        public void Step_DoesNotAllocate_WithPassives()
        {
            var enemies = new EnemyRulesData(new[] { new EnemyKindData(2f, 0.3f, 5f, 1, 10f) }, new[] { new SpawnPhaseData(0f, 30f, 0) }, 12f, 300, 0.5f);
            var upgrades = new UpgradeRulesData(new[] { new UpgradeData(StatTarget.Damage, StatOp.Add, 1f, 3, 0) }, 3);
            var simulation = new Simulation(TicksPerSecond, new PlayerTuningData(PlayerSpeed, 4f, MaxHealth, 0.35f, 0.5f), enemies,
                new WeaponRulesData(new[] { new WeaponKindData(0.05f, 10f, 5f, 10f, 3, 2, 0.1f) }, new[] { 0 }, 64, Vec2.Zero),
                new ExperienceRulesData(new[] { 2, 3 }, 1, 256, 8f, 0.4f), upgrades, RunRulesData.None, SixPassives(maxStage: 3, fallbackHeal: 10f));
            var world = simulation.CreateWorld(21);
            world.PlayerInvincible = true;
            simulation.ApplyPassive(world, 3);
            simulation.SpawnEnemies(world, 250);
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
                    if (i % 10 == 0) simulation.AddExperience(world, 3);
                    while (world.IsChoosingUpgrade) simulation.ChooseUpgrade(world, 0);
                }
            }, NUnit.Framework.Is.Not.AllocatingGCMemory());
            Assert.Greater(world.PassiveSlotCount, 1);
        }

        // ---- 표 에셋 ----

        [Test]
        public void PassiveTable_Exists_RowsAreValid_AndTextKeysAreInTheTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<PassiveTable>("Assets/_Game/Data/PassiveTable.asset");
            var text = TextTable.ParseCsv(System.IO.File.ReadAllText(UnityEngine.Application.dataPath + "/_Game/Text/strings.csv"));
            Assert.IsNotNull(table);
            Assert.AreEqual(6, table.Rows.Count, "first passives: 6 (GDD 5)");
            Assert.AreEqual(6, table.MaxOwned, "GDD 4: passives up to 6");
            Assert.Greater(table.FallbackHeal, 0f, "no stand-in reward");
            Assert.IsTrue(text.Has(table.FallbackNameKey), "missing from strings.csv: " + table.FallbackNameKey);
            Assert.IsTrue(text.Has(table.FallbackDescriptionKey), "missing from strings.csv: " + table.FallbackDescriptionKey);
            var ids = new HashSet<string>();
            foreach (var row in table.Rows)
            {
                Assert.IsFalse(string.IsNullOrEmpty(row.id), "passive row without id");
                Assert.IsTrue(ids.Add(row.id), "duplicate passive id " + row.id);
                Assert.GreaterOrEqual(row.maxStage, 1, row.id);
                Assert.IsFalse(string.IsNullOrEmpty(row.iconId), "no icon id: " + row.id);
                Assert.IsNotEmpty(row.effects, "no effect: " + row.id);
                foreach (var e in row.effects)
                {
                    Assert.IsTrue(StatTargets.IsPlayerStat(e.target), "passive changes a weapon value: " + row.id);
                    Assert.AreNotEqual(e.op == StatOp.Multiply ? 1f : 0f, e.value, "passive changes nothing: " + row.id);
                    if (e.op == StatOp.Multiply) Assert.Greater(e.value, 0f, row.id);
                }
                Assert.IsTrue(text.Has(row.nameKey), "missing from strings.csv: " + row.nameKey);
                Assert.IsTrue(text.Has(row.descriptionKey), "missing from strings.csv: " + row.descriptionKey);
                Assert.IsTrue(text.Has(row.shortKey), "missing from strings.csv: " + row.shortKey);
            }
            Assert.AreEqual(table.Rows.Count, table.ToRules().Rows.Length);
        }

        [Test]
        public void UpgradeTable_HoldsWeaponUpgradesOnly_PlayerValuesAreInThePassiveTable()
        {
            var upgrades = AssetDatabase.LoadAssetAtPath<UpgradeTable>("Assets/_Game/Data/UpgradeTable.asset");
            var passives = AssetDatabase.LoadAssetAtPath<PassiveTable>("Assets/_Game/Data/PassiveTable.asset");
            foreach (var row in upgrades.Rows) Assert.IsFalse(StatTargets.IsPlayerStat(row.target), "player value in the weapon upgrade table: " + row.id);
            var targets = new HashSet<StatTarget>(passives.Rows.SelectMany(r => r.effects).Select(e => e.target));
            foreach (var t in new[] { StatTarget.MoveSpeed, StatTarget.MaxHealth, StatTarget.PickupRadius, StatTarget.HealthRegen, StatTarget.DamageTaken, StatTarget.ExperienceGain })
            {
                Assert.IsTrue(targets.Contains(t), "no passive for " + t);
            }
        }
    }
}
