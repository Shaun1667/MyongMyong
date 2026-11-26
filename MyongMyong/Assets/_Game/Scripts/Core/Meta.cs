using System;
using System.Collections.Generic;

namespace Myong.Core
{
    /// <summary>
    /// 영구 강화 한 줄(데이터 에셋 PermanentUpgradeTable의 한 줄, 1-12): 단계마다 같은 효과를 한 번 더(기본 값에), 단계마다 가격.
    /// 최대 단계 = 가격 수. 이름 · 설명은 Unity 쪽이 같은 줄의 키로 찾는다. 종류별 분기 코드는 없다.
    /// </summary>
    public readonly struct PermanentUpgradeData
    {
        public readonly StatEffect Effect;
        /// <summary>단계 s → s+1의 가격 = Prices[s]. 길이 = 최대 단계.</summary>
        public readonly int[] Prices;

        public PermanentUpgradeData(StatEffect effect, int[] prices)
        {
            Effect = effect;
            Prices = prices ?? throw new ArgumentNullException(nameof(prices));
        }

        public int MaxStage => Prices.Length;
    }

    /// <summary>영구 강화 규칙 수치 전부(PermanentUpgradeTable에서 옮겨 온 값). 만들 때 한 번 검사한다.</summary>
    public sealed class PermanentRulesData
    {
        /// <summary>영구 강화가 없는 규칙(1-11까지와 같다).</summary>
        public static readonly PermanentRulesData None = new PermanentRulesData(new PermanentUpgradeData[0]);

        public readonly PermanentUpgradeData[] Rows;

        public PermanentRulesData(PermanentUpgradeData[] rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            Rows = (PermanentUpgradeData[])rows.Clone();
            foreach (var row in Rows)
            {
                if (row.Prices == null || row.Prices.Length < 1) throw new ArgumentOutOfRangeException(nameof(rows), "a permanent upgrade needs at least one price (one stage)");
                foreach (int price in row.Prices)
                {
                    if (price < 0) throw new ArgumentOutOfRangeException(nameof(rows), "price must not be negative");
                }
                StatEffect e = row.Effect;
                if (!TargetAllowed(e.Target)) throw new ArgumentOutOfRangeException(nameof(rows), "this stat cannot be a permanent upgrade");
                if (e.Op != StatOp.Add && e.Op != StatOp.Multiply) throw new ArgumentOutOfRangeException(nameof(rows), "unknown stat op");
                if (e.Op == StatOp.Multiply && e.Value <= 0f) throw new ArgumentOutOfRangeException(nameof(rows), "multiply value must be positive");
            }
        }

        /// <summary>
        /// 영구 강화가 바꿀 수 있는 값: 플레이어 값 전부 + 판 전체 무기 값 중 소수 값(간격 · 피해 · 탄 속도 · 사거리).
        /// 한 번에 쏘는 수 · 관통은 탄 작업 공간 크기를 판 시작 전에 정하므로 뺀다.
        /// </summary>
        public static bool TargetAllowed(StatTarget target)
        {
            if (!StatTargets.IsValid(target)) return false;
            return target != StatTarget.ShotsPerVolley && target != StatTarget.Pierce;
        }
    }

    /// <summary>판 밖 진행(지갑 · 영구 강화 단계). 판(World)과 따로 있고, 파일로 저장된다(MetaSave · Unity MetaStore).</summary>
    public sealed class MetaProgress
    {
        /// <summary>지갑의 코인(0 이상).</summary>
        public long Coins;
        /// <summary>영구 강화 표 줄마다 산 단계(0 = 안 삼).</summary>
        public readonly int[] Stages;

        public MetaProgress(int rowCount)
        {
            Stages = new int[Math.Max(0, rowCount)];
        }

        public MetaProgress Clone()
        {
            var copy = new MetaProgress(Stages.Length) { Coins = Coins };
            Array.Copy(Stages, copy.Stages, Stages.Length);
            return copy;
        }
    }

