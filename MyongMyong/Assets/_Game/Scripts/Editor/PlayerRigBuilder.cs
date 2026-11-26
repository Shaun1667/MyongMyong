using System;
using System.Collections.Generic;
using Myong.Unity;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Myong.Editor
{
    /// <summary>
    /// 주인공 컷아웃 리그 만들기 (메뉴 Myong/Setup/Build Player Rig, 몇 번을 돌려도 같은 결과).
    /// Parts/layout.json의 위치 · 관절(피벗) · 그리는 순서로 뼈(Transform) 계층을 만들고
    /// 파츠 스프라이트를 뼈마다 통째로 붙인다(메시 변형 없음). 애니메이션은 Transform 키만 쓴다.
    /// 움직임은 모두 도트 한 칸 단위(계단식 키)라 도트가 뭉개지지 않는다.
    /// 뼈 · 애니메이션을 고치려면 아래 Bones / Clips 표를 고치고 메뉴를 다시 돌린다.
    /// </summary>
    public static class PlayerRigBuilder
    {
        const string PartsFolder = "Assets/_Game/Art/Characters/Player/Parts";
        const string LayoutPath = PartsFolder + "/layout.json";
        const string AnimFolder = "Assets/_Game/Art/Characters/Player/Animations";
        const string ControllerPath = AnimFolder + "/Player.controller";
        public const string PrefabPath = "Assets/_Game/Prefabs/Player.prefab";
        const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        const string RootName = "Root";
        const string SpeedParam = "Speed";
        const string HitParam = "Hit";
        const string DeadParam = "Dead";
        // 맞은 순간의 흰색 번쩍임: 스프라이트를 한 색으로 칠하는 재질(없을 때만 만든다). PlayerView가 번쩍일 때만 파츠에 건다.
        const string FlashShaderPath = "Assets/_Game/Art/Shaders/SpriteFlash.shader";
        const string FlashMaterialPath = "Assets/_Game/Art/Shaders/SpriteFlash.mat";
        const float RunThreshold = 0.1f;

        // ---- layout.json ----
        [Serializable] sealed class Layout { public int feetY_BL; public Parts parts; }
        [Serializable] sealed class Parts { public Part hair_back, leg_left, leg_right, torso, arm_back, head, gun, arm_front; }
        [Serializable] sealed class Part { public int sortingOrder; public RectBL rectBL; public Vector2 pivotBL; }
        [Serializable] sealed class RectBL { public int x, y, w, h; }

        // ---- 뼈 표: 이름, 부모, 붙일 파츠(없으면 null), 관절 위치를 가져올 파츠 ----
        readonly struct Bone
        {
            public readonly string Name, Parent, SpritePart, PivotPart;
            public Bone(string name, string parent, string spritePart, string pivotPart) { Name = name; Parent = parent; SpritePart = spritePart; PivotPart = pivotPart; }
        }

        static readonly Bone[] Bones =
        {
            new Bone("Hips", RootName, null, "torso"),
            new Bone("Torso", "Hips", "torso", "torso"),
            new Bone("Head", "Torso", "head", "head"),
            new Bone("HairBack", "Head", "hair_back", "hair_back"),
            new Bone("ArmBack", "Torso", "arm_back", "arm_back"),
            new Bone("WeaponArms", "Torso", null, "gun"),
            new Bone("Gun", "WeaponArms", "gun", "gun"),
            new Bone("ArmFront", "WeaponArms", "arm_front", "arm_front"),
            new Bone("LegL", RootName, "leg_left", "leg_left"),
            new Bone("LegR", RootName, "leg_right", "leg_right"),
        };

        // ---- 애니메이션 표: 프레임마다 쉬는 자세에서 몇 칸(도트) 옮기고 몇 도 돌릴지 ----
        sealed class Track { public int[] X, Y; public float[] Rot; }

        sealed class Clip
        {
            public readonly string Name; public readonly int Frames; public readonly float Fps; public readonly bool Loop;
            public readonly Dictionary<string, Track> Tracks = new Dictionary<string, Track>();
            public Clip(string name, int frames, float fps, bool loop) { Name = name; Frames = frames; Fps = fps; Loop = loop; }
            Track Of(string bone) { if (!Tracks.TryGetValue(bone, out var t)) { t = new Track(); Tracks[bone] = t; } return t; }
            public Clip X(string bone, params int[] v) { Of(bone).X = v; return this; }
            public Clip Y(string bone, params int[] v) { Of(bone).Y = v; return this; }
            public Clip Rot(string bone, params float[] v) { Of(bone).Rot = v; return this; }
        }

        static Clip[] Clips() => new[]
        {
            // 대기: 숨쉬기. 몸통이 한 칸 내려갔다 올라오고, 머리 · 총은 한 박자, 뒷머리는 두 박자 늦게 따라온다.
            new Clip("Player_Idle", 8, 6f, true)
                .Y("Torso", 0, 0, 0, -1, -1, -1, -1, 0)
                .Y("Head", 0, 0, 0, 1, 0, 0, 0, -1)
                .Y("WeaponArms", 0, 0, 0, 1, 0, 0, 0, -1)
                .Y("ArmBack", 0, 0, 0, 1, 0, 0, 0, -1)
                .Y("HairBack", -1, 0, 0, 0, 1, 0, 0, 0)
                .X("HairBack", 0, 1, 1, 0, 0, -1, -1, 0),

            // 달리기: 다리는 한두 칸 들었다 놓으며 살짝 돌고(번갈아), 몸통은 통통, 뒷머리는 뒤로 날리고, 총은 살짝 흔들린다.
            // 참고 영상(2026-11-25)의 달리기: 한 주기 약 27프레임 @47.8fps = 0.57초 → 8프레임 14fps.
            // 한 다리의 순서(다른 다리는 4프레임 뒤): 앞으로 뻗어 디딘(낮게, 앞으로 기울임) → 몸 아래 → 밀어냄
            // → 뒤로 차올림(치마 뒤로 빠져나오게 뒤로 멀리, 2칸 들리고 65도까지 기울임) → 접어서 몸 아래로 → 앞으로 내뻗음.
            // 0 · 4 = 다리가 가장 벌어진 접촉, 1 · 5 = 내려앉음, 2 · 6 = 몸 아래로 모임, 3 · 7 = 두 발이 다 뜬 순간(몸통 가장 높음).
            // 다리 X는 쉬는 자세(좌우로 벌린 다리)에서 몸 가운데로 모은 값이라 크다. 머리는 1프레임, 뒷머리는 2프레임 늦게.
            new Clip("Player_Run", 8, 14f, true)
                .Y("Hips", -1, -2, 0, 1, -1, -2, 0, 1)
                .X("LegL", 11, 8, 3, -1, -3, 1, 6, 10).Y("LegL", 0, 0, 0, 2, 2, 3, 3, 2).Rot("LegL", 25, 8, -20, -45, -65, -40, -5, 25)
                .X("LegR", -15, -11, -6, -2, -1, -4, -9, -13).Y("LegR", 2, 3, 3, 2, 0, 0, 0, 2).Rot("LegR", -65, -40, -5, 25, 25, 8, -20, -45)
                .X("Head", 1, 1, 1, 1, 1, 1, 1, 1).Y("Head", 1, 1, -1, -1, 1, 1, -1, -1)
                .X("HairBack", -2, -3, -3, -2, -2, -3, -3, -2).Y("HairBack", 0, 2, 0, -2, 0, 2, 0, -2)
                .Y("WeaponArms", 0, 1, 0, 0, 0, 1, 0, 0)
                .Y("ArmBack", 0, 1, 0, 0, 0, 1, 0, 0),

            // 피격: 짧게 뒤로 움찔. (흰색 번쩍임 · 깜빡임은 PlayerView가 재질 · 색으로)
            new Clip("Player_Hit", 4, 12f, false)
                .X("Hips", -2, -3, -1, 0)
                .Y("Torso", -1, -1, 0, 0)
                .X("Head", -1, -1, 0, 0).Y("Head", -1, 0, 0, 0)
                .Y("WeaponArms", 1, 1, 0, 0).Rot("WeaponArms", 6, 3, 0, 0)
                .Y("ArmBack", 1, 1, 0, 0)
                .X("HairBack", 2, 1, 1, 0),

            // 사망: 뒤로 움찔 → 무릎이 꺾이며 뒤로 넘어가고 → 총이 손에서 빠져 발 앞에 떨어지고 → 누운 채 한 번 튕기고 멈춘다(반복 없음, 마지막 자세로 멈춤).
            // 몸은 Hips를 0 → 90도로 눕힌다(90도는 도트가 그대로다). 머리가 커서 누우면 머리 옆면이 바닥에 닿게 Hips가 올라간다.
            // 다리는 Root 아래라 따로 움직인다: 3프레임까지는 땅에 두고(치마 뒤), 4프레임부터 Hips가 돈 만큼 돌려 엉덩이 자리에 붙인다.
            // 총(Gun)은 2프레임에 손을 떠난다. 값은 눕는 몸(WeaponArms) 기준이라 크다 — 화면에서는 위로 떴다가 발 앞 바닥에 눕는다(끝 = 몸 기준 -90도 = 화면에서 똑바로).
            new Clip("Player_Death", 10, 12f, false)
                .X("Hips", -2, -3, -2, 0, 4, 8, 9, 10, 10, 10).Y("Hips", 0, -1, -3, -5, 8, 28, 31, 29, 28, 28).Rot("Hips", 0, 8, 20, 40, 65, 90, 90, 90, 90, 90)
                .X("LegL", -2, -3, -2, 1, 9, 16, 17, 18, 18, 18).Y("LegL", 0, 0, 0, 0, 2, 22, 25, 23, 22, 22).Rot("LegL", 0, 0, 5, 15, 65, 90, 100, 90, 90, 90)
                .X("LegR", -2, -3, -2, 1, 1, 2, 3, 4, 4, 4).Y("LegR", 0, 0, 0, 0, 15, 36, 39, 37, 36, 36).Rot("LegR", 0, 0, 5, 15, 70, 90, 105, 90, 90, 90)
                .Rot("Head", 0, -3, -6, -10, -8, 6, -3, 0, 0, 0)
                .X("HairBack", 0, 1, 1, 2, 2, 0, -1, 0, 0, 0)
                .Y("WeaponArms", 1, 1, 0, 0, 0, 0, 0, 0, 0, 0).Rot("WeaponArms", 6, 3, 0, 0, 0, 0, 0, 0, 0, 0)
                .X("Gun", 0, 0, 14, 27, 17, -23, -30, -26, -28, -28).Y("Gun", 0, 0, 5, 3, -19, -28, -30, -31, -32, -32).Rot("Gun", 0, 0, -5, 0, -45, -95, -82, -87, -90, -90)
                .Rot("ArmFront", 0, 0, -15, -30, -40, -20, -30, -20, -20, -20)
                .Rot("ArmBack", 0, 0, 15, 30, 40, 20, 30, 20, 20, 20),
        };

        [MenuItem("Myong/Setup/Build Player Rig", priority = 20)]
        public static void Build()
        {
            var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (layoutAsset == null) { Debug.LogError($"[Myong Setup] layout not found: {LayoutPath}"); return; }
            var layout = JsonUtility.FromJson<Layout>(layoutAsset.text);

            // 1. 스프라이트 피벗 = 관절 위치
            foreach (var bone in Bones)
            {
                if (bone.SpritePart != null) ApplyPivot(bone.SpritePart, PartOf(layout, bone.SpritePart));
            }

            // 2. 뼈 계층 (쉬는 자세)
            Vector2 rootPivot = new Vector2(layout.parts.torso.pivotBL.x, layout.feetY_BL);
            var pivots = new Dictionary<string, Vector2> { { RootName, rootPivot } };
            var paths = new Dictionary<string, string> { { RootName, RootName } };
            var rest = new Dictionary<string, Vector3>();
            foreach (var bone in Bones)
            {
                Vector2 pivot = PartOf(layout, bone.PivotPart).pivotBL;
                pivots[bone.Name] = pivot;
                paths[bone.Name] = paths[bone.Parent] + "/" + bone.Name;
                rest[bone.Name] = (Vector3)((pivot - pivots[bone.Parent]) / PixelArt.PixelsPerUnit);
            }

            // 3. 애니메이션 · 컨트롤러
            SetupUtil.EnsureFolder(AnimFolder);
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var def in Clips()) clips[def.Name] = BuildClip(def, paths, rest);
            var controller = BuildController(clips["Player_Idle"], clips["Player_Run"], clips["Player_Hit"], clips["Player_Death"]);
            Material flashMaterial = EnsureFlashMaterial();

            // 4. 프리팹
            var material = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            var player = new GameObject("Player");
            try
            {
                var nodes = new Dictionary<string, Transform>();
                var root = new GameObject(RootName).transform;
                root.SetParent(player.transform, false);
                nodes[RootName] = root;
                foreach (var bone in Bones)
                {
                    var node = new GameObject(bone.Name).transform;
                    node.SetParent(nodes[bone.Parent], false);
                    node.localPosition = rest[bone.Name];
                    nodes[bone.Name] = node;
                    if (bone.SpritePart == null) continue;

                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PartPath(bone.SpritePart));
                    if (sprite == null) Debug.LogError($"[Myong Setup] sprite not found: {PartPath(bone.SpritePart)}");
                    var renderer = node.gameObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sortingOrder = PartOf(layout, bone.SpritePart).sortingOrder;
                    if (material != null) renderer.sharedMaterial = material;
                }

                player.AddComponent<SortingGroup>();
                var animator = player.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                var view = player.AddComponent<PlayerView>();
                SetupUtil.SetReference(view, "flipRoot", root);
                SetupUtil.SetReference(view, "animator", animator);
                SetupUtil.SetReference(view, "flashMaterial", flashMaterial);

                SetupUtil.EnsureFolder(System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Myong Setup] Player rig built: {PrefabPath}");
        }

        static string PartPath(string part) => PartsFolder + "/" + part + ".png";

        static Part PartOf(Layout layout, string name)
        {
            var field = typeof(Parts).GetField(name);
            var part = field != null ? (Part)field.GetValue(layout.parts) : null;
            if (part == null) throw new InvalidOperationException($"layout.json has no part '{name}'");
            return part;
        }

        static void ApplyPivot(string partName, Part part)
        {
            string path = PartPath(partName);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogError($"[Myong Setup] texture not found: {path}"); return; }

            var pivot = new Vector2(
                (part.pivotBL.x - part.rectBL.x) / part.rectBL.w,
                (part.pivotBL.y - part.rectBL.y) / part.rectBL.h);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool same = importer.textureType == TextureImporterType.Sprite
                && settings.spriteMode == (int)SpriteImportMode.Single
                && settings.spriteAlignment == (int)SpriteAlignment.Custom
                && Mathf.Approximately(settings.spritePivot.x, pivot.x)
                && Mathf.Approximately(settings.spritePivot.y, pivot.y);
            if (same) return;

            // 파츠 하나 = 스프라이트 하나(Single). Multiple이면 피벗이 시트 쪽에 따로 저장돼 여기 값이 먹지 않는다.
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static AnimationClip BuildClip(Clip def, Dictionary<string, string> paths, Dictionary<string, Vector3> rest)
        {
            string path = AnimFolder + "/" + def.Name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.frameRate = def.Fps;

            float unit = 1f / PixelArt.PixelsPerUnit;
            // 모든 뼈에 키를 넣는다(안 움직이는 뼈는 쉬는 자세) — 클립이 바뀔 때 앞 클립의 자세가 남지 않게.
            foreach (var bone in Bones)
            {
                def.Tracks.TryGetValue(bone.Name, out var track);
                Vector3 r = rest[bone.Name];
                string bonePath = paths[bone.Name];
                SetCurve(clip, def, bonePath, "m_LocalPosition.x", i => r.x + Value(track?.X, i) * unit);
                SetCurve(clip, def, bonePath, "m_LocalPosition.y", i => r.y + Value(track?.Y, i) * unit);
                SetCurve(clip, def, bonePath, "m_LocalPosition.z", i => r.z);
                SetCurve(clip, def, bonePath, "localEulerAnglesRaw.x", i => 0f);
                SetCurve(clip, def, bonePath, "localEulerAnglesRaw.y", i => 0f);
                SetCurve(clip, def, bonePath, "localEulerAnglesRaw.z", i => track?.Rot != null ? track.Rot[i] : 0f);
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = def.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static float Value(int[] values, int frame) => values != null ? values[frame] : 0f;

        static void SetCurve(AnimationClip clip, Clip def, string bonePath, string property, Func<int, float> valueAt)
        {
            // 프레임마다 키 하나 + 끝 키(반복이면 첫 프레임 값, 아니면 마지막 값). 키 사이는 계단식(보간 없음).
            var keys = new Keyframe[def.Frames + 1];
            for (int i = 0; i < def.Frames; i++) keys[i] = new Keyframe(i / def.Fps, valueAt(i));
            keys[def.Frames] = new Keyframe(def.Frames / def.Fps, valueAt(def.Loop ? 0 : def.Frames - 1));
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), property), curve);
        }

