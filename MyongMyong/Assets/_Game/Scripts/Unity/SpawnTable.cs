using System;
using System.Collections.Generic;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 스폰 표: 시간대별 초당 마릿수 · 종류 비중, 웨이브(정예 · 떼 돌진), 스폰 반경 · 동시 최대 수.
    /// 새 시간대 = 줄 하나, 새 종류 섞기 = 시간대의 mix 줄 하나, 새 웨이브 = waves 줄 하나.
    /// </summary>
    [CreateAssetMenu(menuName = "Myong/Data/Spawn Table", fileName = "SpawnTable")]
    public sealed class SpawnTable : ScriptableObject
    {
        [Serializable]
        public sealed class MixEntry
        {
            [Tooltip("Enemy Table row id.")]
            public string enemyId;
            [Tooltip("Relative weight within the phase (only the ratio between entries matters).")]
            [Min(0f)] public float weight = 1f;
        }

        [Serializable]
        public sealed class Phase
        {
            [Tooltip("Run time (seconds) this phase starts at. It lasts until the next phase.")]
            [Min(0f)] public float startSeconds;
            [Tooltip("Enemies spawned per second during this phase.")]
            [Min(0f)] public float spawnsPerSecond;
            [Tooltip("Enemy Table row id. Used only when Mix is empty (one kind for the whole phase).")]
            public string enemyId;
            [Tooltip("Kinds and weights for this phase. Empty: only Enemy Id.")]
            public List<MixEntry> mix = new List<MixEntry>();
        }

        [Serializable]
        public sealed class Wave
        {
            [Tooltip("Enemy Table row id.")]
            public string enemyId;
            [Tooltip("Run time (seconds) of the first wave.")]
            [Min(0f)] public float startSeconds;
            [Tooltip("Seconds between waves (0 = once).")]
            [Min(0f)] public float intervalSeconds;
            [Tooltip("Enemies per wave.")]
            [Min(0)] public int count = 1;
            [Tooltip("Scatter: one by one around the spawn circle, chasing. Swarm: a pack at one angle charging in a straight line.")]
            public SpawnPattern pattern;
            [Tooltip("Swarm: distance between pack members (units).")]
            [Min(0f)] public float spacing;
            [Tooltip("Swarm: charge speed = enemy move speed x this.")]
            [Min(0f)] public float speedScale = 1f;
            [Tooltip("Scatter: distance from the player (units) the wave appears at. 0 = Spawn Radius. Use it for big enemies so they still appear off screen. A wave starting at or after the survival goal (Run Tuning) is an end-of-run wave: it only comes after the goal.")]
            [Min(0f)] public float distance;
        }

        [SerializeField] List<Phase> phases = new List<Phase>();

        [SerializeField] List<Wave> waves = new List<Wave>();

        [Tooltip("Distance from the player (units) enemies spawn at. Keep it above half the screen diagonal so they appear off screen.")]
        [SerializeField, Min(0f)] float spawnRadius;

        [Tooltip("Most enemies alive at once (the pool size).")]
        [SerializeField, Min(0)] int maxAlive;

        [Tooltip("A charging (Swarm) enemy this far from the player (units) is removed. Keep it above Spawn Radius plus the pack depth.")]
        [SerializeField, Min(0f)] float chargeDespawnDistance;

        public IReadOnlyList<Phase> Phases => phases;

        public IReadOnlyList<Wave> Waves => waves;

        public float SpawnRadius => spawnRadius;

        public int MaxAlive => maxAlive;

        public float ChargeDespawnDistance => chargeDespawnDistance;

        /// <summary>시간대가 가리키는 적 id와 비중(mix가 비면 enemyId 하나, 비중 1).</summary>
        public static IEnumerable<(string enemyId, float weight)> EntriesOf(Phase phase)
        {
            if (phase.mix == null || phase.mix.Count == 0)
            {
                yield return (phase.enemyId, 1f);
                yield break;
            }
            foreach (var entry in phase.mix) yield return (entry.enemyId, entry.weight);
        }

        /// <summary>두 표를 Core 규칙 수치로 옮긴다. 없는 적 id를 가리키는 줄은 오류를 남기고 뺀다.</summary>
        public EnemyRulesData ToRules(EnemyTable enemies)
        {
            var kinds = new EnemyKindData[enemies.Rows.Count];
            for (int i = 0; i < kinds.Length; i++)
            {
                var row = enemies.Rows[i];
                kinds[i] = new EnemyKindData(row.moveSpeed, row.radius, row.maxHealth, row.experience, row.contactDamage, row.invulnerable, row.lethalContact);
            }

            var list = new List<SpawnPhaseData>(phases.Count);
            var mix = new List<SpawnWeight>();
            foreach (var phase in phases)
            {
                mix.Clear();
                foreach (var (enemyId, weight) in EntriesOf(phase))
                {
                    int kind = enemies.IndexOf(enemyId);
                    if (kind < 0)
                    {
                        Debug.LogError($"[Myong] SpawnTable phase at {phase.startSeconds}s refers to unknown enemy id '{enemyId}'", this);
                        continue;
                    }
                    if (weight > 0f) mix.Add(new SpawnWeight(kind, weight));
                }
                if (mix.Count == 0) continue;
                list.Add(new SpawnPhaseData(phase.startSeconds, phase.spawnsPerSecond, mix.ToArray()));
            }

            var waveList = new List<SpawnWaveData>(waves.Count);
            foreach (var wave in waves)
            {
                int kind = enemies.IndexOf(wave.enemyId);
                if (kind < 0)
                {
                    Debug.LogError($"[Myong] SpawnTable wave at {wave.startSeconds}s refers to unknown enemy id '{wave.enemyId}'", this);
                    continue;
                }
                waveList.Add(new SpawnWaveData(kind, wave.startSeconds, wave.intervalSeconds, wave.count, wave.pattern, wave.spacing, wave.speedScale, wave.distance));
            }
            return new EnemyRulesData(kinds, list.ToArray(), spawnRadius, maxAlive, enemies.SeparationPush, waveList.ToArray(), chargeDespawnDistance);
        }
    }
}
