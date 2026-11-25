using System;

namespace Myong.Core
{
    /// <summary>
    /// 강화가 바꿀 수 있는 값(대상 필드). 강화 표의 한 줄은 "대상 필드 + 연산 + 값"이라 종류별 분기 코드가 없다.
    /// 새 대상 = 줄 하나(값은 바꾸지 않는다: 에셋에 숫자로 저장된다). 무기 값은 무기마다, 플레이어 값은 판 전체에 하나.
    /// </summary>
    public enum StatTarget
    {
        MoveSpeed = 0,
        PickupRadius = 1,
        FireInterval = 2,
        Damage = 3,
        ShotsPerVolley = 4,
        Pierce = 5,
        BulletSpeed = 6,
        Range = 7,
    }

    /// <summary>강화 한 단계의 연산. 실제 값 = (기본 + 더한 값 합) × 곱한 값 곱.</summary>
    public enum StatOp
    {
        Add = 0,
        Multiply = 1,
    }

    public static class StatTargets
    {
        /// <summary>StatTarget 수(배열 크기). 새 대상을 더하면 같이 늘린다.</summary>
        public const int Count = 8;

        /// <summary>플레이어 쪽 값(무기와 상관없음).</summary>
        public static bool IsPlayerStat(StatTarget target) => target == StatTarget.MoveSpeed || target == StatTarget.PickupRadius;

        public static bool IsValid(StatTarget target) => (int)target >= 0 && (int)target < Count;
    }

    /// <summary>강화 한 줄(데이터 에셋 UpgradeTable의 한 줄). 이름 · 설명은 Unity 쪽이 같은 줄의 텍스트 키로 찾는다.</summary>
    public readonly struct UpgradeData
    {
        public readonly StatTarget Target;
        public readonly StatOp Op;
        /// <summary>한 단계에 더하거나 곱하는 값.</summary>
        public readonly float Value;
        /// <summary>최대 단계(1 이상). 닿으면 선택지에서 빠진다.</summary>
        public readonly int MaxStage;
        /// <summary>대상 무기(WeaponRulesData.Kinds의 번호). -1 = 모든 무기(무기 값) 또는 플레이어(플레이어 값).</summary>
        public readonly int WeaponKind;

        public UpgradeData(StatTarget target, StatOp op, float value, int maxStage, int weaponKind = -1)
        {
            Target = target;
            Op = op;
            Value = value;
            MaxStage = maxStage;
            WeaponKind = weaponKind;
        }
    }

    /// <summary>강화 규칙 수치 전부(데이터 에셋 UpgradeTable에서 옮겨 온 값). 만들 때 한 번 검사한다.</summary>
    public sealed class UpgradeRulesData
    {
        public static readonly UpgradeRulesData None = new UpgradeRulesData(new UpgradeData[0], 0);

        public readonly UpgradeData[] Rows;
        /// <summary>레벨업 한 번에 보여 줄 선택지 수.</summary>
        public readonly int ChoicesPerLevelUp;

        public UpgradeRulesData(UpgradeData[] rows, int choicesPerLevelUp)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (choicesPerLevelUp < 0) throw new ArgumentOutOfRangeException(nameof(choicesPerLevelUp));
            Rows = (UpgradeData[])rows.Clone();
            foreach (var row in Rows)
            {
                if (!StatTargets.IsValid(row.Target)) throw new ArgumentOutOfRangeException(nameof(rows), "unknown stat target");
                if (row.Op != StatOp.Add && row.Op != StatOp.Multiply) throw new ArgumentOutOfRangeException(nameof(rows), "unknown stat op");
                if (row.MaxStage < 1) throw new ArgumentOutOfRangeException(nameof(rows), "max stage must be at least 1");
                if (row.Op == StatOp.Multiply && row.Value <= 0f) throw new ArgumentOutOfRangeException(nameof(rows), "multiply value must be positive");
                if (row.WeaponKind < -1) throw new ArgumentOutOfRangeException(nameof(rows), "weapon kind must be -1 or a weapon");
                if (row.WeaponKind >= 0 && StatTargets.IsPlayerStat(row.Target)) throw new ArgumentOutOfRangeException(nameof(rows), "player stat upgrades cannot name a weapon");
            }
            ChoicesPerLevelUp = choicesPerLevelUp;
        }

