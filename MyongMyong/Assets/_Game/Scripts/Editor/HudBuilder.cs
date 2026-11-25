using Myong.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Myong.Editor
{
    /// <summary>
    /// HUD의 경험치 바 · 레벨 숫자 · 레벨업 창(카드)과 EventSystem을 만든다(GameSceneBuilder가 부른다, 몇 번을 돌려도 같은 결과).
    /// 이름으로 찾아 없으면 만들고, 있으면 배치 · 연결만 다시 맞춘다. 글은 넣지 않는다(화면 글은 텍스트 표, 코드가 채운다).
    /// </summary>
    public static class HudBuilder
    {
        public const int CardCount = 3;

        const float BarHeight = 10f;
        const float LevelFontSize = 18f;
        const float Padding = 12f;
        const float CardWidth = 220f;
        const float CardHeight = 260f;
        const float CardGap = 24f;
        const int TitleFontSize = 36;
        const int CardTitleFontSize = 22;
        const int BodyFontSize = 16;
        const int KeyFontSize = 18;

        static readonly Color BarBack = new Color(0.08f, 0.1f, 0.14f, 0.85f);
        static readonly Color BarFill = new Color(0.4f, 0.82f, 1f, 1f);
        static readonly Color Dim = new Color(0f, 0f, 0f, 0.62f);
        static readonly Color TitleColor = new Color(1f, 0.92f, 0.5f, 1f);
        static readonly Color BodyColor = new Color(0.88f, 0.9f, 0.95f, 1f);
        static readonly Color StageColor = new Color(0.7f, 0.86f, 1f, 1f);
        static readonly Color CardNormal = new Color(0.17f, 0.19f, 0.26f, 0.97f);
        static readonly Color CardHighlighted = new Color(0.27f, 0.3f, 0.41f, 1f);
        static readonly Color CardSelected = new Color(0.42f, 0.35f, 0.16f, 1f);
        static readonly Color CardPressed = new Color(0.55f, 0.46f, 0.2f, 1f);

        public static (ExperienceHud hud, LevelUpView levelUp) Build(GameObject hudRoot)
        {
            GetOrAdd<UnityEngine.UI.GraphicRaycaster>(hudRoot);

            // 경험치 바 (화면 맨 위)
            var bar = Child(hudRoot.transform, "ExperienceBar");
            Place(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, BarHeight));
            Image(bar.gameObject, BarBack, false);
            var fill = Child(bar, "Fill");
            Place(fill, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            Image(fill.gameObject, BarFill, false);

            var level = Child(hudRoot.transform, "LevelLabel");
            Place(level, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Padding, -(BarHeight + 4f)), new Vector2(160f, LevelFontSize * 1.6f));
            var levelText = Label(level.gameObject, (int)LevelFontSize, TextAnchor.UpperRight, Color.white);

            var hud = GetOrAdd<ExperienceHud>(hudRoot);
            SetupUtil.SetReference(hud, "fill", fill);
            SetupUtil.SetReference(hud, "levelLabel", levelText);

            // 레벨업 창
            var levelUpRoot = Child(hudRoot.transform, "LevelUp");
            Stretch(levelUpRoot);
            levelUpRoot.SetAsLastSibling(); // 다른 HUD 위에
            var view = GetOrAdd<LevelUpView>(levelUpRoot.gameObject);
            var panel = Child(levelUpRoot, "Panel");
            Stretch(panel);
            Image(panel.gameObject, Dim, true);

            var title = Child(panel, "Title");
            Place(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(600f, 52f));
            var titleText = Label(title.gameObject, TitleFontSize, TextAnchor.MiddleCenter, TitleColor);

            var remaining = Child(panel, "Remaining");
            Place(remaining, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(600f, 24f));
            var remainingText = Label(remaining.gameObject, BodyFontSize, TextAnchor.MiddleCenter, BodyColor);

            var hint = Child(panel, "Hint");
            Place(hint, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(800f, 24f));
            var hintText = Label(hint.gameObject, BodyFontSize, TextAnchor.MiddleCenter, BodyColor);

            var cards = Child(panel, "Cards");
            Place(cards, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(CardCount * CardWidth + (CardCount - 1) * CardGap, CardHeight));

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("title").objectReferenceValue = titleText;
            so.FindProperty("hint").objectReferenceValue = hintText;
            so.FindProperty("remaining").objectReferenceValue = remainingText;
            var cardsProperty = so.FindProperty("cards");
            cardsProperty.arraySize = CardCount;
            for (int i = 0; i < CardCount; i++)
            {
                var card = Child(cards, "Card" + (i + 1));
                float x = (i - (CardCount - 1) * 0.5f) * (CardWidth + CardGap);
                Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(CardWidth, CardHeight));
                var background = Image(card.gameObject, Color.white, true);
                var button = GetOrAdd<UnityEngine.UI.Button>(card.gameObject);
                button.targetGraphic = background;
                var colors = button.colors;
                colors.normalColor = CardNormal;
                colors.highlightedColor = CardHighlighted;
                colors.selectedColor = CardSelected;
                colors.pressedColor = CardPressed;
                colors.colorMultiplier = 1f;
                button.colors = colors;

                var key = Child(card, "Key");
                Place(key, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(60f, 24f));
                var keyText = Label(key.gameObject, KeyFontSize, TextAnchor.UpperLeft, TitleColor);

                var name = Child(card, "Name");
                Place(name, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(-16f, 32f));
                var nameText = Label(name.gameObject, CardTitleFontSize, TextAnchor.MiddleCenter, Color.white);

                var description = Child(card, "Description");
                Place(description, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(-24f, 96f));
                var descriptionText = Label(description.gameObject, BodyFontSize, TextAnchor.MiddleCenter, BodyColor);

                var stage = Child(card, "Stage");
                Place(stage, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-16f, 24f));
                var stageText = Label(stage.gameObject, BodyFontSize, TextAnchor.MiddleCenter, StageColor);

                var element = cardsProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("button").objectReferenceValue = button;
                element.FindPropertyRelative("title").objectReferenceValue = nameText;
                element.FindPropertyRelative("description").objectReferenceValue = descriptionText;
                element.FindPropertyRelative("stage").objectReferenceValue = stageText;
                element.FindPropertyRelative("key").objectReferenceValue = keyText;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            // 창은 Play 중 레벨업 때만 보인다(LevelUpView가 켠다).
            panel.gameObject.SetActive(false);

            return (hud, view);
        }

        const float ResultWidth = 420f;
        const float ResultLineHeight = 30f;
        const int ResultFontSize = 20;
        const float RestartWidth = 220f;
        const float RestartHeight = 52f;
        const int RestartFontSize = 22;
        static readonly Color GameOverDim = new Color(0.05f, 0f, 0.02f, 0.72f);
        static readonly Color GameOverTitleColor = new Color(1f, 0.42f, 0.38f, 1f);
        static readonly Color GameOverBox = new Color(0.1f, 0.09f, 0.14f, 0.94f);

        /// <summary>
        /// 판 끝 창(제목 · 생존 시간 · 레벨 · 처치 수 · [다시 시작]). 레벨업 창보다 위(맨 끝 형제). Build 다음에 부른다.
        /// </summary>
        public static GameOverView BuildGameOver(GameObject hudRoot)
        {
            var root = Child(hudRoot.transform, "GameOver");
            Stretch(root);
            root.SetAsLastSibling();
            var view = GetOrAdd<GameOverView>(root.gameObject);
            var panel = Child(root, "Panel");
            Stretch(panel);
            Image(panel.gameObject, GameOverDim, true);

            // 글 뒤 상자(가운데 있는 플레이어 위에서도 읽히게). 맨 앞 형제 = 가장 뒤.
            var box = Child(panel, "Box");
            Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(500f, 330f));
            Image(box.gameObject, GameOverBox, false);
            box.SetAsFirstSibling();

            var title = Child(panel, "Title");
            Place(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(600f, 56f));
            var titleText = Label(title.gameObject, TitleFontSize, TextAnchor.MiddleCenter, GameOverTitleColor);

            var time = Child(panel, "Time");
            Place(time, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(ResultWidth, ResultLineHeight));
            var timeText = Label(time.gameObject, ResultFontSize, TextAnchor.MiddleCenter, Color.white);

            var level = Child(panel, "Level");
            Place(level, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f - ResultLineHeight), new Vector2(ResultWidth, ResultLineHeight));
            var levelText = Label(level.gameObject, ResultFontSize, TextAnchor.MiddleCenter, Color.white);

            var kills = Child(panel, "Kills");
            Place(kills, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f - 2f * ResultLineHeight), new Vector2(ResultWidth, ResultLineHeight));
            var killsText = Label(kills.gameObject, ResultFontSize, TextAnchor.MiddleCenter, Color.white);

            var restart = Child(panel, "Restart");
            Place(restart, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(RestartWidth, RestartHeight));
            var background = Image(restart.gameObject, Color.white, true);
            var button = GetOrAdd<UnityEngine.UI.Button>(restart.gameObject);
            button.targetGraphic = background;
            var colors = button.colors;
            colors.normalColor = CardNormal;
            colors.highlightedColor = CardHighlighted;
            colors.selectedColor = CardSelected;
            colors.pressedColor = CardPressed;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            var restartLabel = Child(restart, "Label");
            Stretch(restartLabel);
            var restartText = Label(restartLabel.gameObject, RestartFontSize, TextAnchor.MiddleCenter, Color.white);

            var hint = Child(panel, "Hint");
            Place(hint, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -122f), new Vector2(800f, 24f));
            var hintText = Label(hint.gameObject, BodyFontSize, TextAnchor.MiddleCenter, BodyColor);

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("title").objectReferenceValue = titleText;
            so.FindProperty("time").objectReferenceValue = timeText;
            so.FindProperty("level").objectReferenceValue = levelText;
            so.FindProperty("kills").objectReferenceValue = killsText;
            so.FindProperty("hint").objectReferenceValue = hintText;
            so.FindProperty("restartButton").objectReferenceValue = button;
            so.FindProperty("restartLabel").objectReferenceValue = restartText;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 창은 Play 중 판이 끝났을 때만 보인다(GameOverView가 켠다).
            panel.gameObject.SetActive(false);
            return view;
        }

        /// <summary>
        /// 화면 터치용 플로팅 가상 조이스틱(터치 영역 + 바탕 고리 + 손잡이). HUD의 맨 앞 형제(= 가장 뒤) — 레벨업 · 판 끝 창이 그 위에서 터치를 받는다.
        /// 터치 영역 넓이 · 크기는 Play 때 JoystickTuning 값으로 VirtualJoystick이 맞춘다. 그림은 코드가 만드는 임시 원.
        /// </summary>
        public static VirtualJoystick BuildJoystick(GameObject hudRoot, JoystickTuning tuning)
        {
            GetOrAdd<UnityEngine.UI.GraphicRaycaster>(hudRoot);
            var area = Child(hudRoot.transform, "Joystick");
            Stretch(area);
            area.SetAsFirstSibling();
            Image(area.gameObject, Color.clear, true); // 보이지 않는 터치 영역

            var stickBase = Child(area, "Base");
            Place(stickBase, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * JoystickPreviewSize);
            var baseImage = Image(stickBase.gameObject, Color.white, false);
            var knob = Child(area, "Knob");
            Place(knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * JoystickPreviewSize * 0.5f);
            var knobImage = Image(knob.gameObject, Color.white, false);

            var joystick = GetOrAdd<VirtualJoystick>(area.gameObject);
            var so = new SerializedObject(joystick);
            so.FindProperty("stickControlPath").stringValue = JoystickControlPath;
            so.FindProperty("tuning").objectReferenceValue = tuning;
            so.FindProperty("stickBase").objectReferenceValue = stickBase;
            so.FindProperty("knob").objectReferenceValue = knob;
            so.FindProperty("baseImage").objectReferenceValue = baseImage;
            so.FindProperty("knobImage").objectReferenceValue = knobImage;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 누르기 전에는 안 보인다(VirtualJoystick이 켜고 끈다).
            stickBase.gameObject.SetActive(false);
            knob.gameObject.SetActive(false);
            return joystick;
        }

        // 조이스틱 값이 들어갈 자리: 가상 패드의 왼스틱 = Player/Move의 패드 바인딩과 같은 길.
        const string JoystickControlPath = "<Gamepad>/leftStick";
        // 에디터에서 보이는 임시 크기(표시만). Play 때는 JoystickTuning의 반경으로 바뀐다.
        const float JoystickPreviewSize = 144f;

        /// <summary>uGUI 입력: EventSystem + Input System UI 모듈(마우스 클릭 · 패드 · 키보드 이동과 확인). 액션은 프로젝트 기본 UI 액션.</summary>
        public static void EnsureEventSystem(Scene scene)
        {
            GameObject go = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "EventSystem") go = root;
            }
            if (go == null)
            {
                go = new GameObject("EventSystem");
                SceneManager.MoveGameObjectToScene(go, scene);
            }
            GetOrAdd<EventSystem>(go);
            var module = go.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = go.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null || module.point == null || module.leftClick == null || module.move == null || module.submit == null) module.AssignDefaultActions();
        }

        static RectTransform Child(Transform parent, string name)
        {
            var found = parent.Find(name);
            GameObject go = found != null ? found.gameObject : new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect)
        {
            Place(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        static UnityEngine.UI.Image Image(GameObject go, Color color, bool raycast)
        {
            var image = GetOrAdd<UnityEngine.UI.Image>(go);
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        static UnityEngine.UI.Text Label(GameObject go, int fontSize, TextAnchor alignment, Color color)
        {
            var label = GetOrAdd<UnityEngine.UI.Text>(go);
            if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = string.Empty;
            var shadow = GetOrAdd<UnityEngine.UI.Shadow>(go);
            shadow.effectColor = Color.black;
            return label;
        }

        static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
