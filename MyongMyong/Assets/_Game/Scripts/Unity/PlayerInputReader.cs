using Myong.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Myong.Unity
{
    /// <summary>
    /// 입력을 읽는 단 한 곳. 키보드(WASD · 방향키) · 패드 왼스틱 · (나중의) 가상 조이스틱이
    /// 모두 Input System의 Move 액션 하나로 모여 같은 벡터가 된다.
    /// 레벨업 카드 고르기(숫자키)도 여기서 읽는다 — 마우스 클릭 · 패드는 Input System UI 모듈이 버튼으로 넘긴다.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionReference move;
        [Tooltip("Level-up card actions (card 1, 2, 3...). Empty slots fall back to built-in actions bound to the number keys (see ChooseCardFallbackBindings).")]
        [SerializeField] InputActionReference[] chooseCard = new InputActionReference[0];

        /// <summary>
        /// InputSystem_Actions에 카드 고르기 액션(UI/ChooseCard1~3)이 아직 없어서 쓰는 임시 액션의 바인딩.
        /// 에셋에 액션이 생기면 Setup(GameSceneBuilder)이 chooseCard에 잇고 이 임시 액션은 쓰지 않는다.
        /// </summary>
        public static readonly string[][] ChooseCardFallbackBindings =
        {
            new[] { "<Keyboard>/1", "<Keyboard>/numpad1" },
            new[] { "<Keyboard>/2", "<Keyboard>/numpad2" },
            new[] { "<Keyboard>/3", "<Keyboard>/numpad3" },
        };

        InputAction[] chooseActions = new InputAction[0];
        InputAction[] fallbackActions = new InputAction[0];
        int debugChosenCard = -1;

        bool hasDebugMove;
        Vec2 debugMove;

        void Awake()
        {
            int count = Mathf.Max(chooseCard.Length, ChooseCardFallbackBindings.Length);
            chooseActions = new InputAction[count];
            fallbackActions = new InputAction[count];
            for (int i = 0; i < count; i++)
            {
                if (i < chooseCard.Length && chooseCard[i] != null)
                {
                    chooseActions[i] = chooseCard[i].action;
                    continue;
                }
                if (i >= ChooseCardFallbackBindings.Length) continue;
                var action = new InputAction("ChooseCard" + (i + 1), InputActionType.Button);
                foreach (string path in ChooseCardFallbackBindings[i]) action.AddBinding(path);
                fallbackActions[i] = action;
                chooseActions[i] = action;
            }
        }

        void OnEnable()
        {
            if (move != null) move.action.Enable();
            foreach (var action in chooseActions) action?.Enable();
        }

        void OnDisable()
        {
            if (move != null) move.action.Disable();
            foreach (var action in chooseActions) action?.Disable();
        }

        void OnDestroy()
        {
            foreach (var action in fallbackActions) action?.Dispose();
        }

        /// <summary>이번 프레임에 누른 레벨업 카드 번호(0부터). 없으면 -1.</summary>
        public int ReadChosenCard()
        {
            if (debugChosenCard >= 0)
            {
                int chosen = debugChosenCard;
                debugChosenCard = -1;
                return chosen;
            }
            for (int i = 0; i < chooseActions.Length; i++)
            {
                if (chooseActions[i] != null && chooseActions[i].WasPressedThisFrame()) return i;
            }
            return -1;
        }

        /// <summary>디버그: 다음 ReadChosenCard가 이 카드를 돌려주게 한다(키를 누른 것과 같은 길).</summary>
        public void DebugPressCard(int card)
        {
            debugChosenCard = card;
        }

        /// <summary>이동 입력(가공 전). 대각선을 1로 줄이는 것은 Core의 MoveRules가 한다.</summary>
        public Vec2 ReadMove()
        {
            if (hasDebugMove) return debugMove;
            if (move == null) return Vec2.Zero;
            Vector2 v = move.action.ReadValue<Vector2>();
            return new Vec2(v.x, v.y);
        }

        /// <summary>디버그: 실제 입력 대신 이 벡터를 쓴다.</summary>
        public void DebugSetMove(Vec2 value)
        {
            hasDebugMove = true;
            debugMove = value;
        }

        public void DebugClearMove()
        {
            hasDebugMove = false;
            debugMove = Vec2.Zero;
        }
    }
}
