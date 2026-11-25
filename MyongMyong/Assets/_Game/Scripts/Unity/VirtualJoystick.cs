using Myong.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace Myong.Unity
{
    /// <summary>
    /// 화면 터치용 플로팅 가상 조이스틱. 터치 영역 안을 누른 자리에 조이스틱이 나타나고, 끌면 손잡이가 따라오며, 떼면 사라지고 입력 0.
    /// 값은 Input System의 화면 위 컨트롤(OnScreenControl)로 가상 패드의 왼스틱(<Gamepad>/leftStick)에 넣는다 —
    /// 그래서 Move 액션 하나로 키보드 · 패드와 같은 길을 가고, PlayerInputReader는 따로 읽지 않는다.
    /// 누르기 · 끌기 · 떼기는 uGUI 포인터 이벤트(Input System UI 모듈)라 기기의 터치와 에디터의 마우스가 같은 길이다.
    /// 계산(데드존 · 8방향 · 세기)은 Core VirtualStickRules, 수치는 JoystickTuning.
    /// </summary>
    public sealed class VirtualJoystick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        const int NoPointer = int.MinValue;
        const int SpriteSize = 64;
        const float RingThickness = 0.09f; // 원 지름 대비(표시만)

        [InputControl(layout = "Vector2")]
        [SerializeField] string stickControlPath = "<Gamepad>/leftStick";
        [SerializeField] JoystickTuning tuning;
        [SerializeField] RectTransform stickBase;
        [SerializeField] RectTransform knob;
        [SerializeField] UnityEngine.UI.Image baseImage;
        [SerializeField] UnityEngine.UI.Image knobImage;
        [Header("Display")]
        [SerializeField] Color baseColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] Color knobColor = new Color(1f, 1f, 1f, 0.7f);

        RectTransform area;
        VirtualStickPointer pointer;
        Sprite ringSprite;
        Sprite discSprite;

        protected override string controlPathInternal
        {
            get => stickControlPath;
            set => stickControlPath = value;
        }

        /// <summary>손가락이 눌려 있어 조이스틱이 보이는 중.</summary>
        public bool IsActive => pointer != null && pointer.IsActive;

        /// <summary>막혀 있음(레벨업 · 판 끝 창). 막힌 동안은 누르기를 받지 않는다.</summary>
        public bool IsBlocked => pointer != null && pointer.IsBlocked;

        /// <summary>지금 넣고 있는 이동 입력(VirtualStickRules 결과).</summary>
        public Vec2 Value => pointer != null ? pointer.Value : Vec2.Zero;

        public JoystickTuning Tuning => tuning;

        void Awake()
        {
            ApplyTuning();
            ringSprite = CircleSprite(true);
            discSprite = CircleSprite(false);
            if (baseImage != null) { baseImage.sprite = ringSprite; baseImage.color = baseColor; }
            if (knobImage != null) { knobImage.sprite = discSprite; knobImage.color = knobColor; }
            ShowStick(false);
        }

        /// <summary>터치 영역 넓이 · 조이스틱 크기 · 계산 값을 표(JoystickTuning)에서 읽는다(Awake가 부른다).</summary>
        public void ApplyTuning()
        {
            area = (RectTransform)transform;
            var data = tuning != null ? tuning.ToData() : new VirtualStickData(1f, 0f, 0, true);
            if (pointer == null) pointer = new VirtualStickPointer(data);
            else pointer.SetData(data);
            if (tuning == null) return;
            area.anchorMin = Vector2.zero;
            area.anchorMax = new Vector2(tuning.TouchAreaWidth, 1f);
            area.pivot = new Vector2(0.5f, 0.5f);
            area.anchoredPosition = Vector2.zero;
            area.sizeDelta = Vector2.zero;
            if (stickBase != null) stickBase.sizeDelta = Vector2.one * (tuning.Radius * 2f);
            if (knob != null) knob.sizeDelta = Vector2.one * (tuning.KnobRadius * 2f);
        }

        /// <summary>레벨업 · 판 끝 창이 떠 있으면 막는다: 누르고 있던 것도 놓고(입력 0) 새 누르기를 받지 않는다.</summary>
        public void SetBlocked(bool value)
        {
            if (pointer == null || pointer.IsBlocked == value) return;
            if (pointer.SetBlocked(value)) Released();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false; // 조금만 움직여도 바로 따라오게
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointer == null || !ToLocal(eventData, out Vec2 local)) return;
            if (!pointer.Press(eventData.pointerId, local)) return;
            var origin = new Vector2(local.X, local.Y);
            if (stickBase != null) stickBase.anchoredPosition = origin;
            if (knob != null) knob.anchoredPosition = origin;
            ShowStick(true);
            SendValueToControl(Vector2.zero);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (pointer == null || !ToLocal(eventData, out Vec2 local)) return;
            if (!pointer.Drag(eventData.pointerId, local)) return;
            Vec2 knobAt = pointer.Origin + pointer.KnobOffset;
            if (knob != null) knob.anchoredPosition = new Vector2(knobAt.X, knobAt.Y);
            SendValueToControl(new Vector2(pointer.Value.X, pointer.Value.Y));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointer != null && pointer.Release(eventData.pointerId)) Released();
        }

        protected override void OnDisable()
        {
            if (pointer != null && pointer.IsActive)
            {
                pointer.ReleaseAny();
                Released();
            }
            base.OnDisable();
        }

        void OnDestroy()
        {
            if (ringSprite != null) { Destroy(ringSprite.texture); Destroy(ringSprite); }
            if (discSprite != null) { Destroy(discSprite.texture); Destroy(discSprite); }
        }

        // 손을 뗀 뒤(또는 막혀 놓인 뒤): 숨기고 입력 0.
        void Released()
        {
            ShowStick(false);
            SentDefaultValueToControl();
        }

        bool ToLocal(PointerEventData eventData, out Vec2 local)
        {
            bool hit = RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 point);
            local = new Vec2(point.x, point.y);
            return hit;
        }

        void ShowStick(bool show)
        {
            if (stickBase != null) stickBase.gameObject.SetActive(show);
            if (knob != null) knob.gameObject.SetActive(show);
        }


        // 임시 그림: 코드로 만든 흰 원(테두리 고리 · 꽉 찬 원). 색은 Image 색으로.
        static Sprite CircleSprite(bool ring)
        {
            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[SpriteSize * SpriteSize];
            float center = (SpriteSize - 1) * 0.5f;
            float outer = SpriteSize * 0.5f;
            float inner = outer - SpriteSize * RingThickness;
            for (int y = 0; y < SpriteSize; y++)
            {
                for (int x = 0; x < SpriteSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool on = d <= outer && (!ring || d >= inner);
                    pixels[y * SpriteSize + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f), SpriteSize);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        // ---- 디버그: 손가락 흉내(uGUI 포인터 이벤트와 같은 길). screenPosition은 화면 픽셀 ----

        public void DebugPress(Vector2 screenPosition)
        {
            OnPointerDown(DebugEvent(screenPosition));
        }

        public void DebugDrag(Vector2 screenPosition)
        {
            OnDrag(DebugEvent(screenPosition));
        }

        public void DebugRelease()
        {
            if (IsActive) OnPointerUp(DebugEvent(Vector2.zero));
        }

        PointerEventData DebugEvent(Vector2 screenPosition)
        {
            return new PointerEventData(EventSystem.current) { position = screenPosition, pointerId = DebugPointerId };
        }

        const int DebugPointerId = -1; // 마우스 왼쪽 버튼과 같은 번호
    }
}
