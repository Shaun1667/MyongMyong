using Myong.Unity;
using UnityEditor;
using UnityEngine;

namespace Myong.Editor
{
    /// <summary>
    /// 데이터 에셋 자리 만들기. 없을 때만 만들고 첫 값을 넣는다 — 이미 있으면 값을 건드리지 않는다
    /// (수치의 기준은 에셋이다. 여기 적힌 숫자는 처음 만들 때의 값일 뿐).
    /// </summary>
    public static class DataSetup
    {
        public const string Folder = "Assets/_Game/Data";
        public const string PlayerTuningPath = Folder + "/PlayerTuning.asset";
        public const string SimulationTuningPath = Folder + "/SimulationTuning.asset";

        const float InitialMoveSpeed = 4.5f;
        const int InitialTicksPerSecond = 60;
        const int InitialMaxTicksPerFrame = 5;

        [MenuItem("Myong/Setup/Create Data Assets", priority = 11)]
        public static void CreateDataAssets()
        {
            SetupUtil.EnsureFolder(Folder);

            if (AssetDatabase.LoadAssetAtPath<PlayerTuning>(PlayerTuningPath) == null)
            {
                var asset = ScriptableObject.CreateInstance<PlayerTuning>();
                var so = new SerializedObject(asset);
                so.FindProperty("moveSpeed").floatValue = InitialMoveSpeed;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, PlayerTuningPath);
            }

            if (AssetDatabase.LoadAssetAtPath<SimulationTuning>(SimulationTuningPath) == null)
            {
                var asset = ScriptableObject.CreateInstance<SimulationTuning>();
                var so = new SerializedObject(asset);
                so.FindProperty("ticksPerSecond").intValue = InitialTicksPerSecond;
                so.FindProperty("maxTicksPerFrame").intValue = InitialMaxTicksPerFrame;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, SimulationTuningPath);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
