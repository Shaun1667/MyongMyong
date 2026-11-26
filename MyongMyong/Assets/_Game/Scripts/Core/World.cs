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

        /// <summary>LastHurtTick에 넣는 값: 맞은 적 없음.</summary>
        public const long NeverHurt = long.MinValue / 2;

        /// <summary>지금 체력(0이면 판이 끝난다). 최대는 PlayerTuning의 최대 체력.</summary>
        public float Health;
        /// <summary>마지막으로 피해를 받은 틱(표시 쪽 붉은 번쩍임용). 없으면 NeverHurt.</summary>
        public long LastHurtTick;
        /// <summary>이 틱 전까지는 접촉 피해를 받지 않는다(맞은 뒤 무적 시간).</summary>
        public long InvulnerableUntilTick;
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
        /// <summary>종류마다(EnemyRulesData.Kinds의 번호) 스폰 · 처치 누계. 판 시작 때 종류 수만큼 만든다.</summary>
        public long[] EnemiesSpawnedByKind = new long[0];
        public long[] EnemiesKilledByKind = new long[0];
        /// <summary>돌진하다 멀어져 사라진 적의 누계(처치 아님).</summary>
        public long EnemiesEscapedTotal;
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

        /// <summary>레벨업 선택지 전용 난수 스트림(RandomStream.LevelUp).</summary>
        public Rng LevelUpRandom;

        /// <summary>떨어져 있는 경험치 젬. 앞에서 GemCount개만 쓴다(크기 = 젬 최대 수, 판 시작 때 한 번 만든다). 빠지면 맨 끝 것을 그 자리로 옮긴다.</summary>
        public GemState[] Gems = new GemState[0];
        public int GemCount;
        public long GemsDroppedTotal;
        public long GemsCollectedTotal;

        /// <summary>지금 레벨(1부터).</summary>
        public int Level = 1;
        /// <summary>지금 레벨에서 모은 경험치(다음 레벨까지 ExperienceToNext(Level)).</summary>
        public int Experience;
        /// <summary>이 판에서 얻은 경험치 누계.</summary>
        public long ExperienceTotal;
        /// <summary>아직 고르지 않은 레벨업 수(지금 보이는 것 포함). 0보다 크면 시뮬레이션이 멈춘다.</summary>
        public int PendingLevelUps;
        /// <summary>지금 보이는 선택지(강화 표의 줄 번호). 앞에서 LevelUpChoiceCount개만 쓴다.</summary>
        public int[] LevelUpChoices = new int[0];
        public int LevelUpChoiceCount;
        /// <summary>강화 표 줄마다 걸린 단계.</summary>
        public int[] UpgradeStages = new int[0];
        /// <summary>강화 값 칸(Stats 참고): 더한 값 합 · 곱한 값 곱.</summary>
        public float[] StatAdd = new float[StatTargets.Count];
        public float[] StatMul = new float[StatTargets.Count];
        /// <summary>패시브 표 줄마다 단계(0 = 안 가짐).</summary>
        public int[] PassiveStages = new int[0];
        /// <summary>가진 패시브(패시브 표 줄 번호)를 얻은 순서대로. 앞에서 PassiveSlotCount개만 쓴다(HUD 아이콘 줄).</summary>
        public int[] PassiveSlots = new int[0];
        public int PassiveSlotCount;
        /// <summary>경험치 배율로 생긴 소수 부분(다음 젬에 더한다).</summary>
        public float ExperienceCarry;
        /// <summary>이 판에서 회복한 체력 합(재생 · 대체 보상).</summary>
        public float HealedTotal;
        /// <summary>대체 보상(고를 것이 다 떨어졌을 때)을 받은 수.</summary>
        public int FallbackRewardsTaken;


        /// <summary>판이 어떻게 끝났는지(진행 중 = Playing). 끝나면 Step이 아무것도 하지 않는다.</summary>
        public RunOutcome Outcome;
        /// <summary>판이 끝났을 때의 결과 값(생존 틱 · 레벨 · 처치 수). 진행 중이면 기본값.</summary>
        public RunResult Result;
        /// <summary>플레이어가 피해를 받지 않는지(디버그).</summary>
        public bool PlayerInvincible;
        /// <summary>이 판에서 받은 피해 횟수 · 합.</summary>
        public long HitsTaken;
        public float DamageTakenTotal;

        /// <summary>GoalReachedTick에 넣는 값: 아직 생존 목표에 닿지 않음.</summary>
        public const long GoalNotReached = -1;

        /// <summary>생존 목표에 닿은 틱(이때 승리가 확정된다). 아직이면 GoalNotReached.</summary>
        public long GoalReachedTick = GoalNotReached;
        /// <summary>목표에 닿아 사라진 적의 수(처치 아님 · 젬 없음).</summary>
        public long EnemiesDismissedTotal;

        /// <summary>생존 목표에 닿았다(승리 확정 — 이후 판이 어떻게 끝나든 승리).</summary>
        public bool GoalReached => GoalReachedTick >= 0;

        /// <summary>판이 끝났다(패배 · 승리).</summary>
        public bool IsOver => Outcome != RunOutcome.Playing;

        /// <summary>레벨업 선택을 기다리는 중(시뮬레이션이 멈춘다).</summary>
        public bool IsChoosingUpgrade => PendingLevelUps > 0;

        public int AllocateEntityId()
        {
            NextEntityId++;
            return NextEntityId;
        }
    }
}
