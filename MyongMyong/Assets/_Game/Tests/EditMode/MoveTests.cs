using Myong.Core;
using NUnit.Framework;

namespace Myong.Tests
{
    /// <summary>이동 규칙(1-1). 여기의 숫자는 테스트용 값이다(게임 수치는 PlayerTuning 에셋).</summary>
    public sealed class MoveTests
    {
        const int TicksPerSecond = 60;
        const float Speed = 5f;
        const float Tolerance = 0.0001f;

        static Simulation NewSimulation() => new Simulation(TicksPerSecond, new PlayerTuningData(Speed));

        static void Run(Simulation simulation, World world, Vec2 move, int ticks)
        {
            var input = new InputFrame { Move = move };
            for (int i = 0; i < ticks; i++) simulation.Step(world, input);
        }

        [Test]
        public void DiagonalSpeed_EqualsStraightSpeed()
        {
            var simulation = NewSimulation();
            var straight = simulation.CreateWorld(0);
            var diagonal = simulation.CreateWorld(0);
            Run(simulation, straight, new Vec2(1f, 0f), 1);
            Run(simulation, diagonal, new Vec2(1f, 1f), 1);

            Assert.AreEqual(Speed, straight.Player.Velocity.Magnitude, Tolerance);
            Assert.AreEqual(straight.Player.Velocity.Magnitude, diagonal.Player.Velocity.Magnitude, Tolerance);
        }

        [Test]
        public void AllEightDirections_HaveTheSameSpeed()
        {
            var simulation = NewSimulation();
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    var world = simulation.CreateWorld(0);
                    Run(simulation, world, new Vec2(x, y), TicksPerSecond);
                    Assert.AreEqual(Speed, world.Player.Position.Magnitude, Tolerance * TicksPerSecond, $"direction ({x},{y})");
                    Assert.AreEqual(System.Math.Sign(x), System.Math.Sign(world.Player.Position.X));
                    Assert.AreEqual(System.Math.Sign(y), System.Math.Sign(world.Player.Position.Y));
                }
            }
        }

        [Test]
        public void Position_AfterNTicks_IsSpeedTimesTime()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(0);
            int ticks = TicksPerSecond * 2;
            Run(simulation, world, new Vec2(1f, 0f), ticks);

            Assert.AreEqual(ticks, world.Tick);
            Assert.AreEqual(Speed * 2f, world.Player.Position.X, Tolerance * ticks);
            Assert.AreEqual(0f, world.Player.Position.Y, Tolerance);
        }

        [Test]
        public void PartialStick_KeepsItsMagnitude()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(0);
            Run(simulation, world, new Vec2(0.5f, 0f), 1);
            Assert.AreEqual(Speed * 0.5f, world.Player.Velocity.Magnitude, Tolerance);
        }

        [Test]
        public void NoInput_StopsAndStays()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(0);
            Run(simulation, world, new Vec2(0f, 1f), 10);
            Vec2 before = world.Player.Position;
            Run(simulation, world, Vec2.Zero, 10);

            Assert.AreEqual(0f, world.Player.Velocity.Magnitude, Tolerance);
            Assert.AreEqual(before, world.Player.Position);
        }

        [Test]
        public void PreviousPosition_IsThePositionOneTickAgo()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(0);
            Run(simulation, world, new Vec2(1f, 0f), 3);
            Vec2 before = world.Player.Position;
            Run(simulation, world, new Vec2(1f, 0f), 1);
            Assert.AreEqual(before, world.Player.PreviousPosition);
        }

        [Test]
        public void Facing_FollowsHorizontalInput_AndKeepsWhenVertical()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(0);
            Assert.AreEqual(Facing.Right, world.Player.FacingX);
            Run(simulation, world, new Vec2(-1f, 0f), 1);
            Assert.AreEqual(Facing.Left, world.Player.FacingX);
            Run(simulation, world, new Vec2(0f, 1f), 1);
            Assert.AreEqual(Facing.Left, world.Player.FacingX);
            Run(simulation, world, new Vec2(1f, -1f), 1);
            Assert.AreEqual(Facing.Right, world.Player.FacingX);
        }

        [Test]
        public void Teleport_MovesWithoutInterpolationTrail()
        {
            var simulation = NewSimulation();
            var world = simulation.CreateWorld(0);
            var target = new Vec2(12f, -7f);
            simulation.TeleportPlayer(world, target);
            Assert.AreEqual(target, world.Player.Position);
            Assert.AreEqual(target, world.Player.PreviousPosition);
        }

        [Test]
        public void SameSeedAndInputs_GiveTheSameResult()
        {
            var simulation = NewSimulation();
            var a = simulation.CreateWorld(7);
            var b = simulation.CreateWorld(7);
            var moves = new[] { new Vec2(1f, 0f), new Vec2(1f, 1f), new Vec2(-0.3f, 0.8f), Vec2.Zero, new Vec2(0f, -1f) };
            foreach (var move in moves)
            {
                Run(simulation, a, move, 17);
                Run(simulation, b, move, 17);
            }
            Assert.AreEqual(a.Player.Position, b.Player.Position);
            Assert.AreEqual(a.Tick, b.Tick);
            Assert.AreEqual(a.Seed, b.Seed);
        }

        [Test]
        public void World_GivesThePlayerAnId()
        {
            var world = NewSimulation().CreateWorld(0);
            Assert.Greater(world.Player.Id, 0);
            Assert.AreNotEqual(world.Player.Id, world.AllocateEntityId());
        }
    }
}
