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
        public void SimulationTuning_Exists_AndIsValid()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SimulationTuning>("Assets/_Game/Data/SimulationTuning.asset");
            Assert.IsNotNull(asset);
            Assert.Greater(asset.TicksPerSecond, 0);
            Assert.Greater(asset.MaxTicksPerFrame, 0);
        }
    }
}
