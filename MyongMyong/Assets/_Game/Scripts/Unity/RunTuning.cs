using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>한 판의 길이 표: 생존 목표 시간(이만큼 버티면 승리). 코드에 숫자를 쓰지 않고 이 에셋에서 읽는다.</summary>
    [CreateAssetMenu(menuName = "Myong/Data/Run Tuning", fileName = "RunTuning")]
    public sealed class RunTuning : ScriptableObject
    {
        [Tooltip("Survive this many seconds (simulation time, paused during level-up choices) to win. 0 = no time victory.")]
        [SerializeField, Min(0f)] float survivalSeconds;

        public float SurvivalSeconds => survivalSeconds;

        public RunRulesData ToRules() => new RunRulesData(survivalSeconds);
    }
}
