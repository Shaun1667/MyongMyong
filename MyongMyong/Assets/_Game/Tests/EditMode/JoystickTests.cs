using System.Linq;
using Myong.Core;
using Myong.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;

namespace Myong.Tests
{
    /// <summary>가상 조이스틱(1-6). 여기의 숫자는 테스트용 값이다(게임 수치는 JoystickTuning 에셋).</summary>
    public sealed class JoystickTests
    {
        const float Radius = 100f;
        const float DeadZone = 0.2f;
        const float Tolerance = 0.0001f;
        const string TuningPath = "Assets/_Game/Data/JoystickTuning.asset";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        const string ScenePath = "Assets/_Game/Scenes/Game.unity";

        static readonly VirtualStickData EightWay = new VirtualStickData(Radius, DeadZone, 8, false);
        static readonly VirtualStickData Analog = new VirtualStickData(Radius, DeadZone, 0, true);

        [Test]
        public void InsideDeadZone_IsZero()
        {
            Assert.AreEqual(Vec2.Zero, VirtualStickRules.Evaluate(Vec2.Zero, EightWay));
            Assert.AreEqual(Vec2.Zero, VirtualStickRules.Evaluate(new Vec2(19f, 0f), EightWay));
            Assert.AreEqual(Vec2.Zero, VirtualStickRules.Evaluate(new Vec2(0f, -20f), EightWay), "on the edge is still inside");
            Assert.AreEqual(Vec2.Zero, VirtualStickRules.Evaluate(new Vec2(12f, 12f), Analog));
            Assert.AreNotEqual(Vec2.Zero, VirtualStickRules.Evaluate(new Vec2(21f, 0f), EightWay));
        }

        [Test]
        public void EightWay_SnapsToTheNearestOfEightDirections_AtFullStrength()
        {
            for (int k = 0; k < 8; k++)
            {
                double angle = k * System.Math.PI / 4.0;
                foreach (double jitter in new[] { -0.3, 0.0, 0.3 }) // 약 ±17도 — 22.5도 안이면 같은 방향
                {
                    var offset = new Vec2((float)(System.Math.Cos(angle + jitter) * 60.0), (float)(System.Math.Sin(angle + jitter) * 60.0));
                    Vec2 result = VirtualStickRules.Evaluate(offset, EightWay);
                    Assert.AreEqual(1f, result.Magnitude, Tolerance, $"direction {k}");
                    Assert.AreEqual((float)System.Math.Cos(angle), result.X, Tolerance, $"direction {k} x");
                    Assert.AreEqual((float)System.Math.Sin(angle), result.Y, Tolerance, $"direction {k} y");
                }
            }
        }

        [Test]
        public void EightWay_StraightDirections_HaveExactZeroOnTheOtherAxis()
        {
            Vec2 up = VirtualStickRules.Evaluate(new Vec2(3f, 80f), EightWay);
            Vec2 right = VirtualStickRules.Evaluate(new Vec2(80f, -5f), EightWay);
            Assert.AreEqual(0f, up.X, "up must not lean right (facing would flip)");
            Assert.AreEqual(1f, up.Y, Tolerance);
            Assert.AreEqual(0f, right.Y);
            Assert.AreEqual(1f, right.X, Tolerance);
        }

        [Test]
        public void EightWay_IsFullStrengthJustOutsideTheDeadZone_LikeTheKeyboard()
        {
            Vec2 near = VirtualStickRules.Evaluate(new Vec2(21f, 0f), EightWay);
            Vec2 far = VirtualStickRules.Evaluate(new Vec2(400f, 0f), EightWay);
            Assert.AreEqual(1f, near.Magnitude, Tolerance);
            Assert.AreEqual(1f, far.Magnitude, Tolerance);
        }

        [Test]
        public void Analog_KeepsDirection_AndScalesFromDeadZoneToRadius()
        {
            Vec2 half = VirtualStickRules.Evaluate(new Vec2(60f, 0f), Analog); // (60 - 20) / (100 - 20) = 0.5
            Assert.AreEqual(0.5f, half.X, Tolerance);
            Vec2 beyond = VirtualStickRules.Evaluate(new Vec2(300f, 400f), Analog);
            Assert.AreEqual(1f, beyond.Magnitude, Tolerance, "clamped at the radius");
            Assert.AreEqual(0.6f, beyond.X, Tolerance, "direction kept (3:4)");
            Assert.AreEqual(0.8f, beyond.Y, Tolerance);
        }

