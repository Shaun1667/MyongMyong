using System.IO;
using System.Linq;
using Myong.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Myong.Editor
{
    /// <summary>
    /// 게임 씬 만들기 (메뉴 Myong/Setup/Build Game Scene, 몇 번을 돌려도 같은 결과).
    /// 이름으로 오브젝트를 찾아 없으면 만들고, 있으면 연결과 기준 값만 다시 맞춘 뒤 씬을 저장한다.
    /// </summary>
    public static class GameSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Game.unity";
        const string StringsPath = "Assets/_Game/Text/strings.csv";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        const string MoveActionName = "Player/Move";
        const string GroundTexturePath = "Assets/_Game/Art/Temp/ground_grid.png";
        const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string HelloKey = "ui.debug.hello";

        const float CameraZ = -10f;
        const int GroundSortingOrder = -1000;
        const int GroundTileDots = 64;
        const int GroundCellDots = 32;
        const int HudFontSize = 20;
        const float HudPadding = 12f;
        static readonly Color BackgroundColor = new Color32(0x2B, 0x32, 0x3C, 0xFF);
        static readonly Color32 GroundA = new Color32(0x5C, 0x86, 0x62, 0xFF);
        static readonly Color32 GroundB = new Color32(0x55, 0x7E, 0x5C, 0xFF);
        static readonly Color32 GroundLine = new Color32(0x4C, 0x72, 0x55, 0xFF);

        [MenuItem("Myong/Setup/Build Game Scene", priority = 21)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Myong Setup] Stop Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                SetupUtil.EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
                scene = File.Exists(ScenePath)
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            // 플레이어
            var player = Find(scene, "Player");
            if (player == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigBuilder.PrefabPath);
                if (prefab == null) { Debug.LogError("[Myong Setup] Player prefab missing. Run Myong/Setup/Build Player Rig first."); return; }
                player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                player.name = "Player";
            }
            var playerView = player.GetComponent<PlayerView>();

            // 카메라 (픽셀 퍼펙트)
            var cameraObject = FindOrCreate(scene, "Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = GetOrAdd<Camera>(cameraObject);
            camera.orthographic = true;
            camera.orthographicSize = PixelArt.ReferenceHeight / (2f * PixelArt.PixelsPerUnit);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            var cameraPosition = cameraObject.transform.position;
            cameraPosition.z = CameraZ;
            cameraObject.transform.position = cameraPosition;
            GetOrAdd<AudioListener>(cameraObject);
            GetOrAdd<UniversalAdditionalCameraData>(cameraObject);
            var pixelPerfect = GetOrAdd<PixelPerfectCamera>(cameraObject);
            pixelPerfect.assetsPPU = PixelArt.PixelsPerUnit;
            pixelPerfect.refResolutionX = PixelArt.ReferenceWidth;
            pixelPerfect.refResolutionY = PixelArt.ReferenceHeight;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            SetupUtil.SetReference(GetOrAdd<CameraFollow>(cameraObject), "target", player.transform);

            // 임시 바닥
            var groundObject = FindOrCreate(scene, "Ground");
            var groundRenderer = GetOrAdd<SpriteRenderer>(groundObject);
            groundRenderer.sprite = EnsureGroundSprite();
            groundRenderer.drawMode = SpriteDrawMode.Tiled;
            groundRenderer.tileMode = SpriteTileMode.Continuous;
            float tile = (float)GroundTileDots / PixelArt.PixelsPerUnit;
            float viewWidth = (float)PixelArt.ReferenceWidth / PixelArt.PixelsPerUnit;
            float viewHeight = (float)PixelArt.ReferenceHeight / PixelArt.PixelsPerUnit;
            // 화면의 두 배 넓이를 무늬 한 칸의 배수로 올림 — 어디로 가도 가장자리가 보이지 않는다.
            groundRenderer.size = new Vector2(Mathf.Ceil(viewWidth * 2f / tile) * tile, Mathf.Ceil(viewHeight * 2f / tile) * tile);
            groundRenderer.sortingOrder = GroundSortingOrder;
            var material = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            if (material != null) groundRenderer.sharedMaterial = material;
            SetupUtil.SetReference(GetOrAdd<GroundGrid>(groundObject), "follow", cameraObject.transform);

            // 컴포지션 루트 + 입력
            var rootObject = FindOrCreate(scene, "GameRoot");
            var input = GetOrAdd<PlayerInputReader>(rootObject);
            var moveAction = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath).OfType<InputActionReference>().FirstOrDefault(r => r.name == MoveActionName);
            if (moveAction == null) Debug.LogError($"[Myong Setup] input action '{MoveActionName}' not found in {InputActionsPath}");
            SetupUtil.SetReference(input, "move", moveAction);
            var gameRoot = GetOrAdd<GameRoot>(rootObject);
            SetupUtil.SetReference(gameRoot, "stringsCsv", AssetDatabase.LoadAssetAtPath<TextAsset>(StringsPath));
            SetupUtil.SetReference(gameRoot, "playerTuning", AssetDatabase.LoadAssetAtPath<PlayerTuning>(DataSetup.PlayerTuningPath));
            SetupUtil.SetReference(gameRoot, "simulationTuning", AssetDatabase.LoadAssetAtPath<SimulationTuning>(DataSetup.SimulationTuningPath));
            SetupUtil.SetReference(gameRoot, "input", input);
            SetupUtil.SetReference(gameRoot, "playerView", playerView);

            // HUD (텍스트 표 확인용 임시 라벨)
            var hud = FindOrCreate(scene, "HUD");
            var canvas = GetOrAdd<Canvas>(hud);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = GetOrAdd<UnityEngine.UI.CanvasScaler>(hud);
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(PixelArt.ReferenceWidth, PixelArt.ReferenceHeight);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var labelTransform = hud.transform.Find("HelloLabel");
            GameObject labelObject = labelTransform != null ? labelTransform.gameObject : new GameObject("HelloLabel", typeof(RectTransform));
            labelObject.transform.SetParent(hud.transform, false);
            var rect = (RectTransform)labelObject.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(HudPadding, -HudPadding);
            rect.sizeDelta = new Vector2(-2f * HudPadding, HudFontSize * 2f);
            var label = GetOrAdd<UnityEngine.UI.Text>(labelObject);
            if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = HudFontSize;
            label.color = Color.white;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var shadow = GetOrAdd<UnityEngine.UI.Shadow>(labelObject);
            shadow.effectColor = Color.black;
            SetupUtil.SetString(GetOrAdd<LocalizedLabel>(labelObject), "textKey", HelloKey);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            Debug.Log($"[Myong Setup] Game scene built: {ScenePath}");
        }

        static GameObject Find(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(o => o.name == name);
        }

        static GameObject FindOrCreate(Scene scene, string name)
        {
            var found = Find(scene, name);
            if (found != null) return found;
            var created = new GameObject(name);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        // 임시 바닥 무늬: 두 가지 밝기의 칸 + 칸마다 한 줄 테두리. 파일이 없을 때만 만든다.
        static Sprite EnsureGroundSprite()
        {
            if (!File.Exists(GroundTexturePath))
            {
                SetupUtil.EnsureFolder(Path.GetDirectoryName(GroundTexturePath).Replace('\\', '/'));
                var texture = new Texture2D(GroundTileDots, GroundTileDots, TextureFormat.RGBA32, false);
                var pixels = new Color32[GroundTileDots * GroundTileDots];
                for (int y = 0; y < GroundTileDots; y++)
                {
                    for (int x = 0; x < GroundTileDots; x++)
                    {
                        bool line = x % GroundCellDots == 0 || y % GroundCellDots == 0;
                        bool even = (x / GroundCellDots + y / GroundCellDots) % 2 == 0;
                        pixels[y * GroundTileDots + x] = line ? GroundLine : (even ? GroundA : GroundB);
                    }
                }
                texture.SetPixels32(pixels);
                File.WriteAllBytes(GroundTexturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(GroundTexturePath);
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(GroundTexturePath);
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
