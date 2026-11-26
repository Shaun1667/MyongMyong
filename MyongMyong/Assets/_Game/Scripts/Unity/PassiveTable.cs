using System;
using System.Collections.Generic;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 패시브 표. 한 줄 = 패시브 하나 = id · 이름 키 · 설명 키 · 짧은 글 키(HUD 아이콘 글자) · 그림 ID · 단계마다 거는 효과(StatTarget · StatOp · 값) · 최대 단계.
    /// 새 패시브 = 줄 하나(종류별 분기 코드 없음). 효과는 플레이어 값만(이동 속도 · 줍는 반경 · 최대 체력 · 재생 · 받는 피해 · 경험치).
    /// 고를 것이 다 떨어졌을 때의 대체 보상(체력 회복)도 여기.
    /// </summary>
    [CreateAssetMenu(menuName = "Myong/Data/Passive Table", fileName = "PassiveTable")]
    public sealed class PassiveTable : ScriptableObject
    {
        [Serializable]
        public sealed class Effect
        {
            [Tooltip("Player value this effect changes.")]
            public StatTarget target;
            [Tooltip("Add: value is added per stage. Multiply: value is multiplied in per stage (0.92 = -8%).")]
            public StatOp op;
            [Tooltip("Per-stage value.")]
            public float value;
        }

        [Serializable]
        public sealed class Row
        {
            [Tooltip("Row key (debug window, later saves).")]
            public string id;
            [Tooltip("strings.csv key of the card title.")]
            public string nameKey;
            [Tooltip("strings.csv key of the card description. {0}, {1}... = the per-stage value of each effect.")]
            public string descriptionKey;
            [Tooltip("strings.csv key of the one or two letters drawn on the HUD icon when there is no picture.")]
            public string shortKey;
            [Tooltip("Icon id. No picture with this id: a plain coloured square with the short text.")]
            public string iconId;
            [Tooltip("Effects applied once more at every stage.")]
            public List<Effect> effects = new List<Effect>();
            [Tooltip("Stages this passive can be taken (at least 1). Maxed passives are no longer offered.")]
            [Min(1)] public int maxStage = 1;
        }

        [SerializeField] List<Row> rows = new List<Row>();

        [Tooltip("Passive kinds a run can hold. When full, only the next stage of held passives is offered.")]
        [SerializeField, Min(0)] int maxOwned = 6;

        [Tooltip("Health restored by the stand-in card when nothing else can be offered. 0 = no stand-in (the level-up just passes).")]
        [SerializeField, Min(0f)] float fallbackHeal;

        [Tooltip("strings.csv keys of the stand-in card. {0} = health restored.")]
        [SerializeField] string fallbackNameKey;
        [SerializeField] string fallbackDescriptionKey;

        public IReadOnlyList<Row> Rows => rows;

        public int MaxOwned => maxOwned;

        public float FallbackHeal => fallbackHeal;

        public string FallbackNameKey => fallbackNameKey;

        public string FallbackDescriptionKey => fallbackDescriptionKey;

        /// <summary>id의 줄 번호. 없으면 -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].id == id) return i;
            }
            return -1;
        }

        /// <summary>
        /// 표를 Core 규칙 수치로 옮긴다. 쓸 수 없는 효과(플레이어 값이 아님 · 곱하기 0 이하)는 오류를 남기고 아무것도 안 바꾸는 효과로 둔다 —
        /// 줄 번호가 밀리지 않게 줄은 빼지 않는다.
        /// </summary>
        public PassiveRulesData ToRules()
        {
            var list = new PassiveData[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                int count = r.effects != null ? r.effects.Count : 0;
                var effects = new StatEffect[Math.Max(1, count)];
                if (count == 0)
                {
                    Debug.LogError($"[Myong] PassiveTable row '{r.id}' has no effect", this);
                    effects[0] = new StatEffect(StatTarget.MoveSpeed, StatOp.Add, 0f);
                }
                for (int e = 0; e < count; e++)
                {
                    Effect f = r.effects[e];
                    bool usable = StatTargets.IsValid(f.target) && StatTargets.IsPlayerStat(f.target) && (f.op == StatOp.Add || (f.op == StatOp.Multiply && f.value > 0f));
                    if (!usable)
                    {
                        Debug.LogError($"[Myong] PassiveTable row '{r.id}' effect {e} cannot be used (passives change player values only)", this);
                        effects[e] = new StatEffect(StatTarget.MoveSpeed, StatOp.Add, 0f);
                        continue;
                    }
                    effects[e] = new StatEffect(f.target, f.op, f.value);
                }
                list[i] = new PassiveData(effects, Math.Max(1, r.maxStage));
            }
            return new PassiveRulesData(list, maxOwned, fallbackHeal);
        }
    }
}
