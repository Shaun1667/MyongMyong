using System;

namespace Myong.Core
{
    /// <summary>플레이어 수치(데이터 에셋 PlayerTuning에서 옮겨 온 값).</summary>
    public readonly struct PlayerTuningData
    {
        /// <summary>이동 속도(유닛/초).</summary>
        public readonly float MoveSpeed;

        public PlayerTuningData(float moveSpeed)
        {
            MoveSpeed = moveSpeed;
        }
    }

    /// <summary>한 틱에 들어오는 입력. 키보드 · 패드 · 가상 조이스틱이 모두 이 모양이 된다.</summary>
    public struct InputFrame
    {
        public Vec2 Move;
    }

    public static class MoveRules
    {
        /// <summary>길이가 1을 넘는 입력(키보드 대각선)은 1로 줄인다. 스틱을 살짝 민 값(1 미만)은 그대로 둔다.</summary>
        public static Vec2 ClampMove(Vec2 raw)
        {
            return raw.SqrMagnitude > 1f ? raw.Normalized : raw;
        }
    }

    /// <summary>고정 틱 시뮬레이션. Step 한 번 = 한 틱. 상태는 World에만 있다.</summary>
    public sealed class Simulation
    {
        const float FullTurn = (float)(Math.PI * 2.0);

        readonly PlayerTuningData player;
        readonly EnemyRulesData enemies;
        readonly EnemySeparation separation;

        public Simulation(int ticksPerSecond, PlayerTuningData player)
            : this(ticksPerSecond, player, EnemyRulesData.None)
        {
        }

        public Simulation(int ticksPerSecond, PlayerTuningData player, EnemyRulesData enemies)
        {
            if (ticksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            TicksPerSecond = ticksPerSecond;
            TickSeconds = 1f / ticksPerSecond;
            this.player = player;
            this.enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            separation = new EnemySeparation(enemies.MaxAlive, enemies.MaxRadius);
        }

        public int TicksPerSecond { get; }

        public float TickSeconds { get; }

        public EnemyRulesData Enemies => enemies;

        public World CreateWorld(int seed)
        {
            var world = new World
            {
                Seed = seed,
                SpawnRandom = Rng.ForStream(seed, RandomStream.Spawn),
                Enemies = new EnemyState[enemies.MaxAlive],
            };
            world.Player.Id = world.AllocateEntityId();
            world.Player.FacingX = Facing.Right;
            return world;
        }

        /// <summary>판이 시작된 뒤 지난 시간(초) = 틱 수 × 틱 길이.</summary>
        public float ElapsedSeconds(World world) => world.Tick * TickSeconds;

        /// <summary>순서: 플레이어 이동 → 적 추적 → 적끼리 밀어내기 → 스폰(새 적은 그 틱에는 움직이지 않는다).</summary>
        public void Step(World world, in InputFrame input)
        {
            StepPlayer(world, input);
            StepEnemies(world);
            StepSpawning(world);
            world.Tick++;
        }

        void StepPlayer(World world, in InputFrame input)
        {
            ref PlayerState p = ref world.Player;
            Vec2 move = MoveRules.ClampMove(input.Move);
            p.PreviousPosition = p.Position;
            p.Velocity = move * player.MoveSpeed;
            p.Position = p.Position + p.Velocity * TickSeconds;
            if (move.X > 0f) p.FacingX = Facing.Right;
            else if (move.X < 0f) p.FacingX = Facing.Left;
        }

        // 플레이어를 향해 직선으로. 다 왔으면 플레이어 자리에서 멈춘다.
        void StepEnemies(World world)
        {
            Vec2 target = world.Player.Position;
            EnemyState[] list = world.Enemies;
            for (int i = 0; i < world.EnemyCount; i++)
            {
                ref EnemyState e = ref list[i];
                e.PreviousPosition = e.Position;
                Vec2 to = target - e.Position;
                float distance = to.Magnitude;
                float step = enemies.Kinds[e.Kind].MoveSpeed * TickSeconds;
                e.Position = distance <= step ? target : e.Position + to * (step / distance);
            }
            separation.Apply(list, world.EnemyCount, enemies.Kinds, enemies.SeparationPush);
        }

        // 시간대의 초당 마릿수만큼 몫을 쌓고 1이 될 때마다 한 마리. 가득 찼으면 그 몫은 버린다(나중에 몰아서 나오지 않게).
        void StepSpawning(World world)
        {
            if (world.SpawningPaused) return;
            int phaseIndex = enemies.PhaseAt(ElapsedSeconds(world));
            if (phaseIndex < 0) return;
            SpawnPhaseData phase = enemies.Phases[phaseIndex];
            world.SpawnBudget += phase.SpawnsPerSecond * TickSeconds;
            while (world.SpawnBudget >= 1f)
            {
                world.SpawnBudget -= 1f;
                TrySpawn(world, phase.Kind);
            }
        }

        bool TrySpawn(World world, int kind)
        {
            if (world.EnemyCount >= world.Enemies.Length) return false;
            float angle = world.SpawnRandom.Range(0f, FullTurn);
            Vec2 offset = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle)) * enemies.SpawnRadius;
            Vec2 position = world.Player.Position + offset;
            world.Enemies[world.EnemyCount] = new EnemyState
            {
                Id = world.AllocateEntityId(),
                Kind = kind,
                Position = position,
                PreviousPosition = position,
            };
            world.EnemyCount++;
            world.EnemiesSpawnedTotal++;
            return true;
        }

        /// <summary>지금 남은 자리만큼 count마리를 바로 스폰(디버그 · 나중의 이벤트). 종류는 지금 시간대의 것(첫 시간대 전이면 첫 시간대). 스폰한 수를 돌려준다.</summary>
        public int SpawnEnemies(World world, int count)
        {
            if (enemies.Phases.Length == 0) return 0;
            int phaseIndex = Math.Max(0, enemies.PhaseAt(ElapsedSeconds(world)));
            int kind = enemies.Phases[phaseIndex].Kind;
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TrySpawn(world, kind)) break;
                spawned++;
            }
            return spawned;
        }

        /// <summary>적을 모두 없앤다(디버그). ID는 되돌리지 않는다 — 새 적은 새 ID.</summary>
        public void ClearEnemies(World world)
        {
            world.EnemyCount = 0;
            world.SpawnBudget = 0f;
        }

        /// <summary>보간 없이 바로 옮긴다(디버그 순간이동 · 나중의 재배치).</summary>
        public void TeleportPlayer(World world, Vec2 position)
        {
            world.Player.Position = position;
            world.Player.PreviousPosition = position;
        }
    }
}
