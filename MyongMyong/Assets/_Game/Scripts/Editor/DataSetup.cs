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
        public const string JoystickTuningPath = Folder + "/JoystickTuning.asset";
        public const string RunTuningPath = Folder + "/RunTuning.asset";

        // 판 길이 첫 값 (BALANCE.md와 같은 값. 기준은 에셋) — GDD 4번 승리: 30분 생존
        const float InitialSurvivalSeconds = 1800f;

        // 가상 조이스틱 첫 값 (BALANCE.md와 같은 값. 기준은 에셋. 거리는 HUD 단위 = 기준 해상도 도트)
        const float InitialJoystickRadius = 72f;
        const float InitialJoystickDeadZone = 0.2f;
        const int InitialJoystickDirections = 8;
        const bool InitialJoystickAnalogSpeed = false;
        const float InitialJoystickKnobRadius = 28f;
        const float InitialJoystickTouchAreaWidth = 1f;

        const float InitialMoveSpeed = 4.5f;
        const int InitialTicksPerSecond = 60;
        const int InitialMaxTicksPerFrame = 5;

        // 적 · 스폰 첫 값 (BALANCE.md와 같은 값. 기준은 에셋)
        const string InitialEnemyId = "basic";
        const float InitialSeparationPush = 0.5f;
        const float InitialSpawnRadius = 18f;
        const int InitialMaxAlive = 300;
        const float InitialChargeDespawnDistance = 24f;
        // id · 이동 속도 · 반지름 · 체력 · 경험치 · 접촉 피해 · 비주얼 ID (1-8에서 runner · brute · elite)
        public static readonly (string id, float moveSpeed, float radius, float maxHealth, int experience, float contactDamage, string visualId)[] InitialEnemies =
        {
            (InitialEnemyId, 1.8f, 0.3f, 10f, 1, 10f, "enemy_basic"),
            ("runner", 3f, 0.22f, 5f, 1, 5f, "enemy_runner"),
            ("brute", 1.1f, 0.42f, 35f, 3, 15f, "enemy_brute"),
            ("elite", 1.4f, 0.75f, 220f, 20, 20f, "enemy_elite"),
        };
        // 시작 초 · 초당 마릿수 · 종류 비중(비면 basic만). 첫 시간대 1.5마리/초 basic은 사용자 확정(2026-11-25).
        public static readonly (float start, float perSecond, (string id, float weight)[] mix)[] InitialPhases =
        {
            (0f, 1.5f, new (string, float)[0]),
            (60f, 2f, new[] { (InitialEnemyId, 3f), ("runner", 1f) }),
            (180f, 4f, new[] { (InitialEnemyId, 5f), ("runner", 3f), ("brute", 2f) }),
            (300f, 8f, new[] { (InitialEnemyId, 4f), ("runner", 3f), ("brute", 3f) }),
        };
        // 웨이브: 적 · 시작 초 · 간격 초 · 마릿수 · 방식 · 간격(유닛) · 돌진 속도 배수
        public static readonly (string id, float start, float interval, int count, Myong.Core.SpawnPattern pattern, float spacing, float speedScale)[] InitialWaves =
        {
            ("elite", 120f, 60f, 1, Myong.Core.SpawnPattern.Scatter, 0f, 1f),
            ("runner", 150f, 45f, 9, Myong.Core.SpawnPattern.Swarm, 0.6f, 1.6f),
        };

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

        // 체력 · 접촉 피해 첫 값 (BALANCE.md와 같은 값. 기준은 에셋)
        const float InitialMaxHealth = 100f;
        const float InitialBodyRadius = 0.35f;
        const float InitialHurtInvulnerableSeconds = 0.5f;
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
                rows.arraySize = InitialEnemies.Length;
                for (int i = 0; i < InitialEnemies.Length; i++) WriteEnemyRow(rows.GetArrayElementAtIndex(i), InitialEnemies[i]);
                so.FindProperty("separationPush").floatValue = InitialSeparationPush;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, EnemyTablePath);
            }

            if (AssetDatabase.LoadAssetAtPath<SpawnTable>(SpawnTablePath) == null)
            {
                var asset = ScriptableObject.CreateInstance<SpawnTable>();
                var so = new SerializedObject(asset);
                WriteSpawnPhasesAndWaves(so);
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

            if (AssetDatabase.LoadAssetAtPath<JoystickTuning>(JoystickTuningPath) == null)
            {
                var asset = ScriptableObject.CreateInstance<JoystickTuning>();
                var so = new SerializedObject(asset);
                so.FindProperty("radius").floatValue = InitialJoystickRadius;
                so.FindProperty("deadZone").floatValue = InitialJoystickDeadZone;
                so.FindProperty("directionCount").intValue = InitialJoystickDirections;
                so.FindProperty("analogSpeed").boolValue = InitialJoystickAnalogSpeed;
                so.FindProperty("knobRadius").floatValue = InitialJoystickKnobRadius;
                so.FindProperty("touchAreaWidth").floatValue = InitialJoystickTouchAreaWidth;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, JoystickTuningPath);
            }

            if (AssetDatabase.LoadAssetAtPath<RunTuning>(RunTuningPath) == null)
            {
                var asset = ScriptableObject.CreateInstance<RunTuning>();
                var so = new SerializedObject(asset);
                so.FindProperty("survivalSeconds").floatValue = InitialSurvivalSeconds;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, RunTuningPath);
            }

            AssetDatabase.SaveAssets();
        }
    

        /// <summary>적 표 한 줄에 첫 값을 쓴다.</summary>
        public static void WriteEnemyRow(SerializedProperty row, (string id, float moveSpeed, float radius, float maxHealth, int experience, float contactDamage, string visualId) e)
        {
            row.FindPropertyRelative("id").stringValue = e.id;
            row.FindPropertyRelative("moveSpeed").floatValue = e.moveSpeed;
            row.FindPropertyRelative("radius").floatValue = e.radius;
            row.FindPropertyRelative("maxHealth").floatValue = e.maxHealth;
            row.FindPropertyRelative("experience").intValue = e.experience;
            row.FindPropertyRelative("contactDamage").floatValue = e.contactDamage;
            row.FindPropertyRelative("visualId").stringValue = e.visualId;
        }

        /// <summary>스폰 표의 시간대 · 웨이브 · 돌진 사라지는 거리에 첫 값을 쓴다(스폰 반경 · 최대 수는 건드리지 않음).</summary>
        public static void WriteSpawnPhasesAndWaves(SerializedObject so)
        {
            var phases = so.FindProperty("phases");
            phases.arraySize = InitialPhases.Length;
            for (int i = 0; i < InitialPhases.Length; i++)
            {
                var phase = phases.GetArrayElementAtIndex(i);
                phase.FindPropertyRelative("startSeconds").floatValue = InitialPhases[i].start;
                phase.FindPropertyRelative("spawnsPerSecond").floatValue = InitialPhases[i].perSecond;
                phase.FindPropertyRelative("enemyId").stringValue = InitialEnemyId;
                var mix = phase.FindPropertyRelative("mix");
                mix.arraySize = InitialPhases[i].mix.Length;
                for (int m = 0; m < InitialPhases[i].mix.Length; m++)
                {
                    var entry = mix.GetArrayElementAtIndex(m);
                    entry.FindPropertyRelative("enemyId").stringValue = InitialPhases[i].mix[m].id;
                    entry.FindPropertyRelative("weight").floatValue = InitialPhases[i].mix[m].weight;
                }
            }
            var waves = so.FindProperty("waves");
            waves.arraySize = InitialWaves.Length;
            for (int i = 0; i < InitialWaves.Length; i++)
            {
                var w = InitialWaves[i];
                var wave = waves.GetArrayElementAtIndex(i);
                wave.FindPropertyRelative("enemyId").stringValue = w.id;
                wave.FindPropertyRelative("startSeconds").floatValue = w.start;
                wave.FindPropertyRelative("intervalSeconds").floatValue = w.interval;
                wave.FindPropertyRelative("count").intValue = w.count;
                wave.FindPropertyRelative("pattern").intValue = (int)w.pattern;
                wave.FindPropertyRelative("spacing").floatValue = w.spacing;
                wave.FindPropertyRelative("speedScale").floatValue = w.speedScale;
            }
            so.FindProperty("chargeDespawnDistance").floatValue = InitialChargeDespawnDistance;
        }
}
}
