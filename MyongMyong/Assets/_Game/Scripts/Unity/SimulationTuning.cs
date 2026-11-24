using UnityEngine;

namespace Myong.Unity
{
    /// <summary>고정 틱 설정.</summary>
    [CreateAssetMenu(menuName = "Myong/Data/Simulation Tuning", fileName = "SimulationTuning")]
    public sealed class SimulationTuning : ScriptableObject
    {
        [Tooltip("Simulation ticks per second.")]
        [SerializeField, Min(1)] int ticksPerSecond;

        [Tooltip("Most ticks simulated in one rendered frame. Time beyond that is dropped (the game slows down instead of freezing).")]
        [SerializeField, Min(1)] int maxTicksPerFrame;

        public int TicksPerSecond => ticksPerSecond;

        public int MaxTicksPerFrame => maxTicksPerFrame;
    }
}
