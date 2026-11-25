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
        public const string EnemyTablePath = Folder + "/EnemyTable.asset";
        public const string SpawnTablePath = Folder + "/SpawnTable.asset";

        const float InitialMoveSpeed = 4.5f;
        const int InitialTicksPerSecond = 60;
        const int InitialMaxTicksPerFrame = 5;

        // 적 · 스폰 첫 값 (BALANCE.md와 같은 값. 기준은 에셋)
        const string InitialEnemyId = "basic";
        const float InitialEnemyMoveSpeed = 1.8f;
        const float InitialEnemyRadius = 0.3f;
        const string InitialEnemyVisualId = "enemy_basic";
        const float InitialSeparationPush = 0.5f;
        const float InitialSpawnRadius = 18f;
        const int InitialMaxAlive = 300;
        static readonly (float start, float perSecond)[] InitialPhases = { (0f, 1f), (60f, 2f), (180f, 4f), (300f, 8f) };

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

            if (AssetDatabase.LoadAssetAtPath<EnemyTable>(EnemyTablePath) == null)
            {
                var asset = ScriptableObject.CreateInstance<EnemyTable>();
                var so = new SerializedObject(asset);
                var rows = so.FindProperty("rows");
                rows.arraySize = 1;
                var row = rows.GetArrayElementAtIndex(0);
                row.FindPropertyRelative("id").stringValue = InitialEnemyId;
                row.FindPropertyRelative("moveSpeed").floatValue = InitialEnemyMoveSpeed;
                row.FindPropertyRelative("radius").floatValue = InitialEnemyRadius;
                row.FindPropertyRelative("visualId").stringValue = InitialEnemyVisualId;
                so.FindProperty("separationPush").floatValue = InitialSeparationPush;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, EnemyTablePath);
            }

            if (AssetDatabase.LoadAssetAtPath<SpawnTable>(SpawnTablePath) == null)
            {
                var asset = ScriptableObject.CreateInstance<SpawnTable>();
                var so = new SerializedObject(asset);
                var phases = so.FindProperty("phases");
                phases.arraySize = InitialPhases.Length;
                for (int i = 0; i < InitialPhases.Length; i++)
                {
                    var phase = phases.GetArrayElementAtIndex(i);
                    phase.FindPropertyRelative("startSeconds").floatValue = InitialPhases[i].start;
                    phase.FindPropertyRelative("spawnsPerSecond").floatValue = InitialPhases[i].perSecond;
                    phase.FindPropertyRelative("enemyId").stringValue = InitialEnemyId;
                }
                so.FindProperty("spawnRadius").floatValue = InitialSpawnRadius;
                so.FindProperty("maxAlive").intValue = InitialMaxAlive;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, SpawnTablePath);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
