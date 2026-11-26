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
        const int KindFontSize = 14;

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

                var kind = Child(card, "Kind");
                Place(kind, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(130f, 20f));
                var kindText = Label(kind.gameObject, KindFontSize, TextAnchor.UpperRight, StageColor);

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
                element.FindPropertyRelative("kind").objectReferenceValue = kindText;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            // 창은 Play 중 레벨업 때만 보인다(LevelUpView가 켠다).
            panel.gameObject.SetActive(false);

            return (hud, view);
        }

        // 가진 패시브 아이콘 줄(표시만, HUD 단위 = 기준 해상도 도트)
        const float PassiveIconSize = 28f;
        const float PassiveIconGap = 4f;
        const float PassiveRowTop = 56f; // 왼쪽 위 라벨(HelloLabel) 아래
        const int PassiveLetterFontSize = 14;
        const int PassiveStageFontSize = 11;
        static readonly Color PassiveFrame = new Color(0.08f, 0.1f, 0.14f, 0.85f);

        /// <summary>
        /// 가진 패시브 아이콘 줄(화면 왼쪽 위, 얻은 순서대로 오른쪽으로). 칸 수 = slotCount(PassiveTable의 최대 개수).
        /// 칸마다 테두리 · 단색 바탕 · 그림 · 짧은 글 · 단계. 레벨업 · 판 끝 창보다 아래(레벨업 창 앞 형제). Build 다음에 부른다.
        /// </summary>
        public static PassiveHud BuildPassives(GameObject hudRoot, int slotCount)
        {
            var row = Child(hudRoot.transform, "Passives");
            Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Padding, -PassiveRowTop), new Vector2(slotCount * (PassiveIconSize + PassiveIconGap), PassiveIconSize));
            var levelUp = hudRoot.transform.Find("LevelUp");
            if (levelUp != null && row.GetSiblingIndex() > levelUp.GetSiblingIndex()) row.SetSiblingIndex(levelUp.GetSiblingIndex());
            var hud = GetOrAdd<PassiveHud>(row.gameObject);
            var so = new SerializedObject(hud);
            var slots = so.FindProperty("slots");
            slots.arraySize = slotCount;
            for (int i = 0; i < slotCount; i++)
            {
                var slot = Child(row, "Slot" + (i + 1));
                Place(slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * (PassiveIconSize + PassiveIconGap), 0f), new Vector2(PassiveIconSize, PassiveIconSize));
                Image(slot.gameObject, PassiveFrame, false);
                var fill = Child(slot, "Fill");
                Place(fill, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-4f, -4f));
                var fillImage = Image(fill.gameObject, Color.white, false);
                var icon = Child(slot, "Icon");
                Place(icon, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-4f, -4f));
                var iconImage = Image(icon.gameObject, Color.white, false);
                iconImage.preserveAspect = true;
                var letter = Child(slot, "Letter");
                Stretch(letter);
                var letterText = Label(letter.gameObject, PassiveLetterFontSize, TextAnchor.MiddleCenter, Color.white);
                var stage = Child(slot, "Stage");
                Place(stage, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, -1f), new Vector2(PassiveIconSize, 14f));
                var stageText = Label(stage.gameObject, PassiveStageFontSize, TextAnchor.LowerRight, TitleColor);

                var element = slots.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("root").objectReferenceValue = slot.gameObject;
                element.FindPropertyRelative("background").objectReferenceValue = fillImage;
                element.FindPropertyRelative("icon").objectReferenceValue = iconImage;
                element.FindPropertyRelative("letter").objectReferenceValue = letterText;
                element.FindPropertyRelative("stage").objectReferenceValue = stageText;
                // 가진 것이 생길 때만 보인다(PassiveHud가 켜다).
                slot.gameObject.SetActive(false);
            }
            // 칸이 줄었으면 남는 칸을 감춘다(지우지 않음).
            for (int i = slotCount; i < row.childCount; i++) row.GetChild(i).gameObject.SetActive(false);
            so.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        const int TimerFontSize = 22;
        const float TimerWidth = 160f;
        const int GoalFontSize = 34;
        const int FinaleFontSize = 20;
        const float GoalWidth = 520f;
        const float GoalGap = 12f;
        static readonly Color GoalColor = new Color(1f, 0.86f, 0.4f, 1f);
        static readonly Color FinaleColor = new Color(0.86f, 0.74f, 1f, 1f);

        /// <summary>판 시간 mm:ss(화면 위 가운데, 경험치 바 바로 아래). 레벨업 · 판 끝 창보다 아래에 그려지도록 Build 다음, BuildGameOver 전에 부르고 레벨업 창 앞 형제로 둔다.</summary>
        public static RunTimerHud BuildTimer(GameObject hudRoot)
        {
            var timer = Child(hudRoot.transform, "TimerLabel");
            Place(timer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(BarHeight + 4f)), new Vector2(TimerWidth, TimerFontSize * 1.6f));
            var timerText = Label(timer.gameObject, TimerFontSize, TextAnchor.UpperCenter, Color.white);
            var levelUp = hudRoot.transform.Find("LevelUp");
            // 레벨업 창보다 뒤에 있을 때만 앞으로 옮긴다(이미 앞이면 그대로 — 다시 돌려도 같은 순서).
            if (levelUp != null && timer.GetSiblingIndex() > levelUp.GetSiblingIndex()) timer.SetSiblingIndex(levelUp.GetSiblingIndex());
            var hud = GetOrAdd<RunTimerHud>(timer.gameObject);
            SetupUtil.SetReference(hud, "timerLabel", timerText);

            // 생존 목표 글(1-11): 타이머 아래 크게, 그 아래 마무리 적 이름 줄. 타이머의 자식이라 창들보다 아래에 그려진다. 평소에는 꺼 둔다(RunTimerHud가 켬다).
            var goal = Child(timer, "GoalLabel");
            Place(goal, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(TimerFontSize * 1.6f + GoalGap)), new Vector2(GoalWidth, GoalFontSize * 1.5f));
            var goalText = Label(goal.gameObject, GoalFontSize, TextAnchor.MiddleCenter, GoalColor);
            goal.gameObject.SetActive(false);
            var finale = Child(timer, "FinaleLabel");
            Place(finale, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(TimerFontSize * 1.6f + GoalGap + GoalFontSize * 1.5f)), new Vector2(GoalWidth, FinaleFontSize * 1.6f));
            var finaleText = Label(finale.gameObject, FinaleFontSize, TextAnchor.MiddleCenter, FinaleColor);
            finale.gameObject.SetActive(false);
            SetupUtil.SetReference(hud, "goalLabel", goalText);
            SetupUtil.SetReference(hud, "finaleLabel", finaleText);
            return hud;
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
        static readonly Color CoinColor = new Color(1f, 0.84f, 0.32f, 1f);
        const float ResultButtonOffset = 124f;
        const float ResultButtonY = -100f;

        // 글자 버튼 하나(카드와 같은 색). 이름으로 찾아 없으면 만든다.
        static (UnityEngine.UI.Button button, UnityEngine.UI.Text label) Button(RectTransform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var rect = Child(parent, name);
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            var background = Image(rect.gameObject, Color.white, true);
            var button = GetOrAdd<UnityEngine.UI.Button>(rect.gameObject);
            button.targetGraphic = background;
            var colors = button.colors;
            colors.normalColor = CardNormal;
            colors.highlightedColor = CardHighlighted;
            colors.selectedColor = CardSelected;
            colors.pressedColor = CardPressed;
            colors.disabledColor = ButtonDisabled;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            var labelRect = Child(rect, "Label");
            Stretch(labelRect);
            var label = Label(labelRect.gameObject, fontSize, TextAnchor.MiddleCenter, Color.white);
            return (button, label);
        }

        static readonly Color ButtonDisabled = new Color(0.07f, 0.07f, 0.09f, 1f);
        static readonly Color ShopBox = new Color(0.1f, 0.09f, 0.14f, 1f); // 불투명(뒤 창 글이 비치지 않게)

        // 강화 화면(1-12) 배치(HUD 단위 = 기준 해상도 도트)
        const float ShopWidth = 720f;
        const float ShopHeight = 500f;
        const float ShopRowHeight = 50f;
        const float ShopRowsTop = -104f;
        const int ShopTitleFontSize = 30;
        const int ShopRowTitleFontSize = 18;
        const int ShopRowBodyFontSize = 14;
        const float ShopBuyWidth = 96f;
        const float ShopBuyHeight = 38f;
        const float ShopFooterButtonWidth = 220f;
        static readonly Color ShopRowBack = new Color(0.16f, 0.17f, 0.23f, 0.95f);

        /// <summary>
        /// 강화 화면(1-12): 어둠 바탕 · 상자 · 제목 · 지갑 · 줄 본(하나, Play 때 ShopView가 표 줄 수만큼 복제) · [모두 되돌리기] · [닫기].
        /// 판 끝 창보다 위(맨 끝 형제). BuildGameOver 다음에 부른다.
        /// </summary>
        public static ShopView BuildShop(GameObject hudRoot)
        {
            var root = Child(hudRoot.transform, "Shop");
            Stretch(root);
            root.SetAsLastSibling();
            var view = GetOrAdd<ShopView>(root.gameObject);
            var panel = Child(root, "Panel");
            Stretch(panel);
            Image(panel.gameObject, Dim, true);

            var box = Child(panel, "Box");
            Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ShopWidth, ShopHeight));
            Image(box.gameObject, ShopBox, false);
            box.SetAsFirstSibling();

            var title = Child(panel, "Title");
            Place(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, ShopHeight * 0.5f - 12f), new Vector2(ShopWidth - 40f, 44f));
            var titleText = Label(title.gameObject, ShopTitleFontSize, TextAnchor.MiddleCenter, TitleColor);

            var wallet = Child(panel, "Wallet");
            Place(wallet, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, ShopHeight * 0.5f - 58f), new Vector2(ShopWidth - 40f, 28f));
            var walletText = Label(wallet.gameObject, ResultFontSize, TextAnchor.MiddleCenter, CoinColor);

            // 줄이 놓일 자리(위에서 아래로) + 줄 본 하나(꺼 둔다).
            var rows = Child(panel, "Rows");
            Place(rows, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, ShopHeight * 0.5f + ShopRowsTop + 8f), new Vector2(ShopWidth - 40f, 6f * ShopRowHeight));
            var template = Child(rows, "RowTemplate");
            Place(template, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, ShopRowHeight - 4f));
            Image(template.gameObject, ShopRowBack, false);
            var rowView = GetOrAdd<ShopRowView>(template.gameObject);

            var rowTitle = Child(template, "Name");
            Place(rowTitle, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -3f), new Vector2(190f, 24f));
            rowView.title = Label(rowTitle.gameObject, ShopRowTitleFontSize, TextAnchor.MiddleLeft, Color.white);
            var rowDescription = Child(template, "Description");
            Place(rowDescription, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 2f), new Vector2(330f, 20f));
            rowView.description = Label(rowDescription.gameObject, ShopRowBodyFontSize, TextAnchor.MiddleLeft, BodyColor);
            var rowStage = Child(template, "Stage");
            Place(rowStage, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-(ShopBuyWidth + 150f), 0f), new Vector2(90f, 24f));
            rowView.stage = Label(rowStage.gameObject, ShopRowBodyFontSize, TextAnchor.MiddleCenter, StageColor);
            var rowPrice = Child(template, "Price");
            Place(rowPrice, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-(ShopBuyWidth + 16f), 0f), new Vector2(130f, 24f));
            rowView.price = Label(rowPrice.gameObject, ShopRowBodyFontSize, TextAnchor.MiddleRight, CoinColor);
            var buy = Child(template, "Buy");
            Place(buy, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(ShopBuyWidth, ShopBuyHeight));
            var buyBackground = Image(buy.gameObject, Color.white, true);
            var buyButton = GetOrAdd<UnityEngine.UI.Button>(buy.gameObject);
            buyButton.targetGraphic = buyBackground;
            var buyColors = buyButton.colors;
            buyColors.normalColor = CardNormal;
            buyColors.highlightedColor = CardHighlighted;
            buyColors.selectedColor = CardSelected;
            buyColors.pressedColor = CardPressed;
            buyColors.disabledColor = ButtonDisabled;
            buyColors.colorMultiplier = 1f;
            buyButton.colors = buyColors;
            var buyLabel = Child(buy, "Label");
            Stretch(buyLabel);
            rowView.buyButton = buyButton;
            rowView.buyLabel = Label(buyLabel.gameObject, ShopRowTitleFontSize, TextAnchor.MiddleCenter, Color.white);
            EditorUtility.SetDirty(rowView);
            template.gameObject.SetActive(false);

            float footerY = -ShopHeight * 0.5f + 36f;
            var (refundButton, refundText) = Button(panel, "Refund", new Vector2(-ResultButtonOffset, footerY), new Vector2(ShopFooterButtonWidth, RestartHeight - 8f), RestartFontSize - 2);
            var (closeButton, closeText) = Button(panel, "Close", new Vector2(ResultButtonOffset, footerY), new Vector2(ShopFooterButtonWidth, RestartHeight - 8f), RestartFontSize - 2);

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("title").objectReferenceValue = titleText;
            so.FindProperty("wallet").objectReferenceValue = walletText;
            so.FindProperty("rowsRoot").objectReferenceValue = rows;
            so.FindProperty("rowTemplate").objectReferenceValue = rowView;
            so.FindProperty("rowHeight").floatValue = ShopRowHeight;
            so.FindProperty("refundButton").objectReferenceValue = refundButton;
            so.FindProperty("refundLabel").objectReferenceValue = refundText;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("closeLabel").objectReferenceValue = closeText;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 화면은 Play 중 [강화]를 눌렀을 때만 보인다(ShopView가 켬다).
            panel.gameObject.SetActive(false);
            return view;
        }

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
            Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(540f, 380f));
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

            // 이번 판 코인 · 지갑(1-12): 처치 수 아래 줄.
            var coins = Child(panel, "Coins");
            Place(coins, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f - 3f * ResultLineHeight), new Vector2(ResultWidth + 100f, ResultLineHeight));
            var coinsText = Label(coins.gameObject, ResultFontSize, TextAnchor.MiddleCenter, CoinColor);

            // [다시 시작] · [강화](1-12)를 나란히.
            var (button, restartText) = Button(panel, "Restart", new Vector2(-ResultButtonOffset, ResultButtonY), new Vector2(RestartWidth, RestartHeight), RestartFontSize);
            var (shopButton, shopText) = Button(panel, "Shop", new Vector2(ResultButtonOffset, ResultButtonY), new Vector2(RestartWidth, RestartHeight), RestartFontSize);

            var hint = Child(panel, "Hint");
            Place(hint, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, ResultButtonY - 56f), new Vector2(800f, 24f));
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
            so.FindProperty("coins").objectReferenceValue = coinsText;
            so.FindProperty("shopButton").objectReferenceValue = shopButton;
            so.FindProperty("shopLabel").objectReferenceValue = shopText;
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
