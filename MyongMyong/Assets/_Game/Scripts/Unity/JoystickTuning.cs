using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 가상 조이스틱 수치 표. 거리는 HUD 캔버스 단위(= 기준 해상도 960x540의 도트, PixelArt).
    /// 코드에 숫자를 쓰지 않고 이 에셋에서 읽는다 — 값과 이유는 BALANCE.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Myong/Data/Joystick Tuning", fileName = "JoystickTuning")]
    public sealed class JoystickTuning : ScriptableObject
    {
        [Tooltip("Stick radius in HUD units (reference-resolution dots). Dragging this far = full input; the knob stops here.")]
        [SerializeField, Min(1f)] float radius;

        [Tooltip("Dead zone as a fraction of the radius (0-1). Inside it the input is zero.")]
        [SerializeField, Range(0f, 1f)] float deadZone;

        [Tooltip("Number of directions the input snaps to. 0 = analog (any direction), 8 = eight directions like WASD.")]
        [SerializeField, Min(0)] int directionCount;

        [Tooltip("On: input strength grows with the drag distance (dead zone edge 0 -> radius 1). Off: full strength outside the dead zone, like the keyboard.")]
        [SerializeField] bool analogSpeed;

        [Tooltip("Knob radius in HUD units (display only).")]
        [SerializeField, Min(1f)] float knobRadius;

        [Tooltip("Part of the screen, from the left edge, where a touch starts the stick (0-1). 1 = anywhere, 0.5 = left half.")]
        [SerializeField, Range(0.1f, 1f)] float touchAreaWidth;

        public float Radius => radius;

        public float DeadZone => deadZone;

        public int DirectionCount => directionCount;

        public bool AnalogSpeed => analogSpeed;

        public float KnobRadius => knobRadius;

        public float TouchAreaWidth => touchAreaWidth;

        public VirtualStickData ToData() => new VirtualStickData(radius, deadZone, directionCount, analogSpeed);
    }
}
