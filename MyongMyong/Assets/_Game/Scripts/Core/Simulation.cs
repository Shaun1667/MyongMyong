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
        readonly PlayerTuningData player;

        public Simulation(int ticksPerSecond, PlayerTuningData player)
        {
            if (ticksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            TicksPerSecond = ticksPerSecond;
            TickSeconds = 1f / ticksPerSecond;
            this.player = player;
        }

        public int TicksPerSecond { get; }

        public float TickSeconds { get; }

        public World CreateWorld(int seed)
        {
            var world = new World { Seed = seed };
            world.Player.Id = world.AllocateEntityId();
            world.Player.FacingX = Facing.Right;
            return world;
        }

        public void Step(World world, in InputFrame input)
        {
            ref PlayerState p = ref world.Player;
            Vec2 move = MoveRules.ClampMove(input.Move);
            p.PreviousPosition = p.Position;
            p.Velocity = move * player.MoveSpeed;
            p.Position = p.Position + p.Velocity * TickSeconds;
            if (move.X > 0f) p.FacingX = Facing.Right;
            else if (move.X < 0f) p.FacingX = Facing.Left;
            world.Tick++;
        }

        /// <summary>보간 없이 바로 옮긴다(디버그 순간이동 · 나중의 재배치).</summary>
        public void TeleportPlayer(World world, Vec2 position)
        {
            world.Player.Position = position;
            world.Player.PreviousPosition = position;
        }
    }
}
