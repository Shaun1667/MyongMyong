using System.Linq;
using Myong.Core;
using Myong.Unity;
using NUnit.Framework;
using UnityEditor;

namespace Myong.Tests
{
    /// <summary>구조 규칙: Core는 엔진을 모른다, 수치 에셋이 있다.</summary>
    public sealed class ProjectRulesTests
    {
        [Test]
        public void CoreAssembly_DoesNotReferenceTheEngine()
        {
            var references = typeof(World).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
            var engine = references.Where(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor") || n.StartsWith("Unity.")).ToList();
            Assert.IsEmpty(engine, "Myong.Core references: " + string.Join(", ", engine));
        }

        [Test]
        public void PlayerTuning_Exists_AndMoveSpeedIsPositive()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PlayerTuning>("Assets/_Game/Data/PlayerTuning.asset");
            Assert.IsNotNull(asset);
            Assert.Greater(asset.MoveSpeed, 0f);
        }

        [Test]
        public void PlayerTuning_HealthValues_AreValid()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PlayerTuning>("Assets/_Game/Data/PlayerTuning.asset");
            Assert.IsNotNull(asset);
            Assert.Greater(asset.MaxHealth, 0f, "max health");
            Assert.Greater(asset.BodyRadius, 0f, "body radius");
            Assert.GreaterOrEqual(asset.HurtInvulnerableSeconds, 0f, "invulnerability");
            var data = asset.ToData();
            Assert.AreEqual(asset.MaxHealth, data.MaxHealth);
            Assert.AreEqual(asset.BodyRadius, data.Radius);
            Assert.AreEqual(asset.HurtInvulnerableSeconds, data.HurtInvulnerableSeconds);

            // 적 한 마리가 한 번에 죽이지 않는다(맞고 피할 틈이 있다).
            var enemies = AssetDatabase.LoadAssetAtPath<EnemyTable>("Assets/_Game/Data/EnemyTable.asset");
            Assert.IsTrue(enemies.Rows.Any(r => r.contactDamage > 0f), "no enemy deals contact damage");
            foreach (var row in enemies.Rows)
            {
                Assert.GreaterOrEqual(row.contactDamage, 0f, row.id);
                Assert.Less(row.contactDamage, asset.MaxHealth, "one touch kills: " + row.id);
            }
            var spawn = AssetDatabase.LoadAssetAtPath<SpawnTable>("Assets/_Game/Data/SpawnTable.asset");
            var rules = spawn.ToRules(enemies);
            for (int i = 0; i < enemies.Rows.Count; i++) Assert.AreEqual(enemies.Rows[i].contactDamage, rules.Kinds[i].ContactDamage, enemies.Rows[i].id);
        }

        [Test]
        public void EnemyTable_Exists_AndRowsAreValid()
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemyTable>("Assets/_Game/Data/EnemyTable.asset");
            Assert.IsNotNull(table);
            Assert.IsNotEmpty(table.Rows);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var row in table.Rows)
            {
                Assert.IsFalse(string.IsNullOrEmpty(row.id), "enemy row without id");
                Assert.IsTrue(ids.Add(row.id), "duplicate enemy id " + row.id);
                Assert.Greater(row.moveSpeed, 0f, row.id);
                Assert.Greater(row.radius, 0f, row.id);
                Assert.Greater(row.maxHealth, 0f, "max health: " + row.id);
                Assert.IsFalse(string.IsNullOrEmpty(row.visualId), "no visual id: " + row.id);
            }
        }

        [Test]
        public void SpawnTable_Exists_RefersToKnownEnemies_AndSpawnsOffScreen()
        {
            var enemies = AssetDatabase.LoadAssetAtPath<EnemyTable>("Assets/_Game/Data/EnemyTable.asset");
            var spawn = AssetDatabase.LoadAssetAtPath<SpawnTable>("Assets/_Game/Data/SpawnTable.asset");
            Assert.IsNotNull(spawn);
            Assert.IsNotEmpty(spawn.Phases);
            Assert.Greater(spawn.MaxAlive, 0);
            foreach (var phase in spawn.Phases) Assert.GreaterOrEqual(enemies.IndexOf(phase.enemyId), 0, "unknown enemy id " + phase.enemyId);

            // 화면 대각선 절반 + 가장 큰 몸보다 멀어야 화면 밖에서 나타난다.
            float halfWidth = PixelArt.ReferenceWidth / (2f * PixelArt.PixelsPerUnit);
            float halfHeight = PixelArt.ReferenceHeight / (2f * PixelArt.PixelsPerUnit);
            float maxRadius = 0f;
            foreach (var row in enemies.Rows) maxRadius = System.Math.Max(maxRadius, row.radius);
            Assert.Greater(spawn.SpawnRadius, (float)System.Math.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight) + maxRadius);

            var rules = spawn.ToRules(enemies);
            Assert.AreEqual(spawn.Phases.Count, rules.Phases.Length);
        }

