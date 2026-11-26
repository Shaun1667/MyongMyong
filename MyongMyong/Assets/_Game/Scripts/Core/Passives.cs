using System;

namespace Myong.Core
{
    /// <summary>능력치 하나를 바꾸는 효과(대상 필드 + 연산 + 한 단계 값). 강화 표와 같은 StatTarget · StatOp를 쓴다.</summary>
    public readonly struct StatEffect
    {
        public readonly StatTarget Target;
        public readonly StatOp Op;
        public readonly float Value;

        public StatEffect(StatTarget target, StatOp op, float value)
        {
            Target = target;
            Op = op;
            Value = value;
        }
    }

    /// <summary>
    /// 패시브 한 줄(데이터 에셋 PassiveTable의 한 줄): 단계마다 같은 효과들을 한 번 더 건다(최대 단계까지).
    /// 이름 · 설명 · 그림은 Unity 쪽이 같은 줄의 키로 찾는다. 패시브 종류별 분기 코드는 없다.
    /// </summary>
    public readonly struct PassiveData
    {
        public readonly StatEffect[] Effects;
        public readonly int MaxStage;

        public PassiveData(StatEffect[] effects, int maxStage)
        {
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
            MaxStage = maxStage;
        }
    }

    /// <summary>패시브 규칙 수치 전부(데이터 에셋 PassiveTable에서 옮겨 온 값). 만들 때 한 번 검사한다.</summary>
    public sealed class PassiveRulesData
    {
        /// <summary>패시브가 없는 규칙(레벨업 선택지는 강화 표만, 대체 보상 없음 — 1-8까지와 같다).</summary>
        public static readonly PassiveRulesData None = new PassiveRulesData(new PassiveData[0], 0, 0f);

        public readonly PassiveData[] Rows;
        /// <summary>한 판에 가질 수 있는 패시브 종류 수. 다 차면 새 패시브는 선택지에 안 나오고 가진 것의 다음 단계만 나온다.</summary>
        public readonly int MaxOwned;
        /// <summary>뽑을 선택지가 하나도 없을 때의 대체 보상: 체력 회복량. 0이면 대체 보상 없음(레벨만 오르고 넘어간다).</summary>
        public readonly float FallbackHeal;

        public PassiveRulesData(PassiveData[] rows, int maxOwned, float fallbackHeal)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (maxOwned < 0) throw new ArgumentOutOfRangeException(nameof(maxOwned));
            if (fallbackHeal < 0f) throw new ArgumentOutOfRangeException(nameof(fallbackHeal));
            Rows = (PassiveData[])rows.Clone();
            foreach (var row in Rows)
            {
                if (row.MaxStage < 1) throw new ArgumentOutOfRangeException(nameof(rows), "max stage must be at least 1");
                if (row.Effects == null || row.Effects.Length == 0) throw new ArgumentOutOfRangeException(nameof(rows), "a passive needs at least one effect");
                foreach (var effect in row.Effects)
                {
                    if (!StatTargets.IsValid(effect.Target)) throw new ArgumentOutOfRangeException(nameof(rows), "unknown stat target");
                    if (!StatTargets.IsPlayerStat(effect.Target)) throw new ArgumentOutOfRangeException(nameof(rows), "passives change player stats only");
                    if (effect.Op != StatOp.Add && effect.Op != StatOp.Multiply) throw new ArgumentOutOfRangeException(nameof(rows), "unknown stat op");
                    if (effect.Op == StatOp.Multiply && effect.Value <= 0f) throw new ArgumentOutOfRangeException(nameof(rows), "multiply value must be positive");
                }
            }
            MaxOwned = maxOwned;
            FallbackHeal = fallbackHeal;
        }
    }

    /// <summary>레벨업 선택지 하나가 무엇인지(World.LevelUpChoices의 값을 풀어 본 것).</summary>
    public enum LevelUpOptionKind
    {
        /// <summary>강화 표 한 줄(무기 강화).</summary>
        Upgrade = 0,
        /// <summary>패시브 표 한 줄(새로 얻기 또는 다음 단계).</summary>
        Passive = 1,
        /// <summary>고를 것이 다 떨어졌을 때의 대체 보상(체력 회복).</summary>
        Fallback = 2,
    }

    public readonly struct LevelUpOption
    {
        public readonly LevelUpOptionKind Kind;
        /// <summary>Upgrade = 강화 표 줄 번호, Passive = 패시브 표 줄 번호, Fallback = 0.</summary>
        public readonly int Row;

        public LevelUpOption(LevelUpOptionKind kind, int row)
        {
            Kind = kind;
            Row = row;
        }
    }
}
