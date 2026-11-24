using System;

namespace Myong.Core
{
    /// <summary>Core 전용 2D 벡터. 엔진 타입(UnityEngine.Vector2)을 쓰지 않기 위한 최소 구조체.</summary>
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero => default;

        public float SqrMagnitude => X * X + Y * Y;

        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        /// <summary>길이 1로 맞춘 벡터. 길이가 0이면 Zero.</summary>
        public Vec2 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 0f ? new Vec2(X / m, Y / m) : Zero;
            }
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);

        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);

        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);

        public override int GetHashCode() => (X, Y).GetHashCode();

        public override string ToString() => FormattableString.Invariant($"({X:0.###}, {Y:0.###})");
    }
}