[Test]
        public void WeaponTable_Exists_AndRowsAreValid()
        {
            var table = AssetDatabase.LoadAssetAtPath<WeaponTable>("Assets/_Game/Data/WeaponTable.asset");
            Assert.IsNotNull(table);
            Assert.IsNotEmpty(table.Rows);
            Assert.Greater(table.MaxBullets, 0);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var row in table.Rows)
            {
                Assert.IsFalse(string.IsNullOrEmpty(row.id), "weapon row without id");
                Assert.IsTrue(ids.Add(row.id), "duplicate weapon id " + row.id);
                Assert.Greater(row.fireInterval, 0f, row.id);
                Assert.Greater(row.bulletSpeed, 0f, row.id);
                Assert.Greater(row.damage, 0f, row.id);
                Assert.Greater(row.range, 0f, row.id);
                Assert.GreaterOrEqual(row.shotsPerVolley, 1, row.id);
                Assert.GreaterOrEqual(row.pierce, 0, row.id);
                Assert.Greater(row.bulletRadius, 0f, row.id);
                Assert.IsFalse(string.IsNullOrEmpty(row.visualId), "no visual id: " + row.id);
            }
            Assert.IsNotEmpty(table.StartingWeapons, "the player starts with no weapon");
            foreach (string id in table.StartingWeapons) Assert.GreaterOrEqual(table.IndexOf(id), 0, "unknown starting weapon " + id);

            var rules = table.ToRules();
            Assert.AreEqual(table.StartingWeapons.Count, rules.StartingWeapons.Length);
        }

        [Test]
        public void ExperienceTable_Exists_AndIsValid()
        {
            var table = AssetDatabase.LoadAssetAtPath<ExperienceTable>("Assets/_Game/Data/ExperienceTable.asset");
            Assert.IsNotNull(table);
            Assert.IsNotEmpty(table.ExperienceToNext, "empty level curve");
            foreach (int need in table.ExperienceToNext) Assert.GreaterOrEqual(need, 1);
            Assert.GreaterOrEqual(table.ExtraPerLevel, 0);
            Assert.Greater(table.MaxGems, 0);
            Assert.Greater(table.PullSpeed, 0f);
            Assert.Greater(table.CollectDistance, 0f);
            Assert.IsFalse(string.IsNullOrEmpty(table.GemVisualId), "no gem visual id");
            Assert.IsNotNull(table.ToRules());

            var player = AssetDatabase.LoadAssetAtPath<PlayerTuning>("Assets/_Game/Data/PlayerTuning.asset");
            Assert.Greater(player.PickupRadius, table.CollectDistance, "pickup radius must reach past the touch distance");
            // 젬은 플레이어보다 빨리 끌려와야 따라잡는다.
            Assert.Greater(table.PullSpeed, player.MoveSpeed);

            var enemies = AssetDatabase.LoadAssetAtPath<EnemyTable>("Assets/_Game/Data/EnemyTable.asset");
            Assert.IsTrue(enemies.Rows.Any(r => r.experience > 0), "no enemy drops experience");
            foreach (var row in enemies.Rows) Assert.GreaterOrEqual(row.experience, 0, row.id);
        }

        [Test]
        public void UpgradeTable_Exists_RowsAreValid_AndTextKeysAreInTheTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<UpgradeTable>("Assets/_Game/Data/UpgradeTable.asset");
            var weapons = AssetDatabase.LoadAssetAtPath<WeaponTable>("Assets/_Game/Data/WeaponTable.asset");
            var text = TextTable.ParseCsv(System.IO.File.ReadAllText(UnityEngine.Application.dataPath + "/_Game/Text/strings.csv"));
            Assert.IsNotNull(table);
            Assert.IsNotEmpty(table.Rows);
            Assert.GreaterOrEqual(table.ChoicesPerLevelUp, 1);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var row in table.Rows)
            {
                Assert.IsFalse(string.IsNullOrEmpty(row.id), "upgrade row without id");
                Assert.IsTrue(ids.Add(row.id), "duplicate upgrade id " + row.id);
                Assert.IsTrue(StatTargets.IsValid(row.target), row.id);
                Assert.GreaterOrEqual(row.maxStage, 1, row.id);
                if (row.op == StatOp.Multiply) Assert.Greater(row.value, 0f, row.id);
                Assert.AreNotEqual(row.op == StatOp.Multiply ? 1f : 0f, row.value, "upgrade changes nothing: " + row.id);
                if (!string.IsNullOrEmpty(row.weaponId))
                {
                    Assert.GreaterOrEqual(weapons.IndexOf(row.weaponId), 0, "unknown weapon " + row.weaponId);
                    Assert.IsFalse(StatTargets.IsPlayerStat(row.target), "player stat upgrade names a weapon: " + row.id);
                }
                Assert.IsTrue(text.Has(row.nameKey), "name key missing from strings.csv: " + row.nameKey);
                Assert.IsTrue(text.Has(row.descriptionKey), "description key missing from strings.csv: " + row.descriptionKey);
            }
            Assert.AreEqual(table.Rows.Count, table.ToRules(weapons).Rows.Length);
        }

        [Test]
        public void RunTuning_Exists_SurvivalIsPositive_AndSpawnTableCoversTheWholeRun()
        {
            var run = AssetDatabase.LoadAssetAtPath<RunTuning>("Assets/_Game/Data/RunTuning.asset");
            Assert.IsNotNull(run);
            Assert.Greater(run.SurvivalSeconds, 0f, "no time victory");
            Assert.AreEqual(run.SurvivalSeconds, run.ToRules().SurvivalSeconds);

            // 스폰 표는 0초부터 시작하고, 마지막 시간대는 생존 목표 전에 시작해 목표까지 이어진다(다음 시간대가 없으면 끝까지 유지).
            var enemies = AssetDatabase.LoadAssetAtPath<EnemyTable>("Assets/_Game/Data/EnemyTable.asset");
            var spawn = AssetDatabase.LoadAssetAtPath<SpawnTable>("Assets/_Game/Data/SpawnTable.asset");
            var rules = spawn.ToRules(enemies);
            Assert.AreEqual(0, rules.PhaseAt(0f), "no spawn phase at 0 s");
            Assert.Less(rules.Phases[rules.Phases.Length - 1].StartSeconds, run.SurvivalSeconds, "a spawn phase starts after the run is won");
            Assert.AreEqual(rules.Phases.Length - 1, rules.PhaseAt(run.SurvivalSeconds - 1f), "last phase holds until the goal");
            Assert.Greater(rules.Phases[rules.PhaseAt(run.SurvivalSeconds - 1f)].SpawnsPerSecond, 0f, "nothing spawns at the end of the run");
        }

        [Test]
        public void SimulationTuning_Exists_AndIsValid()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SimulationTuning>("Assets/_Game/Data/SimulationTuning.asset");
            Assert.IsNotNull(asset);
            Assert.Greater(asset.TicksPerSecond, 0);
            Assert.Greater(asset.MaxTicksPerFrame, 0);
        }
    }
}
