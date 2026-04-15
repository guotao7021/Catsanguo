using System;
using Microsoft.Xna.Framework;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Battle;

public static class DamageCalculator
{
    private static readonly Random _rng = new();

    public static float Calculate(Squad attacker, Squad defender, float skillCoefficient)
    {
        float baseAttack = attacker.EffectiveAttack;

        // 1. 军种克制倍率（核心新增）
        float unitCounterMod = UnitCounterConfig.GetCounterMultiplier(attacker.UnitType, defender.UnitType);

        // 2. 阵型克制倍率（保留兼容）
        float formationMod = GetFormationModifier(attacker.Formation, defender.Formation);

        // 3. 阵型攻击/防御加成
        float formationAtkBonus = 1f + attacker.GetFormationAttackBonus();
        float formationDefBonus = 1f + defender.GetFormationDefenseBonus();

        // 4. 军种属性加成
        float unitAtkBonus = attacker.GetUnitAttackMultiplier();
        float unitDefBonus = defender.GetUnitDefenseMultiplier();

        // 5. 随机波动
        float randomVariance = 0.9f + (float)_rng.NextDouble() * 0.2f;

        // 6. 士气影响
        float moraleMod = MathHelper.Lerp(0.5f, 1.0f, attacker.Morale / 100f);

        // 组合所有倍率
        float damage = baseAttack * skillCoefficient
                     * unitCounterMod
                     * formationMod
                     * formationAtkBonus
                     * unitAtkBonus
                     / (formationDefBonus * unitDefBonus)
                     * randomVariance
                     * moraleMod;

        // 7. 远程对前排减免
        if (defender.Formation == FormationType.Vanguard && attacker.Formation == FormationType.Archer)
        {
            damage *= 0.6f; // 40% reduction
        }

        // 8. 阵型减伤（鱼鳞阵等）
        float formationDmgReduction = defender.GetFormationDamageReduction();
        if (formationDmgReduction > 0)
        {
            damage *= (1f - formationDmgReduction);
        }

        return Math.Max(1, damage);
    }

    /// <summary>获取对特定目标的克制倍率（用于UI显示）</summary>
    public static float GetCounterDisplay(UnitType attacker, UnitType defender)
    {
        float counter = UnitCounterConfig.GetCounterMultiplier(attacker, defender);
        if (counter > 1.2f) return 1;      // 强克
        if (counter > 1.0f) return 2;      // 弱克
        if (counter < 0.8f) return 4;      // 被克
        return 3;                           // 无克制
    }

    private static float GetFormationModifier(FormationType attacker, FormationType defender)
    {
        // 保留原有的阵型克制（如果有的话）
        if (attacker == FormationType.Cavalry && defender == FormationType.Archer) return 1.1f;
        if (attacker == FormationType.Archer && defender == FormationType.Vanguard) return 1.1f;
        if (attacker == FormationType.Vanguard && defender == FormationType.Cavalry) return 1.1f;

        // 反向
        if (attacker == FormationType.Archer && defender == FormationType.Cavalry) return 0.9f;
        if (attacker == FormationType.Vanguard && defender == FormationType.Archer) return 0.9f;
        if (attacker == FormationType.Cavalry && defender == FormationType.Vanguard) return 0.9f;

        return 1.0f;
    }
}