static AnimatorController BuildController(AnimationClip idle, AnimationClip run, AnimationClip hit, AnimationClip death)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            else if (IsUpToDate(controller, idle, run, hit, death)) return controller; // 같으면 손대지 않는다(다시 만들면 파일 안의 ID가 바뀌어 차이가 생긴다)

            // 비우고 다시 채운다(에셋 자체는 그대로라 프리팹의 연결이 유지된다).
            for (int i = controller.parameters.Length - 1; i >= 0; i--) controller.RemoveParameter(i);
            var machine = controller.layers[0].stateMachine;
            foreach (var t in machine.anyStateTransitions) machine.RemoveAnyStateTransition(t);
            foreach (var s in machine.states) machine.RemoveState(s.state);

            controller.AddParameter(SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(HitParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(DeadParam, AnimatorControllerParameterType.Bool);

            var idleState = machine.AddState("Idle");
            idleState.motion = idle;
            var runState = machine.AddState("Run");
            runState.motion = run;
            var hitState = machine.AddState("Hit");
            hitState.motion = hit;
            var deathState = machine.AddState("Death");
            deathState.motion = death;
            machine.defaultState = idleState;

            var toRun = idleState.AddTransition(runState);
            Instant(toRun, false);
            toRun.AddCondition(AnimatorConditionMode.Greater, RunThreshold, SpeedParam);

            var toIdle = runState.AddTransition(idleState);
            Instant(toIdle, false);
            toIdle.AddCondition(AnimatorConditionMode.Less, RunThreshold, SpeedParam);

            // 사망이 먼저(어느 상태에서든 Dead가 켜지면). 사망 중에는 피격으로 넘어가지 않는다. 끝나면 마지막 자세로 멈춰 있다.
            var toDeath = machine.AddAnyStateTransition(deathState);
            Instant(toDeath, false);
            toDeath.canTransitionToSelf = false;
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, DeadParam);

            var toHit = machine.AddAnyStateTransition(hitState);
            Instant(toHit, false);
            toHit.canTransitionToSelf = true;
            toHit.AddCondition(AnimatorConditionMode.If, 0f, HitParam);
            toHit.AddCondition(AnimatorConditionMode.IfNot, 0f, DeadParam);

            var hitDone = hitState.AddTransition(idleState);
            Instant(hitDone, true);

            // 다시 시작하면(Dead가 꺼지면) 바로 대기로.
            var revive = deathState.AddTransition(idleState);
            Instant(revive, false);
            revive.AddCondition(AnimatorConditionMode.IfNot, 0f, DeadParam);

            EditorUtility.SetDirty(controller);
            return controller;
        }

