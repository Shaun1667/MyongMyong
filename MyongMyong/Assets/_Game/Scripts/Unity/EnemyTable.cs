using System;
using System.Collections.Generic;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>적 종류 표. 새 적 = 줄 하나(종류별 분기 코드 없음). 비주얼은 visualId로만 가리킨다 — 그림이 없으면 단색 도형.</summary>
    [CreateAssetMenu(menuName = "Myong/Data/Enemy Table", fileName = "EnemyTable")]
    public sealed class EnemyTable : ScriptableObject
    {
        [Serializable]
        public sealed class Row
        {
            [Tooltip("Key used by the spawn table.")]
            public string id;
            [Tooltip("Move speed in units per second.")]
            [Min(0f)] public float moveSpeed;
            [Tooltip("Body radius in units (enemy-to-enemy push).")]
            [Min(0f)] public float radius;
            [Tooltip("Health at spawn. Bullets take it down; at 0 the enemy dies.")]
            [Min(0f)] public float maxHealth;
            [Tooltip("Visual key. With no sprite for it, a solid placeholder shape is drawn.")]
            public string visualId;
        }

        [SerializeField] List<Row> rows = new List<Row>();

        [Tooltip("Share of the overlap between two enemies resolved per tick (0 = no push, 1 = all at once).")]
        [SerializeField, Range(0f, 1f)] float separationPush;

        public IReadOnlyList<Row> Rows => rows;

        public float SeparationPush => separationPush;

        /// <summary>id의 줄 번호. 없으면 -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].id == id) return i;
            }
            return -1;
        }
    }
}
