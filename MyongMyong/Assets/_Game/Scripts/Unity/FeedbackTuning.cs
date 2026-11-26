using UnityEngine;

namespace Myong.Unity
{
    /// <summary>피격 · 사망 연출 수치 표(표시만 — 규칙에는 영향이 없다). 코드에 숫자를 쓰지 않고 이 에셋에서 읽는다.</summary>
    [CreateAssetMenu(menuName = "Myong/Data/Feedback Tuning", fileName = "FeedbackTuning")]
    public sealed class FeedbackTuning : ScriptableObject
    {
        [Header("Player hurt")]
        [Tooltip("Seconds the whole body stays white right after a hit (rounded to ticks).")]
        [SerializeField, Min(0f)] float playerFlashSeconds;

        [Tooltip("While invulnerable after a hit: seconds of each dim / normal half of the blink (rounded to ticks, 0 = no blink). The blink ends when the invulnerability ends.")]
        [SerializeField, Min(0f)] float playerBlinkSeconds;

        [Tooltip("Alpha of the dim half of the blink.")]
        [SerializeField, Range(0f, 1f)] float playerBlinkAlpha;

        [Header("Player death")]
        [Tooltip("Seconds between the end of the death animation and the game over window.")]
        [SerializeField, Min(0f)] float gameOverDelaySeconds;

        [Tooltip("The game over window opens after this many seconds even if the death animation never reports its end.")]
        [SerializeField, Min(0f)] float deathMaxWaitSeconds;

        [Header("Enemies")]
        [Tooltip("Seconds an enemy stays white after a bullet hit (rounded to ticks).")]
        [SerializeField, Min(0f)] float enemyFlashSeconds;

        [Tooltip("Seconds of the vanish effect where an enemy was killed (rounded to ticks, 0 = none).")]
        [SerializeField, Min(0f)] float enemyDeathSeconds;

        [Tooltip("Number of size steps of the vanish effect (stepped, not smooth).")]
        [SerializeField, Min(1)] int enemyDeathSteps = 1;

        [Tooltip("Size of the first step of the vanish effect (1 = the enemy's own size). Later steps shrink evenly to nothing.")]
        [SerializeField, Min(0f)] float enemyDeathPopScale = 1f;

        [Tooltip("Vanish effects shown at once (pooled renderers). When more enemies die, the oldest effect is reused.")]
        [SerializeField, Min(0)] int maxDeathEffects;

        public float PlayerFlashSeconds => playerFlashSeconds;

        public float PlayerBlinkSeconds => playerBlinkSeconds;

        public float PlayerBlinkAlpha => playerBlinkAlpha;

        public float GameOverDelaySeconds => gameOverDelaySeconds;

        public float DeathMaxWaitSeconds => deathMaxWaitSeconds;

        public float EnemyFlashSeconds => enemyFlashSeconds;

        public float EnemyDeathSeconds => enemyDeathSeconds;

        public int EnemyDeathSteps => enemyDeathSteps;

        public float EnemyDeathPopScale => enemyDeathPopScale;

        public int MaxDeathEffects => maxDeathEffects;
    }
}
