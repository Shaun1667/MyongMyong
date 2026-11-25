using System;
using System.Collections.Generic;
using Myong.Core;
using Myong.Unity;
using UnityEditor;
using UnityEngine;

namespace Myong.Editor
{
    /// <summary>
    /// 개발자 디버그 창 (메뉴 Myong/Debug/Debug Window). 에디터 전용이라 빌드에 들어가지 않는다.
    /// 시스템이 생기면 sections에 섹션 하나를 더한다. 게임 쪽 진입점은 GameRoot.DebugXxx.
    /// 게임 상태를 바꾸는 버튼에는 [상태 변경]을 붙인다. (이 창의 글은 텍스트 표 대상이 아니다.)
    /// </summary>
    public sealed class MyongDebugWindow : EditorWindow
    {
        const string StateChange = "[상태 변경] ";

        sealed class Section
        {
            public string Title;
            public Action<GameRoot> Draw;
            public bool Open = true;
        }

        List<Section> sections;
        Vector2 scroll;
        Vector2 teleportTarget;
        int seedInput;
        int spawnCount = 200;
        int experienceAmount = 5;

        [MenuItem("Myong/Debug/Debug Window")]
        public static void Open()
        {
            GetWindow<MyongDebugWindow>("Myong Debug");
        }

        void OnEnable()
        {
            sections = new List<Section>
            {
                new Section { Title = "판 · 시드", Draw = DrawRun },
                new Section { Title = "플레이어", Draw = DrawPlayer },
                new Section { Title = "적", Draw = DrawEnemies },
                new Section { Title = "무기", Draw = DrawWeapons },
                new Section { Title = "경험치 / 레벨", Draw = DrawExperience },
                new Section { Title = "텍스트", Draw = DrawText },
            };
        }

        void OnInspectorUpdate()
        {
            if (Application.isPlaying) Repaint();
        }

