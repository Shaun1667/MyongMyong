using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>한 판의 길이 표: 생존 목표 시간(이만큼 버티면 승리) · 목표 뒤 마무리 시간(1-11). 코드에 숫자를 쓰지 않고 이 에셋에서 읽는다.</summary>
    [CreateAssetMenu(menuName = "Myong/Data/Run Tuning", fileName = "RunTuning")]
    public sealed class RunTuning : ScriptableObject
    {
        [Tooltip("Survive this many seconds (simulation time, paused during level-up choices) to win. 0 = no time victory.")]
        [SerializeField, Min(0f)] float survivalSeconds;

        [Tooltip("After the survival goal: the remaining enemies vanish, only end-of-run waves come (Spawn Table waves starting at or after the goal), and the run ends as a victory when the player is caught or after this many seconds at most. 0 = the run ends right at the goal.")]
        [SerializeField, Min(0f)] float finaleSeconds;

        public float SurvivalSeconds => survivalSeconds;

        public float FinaleSeconds => finaleSeconds;

        public RunRulesData ToRules() => new RunRulesData(survivalSeconds, finaleSeconds);
    }
}
