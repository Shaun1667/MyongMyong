using System.Collections.Generic;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>경험치 표: 레벨 곡선(레벨마다 필요한 경험치) · 젬(최대 수 · 끌려오는 속도 · 줍는 거리 · 비주얼 ID). 새 레벨 = 곡선에 숫자 하나.</summary>
    [CreateAssetMenu(menuName = "Myong/Data/Experience Table", fileName = "ExperienceTable")]
    public sealed class ExperienceTable : ScriptableObject
    {
        [Tooltip("Experience needed to go from level n to n+1 (element 0 = level 1 to 2).")]
        [SerializeField] List<int> experienceToNext = new List<int>();

        [Tooltip("Past the end of the list: each further level needs this much more than the one before.")]
        [SerializeField, Min(0)] int extraPerLevel;

        [Tooltip("Most gems on the ground at once (the pool size). When full, a new gem's experience is added to the last gem.")]
        [SerializeField, Min(0)] int maxGems;

        [Tooltip("Speed (units per second) a gem flies to the player once inside the pickup radius.")]
        [SerializeField, Min(0f)] float pullSpeed;

        [Tooltip("Distance (units) at which a gem touches the player and becomes experience.")]
        [SerializeField, Min(0f)] float collectDistance;

        [Tooltip("Gem visual key. With no sprite for it, a solid placeholder dot is drawn.")]
        [SerializeField] string gemVisualId;

        public IReadOnlyList<int> ExperienceToNext => experienceToNext;

        public int ExtraPerLevel => extraPerLevel;

        public int MaxGems => maxGems;

        public float PullSpeed => pullSpeed;

        public float CollectDistance => collectDistance;

        public string GemVisualId => gemVisualId;

        public ExperienceRulesData ToRules()
        {
            return new ExperienceRulesData(experienceToNext.ToArray(), extraPerLevel, maxGems, pullSpeed, collectDistance);
        }
    }
}