        [Test]
        public void ClampKnob_StopsAtTheRadius()
        {
            Vec2 inside = VirtualStickRules.ClampKnob(new Vec2(30f, 40f), Radius);
            Vec2 outside = VirtualStickRules.ClampKnob(new Vec2(300f, 400f), Radius);
            Assert.AreEqual(new Vec2(30f, 40f), inside);
            Assert.AreEqual(Radius, outside.Magnitude, Tolerance);
            Assert.AreEqual(60f, outside.X, Tolerance);
        }

        [Test]
        public void JoystickInput_MovesLikeTheKeyboard_InTheSimulation()
        {
            var simulation = new Simulation(60, new PlayerTuningData(5f));
            var keyboard = simulation.CreateWorld(0);
            var stick = simulation.CreateWorld(0);
            var keys = new InputFrame { Move = new Vec2(1f, 1f) }; // W + D
            var touch = new InputFrame { Move = VirtualStickRules.Evaluate(new Vec2(70f, 64f), EightWay) };
            for (int i = 0; i < 60; i++)
            {
                simulation.Step(keyboard, keys);
                simulation.Step(stick, touch);
            }
            Assert.AreEqual(keyboard.Player.Position.X, stick.Player.Position.X, Tolerance);
            Assert.AreEqual(keyboard.Player.Position.Y, stick.Player.Position.Y, Tolerance);
        }

        [Test]
        public void StraightUp_KeepsTheFacing()
        {
            var simulation = new Simulation(60, new PlayerTuningData(5f));
            var world = simulation.CreateWorld(0);
            simulation.Step(world, new InputFrame { Move = new Vec2(-1f, 0f) });
            int facing = world.Player.FacingX;
            var up = new InputFrame { Move = VirtualStickRules.Evaluate(new Vec2(2f, 90f), EightWay) };
            for (int i = 0; i < 10; i++) simulation.Step(world, up);
            Assert.AreEqual(facing, world.Player.FacingX);
        }

        // ---- 손가락 상태(VirtualStickPointer): 누르기 · 끌기 · 떼기 · 막기. 화면 쪽 VirtualJoystick은 이 값으로 그리고 입력을 넣기만 한다 ----

        [Test]
        public void Stick_AppearsWhereTouched_FollowsTheDrag_AndReleasesToZero()
        {
            var stick = new VirtualStickPointer(EightWay);
            Assert.IsTrue(stick.Press(0, new Vec2(100f, 50f)));
            Assert.IsTrue(stick.IsActive);
            Assert.AreEqual(new Vec2(100f, 50f), stick.Origin, "origin = touched spot");
            Assert.AreEqual(Vec2.Zero, stick.Value);

            Assert.IsTrue(stick.Drag(0, new Vec2(400f, 50f))); // 300 to the right, beyond the radius
            Assert.AreEqual(1f, stick.Value.X, Tolerance);
            Assert.AreEqual(0f, stick.Value.Y);
            Assert.AreEqual(Radius, stick.KnobOffset.Magnitude, Tolerance, "knob stops at the radius");

            Assert.IsFalse(stick.Press(5, new Vec2(800f, 50f)), "a second finger does not take over");
            Assert.IsFalse(stick.Drag(5, new Vec2(0f, 50f)), "a second finger is ignored");
            Assert.AreEqual(1f, stick.Value.X, Tolerance);
            Assert.IsFalse(stick.Release(5));
            Assert.IsTrue(stick.IsActive);

            Assert.IsTrue(stick.Release(0));
            Assert.IsFalse(stick.IsActive);
            Assert.AreEqual(Vec2.Zero, stick.Value);
            Assert.AreEqual(Vec2.Zero, stick.KnobOffset);

            Assert.IsTrue(stick.Press(1, new Vec2(10f, 10f)), "a new touch starts a new stick at the new spot");
            Assert.AreEqual(new Vec2(10f, 10f), stick.Origin);
        }

