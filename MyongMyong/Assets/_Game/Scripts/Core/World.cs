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

        /// <summary>스폰 전용 난수 스트림(RandomStream.Spawn).</summary>
        public Rng SpawnRandom;

        /// <summary>살아 있는 적. 앞에서 EnemyCount개만 쓴다(크기 = 동시 최대 수, 판 시작 때 한 번 만든다). 빠지면 맨 끝 것을 그 자리로 옮긴다.</summary>
        public EnemyState[] Enemies = new EnemyState[0];
        public int EnemyCount;
        /// <summary>이 판에서 스폰한 적의 누계.</summary>
        public long EnemiesSpawnedTotal;
        /// <summary>쌓인 스폰 몫(1이 되면 한 마리).</summary>
        public float SpawnBudget;
        /// <summary>시간대 스폰을 멈췄는지(디버그).</summary>
        public bool SpawningPaused;

        /// <summary>이 판에서 처치한 적의 누계.</summary>
        public long EnemiesKilledTotal;
        /// <summary>적이 피해를 받지 않는지(디버그). 탄은 맞고 사라지지만 체력이 줄지 않는다.</summary>
        public bool EnemiesInvincible;

        /// <summary>가진 무기(판 시작 때 만든다).</summary>
        public WeaponState[] Weapons = new WeaponState[0];
        /// <summary>날아가는 탄. 앞에서 BulletCount개만 쓴다(크기 = 탄 최대 수, 판 시작 때 한 번 만든다). 빠지면 맨 끝 것을 그 자리로 옮긴다.</summary>
        public BulletState[] Bullets = new BulletState[0];
        public int BulletCount;
        /// <summary>탄마다 맞힌 적 ID(탄 i의 칸 = i * BulletHitStride부터 BulletHitStride개).</summary>
        public int[] BulletHitIds = new int[0];
        public int BulletHitStride = 1;
        /// <summary>이 판에서 쏜 탄의 누계.</summary>
        public long BulletsFiredTotal;
        /// <summary>자동 사격을 멈추는지(디버그).</summary>
        public bool FiringPaused;

        public int AllocateEntityId()
        {
            NextEntityId++;
            return NextEntityId;
        }
    }
}
