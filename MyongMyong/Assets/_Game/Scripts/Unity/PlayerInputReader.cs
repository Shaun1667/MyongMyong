using Myong.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Myong.Unity
{
    /// <summary>
    /// 입력을 읽는 단 한 곳. 키보드(WASD · 방향키) · 패드 왼스틱 · (나중의) 가상 조이스틱이
    /// 모두 Input System의 Move 액션 하나로 모여 같은 벡터가 된다.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionReference move;

        bool hasDebugMove;
        Vec2 debugMove;

        void OnEnable()
        {
            if (move != null) move.action.Enable();
        }

        void OnDisable()
        {
            if (move != null) move.action.Disable();
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
