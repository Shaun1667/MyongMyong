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
        int kindSpawnCount = 10;
        int kindToSpawn;
        int experienceAmount = 5;
        float damageAmount = 10f;
        float skipBeforeGoalSeconds = 10f;
        int skipMinutes = 29;
        int skipSeconds = 50;

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
                new Section { Title = "판 시간 · 승리", Draw = DrawRunTime },
                new Section { Title = "플레이어", Draw = DrawPlayer },
                new Section { Title = "가상 조이스틱", Draw = DrawJoystick },
                new Section { Title = "적", Draw = DrawEnemies },
                new Section { Title = "무기", Draw = DrawWeapons },
                new Section { Title = "경험치 / 레벨", Draw = DrawExperience },
                new Section { Title = "패시브", Draw = DrawPassives },
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
            if (root.DebugIsOver)
            {
                RunResult result = root.DebugResult;
                EditorGUILayout.LabelField("판 끝", $"{result.Outcome}  생존 {result.Ticks}틱 ({root.DebugSurvivedSeconds:0.00}초) · 레벨 {result.Level} · 처치 {result.EnemiesKilled}  (창 {(root.DebugGameOverOpen ? "떠 있음" : "없음")})");
                if (GUILayout.Button(StateChange + "[다시 시작] 누르기 (새 시드)")) root.DebugPressRestart();
            }
            else
            {
                EditorGUILayout.LabelField("판 끝", "진행 중");
            }
            if (GUILayout.Button(StateChange + "재시작 (같은 시드)")) root.DebugRestart();
            using (new EditorGUILayout.HorizontalScope())
            {
                seedInput = EditorGUILayout.IntField("새 시드", seedInput);
                if (GUILayout.Button(StateChange + "이 시드로 재시작")) root.DebugRestartWithSeed(seedInput);
            }
        }

        void DrawRunTime(GameRoot root)
        {
            float goal = root.DebugSurvivalSeconds;
            long ticks = root.DebugRunTicks;
            EditorGUILayout.LabelField("판 시간", $"{ticks}틱 ({root.DebugElapsedSeconds:0.00}초) · HUD '{root.DebugTimerText}'");
            EditorGUILayout.LabelField("생존 목표", goal > 0f ? $"{goal:0}초 ({(int)goal / 60:00}:{(int)goal % 60:00}) · 남은 {Mathf.Max(0f, goal - root.DebugSurvivedSeconds):0.0}초" : "없음 (RunTuning이 없거나 0)");
            EditorGUILayout.LabelField("레벨업 선택 중", root.DebugPendingLevelUps > 0 ? "예 (타이머 멈춤)" : "아니오");
            if (root.DebugIsOver) EditorGUILayout.LabelField("판 끝 창", $"'{root.DebugGameOverTitle}' · '{root.DebugGameOverTime}'");

            using (new EditorGUI.DisabledScope(root.DebugIsOver || goal <= 0f))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    skipBeforeGoalSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("목표 몇 초 전", skipBeforeGoalSeconds));
                    if (GUILayout.Button(StateChange + "목표 전으로 건너뛰기")) root.DebugSkipToBeforeGoal(skipBeforeGoalSeconds);
                }
            }
            using (new EditorGUI.DisabledScope(root.DebugIsOver))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    skipMinutes = Mathf.Max(0, EditorGUILayout.IntField("분", skipMinutes));
                    skipSeconds = Mathf.Clamp(EditorGUILayout.IntField("초", skipSeconds), 0, 59);
                    if (GUILayout.Button(StateChange + $"{skipMinutes:00}:{skipSeconds:00}로 건너뛰기 (앞으로만)")) root.DebugSkipToSeconds(skipMinutes * 60f + skipSeconds);
                }
                if (GUILayout.Button(StateChange + "바로 승리")) root.DebugVictoryNow();
            }
        }

        void DrawPlayer(GameRoot root)
        {
            Vec2 position = root.DebugPlayerPosition;
            Vec2 velocity = root.DebugPlayerVelocity;
            EditorGUILayout.LabelField("위치", position.ToString());
            EditorGUILayout.LabelField("속도", $"{velocity}  |v| = {velocity.Magnitude:0.###}");
            EditorGUILayout.LabelField("방향", root.DebugPlayerFacingX > 0 ? "오른쪽" : "왼쪽");
            EditorGUILayout.LabelField("체력", $"{root.DebugPlayerHealth:0.#} / {root.DebugPlayerMaxHealth:0.#}  (바 {root.DebugHealthBarDots}도트)");
            EditorGUILayout.LabelField("무적", $"{(root.DebugPlayerInvincible ? "디버그 무적" : "보통")} · 맞은 뒤 무적 남은 {root.DebugInvulnerableTicksLeft}틱 (한 번 {root.DebugHurtInvulnerableTicks}틱)");
            EditorGUILayout.LabelField("받은 피해", $"{root.DebugHitsTaken}번 · 합 {root.DebugDamageTakenTotal:0.#}  (번쩍임 {(root.DebugPlayerFlashing ? "켜짐" : "꺼짐")})");
            bool playerInvincible = root.DebugPlayerInvincible;
            if (GUILayout.Button(StateChange + (playerInvincible ? "플레이어 무적 끄기" : "플레이어 무적 켜기"))) root.DebugSetPlayerInvincible(!playerInvincible);
            if (GUILayout.Button(StateChange + "체력 채우기")) root.DebugFillHealth();
            using (new EditorGUILayout.HorizontalScope())
            {
                damageAmount = Mathf.Max(0f, EditorGUILayout.FloatField("피해", damageAmount));
                if (GUILayout.Button(StateChange + $"피해 {damageAmount:0.#}")) root.DebugDamagePlayer(damageAmount);
            }
            if (GUILayout.Button(StateChange + "바로 게임 오버")) root.DebugGameOverNow();

            // 피격 · 사망 연출(1-10)
            EditorGUILayout.LabelField("연출", $"{(root.DebugPlayerFlashing ? "흰색 번쩍임" : root.DebugPlayerBlinkDim ? "깜빡임(흐림)" : "보통")} · 사망 {(root.DebugPlayerDead ? $"{root.DebugDeathSeconds:0.00}초 (애니메이션 {(root.DebugDeathAnimationEnded ? "끝" : "재생 중")} · 창 {(root.DebugGameOverOpen ? "떠 있음" : "아직")})" : "아님")}");
            using (new EditorGUI.DisabledScope(root.DebugIsOver))
            {
                if (GUILayout.Button(StateChange + $"플레이어 피격 흉내 (피해 {damageAmount:0.#} + 무적 시간)")) root.DebugSimulateHit(damageAmount);
                if (GUILayout.Button(StateChange + "플레이어 사망 흉내 (체력 0 → 사망 애니메이션 → 게임 오버 창)")) root.DebugKillPlayer();
            }

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

        void DrawJoystick(GameRoot root)
        {
            if (!root.DebugHasJoystick)
            {
                EditorGUILayout.HelpBox("씬에 조이스틱이 없습니다. Myong/Setup/Build Game Scene을 돌리세요.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("상태", $"{(root.DebugJoystickActive ? "눌림" : "안 눌림")} · {(root.DebugJoystickBlocked ? "막힘(레벨업 · 판 끝 창)" : "받음")}");
            EditorGUILayout.LabelField("조이스틱 값", root.DebugJoystickValue.ToString());
            EditorGUILayout.LabelField("Move 액션 값", root.DebugMoveInput.ToString());
            EditorGUILayout.LabelField(StateChange + "손가락 흉내 (화면 왼쪽 1/4 자리를 눌러 그 방향으로 화면 높이의 1/4만큼 끌기)");
            var camera = Camera.main;
            float width = camera != null ? camera.pixelWidth : 960f;
            float height = camera != null ? camera.pixelHeight : 540f;
            float x0 = width * 0.25f;
            float y0 = height * 0.5f;
            float reach = height * 0.25f;
            for (int y = 1; y >= -1; y--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                    for (int x = -1; x <= 1; x++)
                    {
                        string name = x == 0 && y == 0 ? "떼기" : $"{x},{y}";
                        if (!GUILayout.Button(name, GUILayout.Width(48f))) continue;
                        root.DebugJoystickRelease();
                        if (x == 0 && y == 0) continue;
                        root.DebugJoystickPress(x0, y0);
                        root.DebugJoystickDrag(x0 + x * reach, y0 + y * reach);
                    }
                }
            }
        }

        void DrawEnemies(GameRoot root)
        {
            EditorGUILayout.LabelField("살아 있는 수", $"{root.DebugEnemyCount} / {root.DebugEnemyCapacity}  (돌진 중 {root.DebugChargingCount})");
            EditorGUILayout.LabelField("스폰 누계", $"{root.DebugEnemiesSpawnedTotal} · 돌진하다 사라짐 {root.DebugEnemiesEscapedTotal}");
            EditorGUILayout.LabelField("경과 시간", $"{root.DebugElapsedSeconds:0.0}초");
            EditorGUILayout.LabelField("시간대 스폰", root.DebugSpawningPaused ? "멈춤" : "돌아감");
            int phaseIndex = root.DebugPhaseIndex;
            if (phaseIndex >= 0)
            {
                SpawnPhaseData phase = root.DebugPhase(phaseIndex);
                var mix = new System.Text.StringBuilder();
                foreach (var w in phase.Mix)
                {
                    if (mix.Length > 0) mix.Append(" · ");
                    mix.Append($"{root.DebugEnemyKindId(w.Kind)} {w.Weight / phase.TotalWeight * 100f:0}%");
                }
                EditorGUILayout.LabelField("지금 시간대", $"{phaseIndex} ({phase.StartSeconds:0}초부터) 초당 {phase.SpawnsPerSecond:0.##}마리 — {mix}");
            }
            for (int w = 0; w < root.DebugWaveCount; w++)
            {
                SpawnWaveData wave = root.DebugWave(w);
                long next = root.DebugNextWaveTick(w);
                string when = next < 0 ? "끝" : $"틱 {next} ({next / 60f:0.0}초)";
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"웨이브 {w}", $"{root.DebugEnemyKindId(wave.Kind)} ×{wave.Count} {wave.Pattern} · {wave.StartSeconds:0}초부터 {wave.IntervalSeconds:0}초마다 · 다음 {when}");
                    if (GUILayout.Button(StateChange + "지금", GUILayout.Width(110f))) root.DebugSpawnWaveNow(w);
                }
            }
            EditorGUILayout.LabelField("프레임 / 시뮬레이션", $"{root.DebugFrameMilliseconds:0.00} ms (평균) / {root.DebugStepMilliseconds:0.000} ms");

            // 종류별: 지금 수 · 스폰 누계 · 처치 누계
            int kinds = root.DebugEnemyKindCount;
            var names = new string[kinds];
            for (int k = 0; k < kinds; k++)
            {
                names[k] = root.DebugEnemyKindId(k);
                EditorGUILayout.LabelField($"  {names[k]}", $"지금 {root.DebugAliveOfKind(k)} · 스폰 {root.DebugSpawnedOfKind(k)} · 처치 {root.DebugKilledOfKind(k)}");
            }
            EditorGUILayout.LabelField("큰 젬(화면)", root.DebugBigGemsShown.ToString());
            if (kinds > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    kindToSpawn = Mathf.Clamp(EditorGUILayout.Popup("종류", kindToSpawn, names), 0, kinds - 1);
                    kindSpawnCount = Mathf.Max(1, EditorGUILayout.IntField(kindSpawnCount, GUILayout.Width(60f)));
                    if (GUILayout.Button(StateChange + "이 종류 소환")) root.DebugSpawnEnemiesOfKind(kindToSpawn, kindSpawnCount);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                spawnCount = Mathf.Max(1, EditorGUILayout.IntField("마릿수", spawnCount));
                if (GUILayout.Button(StateChange + "지금 스폰 (시간대 비중)")) root.DebugSpawnEnemies(spawnCount);
            }
            if (GUILayout.Button(StateChange + "모두 제거")) root.DebugClearEnemies();
            EditorGUILayout.LabelField("연출", $"번쩍이는 적 {root.DebugFlashingEnemies} · 소멸 연출 {root.DebugDeathEffectsShown} / {root.DebugDeathEffectCapacity} (누계 {root.DebugDeathEffectsStarted})");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("적 번쩍임 보기")) root.DebugPreviewEnemyFlash();
                if (GUILayout.Button("적 소멸 연출 보기")) root.DebugPreviewEnemyDeaths();
            }
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
                    EditorGUILayout.LabelField($"선택지 {i + 1}", ChoiceLabel(root, i));
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

        static string ChoiceLabel(GameRoot root, int index)
        {
            LevelUpOption o = root.DebugChoiceOption(index);
            switch (o.Kind)
            {
                case LevelUpOptionKind.Upgrade:
                    return $"무기 강화 {root.DebugUpgradeId(o.Row)}  {root.DebugUpgradeStage(o.Row)} → {root.DebugUpgradeStage(o.Row) + 1} / {root.DebugUpgradeMaxStage(o.Row)}";
                case LevelUpOptionKind.Passive:
                    return $"패시브 {root.DebugPassiveId(o.Row)}  {root.DebugPassiveStage(o.Row)} → {root.DebugPassiveStage(o.Row) + 1} / {root.DebugPassiveMaxStage(o.Row)}";
                default:
                    return "대체 보상 (체력 회복)";
            }
        }

        void DrawPassives(GameRoot root)
        {
            EditorGUILayout.LabelField("가진 패시브", $"{root.DebugPassiveOwnedCount} / {root.DebugPassiveMaxOwned}  (HUD 아이콘 {root.DebugPassiveHudShown})");
            for (int i = 0; i < root.DebugPassiveOwnedCount; i++)
            {
                int row = root.DebugPassiveOwnedRow(i);
                EditorGUILayout.LabelField($"  {i + 1}. {root.DebugPassiveId(row)}", $"{root.DebugPassiveStage(row)} / {root.DebugPassiveMaxStage(row)}");
            }
            EditorGUILayout.LabelField("플레이어(패시브 반영)", $"이동 {root.DebugMoveSpeed:0.##} · 반경 {root.DebugPickupRadius:0.##} · 최대 체력 {root.DebugPlayerMaxHealth:0.#}");
            EditorGUILayout.LabelField(" ", $"재생 {root.DebugHealthRegen:0.##}/초 · 받는 피해 x{root.DebugDamageTakenScale:0.###} · 경험치 x{root.DebugExperienceGain:0.###}");
            EditorGUILayout.LabelField("회복 · 대체 보상", $"회복 합 {root.DebugHealedTotal:0.#} · 대체 보상 {root.DebugFallbackRewardsTaken}번");

            EditorGUILayout.LabelField("패시브 표 (단계 / 최대)");
            for (int row = 0; row < root.DebugPassiveRowCount; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int stage = root.DebugPassiveStage(row);
                    int max = root.DebugPassiveMaxStage(row);
                    EditorGUILayout.LabelField("  " + root.DebugPassiveId(row), stage > 0 ? $"{stage} / {max}" : $"- / {max}");
                    using (new EditorGUI.DisabledScope(!root.DebugPassiveOfferable(row)))
                    {
                        if (GUILayout.Button(StateChange + (stage > 0 ? "단계 올리기" : "패시브 주기"), GUILayout.Width(150f))) root.DebugApplyPassive(row);
                    }
                }
            }
            EditorGUILayout.LabelField("(강화 · 패시브 모두 풀기 = 경험치 / 레벨 섹션의 [강화 모두 초기화])");
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
