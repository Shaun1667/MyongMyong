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
        public void SimulationTuning_Exists_AndIsValid()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SimulationTuning>("Assets/_Game/Data/SimulationTuning.asset");
            Assert.IsNotNull(asset);
            Assert.Greater(asset.TicksPerSecond, 0);
            Assert.Greater(asset.MaxTicksPerFrame, 0);
        }
    }
}
