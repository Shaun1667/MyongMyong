using System;
using System.Collections.Generic;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 영구 강화 표(1-12, 판 밖 상점). 한 줄 = id · 이름 키 · 설명 키 · 효과(StatTarget · StatOp · 단계마다 값) · 단계별 가격(길이 = 최대 단계).
    /// 판을 시작할 때 산 단계만큼 효과를 기본 값에 건다. 새 영구 강화 = 줄 하나 + 텍스트 두 줄(`meta.&lt;id&gt;.name` · `.desc`, {0} = 한 단계 값).
    /// </summary>
    [CreateAssetMenu(menuName = "Myong/Data/Permanent Upgrade Table", fileName = "PermanentUpgradeTable")]
    public sealed class PermanentUpgradeTable : ScriptableObject
    {
        [Serializable]
        public sealed class Row
        {
            [Tooltip("Row key. The save file stores stages by this id, so do not rename it after release.")]
            public string id;
            [Tooltip("strings.csv key of the shop row title.")]
            public string nameKey;
            [Tooltip("strings.csv key of the shop row description. {0} = the per-stage value (e.g. +10, +5%).")]
            public string descriptionKey;
            [Tooltip("Value this upgrade changes (applied to the base value at the start of a run). Shots per volley and pierce are not allowed.")]
            public StatTarget target;
            [Tooltip("Add: value is added per stage. Multiply: value is multiplied in per stage (1.05 = +5%).")]
            public StatOp op;
            [Tooltip("Per-stage value.")]
            public float value;
            [Tooltip("Price of each stage in coins (element 0 = the first stage). The count is the max stage.")]
            public List<int> prices = new List<int>();

            public int MaxStage => prices != null ? prices.Count : 0;
        }

        [SerializeField] List<Row> rows = new List<Row>();

        public IReadOnlyList<Row> Rows => rows;

        /// <summary>id의 줄 번호. 없으면 -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].id == id) return i;
            }
            return -1;
        }

        /// <summary>줄마다 id(저장 파일이 쓴다).</summary>
        public string[] Ids()
        {
            var ids = new string[rows.Count];
            for (int i = 0; i < rows.Count; i++) ids[i] = rows[i].id ?? string.Empty;
            return ids;
        }

        /// <summary>
        /// 표를 Core 규칙 수치로 옮긴다. 쓸 수 없는 줄(대상 · 연산 · 가격이 맞지 않음)은 오류를 남기고 아무것도 안 바꾸는 한 단계 줄(살 수 없는 가격)로 둔다 —
        /// 줄 번호가 밀리지 않게 줄은 빼지 않는다.
        /// </summary>
        public PermanentRulesData ToRules()
        {
            var list = new PermanentUpgradeData[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                bool pricesOk = r.prices != null && r.prices.Count > 0 && r.prices.TrueForAll(p => p >= 0);
                bool usable = PermanentRulesData.TargetAllowed(r.target) && (r.op == StatOp.Add || (r.op == StatOp.Multiply && r.value > 0f)) && pricesOk;
                if (!usable)
                {
                    Debug.LogError($"[Myong] PermanentUpgradeTable row '{r.id}' cannot be used (target, op or prices)", this);
                    list[i] = new PermanentUpgradeData(new StatEffect(StatTarget.MoveSpeed, StatOp.Add, 0f), new[] { int.MaxValue });
                    continue;
                }
                list[i] = new PermanentUpgradeData(new StatEffect(r.target, r.op, r.value), r.prices.ToArray());
            }
            return new PermanentRulesData(list);
        }
    }
}