        [Test]
        public void Stick_WhenBlocked_ReleasesAndIgnoresNewTouches()
        {
            var stick = new VirtualStickPointer(EightWay);
            stick.Press(0, Vec2.Zero);
            stick.Drag(0, new Vec2(0f, 90f));
            Assert.AreEqual(1f, stick.Value.Y, Tolerance);

            Assert.IsTrue(stick.SetBlocked(true), "level-up / game-over window opened: the held touch is let go");
            Assert.IsFalse(stick.IsActive);
            Assert.AreEqual(Vec2.Zero, stick.Value);
            Assert.IsFalse(stick.Press(0, new Vec2(10f, 10f)), "no touch while blocked");
            Assert.IsFalse(stick.Drag(0, new Vec2(200f, 10f)));
            Assert.AreEqual(Vec2.Zero, stick.Value);

            Assert.IsFalse(stick.SetBlocked(false), "window closed");
            Assert.AreEqual(Vec2.Zero, stick.Value, "still zero until a new touch");
            Assert.IsTrue(stick.Press(0, new Vec2(10f, 10f)));
            stick.Drag(0, new Vec2(-200f, 10f));
            Assert.AreEqual(-1f, stick.Value.X, Tolerance);
        }

        [Test]
        public void BlockedStick_GivesNoMovementInTheSimulation()
        {
            var simulation = new Simulation(60, new PlayerTuningData(5f));
            var world = simulation.CreateWorld(0);
            var stick = new VirtualStickPointer(EightWay);
            stick.Press(0, Vec2.Zero);
            stick.Drag(0, new Vec2(90f, 0f));
            stick.SetBlocked(true);
            for (int i = 0; i < 30; i++) simulation.Step(world, new InputFrame { Move = stick.Value });
            Assert.AreEqual(Vec2.Zero, world.Player.Position);
        }

        [Test]
        public void SceneJoystick_FeedsTheControlTheMoveActionReads()
        {
            // 테스트 어셈블리는 Input System · uGUI를 참조하지 않아 파일 글에서 찾는다.
            // 씬(Setup이 만든 것)의 조이스틱이 값을 넣는 자리 = <Gamepad>/leftStick
            string scene = System.IO.File.ReadAllText(ScenePath);
            var pathInScene = new Regex("stickControlPath: (\\S+)");
            var match = pathInScene.Match(scene);
            Assert.IsTrue(match.Success, "no VirtualJoystick in " + ScenePath + " (run Myong/Setup/Build Game Scene)");
            string path = match.Groups[1].Value;
            Assert.AreEqual("<Gamepad>/leftStick", path);
            StringAssert.Contains("m_Name: Joystick", scene);

            // Player/Move에 그 자리의 바인딩이 있다: { ... "path": "<Gamepad>/leftStick", ... "action": "Move" ... }
            string json = System.IO.File.ReadAllText(InputActionsPath);
            var binding = new Regex("\"path\"\\s*:\\s*\"" + Regex.Escape(path) + "\"[^}]*\"action\"\\s*:\\s*\"Move\"");
            Assert.IsTrue(binding.IsMatch(json), "Player/Move has no binding for " + path);
        }

        [Test]
        public void JoystickTuning_Exists_AndValuesAreValid()
        {
            var asset = AssetDatabase.LoadAssetAtPath<JoystickTuning>(TuningPath);
            Assert.IsNotNull(asset, TuningPath);
            Assert.Greater(asset.Radius, 0f, "radius");
            Assert.GreaterOrEqual(asset.DeadZone, 0f, "dead zone");
            Assert.Less(asset.DeadZone, 1f, "dead zone must leave room to move");
            Assert.IsTrue(asset.DirectionCount == 0 || asset.DirectionCount >= 4, "directions: 0 (analog) or 4+");
            Assert.Greater(asset.KnobRadius, 0f, "knob");
            Assert.LessOrEqual(asset.KnobRadius, asset.Radius, "knob bigger than the stick");
            Assert.Greater(asset.TouchAreaWidth, 0f);
            Assert.LessOrEqual(asset.TouchAreaWidth, 1f);
            var data = asset.ToData();
            Assert.AreEqual(asset.Radius, data.Radius);
            Assert.AreEqual(asset.DeadZone, data.DeadZone);
            Assert.AreEqual(asset.DirectionCount, data.DirectionCount);
            Assert.AreEqual(asset.AnalogSpeed, data.AnalogSpeed);
        }
    }
}