    /// <summary>영구 강화 사기 · 되돌리기 · 판에 걸기 · 판 끝 코인 더하기. 상태는 MetaProgress에만.</summary>
    public static class MetaRules
    {
        /// <summary>row의 다음 단계 가격. 최대 단계면 -1.</summary>
        public static long PriceOfNext(PermanentRulesData rules, MetaProgress progress, int row)
        {
            if (row < 0 || row >= rules.Rows.Length || row >= progress.Stages.Length) return -1;
            int stage = progress.Stages[row];
            int[] prices = rules.Rows[row].Prices;
            return stage >= prices.Length ? -1 : prices[stage];
        }

        public static bool CanBuy(PermanentRulesData rules, MetaProgress progress, int row)
        {
            long price = PriceOfNext(rules, progress, row);
            return price >= 0 && progress.Coins >= price;
        }

        /// <summary>row를 한 단계 산다(지갑에서 가격만큼 뺌). 최대 단계거나 코인이 모자라면 false.</summary>
        public static bool Buy(PermanentRulesData rules, MetaProgress progress, int row)
        {
            if (!CanBuy(rules, progress, row)) return false;
            progress.Coins -= PriceOfNext(rules, progress, row);
            progress.Stages[row]++;
            return true;
        }

        /// <summary>row에 쓴 코인 합(산 단계까지의 가격 합).</summary>
        public static long SpentOn(PermanentRulesData rules, MetaProgress progress, int row)
        {
            if (row < 0 || row >= rules.Rows.Length || row >= progress.Stages.Length) return 0;
            int[] prices = rules.Rows[row].Prices;
            long sum = 0;
            for (int s = 0; s < progress.Stages[row] && s < prices.Length; s++) sum += prices[s];
            return sum;
        }

        /// <summary>모두 되돌리기: 모든 줄을 0단계로, 쓴 코인을 모두 지갑에 돌려준다. 돌려준 코인을 돌려준다.</summary>
        public static long RefundAll(PermanentRulesData rules, MetaProgress progress)
        {
            long refund = 0;
            for (int row = 0; row < progress.Stages.Length; row++)
            {
                refund += SpentOn(rules, progress, row);
                progress.Stages[row] = 0;
            }
            AddCoins(progress, refund);
            return refund;
        }

        /// <summary>지갑에 코인을 더한다(빼는 것은 Buy만). 넘치지 않게 long 최대에서 멈춘다.</summary>
        public static void AddCoins(MetaProgress progress, long amount)
        {
            if (amount <= 0) return;
            progress.Coins = progress.Coins > long.MaxValue - amount ? long.MaxValue : progress.Coins + amount;
        }

        /// <summary>
        /// 끝난 판의 코인을 지갑에 더한다(승리 · 패배 모두). 진행 중이거나 디버그를 쓴 판(RunResult.DebugUsed)이면 더하지 않는다 — 더한 코인을 돌려준다.
        /// 한 판에 한 번만 부른다(부르는 쪽이 지킨다).
        /// </summary>
        public static long CreditRun(MetaProgress progress, in RunResult result)
        {
            if (result.Outcome == RunOutcome.Playing || result.DebugUsed || result.Coins <= 0) return 0;
            AddCoins(progress, result.Coins);
            return result.Coins;
        }

        /// <summary>산 단계만큼 효과를 판의 기본 값 칸에 건다(판을 만들 때 한 번). 건 효과 수를 돌려준다.</summary>
        public static int ApplyToWorld(PermanentRulesData rules, MetaProgress progress, World world)
        {
            int applied = 0;
            int n = Math.Min(rules.Rows.Length, progress.Stages.Length);
            for (int row = 0; row < n; row++)
            {
                StatEffect e = rules.Rows[row].Effect;
                int stages = Math.Min(progress.Stages[row], rules.Rows[row].MaxStage);
                for (int s = 0; s < stages; s++)
                {
                    Stats.ApplyBase(world, e.Target, e.Op, e.Value);
                    applied++;
                }
            }
            return applied;
        }
    }

