namespace Myong.Core
{
    /// <summary>판이 어떻게 끝났는지. 진행 중 = Playing. 승리(30분 생존)도 같은 "판 끝" 구조로 받는다 — 새 결과 = 줄 하나(기존 값은 바꾸지 않는다).</summary>
    public enum RunOutcome
    {
        Playing = 0,
        Defeat = 1,
        Victory = 2,
    }

    /// <summary>판이 끝났을 때 남기는 결과 값(World.Result). 판이 진행 중이면 Outcome = Playing.</summary>
    public struct RunResult
    {
        public RunOutcome Outcome;
        /// <summary>판이 끝날 때까지 진행한 틱 수(끝난 틱 포함). 생존 시간 = Ticks ÷ 초당 틱.</summary>
        public long Ticks;
        public int Level;
        public long EnemiesKilled;
    }

    /// <summary>
    /// 플레이어 체력 · 적 접촉 피해 · 판 끝. 상태는 World에만 둔다 — 틱마다 할당하지 않는다.
    /// 방식: 겹친(반지름 합) 적 중 접촉 피해가 가장 큰 적의 피해를 한 번 받고, 무적 시간(틱) 동안은 더 받지 않는다.
    /// </summary>
    sealed class HealthRules
    {
        readonly EnemyKindData[] enemyKinds;
        readonly float maxHealth;
        readonly float playerRadius;
        readonly int invulnerableTicks;

        public HealthRules(EnemyRulesData enemies, PlayerTuningData player, int ticksPerSecond)
        {
            enemyKinds = enemies.Kinds;
            maxHealth = player.MaxHealth;
            playerRadius = player.Radius;
            invulnerableTicks = InvulnerableTicksOf(player.HurtInvulnerableSeconds, ticksPerSecond);
        }

        /// <summary>무적 시간(초) → 틱(반올림, 0 이상). 0이면 닿아 있는 동안 틱마다 맞는다.</summary>
        public static int InvulnerableTicksOf(float seconds, int ticksPerSecond)
        {
            if (seconds <= 0f) return 0;
            return (int)System.Math.Round(seconds * ticksPerSecond, System.MidpointRounding.AwayFromZero);
        }

        public int InvulnerableTicks => invulnerableTicks;

        public float MaxHealth => maxHealth;

        public void InitWorld(World world)
        {
            world.Player.Health = maxHealth;
            world.Player.LastHurtTick = PlayerState.NeverHurt;
            world.Player.InvulnerableUntilTick = 0;
            world.Outcome = RunOutcome.Playing;
            world.Result = default;
        }

        /// <summary>겹친 적이 있으면 피해 한 번(무적 시간 중 · 디버그 무적이면 없음). 체력이 0 이하가 되면 true.</summary>
        public bool StepContact(World world)
        {
            ref PlayerState p = ref world.Player;
            if (world.PlayerInvincible || world.Tick < p.InvulnerableUntilTick) return false;

            float damage = 0f;
            Vec2 position = p.Position;
            EnemyState[] list = world.Enemies;
            for (int i = 0; i < world.EnemyCount; i++)
            {
                ref readonly EnemyState e = ref list[i];
                float contact = enemyKinds[e.Kind].ContactDamage;
                if (contact <= damage) continue;
                float reach = playerRadius + enemyKinds[e.Kind].Radius;
                if ((e.Position - position).SqrMagnitude < reach * reach) damage = contact;
            }
            if (damage <= 0f) return false;

            Hurt(world, damage);
            p.InvulnerableUntilTick = world.Tick + invulnerableTicks;
            return p.Health <= 0f;
        }

        /// <summary>피해를 바로 준다(무적 시간은 걸지 않는다). 체력은 0 아래로 내려가지 않는다.</summary>
        public static void Hurt(World world, float damage)
        {
            ref PlayerState p = ref world.Player;
            p.Health -= damage;
            if (p.Health < 0f) p.Health = 0f;
            p.LastHurtTick = world.Tick;
            world.HitsTaken++;
            world.DamageTakenTotal += damage;
        }

        /// <summary>판을 끝내고 결과 값을 남긴다. 그 뒤 Step은 아무것도 하지 않는다. 이미 끝났으면 그대로.</summary>
        public static void EndRun(World world, RunOutcome outcome)
        {
            if (world.IsOver || outcome == RunOutcome.Playing) return;
            world.Outcome = outcome;
            world.PendingLevelUps = 0;
            world.LevelUpChoiceCount = 0;
            world.Result = new RunResult
            {
                Outcome = outcome,
                Ticks = world.Tick,
                Level = world.Level,
                EnemiesKilled = world.EnemiesKilledTotal,
            };
        }
    }
}
