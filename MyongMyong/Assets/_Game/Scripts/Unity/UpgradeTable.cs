using System;
using System.Collections.Generic;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 강화 표. 한 줄 = 강화 하나 = "대상 필드 + 연산(더하기 · 곱하기) + 한 단계 값 + 최대 단계 (+ 대상 무기)".
    /// 새 강화 = 줄 하나(종류별 분기 코드 없음). 이름 · 설명은 텍스트 표의 키로만 가리킨다.
    /// </summary>
    [CreateAssetMenu(menuName = "Myong/Data/Upgrade Table", fileName = "UpgradeTable")]
    public sealed class UpgradeTable : ScriptableObject
    {
        [Serializable]
        public sealed class Row
        {
            [Tooltip("Row key (debug window, later saves).")]
            public string id;
            [Tooltip("strings.csv key of the card title.")]
            public string nameKey;
            [Tooltip("strings.csv key of the card description. {0} = the per-stage value (e.g. +1, -12%).")]
            public string descriptionKey;
            [Tooltip("Value this upgrade changes.")]
            public StatTarget target;
            [Tooltip("Add: value is added per stage. Multiply: value is multiplied in per stage (0.9 = -10%).")]
            public StatOp op;
            [Tooltip("Per-stage value.")]
            public float value;
            [Tooltip("Stages this upgrade can be taken (at least 1). Maxed upgrades are no longer offered.")]
            [Min(1)] public int maxStage = 1;
            [Tooltip("Weapon Table row id for a weapon-only upgrade. Empty: player stats, or every weapon for weapon stats.")]
            public string weaponId;
        }

        [SerializeField] List<Row> rows = new List<Row>();

        [Tooltip("Cards shown per level-up.")]
        [SerializeField, Min(1)] int choicesPerLevelUp = 3;

        public IReadOnlyList<Row> Rows => rows;

        public int ChoicesPerLevelUp => choicesPerLevelUp;

        /// <summary>id의 줄 번호. 없으면 -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].id == id) return i;
            }
            return -1;
        }

        /// <summary>표를 Core 규칙 수치로 옮긴다. 없는 무기 id를 가리키는 줄은 오류를 남기고 모든 무기 대상으로 둔다 — 줄 번호가 밀리지 않게 빼지 않는다.</summary>
        public UpgradeRulesData ToRules(WeaponTable weapons)
        {
            var list = new UpgradeData[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                int weaponKind = -1;
                if (!string.IsNullOrEmpty(r.weaponId))
                {
                    weaponKind = weapons != null ? weapons.IndexOf(r.weaponId) : -1;
                    if (weaponKind < 0) Debug.LogError($"[Myong] UpgradeTable row '{r.id}' refers to unknown weapon id '{r.weaponId}'", this);
                }
                list[i] = new UpgradeData(r.target, r.op, r.value, Math.Max(1, r.maxStage), weaponKind);
            }
            return new UpgradeRulesData(list, choicesPerLevelUp);
        }
    }
}
