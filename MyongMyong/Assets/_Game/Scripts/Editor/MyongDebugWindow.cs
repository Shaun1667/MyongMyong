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