        /// <summary>
        /// 모든 강화를 최대 단계까지 받았을 때 그 무기의 정수 값이 가장 클 수 있는 값(배열 크기를 정하는 데 쓴다).
        /// 더하기는 양수만, 곱하기는 1보다 큰 것만 센다.
        /// </summary>
        public int MaxPossible(StatTarget target, int weaponKind, int baseValue)
        {
            double add = 0.0;
            double mul = 1.0;
            foreach (var row in Rows)
            {
                if (row.Target != target || (row.WeaponKind != -1 && row.WeaponKind != weaponKind)) continue;
                if (row.Op == StatOp.Add && row.Value > 0f) add += row.Value * row.MaxStage;
                else if (row.Op == StatOp.Multiply && row.Value > 1f) mul *= Math.Pow(row.Value, row.MaxStage);
            }
            return Math.Max(baseValue, (int)Math.Ceiling((baseValue + add) * mul));
        }
    }

    /// <summary>경험치 수치(데이터 에셋 ExperienceTable에서 옮겨 온 값): 젬 · 레벨 곡선.</summary>
    public sealed class ExperienceRulesData
    {
        /// <summary>경험치가 없는 규칙(젬이 떨어지지 않고 레벨이 오르지 않는다).</summary>
        public static readonly ExperienceRulesData None = new ExperienceRulesData(new int[0], 0, 0, 0f, 0f);

        /// <summary>레벨 n → n+1에 필요한 경험치 = ExperienceToNext[n-1]. 표가 끝나면 마지막 값에 레벨마다 ExtraPerLevel씩 더한다.</summary>
        public readonly int[] ExperienceToNext;
        public readonly int ExtraPerLevel;
        /// <summary>동시에 떨어져 있을 수 있는 젬 최대 수 = World의 젬 배열 크기. 가득 차면 새 젬의 경험치는 맨 끝 젬에 더한다.</summary>
        public readonly int MaxGems;
        /// <summary>줍는 반경 안에 들어온 젬이 플레이어 쪽으로 끌려오는 속도(유닛/초).</summary>
        public readonly float PullSpeed;
        /// <summary>플레이어와 이만큼(유닛) 가까워지면 젬이 경험치가 된다.</summary>
        public readonly float CollectDistance;

        public ExperienceRulesData(int[] experienceToNext, int extraPerLevel, int maxGems, float pullSpeed, float collectDistance)
        {
            if (experienceToNext == null) throw new ArgumentNullException(nameof(experienceToNext));
            if (maxGems < 0) throw new ArgumentOutOfRangeException(nameof(maxGems));
            if (extraPerLevel < 0) throw new ArgumentOutOfRangeException(nameof(extraPerLevel));
            foreach (int need in experienceToNext)
            {
                if (need < 1) throw new ArgumentOutOfRangeException(nameof(experienceToNext), "experience to next level must be at least 1");
            }
            ExperienceToNext = (int[])experienceToNext.Clone();
            ExtraPerLevel = extraPerLevel;
            MaxGems = maxGems;
            PullSpeed = pullSpeed;
            CollectDistance = collectDistance;
        }

        /// <summary>level → level+1에 필요한 경험치. 곡선이 비었으면 오르지 않는다(int.MaxValue).</summary>
        public int ToNext(int level)
        {
            if (ExperienceToNext.Length == 0) return int.MaxValue;
            int index = Math.Max(0, level - 1);
            if (index < ExperienceToNext.Length) return ExperienceToNext[index];
            long need = ExperienceToNext[ExperienceToNext.Length - 1] + (long)ExtraPerLevel * (index - ExperienceToNext.Length + 1);
            return need > int.MaxValue ? int.MaxValue : (int)need;
        }
    }

    public struct GemState
    {
        public int Id;
        public Vec2 Position;
        /// <summary>직전 틱의 위치(표시 보간용).</summary>
        public Vec2 PreviousPosition;
        /// <summary>주우면 얻는 경험치.</summary>
        public int Value;
        /// <summary>한 번 줍는 반경에 들어오면 주울 때까지 끌려온다.</summary>
        public bool Attracted;
    }

    /// <summary>
    /// 강화 값 읽기 · 적용. 값은 World.StatAdd · StatMul에 칸으로 들어 있다:
    /// 칸 번호 = 범위 × StatTargets.Count + 대상, 범위 0 = 판 전체(플레이어 · 모든 무기), 범위 1 + k = 무기 종류 k.
    /// </summary>
    public static class Stats
    {
        public static int Index(int weaponKind, StatTarget target) => (weaponKind + 1) * StatTargets.Count + (int)target;

        /// <summary>강화가 걸린 실제 값 = (기본 + 판 전체 더하기 + 무기 더하기) × 판 전체 곱하기 × 무기 곱하기. weaponKind -1 = 판 전체만.</summary>
        public static float Value(World world, int weaponKind, StatTarget target, float baseValue)
        {
            int g = (int)target;
            float add = world.StatAdd[g];
            float mul = world.StatMul[g];
            if (weaponKind >= 0)
            {
                int k = Index(weaponKind, target);
                add += world.StatAdd[k];
                mul *= world.StatMul[k];
            }
            return (baseValue + add) * mul;
        }

        /// <summary>정수 값(한 번에 쏘는 수 · 관통): 반올림 후 [min, max]로 자른다.</summary>
        public static int IntValue(World world, int weaponKind, StatTarget target, int baseValue, int min, int max)
        {
            int v = (int)Math.Round(Value(world, weaponKind, target, baseValue), MidpointRounding.AwayFromZero);
            return v < min ? min : (v > max ? max : v);
        }

        /// <summary>강화 한 단계를 건다. 연산만 보고 대상 필드의 칸에 더하거나 곱한다(강화 종류별 분기 없음).</summary>
        public static void Apply(World world, in UpgradeData upgrade)
        {
            int i = Index(upgrade.WeaponKind, upgrade.Target);
            if (upgrade.Op == StatOp.Add) world.StatAdd[i] += upgrade.Value;
            else world.StatMul[i] *= upgrade.Value;
        }

        public static void Reset(World world)
        {
            for (int i = 0; i < world.StatAdd.Length; i++)
            {
                world.StatAdd[i] = 0f;
                world.StatMul[i] = 1f;
            }
            for (int i = 0; i < world.UpgradeStages.Length; i++) world.UpgradeStages[i] = 0;
        }
    }

    /// <summary>
    /// 경험치 젬 · 레벨 · 레벨업 선택지. 상태는 World에만 두고 여기는 계산용 작업 공간(고정 크기 배열)만 든다 — 틱마다 할당하지 않는다.
    /// </summary>
    sealed class ProgressionRules
    {
        readonly ExperienceRulesData experience;
        readonly UpgradeRulesData upgrades;
        readonly EnemyKindData[] enemyKinds;
        readonly float baseMoveSpeed;
        readonly float basePickupRadius;
        readonly float tickSeconds;
        readonly int[] candidates;

        public ProgressionRules(ExperienceRulesData experience, UpgradeRulesData upgrades, EnemyRulesData enemies, PlayerTuningData player, int ticksPerSecond)
        {
            this.experience = experience;
            this.upgrades = upgrades;
            enemyKinds = enemies.Kinds;
            baseMoveSpeed = player.MoveSpeed;
            basePickupRadius = player.PickupRadius;
            tickSeconds = 1f / ticksPerSecond;
            candidates = new int[Math.Max(1, upgrades.Rows.Length)];
        }

        public void InitWorld(World world, int weaponKinds)
        {
            world.Level = 1;
            world.Gems = new GemState[experience.MaxGems];
            world.LevelUpChoices = new int[upgrades.ChoicesPerLevelUp];
            world.UpgradeStages = new int[upgrades.Rows.Length];
            int cells = (weaponKinds + 1) * StatTargets.Count;
            world.StatAdd = new float[cells];
            world.StatMul = new float[cells];
            Stats.Reset(world);
        }

        public float MoveSpeed(World world) => Math.Max(0f, Stats.Value(world, -1, StatTarget.MoveSpeed, baseMoveSpeed));

        public float PickupRadius(World world) => Math.Max(0f, Stats.Value(world, -1, StatTarget.PickupRadius, basePickupRadius));

        public int ToNext(int level) => experience.ToNext(level);

        // 줍는 반경 안의 젬은 끌려오기 시작하고(한 번 끌리면 끝까지), 닿으면 경험치가 된다. 빠진 자리는 맨 끝 젬이 채운다.
        public void StepGems(World world)
        {
            Vec2 player = world.Player.Position;
            float radius = PickupRadius(world);
            float radius2 = radius * radius;
            float collect2 = experience.CollectDistance * experience.CollectDistance;
            float step = experience.PullSpeed * tickSeconds;

            int i = 0;
            while (i < world.GemCount)
            {
                ref GemState g = ref world.Gems[i];
                g.PreviousPosition = g.Position;
                Vec2 to = player - g.Position;
                float d2 = to.SqrMagnitude;
                if (!g.Attracted && d2 <= radius2) g.Attracted = true;
                if (g.Attracted)
                {
                    float d = (float)Math.Sqrt(d2);
                    g.Position = d <= step ? player : g.Position + to * (step / d);
                    d2 = (player - g.Position).SqrMagnitude;
                }
                if (d2 <= collect2)
                {
                    int value = g.Value;
                    world.GemsCollectedTotal++;
                    RemoveGem(world, i);
                    GainExperience(world, value);
                    continue; // 맨 끝 젬이 이 자리로 왔다(아직 이번 틱에 안 움직인 젬)
                }
                i++;
            }
        }

        static void RemoveGem(World world, int index)
        {
            int last = world.GemCount - 1;
            if (index != last) world.Gems[index] = world.Gems[last];
            world.GemCount--;
        }

        /// <summary>이번 틱에 죽은 적(Dead) 자리에 젬을 떨어뜨린다. 가득 차면 맨 끝 젬에 경험치를 더한다(사라지지 않게).</summary>
        public void DropGems(World world)
        {
            if (world.Gems.Length == 0) return;
            for (int i = 0; i < world.EnemyCount; i++)
            {
                ref readonly EnemyState e = ref world.Enemies[i];
                if (!e.Dead) continue;
                int value = enemyKinds[e.Kind].ExperienceValue;
                if (value <= 0) continue;
                world.GemsDroppedTotal++;
                if (world.GemCount < world.Gems.Length)
                {
                    world.Gems[world.GemCount] = new GemState
                    {
                        Id = world.AllocateEntityId(),
                        Position = e.Position,
                        PreviousPosition = e.Position,
                        Value = value,
                    };
                    world.GemCount++;
                }
                else
                {
                    world.Gems[world.GemCount - 1].Value += value;
                }
            }
        }

        /// <summary>경험치를 더하고 곡선대로 레벨을 올린다. 오른 만큼 대기열(PendingLevelUps)에 쌓고, 대기열이 막 생겼으면 선택지를 뽑는다.</summary>
        public void GainExperience(World world, int amount)
        {
            if (amount <= 0) return;
            bool wasChoosing = world.PendingLevelUps > 0;
            long xp = (long)world.Experience + amount;
            world.ExperienceTotal += amount;
            while (true)
            {
                int need = experience.ToNext(world.Level);
                if (need == int.MaxValue || xp < need) break;
                xp -= need;
                world.Level++;
                world.PendingLevelUps++;
            }
            world.Experience = xp > int.MaxValue ? int.MaxValue : (int)xp;
            if (!wasChoosing && world.PendingLevelUps > 0) RollChoices(world);
        }

        bool Offerable(World world, int row)
        {
            ref readonly UpgradeData u = ref upgrades.Rows[row];
            if (world.UpgradeStages[row] >= u.MaxStage) return false;
            if (u.WeaponKind < 0) return true;
            for (int w = 0; w < world.Weapons.Length; w++)
            {
                if (world.Weapons[w].Kind == u.WeaponKind) return true;
            }
            return false;
        }

        /// <summary>
        /// 레벨업 선택지를 레벨업 전용 난수 스트림으로 뽑는다: 걸 수 있는 강화(최대 단계 전 · 가진 무기) 중 겹치지 않게 최대 ChoicesPerLevelUp개.
        /// 뽑을 것이 없으면 대기열을 비운다(레벨은 그대로 오른 채).
        /// </summary>
        public void RollChoices(World world)
        {
            int n = 0;
            for (int r = 0; r < upgrades.Rows.Length; r++)
            {
                if (Offerable(world, r)) candidates[n++] = r;
            }
            int pick = Math.Min(n, world.LevelUpChoices.Length);
            for (int i = 0; i < pick; i++)
            {
                int j = world.LevelUpRandom.Range(i, n);
                int t = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = t;
                world.LevelUpChoices[i] = candidates[i];
            }
            world.LevelUpChoiceCount = pick;
            if (pick == 0) world.PendingLevelUps = 0;
        }

        /// <summary>지금 보이는 선택지 중 choice번째를 고른다. 대기열이 남았으면 다음 선택지를 뽑는다. 고를 수 없으면 false.</summary>
        public bool Choose(World world, int choice)
        {
            if (world.PendingLevelUps <= 0 || choice < 0 || choice >= world.LevelUpChoiceCount) return false;
            int row = world.LevelUpChoices[choice];
            ApplyUpgrade(world, row);
            world.PendingLevelUps--;
            world.LevelUpChoiceCount = 0;
            if (world.PendingLevelUps > 0) RollChoices(world);
            return true;
        }

        /// <summary>강화 한 줄을 한 단계 올린다. 최대 단계면 false.</summary>
        public bool ApplyUpgrade(World world, int row)
        {
            if (row < 0 || row >= upgrades.Rows.Length) return false;
            if (world.UpgradeStages[row] >= upgrades.Rows[row].MaxStage) return false;
            Stats.Apply(world, upgrades.Rows[row]);
            world.UpgradeStages[row]++;
            return true;
        }
    }
}
