namespace Myong.Core
{
    /// <summary>바라보는 방향(좌우). 표시 쪽은 이 값으로 좌우를 뒤집는다.</summary>
    public static class Facing
    {
        public const int Right = 1;
        public const int Left = -1;
    }

    public struct PlayerState
    {
        public int Id;
        public Vec2 Position;
        /// <summary>직전 틱의 위치. 표시 쪽이 두 위치 사이를 보간한다.</summary>
        public Vec2 PreviousPosition;
        public Vec2 Velocity;
        public int FacingX;
    }

    /// <summary>한 판의 상태 전부(스냅샷). 시스템은 상태를 따로 들지 않는다.</summary>
    public sealed class World
    {
        public int Seed;
        public long Tick;
        public int NextEntityId;
        public PlayerState Player;

        public int AllocateEntityId()
        {
            NextEntityId++;
            return NextEntityId;
        }
    }
}