static bool IsUpToDate(AnimatorController controller, AnimationClip idle, AnimationClip run, AnimationClip hit, AnimationClip death)
        {
            var parameters = controller.parameters;
            if (parameters.Length != 3 || parameters[0].name != SpeedParam || parameters[1].name != HitParam || parameters[2].name != DeadParam) return false;
            var machine = controller.layers[0].stateMachine;
            var states = machine.states;
            var any = machine.anyStateTransitions;
            if (states.Length != 4 || any.Length != 2) return false;
            if (any[0].destinationState == null || any[0].destinationState.name != "Death" || any[0].conditions.Length != 1 || any[0].canTransitionToSelf) return false;
            if (any[1].destinationState == null || any[1].destinationState.name != "Hit" || any[1].conditions.Length != 2) return false;
            foreach (var child in states)
            {
                var state = child.state;
                Motion expected = state.name == "Idle" ? idle : state.name == "Run" ? run : state.name == "Hit" ? hit : state.name == "Death" ? death : null;
                if (expected == null || state.motion != expected || state.transitions.Length != 1) return false;
                var transition = state.transitions[0];
                if (transition.duration != 0f) return false;
                bool moves = state.name == "Idle" || state.name == "Run";
                if (moves && (transition.conditions.Length != 1 || transition.conditions[0].threshold != RunThreshold)) return false;
                if (state.name == "Death" && (transition.conditions.Length != 1 || transition.conditions[0].parameter != DeadParam)) return false;
            }
            return machine.defaultState != null && machine.defaultState.name == "Idle";
        }

static Material EnsureFlashMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
            if (material != null) return material;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(FlashShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[Myong Setup] shader not found: {FlashShaderPath}");
                return null;
            }
            material = new Material(shader) { name = "SpriteFlash" };
            AssetDatabase.CreateAsset(material, FlashMaterialPath);
            return material;
        }


        
// 도트 애니메이션은 섞지 않고 바로 바꾼다.
        static void Instant(AnimatorStateTransition transition, bool waitForEnd)
        {
            transition.hasExitTime = waitForEnd;
            transition.exitTime = 1f;
            transition.duration = 0f;
            transition.hasFixedDuration = true;
        }
    }
}
