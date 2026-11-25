using System;
using System.Collections.Generic;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>스폰 표: 시간대별 초당 마릿수 · 스폰 반경 · 동시 최대 수. 새 시간대 = 줄 하나.</summary>
    [CreateAssetMenu(menuName = "Myong/Data/Spawn Table", fileName = "SpawnTable")]
    public sealed class SpawnTable : ScriptableObject
    {
        [Serializable]
        public sealed class Phase
        {
            [Tooltip("Run time (seconds) this phase starts at. It lasts until the next phase.")]
            [Min(0f)] public float startSeconds;
            [Tooltip("Enemies spawned per second during this phase.")]
            [Min(0f)] public float spawnsPerSecond;
            [Tooltip("Enemy Table row id.")]
            public string enemyId;
        }

        [SerializeField] List<Phase> phases = new List<Phase>();

        [Tooltip("Distance from the player (units) enemies spawn at. Keep it above half the screen diagonal so they appear off screen.")]
        [SerializeField, Min(0f)] float spawnRadius;

        [Tooltip("Most enemies alive at once (the pool size).")]
        [SerializeField, Min(0)] int maxAlive;

        public IReadOnlyList<Phase> Phases => phases;

        public float SpawnRadius => spawnRadius;

        public int MaxAlive => maxAlive;

        /// <summary>두 표를 Core 규칙 수치로 옮긴다. 없는 적 id를 가리키는 시간대는 오류를 남기고 뺀다.</summary>
        public EnemyRulesData ToRules(EnemyTable enemies)
        {
            var kinds = new EnemyKindData[enemies.Rows.Count];
            for (int i = 0; i < kinds.Length; i++) kinds[i] = new EnemyKindData(enemies.Rows[i].moveSpeed, enemies.Rows[i].radius);

            var list = new List<SpawnPhaseData>(phases.Count);
            foreach (var phase in phases)
            {
                int kind = enemies.IndexOf(phase.enemyId);
                if (kind < 0)
                {
                    Debug.LogError($"[Myong] SpawnTable phase at {phase.startSeconds}s refers to unknown enemy id '{phase.enemyId}'", this);
                    continue;
                }
                list.Add(new SpawnPhaseData(phase.startSeconds, phase.spawnsPerSecond, kind));
            }
            return new EnemyRulesData(kinds, list.ToArray(), spawnRadius, maxAlive, enemies.SeparationPush);
        }
    }
}
