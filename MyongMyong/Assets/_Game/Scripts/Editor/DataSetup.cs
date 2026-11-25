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
        public const string WeaponTablePath = Folder + "/WeaponTable.asset";
        public const string ExperienceTablePath = Folder + "/ExperienceTable.asset";
        public const string UpgradeTablePath = Folder + "/UpgradeTable.asset";

        const float InitialMoveSpeed = 4.5f;
        const int InitialTicksPerSecond = 60;
        const int InitialMaxTicksPerFrame = 5;

        // 적 · 스폰 첫 값 (BALANCE.md와 같은 값. 기준은 에셋)
        const string InitialEnemyId = "basic";
        const float InitialEnemyMoveSpeed = 1.8f;
        const float InitialEnemyRadius = 0.3f;
        const float InitialEnemyMaxHealth = 10f;
        const string InitialEnemyVisualId = "enemy_basic";
        const float InitialSeparationPush = 0.5f;
        const float InitialSpawnRadius = 18f;
        const int InitialMaxAlive = 300;
        static readonly (float start, float perSecond)[] InitialPhases = { (0f, 1.5f), (60f, 2f), (180f, 4f), (300f, 8f) };

        // 무기 첫 값 (BALANCE.md와 같은 값. 기준은 에셋)
        const string InitialWeaponId = "girl_gun";
        const float InitialFireInterval = 0.4f;
        const float InitialBulletSpeed = 10f;
        const float InitialDamage = 5f;
        const float InitialRange = 11f;
        const int InitialShotsPerVolley = 1;
        const int InitialPierce = 0;
        const float InitialBulletRadius = 0.1f;
        const string InitialBulletVisualId = "bullet_girl_gun";
        const int InitialMaxBullets = 256;
        const float InitialMuzzleHeight = 0.5f;

        // 경험치 · 강화 첫 값 (BALANCE.md와 같은 값. 기준은 에셋)
        const float InitialPickupRadius = 2.5f;
        const int InitialEnemyExperience = 1;

        // 체력 · 접촉 피해 첫 값 (BALANCE.md와 같은 값. 기준은 에셋)
        const float InitialMaxHealth = 100f;
        const float InitialBodyRadius = 0.35f;
        const float InitialHurtInvulnerableSeconds = 0.5f;
        const float InitialEnemyContactDamage = 10f;
        static readonly int[] InitialExperienceToNext = { 5, 10, 15, 20, 26, 32, 38, 45, 52, 60 };
        const int InitialExtraPerLevel = 8;
        const int InitialMaxGems = 400;
        const float InitialGemPullSpeed = 9f;
        const float InitialGemCollectDistance = 0.4f;
        const string InitialGemVisualId = "gem_basic";
        const int InitialChoicesPerLevelUp = 3;
        static readonly (string id, Myong.Core.StatTarget target, Myong.Core.StatOp op, float value, int maxStage, string weaponId)[] InitialUpgrades =
        {
            ("fire_interval", Myong.Core.StatTarget.FireInterval, Myong.Core.StatOp.Multiply, 0.88f, 5, InitialWeaponId),
            ("damage", Myong.Core.StatTarget.Damage, Myong.Core.StatOp.Add, 2.5f, 5, InitialWeaponId),
            ("multishot", Myong.Core.StatTarget.ShotsPerVolley, Myong.Core.StatOp.Add, 1f, 3, InitialWeaponId),
            ("pierce", Myong.Core.StatTarget.Pierce, Myong.Core.StatOp.Add, 1f, 3, InitialWeaponId),
            ("bullet_speed", Myong.Core.StatTarget.BulletSpeed, Myong.Core.StatOp.Multiply, 1.15f, 5, InitialWeaponId),
            ("move_speed", Myong.Core.StatTarget.MoveSpeed, Myong.Core.StatOp.Multiply, 1.08f, 5, ""),
            ("pickup_radius", Myong.Core.StatTarget.PickupRadius, Myong.Core.StatOp.Multiply, 1.3f, 5, ""),
        };

        [MenuItem("Myong/Setup/Create Data Assets", priority = 11)]
        public static void CreateDataAssets()
        {
            SetupUtil.EnsureFolder(Folder);

            if (AssetDatabase.LoadAssetAtPath<PlayerTuning>(PlayerTuningPath) == null)
            {
                var asset = ScriptableObject.CreateInstance<PlayerTuning>();
                var so = new SerializedObject(asset);
                so.FindProperty("moveSpeed").floatValue = InitialMoveSpeed;
                so.FindProperty("pickupRadius").floatValue = InitialPickupRadius;
                so.FindProperty("maxHealth").floatValue = InitialMaxHealth;
                so.FindProperty("bodyRadius").floatValue = InitialBodyRadius;
                so.FindProperty("hurtInvulnerableSeconds").floatValue = InitialHurtInvulnerableSeconds;
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
                row.FindPropertyRelative("maxHealth").floatValue = InitialEnemyMaxHealth;
                row.FindPropertyRelative("experience").intValue = InitialEnemyExperience;
                row.FindPropertyRelative("contactDamage").floatValue = InitialEnemyContactDamage;
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

if (AssetDatabase.LoadAssetAtPath<WeaponTable>(WeaponTablePath) == null)
            {
                var asset = ScriptableObject.CreateInstance<WeaponTable>();
                var so = new SerializedObject(asset);
                var rows = so.FindProperty("rows");
                rows.arraySize = 1;
                var row = rows.GetArrayElementAtIndex(0);
                row.FindPropertyRelative("id").stringValue = InitialWeaponId;
                row.FindPropertyRelative("fireInterval").floatValue = InitialFireInterval;
                row.FindPropertyRelative("bulletSpeed").floatValue = InitialBulletSpeed;
                row.FindPropertyRelative("damage").floatValue = InitialDamage;
                row.FindPropertyRelative("range").floatValue = InitialRange;
                row.FindPropertyRelative("shotsPerVolley").intValue = InitialShotsPerVolley;
                row.FindPropertyRelative("pierce").intValue = InitialPierce;
                row.FindPropertyRelative("bulletRadius").floatValue = InitialBulletRadius;
                row.FindPropertyRelative("visualId").stringValue = InitialBulletVisualId;
                var starting = so.FindProperty("startingWeapons");
                starting.arraySize = 1;
                starting.GetArrayElementAtIndex(0).stringValue = InitialWeaponId;
                so.FindProperty("maxBullets").intValue = InitialMaxBullets;
                so.FindProperty("muzzleHeight").floatValue = InitialMuzzleHeight;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, WeaponTablePath);
            }

            if (AssetDatabase.LoadAssetAtPath<ExperienceTable>(ExperienceTablePath) == null)
            {
                var asset = ScriptableObject.CreateInstance<ExperienceTable>();
                var so = new SerializedObject(asset);
                var curve = so.FindProperty("experienceToNext");
                curve.arraySize = InitialExperienceToNext.Length;
                for (int i = 0; i < InitialExperienceToNext.Length; i++) curve.GetArrayElementAtIndex(i).intValue = InitialExperienceToNext[i];
                so.FindProperty("extraPerLevel").intValue = InitialExtraPerLevel;
                so.FindProperty("maxGems").intValue = InitialMaxGems;
                so.FindProperty("pullSpeed").floatValue = InitialGemPullSpeed;
                so.FindProperty("collectDistance").floatValue = InitialGemCollectDistance;
                so.FindProperty("gemVisualId").stringValue = InitialGemVisualId;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, ExperienceTablePath);
            }

            if (AssetDatabase.LoadAssetAtPath<UpgradeTable>(UpgradeTablePath) == null)
            {
                var asset = ScriptableObject.CreateInstance<UpgradeTable>();
                var so = new SerializedObject(asset);
                var rows = so.FindProperty("rows");
                rows.arraySize = InitialUpgrades.Length;
                for (int i = 0; i < InitialUpgrades.Length; i++)
                {
                    var u = InitialUpgrades[i];
                    var row = rows.GetArrayElementAtIndex(i);
                    row.FindPropertyRelative("id").stringValue = u.id;
                    row.FindPropertyRelative("nameKey").stringValue = "upgrade." + u.id + ".name";
                    row.FindPropertyRelative("descriptionKey").stringValue = "upgrade." + u.id + ".desc";
                    row.FindPropertyRelative("target").intValue = (int)u.target;
                    row.FindPropertyRelative("op").intValue = (int)u.op;
                    row.FindPropertyRelative("value").floatValue = u.value;
                    row.FindPropertyRelative("maxStage").intValue = u.maxStage;
                    row.FindPropertyRelative("weaponId").stringValue = u.weaponId;
                }
                so.FindProperty("choicesPerLevelUp").intValue = InitialChoicesPerLevelUp;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, UpgradeTablePath);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