        void OnGUI()
        {
            GameRoot root = Application.isPlaying ? FindFirstObjectByType<GameRoot>() : null;
            if (root == null || !root.DebugIsRunning)
            {
                EditorGUILayout.HelpBox("Game 씬(Assets/_Game/Scenes/Game.unity)을 Play 하면 쓸 수 있습니다.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var section in sections)
            {
                section.Open = EditorGUILayout.BeginFoldoutHeaderGroup(section.Open, section.Title);
                if (section.Open)
                {
                    EditorGUI.indentLevel++;
                    section.Draw(root);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawRun(GameRoot root)
        {
            EditorGUILayout.LabelField("시드", root.DebugSeed.ToString());
            EditorGUILayout.LabelField("틱", root.DebugTick.ToString());
            if (GUILayout.Button(StateChange + "재시작 (같은 시드)")) root.DebugRestart();
            using (new EditorGUILayout.HorizontalScope())
            {
                seedInput = EditorGUILayout.IntField("새 시드", seedInput);
                if (GUILayout.Button(StateChange + "이 시드로 재시작")) root.DebugRestartWithSeed(seedInput);
            }
        }

        void DrawPlayer(GameRoot root)
        {
            Vec2 position = root.DebugPlayerPosition;
            Vec2 velocity = root.DebugPlayerVelocity;
            EditorGUILayout.LabelField("위치", position.ToString());
            EditorGUILayout.LabelField("속도", $"{velocity}  |v| = {velocity.Magnitude:0.###}");
            EditorGUILayout.LabelField("방향", root.DebugPlayerFacingX > 0 ? "오른쪽" : "왼쪽");

            using (new EditorGUILayout.HorizontalScope())
            {
                teleportTarget = EditorGUILayout.Vector2Field("순간이동 위치", teleportTarget);
                if (GUILayout.Button(StateChange + "순간이동")) root.DebugTeleportPlayer(teleportTarget.x, teleportTarget.y);
            }

            EditorGUILayout.LabelField(StateChange + "입력 대신 넣기 (가운데 = 풀기)");
            for (int y = 1; y >= -1; y--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                    for (int x = -1; x <= 1; x++)
                    {
                        string name = x == 0 && y == 0 ? "풀기" : $"{x},{y}";
                        if (!GUILayout.Button(name, GUILayout.Width(48f))) continue;
                        if (x == 0 && y == 0) root.DebugClearMoveInput();
                        else root.DebugSetMoveInput(x, y);
                    }
                }
            }

            if (GUILayout.Button("피격 애니메이션 보기")) root.DebugPlayHit();
        }

        void DrawEnemies(GameRoot root)
        {
            EditorGUILayout.LabelField("살아 있는 수", $"{root.DebugEnemyCount} / {root.DebugEnemyCapacity}");
            EditorGUILayout.LabelField("스폰 누계", root.DebugEnemiesSpawnedTotal.ToString());
            EditorGUILayout.LabelField("경과 시간", $"{root.DebugElapsedSeconds:0.0}초");
            EditorGUILayout.LabelField("시간대 스폰", root.DebugSpawningPaused ? "멈춤" : "돌아감");
            EditorGUILayout.LabelField("프레임 / 시뮬레이션", $"{root.DebugFrameMilliseconds:0.00} ms (평균) / {root.DebugStepMilliseconds:0.000} ms");

            using (new EditorGUILayout.HorizontalScope())
            {
                spawnCount = Mathf.Max(1, EditorGUILayout.IntField("마릿수", spawnCount));
                if (GUILayout.Button(StateChange + "지금 스폰")) root.DebugSpawnEnemies(spawnCount);
            }
            if (GUILayout.Button(StateChange + "모두 제거")) root.DebugClearEnemies();
            bool paused = root.DebugSpawningPaused;
            if (GUILayout.Button(StateChange + (paused ? "스폰 재개" : "스폰 멈춤"))) root.DebugSetSpawningPaused(!paused);
        }

        void DrawWeapons(GameRoot root)
        {
            for (int i = 0; i < root.DebugWeaponCount; i++)
            {
                WeaponState weapon = root.DebugWeapon(i);
                EditorGUILayout.LabelField($"무기 {i}", $"{root.DebugWeaponId(weapon.Kind)}  간격 {root.DebugFireIntervalTicksNow(weapon.Kind)}틱(표 {root.DebugFireIntervalTicks(weapon.Kind)}) · 남은 {weapon.CooldownTicks}틱");
                EditorGUILayout.LabelField("  강화 반영 값", $"피해 {root.DebugWeaponValue(weapon.Kind, StatTarget.Damage):0.##} · 한 번에 {root.DebugWeaponValue(weapon.Kind, StatTarget.ShotsPerVolley):0} · 관통 {root.DebugWeaponValue(weapon.Kind, StatTarget.Pierce):0} · 탄 속도 {root.DebugWeaponValue(weapon.Kind, StatTarget.BulletSpeed):0.##} · 사거리 {root.DebugWeaponValue(weapon.Kind, StatTarget.Range):0.##}");
            }
            EditorGUILayout.LabelField("탄 수", $"{root.DebugBulletCount} / {root.DebugBulletCapacity}  (쏜 누계 {root.DebugBulletsFiredTotal})");
            EditorGUILayout.LabelField("처치 수", root.DebugEnemiesKilledTotal.ToString());
            EditorGUILayout.LabelField("번쩍이는 적", root.DebugFlashingEnemies.ToString());
            EditorGUILayout.LabelField("사격 · 적 무적", $"{(root.DebugFiringPaused ? "꺼짐" : "켜짐")} · {(root.DebugEnemiesInvincible ? "무적" : "보통")}");

            bool firingPaused = root.DebugFiringPaused;
            if (GUILayout.Button(StateChange + (firingPaused ? "사격 켜기" : "사격 끄기"))) root.DebugSetFiringPaused(!firingPaused);
            bool invincible = root.DebugEnemiesInvincible;
            if (GUILayout.Button(StateChange + (invincible ? "적 무적 끄기" : "적 무적 켜기"))) root.DebugSetEnemiesInvincible(!invincible);
            if (GUILayout.Button(StateChange + "탄 모두 제거")) root.DebugClearBullets();
        }

        void DrawExperience(GameRoot root)
        {
            EditorGUILayout.LabelField("레벨", root.DebugLevel.ToString());
            EditorGUILayout.LabelField("경험치", $"{root.DebugExperience} / {root.DebugExperienceToNext}  (누계 {root.DebugExperienceTotal})");
            EditorGUILayout.LabelField("젬", $"{root.DebugGemCount} / {root.DebugGemCapacity}  (떨어뜨린 누계 {root.DebugGemsDroppedTotal} · 주운 누계 {root.DebugGemsCollectedTotal})");
            EditorGUILayout.LabelField("플레이어(강화 반영)", $"이동 속도 {root.DebugMoveSpeed:0.##} · 줍는 반경 {root.DebugPickupRadius:0.##}");
            EditorGUILayout.LabelField("레벨업 대기", root.DebugPendingLevelUps > 0 ? $"{root.DebugPendingLevelUps}개 (시뮬레이션 멈춤)" : "없음");

            for (int i = 0; i < root.DebugChoiceCount; i++)
            {
                int row = root.DebugChoiceRow(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"선택지 {i + 1}", $"{root.DebugUpgradeId(row)}  {root.DebugUpgradeStage(row)} → {root.DebugUpgradeStage(row) + 1} / {root.DebugUpgradeMaxStage(row)}");
                    if (GUILayout.Button(StateChange + "고르기", GUILayout.Width(110f))) root.DebugPressCard(i);
                }
            }

            bool auto = root.DebugAutoChooseFirst;
            bool newAuto = EditorGUILayout.ToggleLeft(StateChange + "레벨업 창이 뜨면 첫 카드를 바로 고르기", auto);
            if (newAuto != auto) root.DebugAutoChooseFirst = newAuto;

            using (new EditorGUILayout.HorizontalScope())
            {
                experienceAmount = Mathf.Max(1, EditorGUILayout.IntField("경험치", experienceAmount));
                if (GUILayout.Button(StateChange + $"경험치 +{experienceAmount}")) root.DebugAddExperience(experienceAmount);
            }
            if (GUILayout.Button(StateChange + "레벨업 바로")) root.DebugLevelUpNow();

            EditorGUILayout.LabelField("걸려 있는 강화 (단계 / 최대)");
            for (int row = 0; row < root.DebugUpgradeRowCount; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int stage = root.DebugUpgradeStage(row);
                    int max = root.DebugUpgradeMaxStage(row);
                    EditorGUILayout.LabelField("  " + root.DebugUpgradeId(row), stage > 0 ? $"{stage} / {max}" : $"- / {max}");
                    using (new EditorGUI.DisabledScope(stage >= max))
                    {
                        if (GUILayout.Button(StateChange + "한 단계 걸기", GUILayout.Width(130f))) root.DebugApplyUpgrade(row);
                    }
                }
            }
            if (GUILayout.Button(StateChange + "강화 모두 초기화")) root.DebugResetUpgrades();
        }

        void DrawText(GameRoot root)
        {
            EditorGUILayout.LabelField("지금 언어", Text.Language.ToString());
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (Language language in Enum.GetValues(typeof(Language)))
                {
                    if (GUILayout.Button(language.ToString())) root.DebugSetLanguage(language);
                }
            }
        }
    }
}
