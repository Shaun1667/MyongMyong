using Myong.Core;
using Myong.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Myong.Tests
{
    /// <summary>
    /// 피격 · 사망 연출(1-10): 틱 환산 · 플레이어 번쩍임과 깜빡임(무적 시간과 맞음) · 적 번쩍임 · 소멸 단계 ·
    /// 처치된 적 찾기(Core 상태만 읽음, 할당 없음) · 연출 에셋과 리그. 여기의 숫자는 테스트용 값이다(게임 수치는 에셋).
    /// </summary>
    public sealed class FeedbackTests
    {
        const int TicksPerSecond = 60;
        const int FlashTicks = 6;
        const int BlinkTicks = 4;

        [Test]
        public void TicksOf_RoundsSeconds_ZeroIsOff_AndPositiveIsAtLeastOneTick()
        {
            Assert.AreEqual(0, FeedbackRules.TicksOf(0f, TicksPerSecond));
            Assert.AreEqual(0, FeedbackRules.TicksOf(-1f, TicksPerSecond));
            Assert.AreEqual(6, FeedbackRules.TicksOf(0.1f, TicksPerSecond));
            Assert.AreEqual(4, FeedbackRules.TicksOf(0.066f, TicksPerSecond));
            Assert.AreEqual(2, FeedbackRules.TicksOf(0.033f, TicksPerSecond));
            Assert.AreEqual(1, FeedbackRules.TicksOf(0.001f, TicksPerSecond));
            Assert.AreEqual(12, FeedbackRules.TicksOf(0.2f, TicksPerSecond));
        }

        [Test]
        public void PlayerLook_NeverHurt_IsNormal()
        {
            Assert.AreEqual(PlayerLook.Normal, FeedbackRules.PlayerLookAt(0, PlayerState.NeverHurt, 0, FlashTicks, BlinkTicks));
            Assert.AreEqual(PlayerLook.Normal, FeedbackRules.PlayerLookAt(5000, PlayerState.NeverHurt, 0, FlashTicks, BlinkTicks));
        }

        [Test]
        public void PlayerLook_FlashesWhiteFirst_ThenBlinks_AndTheBlinkEndsWithTheInvulnerability()
        {
            const long hurt = 100;
            const long until = 130;
            for (long t = hurt; t <= hurt + FlashTicks; t++) Assert.AreEqual(PlayerLook.Flash, Look(t, hurt, until), "tick " + t);
            for (long t = 107; t <= 109; t++) Assert.AreEqual(PlayerLook.Normal, Look(t, hurt, until), "tick " + t);
            for (long t = 110; t <= 113; t++) Assert.AreEqual(PlayerLook.BlinkDim, Look(t, hurt, until), "tick " + t);
            for (long t = 114; t <= 117; t++) Assert.AreEqual(PlayerLook.Normal, Look(t, hurt, until), "tick " + t);
            for (long t = 118; t <= 121; t++) Assert.AreEqual(PlayerLook.BlinkDim, Look(t, hurt, until), "tick " + t);
            for (long t = 122; t <= 125; t++) Assert.AreEqual(PlayerLook.Normal, Look(t, hurt, until), "tick " + t);
            // 마지막 흐린 구간이 끝나는 틱 = 무적이 끝나는 틱.
            for (long t = 126; t <= 129; t++) Assert.AreEqual(PlayerLook.BlinkDim, Look(t, hurt, until), "tick " + t);
            for (long t = until; t <= until + 40; t++) Assert.AreEqual(PlayerLook.Normal, Look(t, hurt, until), "tick " + t);
        }

        [Test]
        public void PlayerLook_NoBlinkSetting_OnlyFlashes_AndNoFlashSetting_OnlyBlinks()
        {
            Assert.AreEqual(PlayerLook.Flash, FeedbackRules.PlayerLookAt(103, 100, 130, FlashTicks, 0));
            for (long t = 107; t < 140; t++) Assert.AreEqual(PlayerLook.Normal, FeedbackRules.PlayerLookAt(t, 100, 130, FlashTicks, 0), "tick " + t);
            Assert.AreNotEqual(PlayerLook.Flash, FeedbackRules.PlayerLookAt(100, 100, 130, 0, BlinkTicks));
            Assert.AreEqual(PlayerLook.BlinkDim, FeedbackRules.PlayerLookAt(129, 100, 130, 0, BlinkTicks));
            // 무적 시간 없이 맞은 경우(디버그 피해): 번쩍임만.
            Assert.AreEqual(PlayerLook.Flash, FeedbackRules.PlayerLookAt(101, 100, 0, FlashTicks, BlinkTicks));
            Assert.AreEqual(PlayerLook.Normal, FeedbackRules.PlayerLookAt(107, 100, 0, FlashTicks, BlinkTicks));
        }

        static PlayerLook Look(long tick, long hurt, long until) => FeedbackRules.PlayerLookAt(tick, hurt, until, FlashTicks, BlinkTicks);

        [Test]
        public void PlayerLook_FollowsARealHit_WhiteAtOnce_NotNormalAtTheLastInvulnerableTick_NormalAfter()
        {
            var simulation = NewSimulation(contactDamage: 10f, enemyHealth: 1000f, weapons: null);
            var world = simulation.CreateWorld(1);
            PlaceEnemy(world, new Vec2(0.5f, 0f), 1000f);
            simulation.Step(world, new InputFrame());
            ref readonly PlayerState p = ref world.Player;
            Assert.AreNotEqual(PlayerState.NeverHurt, p.LastHurtTick, "the enemy did not touch");
            Assert.AreEqual(simulation.HurtInvulnerableTicks, p.InvulnerableUntilTick - p.LastHurtTick);

            // 맞은 틱 바로 뒤의 화면(World.Tick = 맞은 틱 + 1)부터 흰색.
            Assert.AreEqual(PlayerLook.Flash, FeedbackRules.PlayerLookAt(world.Tick, p.LastHurtTick, p.InvulnerableUntilTick, FlashTicks, BlinkTicks));
            Assert.AreEqual(PlayerLook.BlinkDim, FeedbackRules.PlayerLookAt(p.InvulnerableUntilTick - 1, p.LastHurtTick, p.InvulnerableUntilTick, FlashTicks, BlinkTicks));
            Assert.AreEqual(PlayerLook.Normal, FeedbackRules.PlayerLookAt(p.InvulnerableUntilTick, p.LastHurtTick, p.InvulnerableUntilTick, FlashTicks, BlinkTicks));
        }

        [Test]
        public void EnemyFlash_LastsTheGivenTicksAfterAHit()
        {
            Assert.IsFalse(FeedbackRules.EnemyFlashAt(10, EnemyState.NeverHit, 2));
            Assert.IsTrue(FeedbackRules.EnemyFlashAt(10, 10, 2));
            Assert.IsTrue(FeedbackRules.EnemyFlashAt(11, 10, 2));
            Assert.IsTrue(FeedbackRules.EnemyFlashAt(12, 10, 2));
            Assert.IsFalse(FeedbackRules.EnemyFlashAt(13, 10, 2));
            Assert.IsFalse(FeedbackRules.EnemyFlashAt(11, 10, 0), "flash turned off");
        }

        [Test]
        public void DeathEffect_StepsThroughTheDuration_PopsFirst_ThenShrinksToNothing()
        {
            const int duration = 12;
            const int steps = 4;
            Assert.AreEqual(-1, FeedbackRules.DeathStepAt(-1, duration, steps));
            for (int age = 0; age < duration; age++) Assert.AreEqual(age / 3, FeedbackRules.DeathStepAt(age, duration, steps), "age " + age);
            Assert.AreEqual(-1, FeedbackRules.DeathStepAt(duration, duration, steps));
            Assert.AreEqual(-1, FeedbackRules.DeathStepAt(0, 0, steps), "effect turned off");

            Assert.AreEqual(1.25f, FeedbackRules.DeathScaleAt(0, steps, 1.25f), 1e-5f);
            float previous = float.MaxValue;
            for (int step = 0; step < steps; step++)
            {
                float scale = FeedbackRules.DeathScaleAt(step, steps, 1.25f);
                Assert.Less(scale, previous, "step " + step);
                Assert.Greater(scale, 0f, "step " + step);
                previous = scale;
            }
            Assert.AreEqual(0f, FeedbackRules.DeathScaleAt(steps, steps, 1.25f));
            Assert.AreEqual(0f, FeedbackRules.DeathScaleAt(-1, steps, 1.25f));
        }

        // ---- 처치된 적 찾기 ----

        static EnemyState Enemy(int id, int kind, float x, float y) => new EnemyState
        {
            Id = id,
            Kind = kind,
            Position = new Vec2(x, y),
            PreviousPosition = new Vec2(x, y),
            Health = 1f,
            LastHitTick = EnemyState.NeverHit,
        };

        [Test]
        public void DeathTracker_FindsTheKilledEnemy_WhereItWasLastSeen()
        {
            var tracker = new EnemyDeathTracker(8);
            var enemies = new EnemyState[8];
            for (int i = 0; i < 5; i++) enemies[i] = Enemy(i + 1, i % 2, i * 2f, 1f);
            Assert.AreEqual(0, tracker.Observe(enemies, 5, 0));

            // Core처럼: 빠진 자리를 맨 끝 적이 채운다(ID 2가 죽고 ID 5가 그 자리로).
            enemies[1] = enemies[4];
            Assert.AreEqual(1, tracker.Observe(enemies, 4, 1));
            Assert.AreEqual(1, tracker.DeathCount);
            Assert.AreEqual(2f, tracker.DeathPosition(0).X);
            Assert.AreEqual(1f, tracker.DeathPosition(0).Y);
            Assert.AreEqual(1, tracker.DeathKind(0));

            // 그 뒤로 변화가 없으면 다시 알리지 않는다.
            Assert.AreEqual(0, tracker.Observe(enemies, 4, 1));
        }

        [Test]
        public void DeathTracker_FindsSeveralKills_AndIgnoresNewEnemies()
        {
            var tracker = new EnemyDeathTracker(8);
            var enemies = new EnemyState[8];
            for (int i = 0; i < 4; i++) enemies[i] = Enemy(i + 1, 0, i, 0f);
            tracker.Observe(enemies, 4, 0);

            // ID 1 · 3이 죽고, 새 적 ID 5 · 6이 들어왔다.
            enemies[0] = enemies[3]; // 4 → 0
            enemies[2] = Enemy(5, 1, 9f, 9f);
            enemies[3] = Enemy(6, 1, 8f, 8f);
            Assert.AreEqual(2, tracker.Observe(enemies, 4, 2));
            float x0 = tracker.DeathPosition(0).X;
            float x1 = tracker.DeathPosition(1).X;
            Assert.IsTrue((x0 == 0f && x1 == 2f) || (x0 == 2f && x1 == 0f), $"deaths at x {x0}, {x1}");

            // 새 적은 다음부터 본 것으로 친다.
            Assert.AreEqual(1, tracker.Observe(enemies, 3, 3));
            Assert.AreEqual(8f, tracker.DeathPosition(0).X);
            Assert.AreEqual(1, tracker.DeathKind(0));
        }

        [Test]
        public void DeathTracker_RemovedWithoutAKill_IsNotADeath()
        {
            var tracker = new EnemyDeathTracker(8);
            var enemies = new EnemyState[8];
            for (int i = 0; i < 4; i++) enemies[i] = Enemy(i + 1, 0, i, 0f);
            tracker.Observe(enemies, 4, 0);

            // 디버그 "모두 제거" · 돌진하다 사라짐: 처치 누계가 그대로면 연출 없음.
            Assert.AreEqual(0, tracker.Observe(enemies, 0, 0));
            // 그 뒤의 처치에서 예전에 사라진 적이 뒤늦게 나오지 않는다.
            enemies[0] = Enemy(9, 0, 5f, 5f);
            Assert.AreEqual(0, tracker.Observe(enemies, 1, 0));
            Assert.AreEqual(1, tracker.Observe(enemies, 0, 1));
            Assert.AreEqual(5f, tracker.DeathPosition(0).X);
        }

        [Test]
        public void DeathTracker_Reset_ForgetsWhatItSaw()
        {
            var tracker = new EnemyDeathTracker(4);
            var enemies = new EnemyState[4];
            for (int i = 0; i < 4; i++) enemies[i] = Enemy(i + 1, 0, i, 0f);
            tracker.Observe(enemies, 4, 7);
            tracker.Reset();
            // 새 판: 처치 누계가 0부터 다시 오르고, 앞 판의 적은 죽은 것으로 치지 않는다.
            Assert.AreEqual(0, tracker.Observe(enemies, 0, 1));
            Assert.AreEqual(0, tracker.DeathCount);
        }

        [Test]
        public void DeathTracker_SeesEveryKillOfARealRun_AndReadsOnly()
        {
            var weapons = new WeaponRulesData(new[] { new WeaponKindData(0.05f, 20f, 50f, 15f, 1, 0, 0.1f) }, new[] { 0 }, 64, Vec2.Zero);
            var simulation = NewSimulation(contactDamage: 0f, enemyHealth: 10f, weapons: weapons, spawnsPerSecond: 6f, enemySpeed: 1f);
            var watched = simulation.CreateWorld(77);
            var plain = simulation.CreateWorld(77);
            var tracker = new EnemyDeathTracker(watched.Enemies.Length);
            var input = new InputFrame();
            long found = 0;
            for (int i = 0; i < 1800; i++)
            {
                simulation.Step(watched, input);
                simulation.Step(plain, input);
                found += tracker.Observe(watched.Enemies, watched.EnemyCount, watched.EnemiesKilledTotal);
            }
            Assert.Greater(watched.EnemiesKilledTotal, 20, "the run killed too few enemies to test");
            // 틱마다 보면 처치 하나에 하나씩(스폰된 틱에는 죽지 않는다 — 새 적은 그 틱에 맞지 않는다).
            Assert.AreEqual(watched.EnemiesKilledTotal, found);
            // 보기만 한다: 본 판과 안 본 판이 같다.
            Assert.AreEqual(plain.EnemiesKilledTotal, watched.EnemiesKilledTotal);
            Assert.AreEqual(plain.EnemyCount, watched.EnemyCount);
            Assert.AreEqual(plain.NextEntityId, watched.NextEntityId);
            Assert.AreEqual(plain.Player.Health, watched.Player.Health);
        }

        [Test]
        public void DeathTracker_Observe_DoesNotAllocate()
        {
            const int capacity = 300;
            var tracker = new EnemyDeathTracker(capacity);
            var enemies = new EnemyState[capacity];
            for (int i = 0; i < capacity; i++) enemies[i] = Enemy(i + 1, i % 3, i, i);
            int count = capacity;
            long killed = 0;
            int nextId = capacity + 1;
            tracker.Observe(enemies, count, killed);

            Assert.That(() =>
            {
                for (int frame = 0; frame < 120; frame++)
                {
                    // 하나 죽고(맨 끝 것이 그 자리로) 하나 새로 온다.
                    enemies[frame % count] = enemies[count - 1];
                    enemies[count - 1] = Enemy(nextId++, 0, frame, frame);
                    killed++;
                    tracker.Observe(enemies, count, killed);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(1, tracker.DeathCount);
        }

        // ---- 에셋 · 리그 ----

        [Test]
        public void FeedbackTuning_Exists_AndFitsTheInvulnerability()
        {
            var feedback = AssetDatabase.LoadAssetAtPath<FeedbackTuning>("Assets/_Game/Data/FeedbackTuning.asset");
            var player = AssetDatabase.LoadAssetAtPath<PlayerTuning>("Assets/_Game/Data/PlayerTuning.asset");
            var simulation = AssetDatabase.LoadAssetAtPath<SimulationTuning>("Assets/_Game/Data/SimulationTuning.asset");
            Assert.IsNotNull(feedback);
            int tps = simulation.TicksPerSecond;
            int flash = FeedbackRules.TicksOf(feedback.PlayerFlashSeconds, tps);
            int blink = FeedbackRules.TicksOf(feedback.PlayerBlinkSeconds, tps);
            int invulnerable = FeedbackRules.TicksOf(player.HurtInvulnerableSeconds, tps);
            Assert.GreaterOrEqual(flash, 1, "no white flash");
            Assert.GreaterOrEqual(blink, 1, "no blink");
            // 번쩍임 뒤에 깜빡임이 적어도 한 번(흐림 + 보통) 들어간다.
            Assert.LessOrEqual(flash + 2 * blink, invulnerable, "the blink does not fit in the invulnerability");
            Assert.Less(feedback.PlayerBlinkAlpha, 1f, "the dim half is not dim");
            Assert.Greater(feedback.DeathMaxWaitSeconds, 0f);
            Assert.GreaterOrEqual(feedback.GameOverDelaySeconds, 0f);

            int enemyFlash = FeedbackRules.TicksOf(feedback.EnemyFlashSeconds, tps);
            Assert.GreaterOrEqual(enemyFlash, 1, "no enemy flash");
            Assert.LessOrEqual(enemyFlash, 2, "enemy flash is 1 to 2 ticks");
            Assert.GreaterOrEqual(FeedbackRules.TicksOf(feedback.EnemyDeathSeconds, tps), feedback.EnemyDeathSteps, "a vanish step shorter than a tick");
            Assert.GreaterOrEqual(feedback.EnemyDeathSteps, 1);
            Assert.Greater(feedback.EnemyDeathPopScale, 0f);
            Assert.Greater(feedback.MaxDeathEffects, 0);
        }

        [Test]
        public void PlayerRig_HasTheDeathClip_TheFlashMaterial_AndTheDeadParameter()
        {
            var death = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Game/Art/Characters/Player/Animations/Player_Death.anim");
            Assert.IsNotNull(death, "Player_Death.anim missing (run Myong/Setup/Build Player Rig)");
            Assert.IsFalse(death.isLooping, "the death clip must stop on its last pose");
            // 컷아웃 규칙: Transform 키만, 계단식(키 사이 보간 없음).
            foreach (var binding in AnimationUtility.GetCurveBindings(death))
            {
                Assert.AreEqual(typeof(Transform), binding.type, binding.path + " " + binding.propertyName);
                foreach (var key in AnimationUtility.GetEditorCurve(death, binding).keys)
                {
                    Assert.IsTrue(float.IsInfinity(key.outTangent), "not a stepped key: " + binding.path + " " + binding.propertyName);
                }
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab");
            var view = prefab.GetComponent<PlayerView>();
            Assert.IsNotNull(new SerializedObject(view).FindProperty("flashMaterial").objectReferenceValue, "PlayerView has no flash material");

            var controller = (UnityEditor.Animations.AnimatorController)prefab.GetComponent<Animator>().runtimeAnimatorController;
            bool hasDead = false;
            foreach (var parameter in controller.parameters) hasDead |= parameter.name == "Dead" && parameter.type == AnimatorControllerParameterType.Bool;
            Assert.IsTrue(hasDead, "the controller has no Dead parameter");
            bool hasDeath = false;
            foreach (var state in controller.layers[0].stateMachine.states) hasDeath |= state.state.name == "Death" && state.state.motion == death;
            Assert.IsTrue(hasDeath, "the controller has no Death state");
        }

        // ---- 도우미 ----

        static Simulation NewSimulation(float contactDamage, float enemyHealth, WeaponRulesData weapons, float spawnsPerSecond = 0f, float enemySpeed = 0f)
        {
            var enemies = new EnemyRulesData(
                new[] { new EnemyKindData(enemySpeed, 0.3f, enemyHealth, 1, contactDamage) },
                new[] { new SpawnPhaseData(0f, spawnsPerSecond, 0) },
                8f, 300, 0.5f);
            return new Simulation(TicksPerSecond, new PlayerTuningData(5f, 1f, 100f, 0.4f, 0.5f),
                enemies, weapons ?? WeaponRulesData.None, ExperienceRulesData.None, UpgradeRulesData.None);
        }

        static void PlaceEnemy(World world, Vec2 position, float health)
        {
            world.Enemies[world.EnemyCount] = new EnemyState
            {
                Id = world.AllocateEntityId(),
                Kind = 0,
                Position = position,
                PreviousPosition = position,
                Health = health,
                LastHitTick = EnemyState.NeverHit,
            };
            world.EnemyCount++;
        }
    }
}
