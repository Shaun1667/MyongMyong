using System;

namespace Myong.Core
{
    /// <summary>
    /// 가상 조이스틱 수치(데이터 에셋 JoystickTuning에서 온다). 거리 단위는 화면 기준 해상도의 도트(HUD 캔버스 단위).
    /// </summary>
    public readonly struct VirtualStickData
    {
        /// <summary>손가락이 이만큼 멀어지면 끝까지 민 것(손잡이도 여기서 멈춘다).</summary>
        public readonly float Radius;

        /// <summary>반경 대비 0~1. 이 안이면 입력 0.</summary>
        public readonly float DeadZone;

        /// <summary>방향 수. 0이면 아날로그(모든 방향), 8이면 8방향으로 맞춘다.</summary>
        public readonly int DirectionCount;

        /// <summary>true면 민 거리만큼 느리게(데드존 끝 0 → 반경 1), false면 데드존 밖은 늘 끝까지(키보드와 같음).</summary>
        public readonly bool AnalogSpeed;

        public VirtualStickData(float radius, float deadZone, int directionCount, bool analogSpeed)
        {
            Radius = radius;
            DeadZone = deadZone;
            DirectionCount = directionCount;
            AnalogSpeed = analogSpeed;
        }
    }

    /// <summary>
    /// 플로팅 조이스틱 하나의 손가락 상태: 누른 자리 = 원점, 끌면 손잡이 · 입력, 떼면 0. 첫 손가락만 따른다.
    /// 막히면(레벨업 · 판 끝 창) 누르던 것도 놓고 새 누르기를 받지 않는다. 화면 쪽(VirtualJoystick)은 이 값으로 그리고 입력을 넣기만 한다.
    /// 위치는 터치 영역 좌표(HUD 단위).
    /// </summary>
    public sealed class VirtualStickPointer
    {
        public const int NoPointer = int.MinValue;

        VirtualStickData data;

        public VirtualStickPointer(in VirtualStickData data)
        {
            this.data = data;
        }

        public int PointerId { get; private set; } = NoPointer;

        public bool IsActive => PointerId != NoPointer;

        public bool IsBlocked { get; private set; }

        /// <summary>누른 자리.</summary>
        public Vec2 Origin { get; private set; }

        /// <summary>손잡이 자리(원점 기준, 반경 안).</summary>
        public Vec2 KnobOffset { get; private set; }

        /// <summary>이동 입력(길이 0~1).</summary>
        public Vec2 Value { get; private set; }

        public void SetData(in VirtualStickData value)
        {
            data = value;
        }

        /// <summary>누르기. 막혔거나 이미 다른 손가락이 눌러 있으면 false.</summary>
        public bool Press(int pointerId, Vec2 position)
        {
            if (IsBlocked || IsActive || pointerId == NoPointer) return false;
            PointerId = pointerId;
            Origin = position;
            KnobOffset = Vec2.Zero;
            Value = Vec2.Zero;
            return true;
        }

        /// <summary>끌기. 누른 그 손가락이 아니면 false.</summary>
        public bool Drag(int pointerId, Vec2 position)
        {
            if (!IsActive || pointerId != PointerId) return false;
            Vec2 offset = position - Origin;
            KnobOffset = VirtualStickRules.ClampKnob(offset, data.Radius);
            Value = VirtualStickRules.Evaluate(offset, data);
            return true;
        }

        /// <summary>떼기. 누른 그 손가락이 아니면 false.</summary>
        public bool Release(int pointerId)
        {
            if (!IsActive || pointerId != PointerId) return false;
            ReleaseAny();
            return true;
        }

        /// <summary>어느 손가락이든 놓는다(입력 0).</summary>
        public void ReleaseAny()
        {
            PointerId = NoPointer;
            KnobOffset = Vec2.Zero;
            Value = Vec2.Zero;
        }

        /// <summary>막기 · 풀기. 막을 때 눌러 있던 것을 놓았으면 true.</summary>
        public bool SetBlocked(bool blocked)
        {
            bool released = blocked && IsActive;
            if (released) ReleaseAny();
            IsBlocked = blocked;
            return released;
        }
    }

    /// <summary>
    /// 가상 조이스틱의 계산 한 곳: 누른 자리(원점)에서 손가락까지의 거리 → 이동 입력(길이 0~1).
    /// 이 값이 Input System의 Move 액션(패드 왼스틱 자리)으로 들어가 키보드 · 패드와 같은 길을 간다.
    /// </summary>
    public static class VirtualStickRules
    {
        const float AxisEpsilon = 1e-5f;

        /// <summary>원점 → 손가락 거리(offset)를 이동 입력으로. 데드존 안이면 0.</summary>
        public static Vec2 Evaluate(Vec2 offset, in VirtualStickData data)
        {
            float radius = data.Radius > 0f ? data.Radius : 1f;
            float dead = Clamp01(data.DeadZone) * radius;
            float distance = offset.Magnitude;
            if (distance <= dead || distance <= 0f) return Vec2.Zero;

            Vec2 direction = new Vec2(offset.X / distance, offset.Y / distance);
            if (data.DirectionCount > 0) direction = Snap(direction, data.DirectionCount);

            float strength = 1f;
            if (data.AnalogSpeed)
            {
                float span = radius - dead;
                strength = span > 0f ? Clamp01((distance - dead) / span) : 1f;
            }
            return direction * strength;
        }

        /// <summary>손잡이를 그릴 자리(원점 기준): 반경 밖이면 반경 위로 당긴다.</summary>
        public static Vec2 ClampKnob(Vec2 offset, float radius)
        {
            if (radius <= 0f) return Vec2.Zero;
            float distance = offset.Magnitude;
            return distance > radius ? offset * (radius / distance) : offset;
        }

        /// <summary>단위 벡터를 가장 가까운 count방향 중 하나로. 축에 붙은 값은 정확히 0(좌우 방향이 흔들리지 않게).</summary>
        public static Vec2 Snap(Vec2 direction, int count)
        {
            if (count <= 0) return direction;
            double step = Math.PI * 2.0 / count;
            double angle = Math.Atan2(direction.Y, direction.X);
            double snapped = Math.Round(angle / step) * step;
            float x = (float)Math.Cos(snapped);
            float y = (float)Math.Sin(snapped);
            if (Math.Abs(x) < AxisEpsilon) x = 0f;
            if (Math.Abs(y) < AxisEpsilon) y = 0f;
            return new Vec2(x, y);
        }

        static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
