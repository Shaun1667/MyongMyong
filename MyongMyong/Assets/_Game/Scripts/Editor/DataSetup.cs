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
        public const string PassiveTablePath = Folder + "/PassiveTable.asset";
        public const string JoystickTuningPath = Folder + "/JoystickTuning.asset";
        public const string RunTuningPath = Folder + "/RunTuning.asset";
        public const string FeedbackTuningPath = Folder + "/FeedbackTuning.asset";

        // 피격 · 사망 연출 첫 값 (BALANCE.md와 같은 값. 기준은 에셋. 표시만 — 규칙에는 영향 없음)
        const float InitialPlayerFlashSeconds = 0.1f;
        const float InitialPlayerBlinkSeconds = 0.066f;
        const float InitialPlayerBlinkAlpha = 0.35f;
        const float InitialGameOverDelaySeconds = 0.5f;
        const float InitialDeathMaxWaitSeconds = 3f;
        const float InitialEnemyFlashSeconds = 0.033f;
        const float InitialEnemyDeathSeconds = 0.2f;
        const int InitialEnemyDeathSteps = 4;
        const float InitialEnemyDeathPopScale = 1.25f;
        const int InitialMaxDeathEffects = 64;

        // 판 길이 첫 값 (BALANCE.md와 같은 값. 기준은 에셋) — GDD 4번 승리: 30분 생존
        const float InitialSurvivalSeconds = 1800f;
        // 1-11: 목표 뒤 마무리 최대 길이(초) · "생존 성공!" 글을 보이는 초(표시만)
        const float InitialFinaleSeconds = 60f;
        const float InitialGoalBannerSeconds = 3f;

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
            (InitialFinaleEnemyId, 7.5f, 1f, 99999f, 0, 0f, "enemy_clockkeeper"), // 1-11 30분 마무리 적(아래 특수 규칙)
        };
        // 1-11 30분 마무리 적: id · 무적 몸 · 닿으면 즉사 · 이름 글 키
        const string InitialFinaleEnemyId = "clockkeeper";
        public static readonly (string id, bool invulnerable, bool lethalContact, string nameKey)[] InitialEnemyRules =
        {
            (InitialFinaleEnemyId, true, true, "enemy.clockkeeper.name"),
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
        // 웨이브의 나오는 거리(유닛, 0 = 스폰 반경). 생존 목표(1800초) 이후에 시작하는 웨이브 = 마무리 웨이브(1-11).
        public static readonly (string id, float start, float interval, int count, Myong.Core.SpawnPattern pattern, float spacing, float speedScale, float distance)[] InitialWaves =
        {
            ("elite", 120f, 60f, 1, Myong.Core.SpawnPattern.Scatter, 0f, 1f, 0f),
            ("runner", 150f, 45f, 9, Myong.Core.SpawnPattern.Swarm, 0.6f, 1.6f, 0f),
            (InitialFinaleEnemyId, InitialSurvivalSeconds, 0f, 1, Myong.Core.SpawnPattern.Scatter, 0f, 1f, 22f),
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
            // move_speed · pickup_radius는 1-9에서 패시브 표로 옮김(강화 표 = 무기 강화)
        };

        // 패시브 첫 값 (BALANCE.md와 같은 값. 기준은 에셋) — 한 줄 = id · 그림 ID · 효과(단계마다) · 최대 단계
        const int InitialMaxPassives = 6;
        const float InitialFallbackHeal = 30f;
        public static readonly (string id, Myong.Core.StatTarget target, Myong.Core.StatOp op, float value, int maxStage)[] InitialPassives =
        {
            ("move_speed", Myong.Core.StatTarget.MoveSpeed, Myong.Core.StatOp.Multiply, 1.08f, 5),
            ("max_health", Myong.Core.StatTarget.MaxHealth, Myong.Core.StatOp.Add, 20f, 5),
            ("pickup_radius", Myong.Core.StatTarget.PickupRadius, Myong.Core.StatOp.Multiply, 1.3f, 5),
            ("regen", Myong.Core.StatTarget.HealthRegen, Myong.Core.StatOp.Add, 0.2f, 5),
            ("armor", Myong.Core.StatTarget.DamageTaken, Myong.Core.StatOp.Multiply, 0.92f, 5),
            ("growth", Myong.Core.StatTarget.ExperienceGain, Myong.Core.StatOp.Multiply, 1.1f, 5),
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

            if (AssetDatabase.LoadAssetAtPath<PassiveTable>(PassiveTablePath) == null)
            {
                var asset = ScriptableObject.CreateInstance<PassiveTable>();
                var so = new SerializedObject(asset);
                WritePassiveRows(so);
                so.FindProperty("maxOwned").intValue = InitialMaxPassives;
                so.FindProperty("fallbackHeal").floatValue = InitialFallbackHeal;
                so.FindProperty("fallbackNameKey").stringValue = "reward.heal.name";
                so.FindProperty("fallbackDescriptionKey").stringValue = "reward.heal.desc";
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, PassiveTablePath);
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
                so.FindProperty("finaleSeconds").floatValue = InitialFinaleSeconds;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, RunTuningPath);
            }

            if (AssetDatabase.LoadAssetAtPath<FeedbackTuning>(FeedbackTuningPath) == null)
            {
                var asset = ScriptableObject.CreateInstance<FeedbackTuning>();
                var so = new SerializedObject(asset);
                so.FindProperty("playerFlashSeconds").floatValue = InitialPlayerFlashSeconds;
                so.FindProperty("playerBlinkSeconds").floatValue = InitialPlayerBlinkSeconds;
                so.FindProperty("playerBlinkAlpha").floatValue = InitialPlayerBlinkAlpha;
                so.FindProperty("gameOverDelaySeconds").floatValue = InitialGameOverDelaySeconds;
                so.FindProperty("deathMaxWaitSeconds").floatValue = InitialDeathMaxWaitSeconds;
                so.FindProperty("enemyFlashSeconds").floatValue = InitialEnemyFlashSeconds;
                so.FindProperty("enemyDeathSeconds").floatValue = InitialEnemyDeathSeconds;
                so.FindProperty("enemyDeathSteps").intValue = InitialEnemyDeathSteps;
                so.FindProperty("enemyDeathPopScale").floatValue = InitialEnemyDeathPopScale;
                so.FindProperty("maxDeathEffects").intValue = InitialMaxDeathEffects;
                so.FindProperty("goalBannerSeconds").floatValue = InitialGoalBannerSeconds;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, FeedbackTuningPath);
            }

            AssetDatabase.SaveAssets();
        }
    

        /// <summary>패시브 표의 줄에 첫 값을 쓴다(효과 하나씩, 글 키 = passive.&lt;id&gt;.name · .desc · .short, 그림 ID = passive_&lt;id&gt;).</summary>
        public static void WritePassiveRows(SerializedObject so)
        {
            var rows = so.FindProperty("rows");
            rows.arraySize = InitialPassives.Length;
            for (int i = 0; i < InitialPassives.Length; i++)
            {
                var p = InitialPassives[i];
                var row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("id").stringValue = p.id;
                row.FindPropertyRelative("nameKey").stringValue = "passive." + p.id + ".name";
                row.FindPropertyRelative("descriptionKey").stringValue = "passive." + p.id + ".desc";
                row.FindPropertyRelative("shortKey").stringValue = "passive." + p.id + ".short";
                row.FindPropertyRelative("iconId").stringValue = "passive_" + p.id;
                row.FindPropertyRelative("maxStage").intValue = p.maxStage;
                var effects = row.FindPropertyRelative("effects");
                effects.arraySize = 1;
                var effect = effects.GetArrayElementAtIndex(0);
                effect.FindPropertyRelative("target").intValue = (int)p.target;
                effect.FindPropertyRelative("op").intValue = (int)p.op;
                effect.FindPropertyRelative("value").floatValue = p.value;
            }
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
            bool invulnerable = false, lethalContact = false;
            string nameKey = string.Empty;
            foreach (var r in InitialEnemyRules)
            {
                if (r.id != e.id) continue;
                invulnerable = r.invulnerable;
                lethalContact = r.lethalContact;
                nameKey = r.nameKey;
            }
            row.FindPropertyRelative("invulnerable").boolValue = invulnerable;
            row.FindPropertyRelative("lethalContact").boolValue = lethalContact;
            row.FindPropertyRelative("nameKey").stringValue = nameKey;
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
                wave.FindPropertyRelative("distance").floatValue = w.distance;
            }
            so.FindProperty("chargeDespawnDistance").floatValue = InitialChargeDespawnDistance;
        }
}
}