    /// <summary>저장 파일 한 칸(영구 강화 한 줄). 줄 번호가 아니라 id로 남긴다 — 표 순서가 바뀌어도 맞게 읽힌다.</summary>
    [Serializable]
    public sealed class MetaSaveStage
    {
        public string id;
        public int stage;
    }

    /// <summary>판 밖 진행 저장 형식(JSON으로 쓰는 것은 Unity 쪽 MetaStore). 화면 글은 넣지 않는다(키와 값만).</summary>
    [Serializable]
    public sealed class MetaSaveData
    {
        /// <summary>형식 번호(MetaSave.CurrentVersion). 0 = 읽을 수 없는 파일.</summary>
        public int version;
        public long coins;
        public MetaSaveStage[] upgrades = new MetaSaveStage[0];
    }

    /// <summary>저장 파일을 읽은 결과.</summary>
    public enum MetaLoadStatus
    {
        /// <summary>읽었다.</summary>
        Loaded = 0,
        /// <summary>파일이 없다(처음) — 0에서 시작.</summary>
        Missing = 1,
        /// <summary>깨졌다(JSON이 아님 · 형식 번호 없음 · 음수 코인) — 0에서 시작, 원본은 옆에 백업.</summary>
        Corrupt = 2,
        /// <summary>이 게임보다 새 형식 — 0에서 시작, 원본은 옆에 백업(덮어써도 원본이 남게).</summary>
        NewerVersion = 3,
    }

    /// <summary>저장 규칙(형식 번호 · 검사 · id ↔ 줄 번호). 파일 읽기 · 쓰기는 Unity 쪽(MetaStore)이 한다.</summary>
    public static class MetaSave
    {
        /// <summary>지금 형식 번호. 형식이 바뀌면 올리고 FromSave에 옮기는 길을 더한다.</summary>
        public const int CurrentVersion = 1;

        /// <summary>진행을 저장 형식으로(0단계 줄은 남기지 않는다).</summary>
        public static MetaSaveData ToSave(MetaProgress progress, IReadOnlyList<string> ids)
        {
            var list = new List<MetaSaveStage>();
            for (int row = 0; row < progress.Stages.Length && row < ids.Count; row++)
            {
                if (progress.Stages[row] <= 0 || string.IsNullOrEmpty(ids[row])) continue;
                list.Add(new MetaSaveStage { id = ids[row], stage = progress.Stages[row] });
            }
            return new MetaSaveData { version = CurrentVersion, coins = progress.Coins, upgrades = list.ToArray() };
        }

        /// <summary>
        /// 저장 형식 → 진행. 깨졌거나(Corrupt) 새 형식이면(NewerVersion) 0에서 시작한 진행을 돌려준다.
        /// 표에 없는 id는 버린다(코인은 그대로) · 단계는 0 ~ 최대로 자른다 · 같은 id가 또 나오면 처음 것.
        /// </summary>
        public static MetaLoadStatus FromSave(MetaSaveData data, IReadOnlyList<string> ids, PermanentRulesData rules, out MetaProgress progress)
        {
            progress = new MetaProgress(ids.Count);
            if (data == null || data.version < 1 || data.coins < 0) return MetaLoadStatus.Corrupt;
            if (data.version > CurrentVersion) return MetaLoadStatus.NewerVersion;
            progress.Coins = data.coins;
            var seen = new bool[ids.Count];
            if (data.upgrades == null) return MetaLoadStatus.Loaded;
            foreach (var entry in data.upgrades)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                int row = IndexOf(ids, entry.id);
                if (row < 0 || seen[row]) continue;
                seen[row] = true;
                int max = row < rules.Rows.Length ? rules.Rows[row].MaxStage : 0;
                progress.Stages[row] = Math.Max(0, Math.Min(entry.stage, max));
            }
            return MetaLoadStatus.Loaded;
        }

        static int IndexOf(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id) return i;
            }
            return -1;
        }
    }
}
