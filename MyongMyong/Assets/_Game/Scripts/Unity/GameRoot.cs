using System;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 컴포지션 루트. 씬의 모든 것을 여기서 잇는다: 텍스트 표 읽기, 한 판(World) 만들기,
    /// 고정 틱으로 시뮬레이션 돌리기, 표시 쪽에 상태 넘기기. 디버그 진입점(DebugXxx)도 여기 모은다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameRoot : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] TextAsset stringsCsv;
        [SerializeField] PlayerTuning playerTuning;
        [SerializeField] SimulationTuning simulationTuning;
        [SerializeField] EnemyTable enemyTable;
        [SerializeField] SpawnTable spawnTable;

        [Header("Scene")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerView playerView;
        [SerializeField] EnemyRenderPool enemyView;

        [Header("Seed")]
        [Tooltip("Off: a new seed every run. On: always Fixed Seed.")]
        [SerializeField] bool useFixedSeed;
        [SerializeField] int fixedSeed;

        Simulation simulation;
        World world;
        float accumulator;
        readonly System.Diagnostics.Stopwatch stepTimer = new System.Diagnostics.Stopwatch();
        double lastStepMilliseconds;
        float frameMillisecondsAverage;

        void Awake()
        {
            if (stringsCsv != null) Text.Load(TextTable.ParseCsv(stringsCsv.text));
            StartRun(useFixedSeed ? fixedSeed : Environment.TickCount);
        }

        void StartRun(int seed)
        {
            EnemyRulesData enemyRules = enemyTable != null && spawnTable != null ? spawnTable.ToRules(enemyTable) : EnemyRulesData.None;
            simulation = new Simulation(simulationTuning.TicksPerSecond, playerTuning.ToData(), enemyRules);
            world = simulation.CreateWorld(seed);
            accumulator = 0f;
            if (enemyView != null) enemyView.Prepare(enemyRules.MaxAlive, enemyTable);
        }

        void Update()
        {
            float tick = simulation.TickSeconds;
            int budget = simulationTuning.MaxTicksPerFrame;
            var frame = new InputFrame { Move = input != null ? input.ReadMove() : Vec2.Zero };

            stepTimer.Restart();
            accumulator += Time.deltaTime;
            while (accumulator >= tick && budget > 0)
            {
                simulation.Step(world, frame);
                accumulator -= tick;
                budget--;
            }
            // 한 프레임에 낼 수 있는 틱을 다 쓰고도 시간이 남으면 버린다(멈추는 대신 느려진다).
            if (accumulator >= tick) accumulator = 0f;
            stepTimer.Stop();
            lastStepMilliseconds = stepTimer.Elapsed.TotalMilliseconds;
            frameMillisecondsAverage += (Time.unscaledDeltaTime * 1000f - frameMillisecondsAverage) * DebugFrameAverageWeight;

            float alpha = accumulator / tick;
            if (playerView != null) playerView.Render(world.Player, alpha, playerTuning.MoveSpeed);
            if (enemyView != null) enemyView.Render(world.Enemies, world.EnemyCount, alpha);
        }

        // ---- 디버그 진입점 (에디터 디버그 창 Myong/Debug/Debug Window 가 부른다) ----

        const float DebugFrameAverageWeight = 0.05f;

        public bool DebugIsRunning => world != null;

        public int DebugSeed => world.Seed;

        public long DebugTick => world.Tick;

        public float DebugElapsedSeconds => simulation.ElapsedSeconds(world);

        /// <summary>이번 프레임에 시뮬레이션(Step 여러 번)에 든 시간(ms).</summary>
        public double DebugStepMilliseconds => lastStepMilliseconds;

        /// <summary>프레임 시간(ms)의 이동 평균.</summary>
        public float DebugFrameMilliseconds => frameMillisecondsAverage;

        /// <summary>살아 있는 적 수.</summary>
        public int DebugEnemyCount => world.EnemyCount;

        public int DebugEnemyCapacity => world.Enemies.Length;

        public long DebugEnemiesSpawnedTotal => world.EnemiesSpawnedTotal;

        public bool DebugSpawningPaused => world.SpawningPaused;

        /// <summary>적 하나의 상태(확인용, 복사본).</summary>
        public EnemyState DebugEnemy(int index) => world.Enemies[index];

        /// <summary>[상태 변경] 지금 적 count마리를 바로 스폰(최대 수까지). 스폰한 수를 돌려준다.</summary>
        public int DebugSpawnEnemies(int count)
        {
            return simulation.SpawnEnemies(world, count);
        }

        /// <summary>[상태 변경] 적 모두 제거.</summary>
        public void DebugClearEnemies()
        {
            simulation.ClearEnemies(world);
        }

        /// <summary>[상태 변경] 시간대 스폰 멈춤 / 재개.</summary>
        public void DebugSetSpawningPaused(bool paused)
        {
            world.SpawningPaused = paused;
        }

        public Vec2 DebugPlayerPosition => world.Player.Position;

        public Vec2 DebugPlayerVelocity => world.Player.Velocity;

        public int DebugPlayerFacingX => world.Player.FacingX;

        /// <summary>[상태 변경] 같은 시드로 판을 처음부터. 수치 에셋도 다시 읽는다.</summary>
        public void DebugRestart()
        {
            StartRun(world.Seed);
        }

        /// <summary>[상태 변경] 시드를 정해서 판을 처음부터.</summary>
        public void DebugRestartWithSeed(int seed)
        {
            StartRun(seed);
        }

        /// <summary>[상태 변경] 플레이어를 그 자리로.</summary>
        public void DebugTeleportPlayer(float x, float y)
        {
            simulation.TeleportPlayer(world, new Vec2(x, y));
        }

        /// <summary>[상태 변경] 실제 입력 대신 이 이동 벡터를 넣는다. DebugClearMoveInput으로 되돌린다.</summary>
        public void DebugSetMoveInput(float x, float y)
        {
            if (input != null) input.DebugSetMove(new Vec2(x, y));
        }

        public void DebugClearMoveInput()
        {
            if (input != null) input.DebugClearMove();
        }

        /// <summary>피격 애니메이션만 재생(보기만).</summary>
        public void DebugPlayHit()
        {
            if (playerView != null) playerView.PlayHit();
        }

        /// <summary>표시 언어 바꾸기(보기만).</summary>
        public void DebugSetLanguage(Language language)
        {
            Text.SetLanguage(language);
        }
    }
}
