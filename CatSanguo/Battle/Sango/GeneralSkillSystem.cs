using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Battle.Sango;

/// <summary>
/// 已解析的技能 - 绑定 SkillData + 运行时冷却状态
/// </summary>
public class ResolvedSkill
{
    public SkillData Data { get; }
    public int CooldownRoundsLeft { get; set; }
    public int CooldownRoundsTotal { get; }
    public bool IsReady => CooldownRoundsLeft == 0 && Data.Type == "active";

    public ResolvedSkill(SkillData data, float executionDuration)
    {
        Data = data;
        CooldownRoundsTotal = Math.Max(1, (int)Math.Ceiling(data.Cooldown / executionDuration));
        CooldownRoundsLeft = 0;
    }
}

/// <summary>
/// 武将技能系统 - 技能解析、释放、冷却管理
/// </summary>
public class GeneralSkillSystem
{
    /// <summary>为武将解析技能 (绑定 SkillData 到 GeneralUnit)</summary>
    public void ResolveSkills(GeneralUnit unit, List<SkillData> allSkills)
    {
        unit.ResolvedSkills.Clear();
        float execDuration = Core.GameSettings.SangoExecutionDuration;

        // Slot 0: ActiveSkillId
        var activeSkill = FindSkill(unit.General.ActiveSkillId, allSkills);
        if (activeSkill != null)
            unit.ResolvedSkills.Add(new ResolvedSkill(activeSkill, execDuration));

        // Slot 1-2: SpecialSkills[0..1]
        var specials = unit.General.SpecialSkills;
        for (int i = 0; i < 2 && i < specials.Count; i++)
        {
            var skill = FindSkill(specials[i], allSkills);
            if (skill != null)
                unit.ResolvedSkills.Add(new ResolvedSkill(skill, execDuration));
        }

        // 若不足3个，用默认技能补齐 (基础攻击)
        while (unit.ResolvedSkills.Count < 3)
        {
            var fallback = allSkills.FirstOrDefault(s => s.Type == "active");
            if (fallback != null)
                unit.ResolvedSkills.Add(new ResolvedSkill(fallback, execDuration));
            else
                break;
        }
    }

    private SkillData? FindSkill(string? skillId, List<SkillData> allSkills)
    {
        if (string.IsNullOrEmpty(skillId)) return null;
        return allSkills.FirstOrDefault(s =>
            s.Id.Equals(skillId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>释放技能, 返回是否成功</summary>
    public bool CastSkill(GeneralUnit caster, int slotIndex,
                           ArmyGroup enemyArmy, ArmyGroup friendlyArmy,
                           BattleVFXSystem vfx)
    {
        if (caster.IsDefeated) return false;
        if (slotIndex < 0 || slotIndex >= caster.ResolvedSkills.Count) return false;

        var skill = caster.ResolvedSkills[slotIndex];
        if (skill.CooldownRoundsLeft > 0) return false;

        // 计算基础数值 (EffectiveXxx 含装备加成, 为0时回退到基础值)
        float baseStat;
        if (skill.Data.StatBasis?.ToLower() == "intelligence")
            baseStat = caster.General.EffectiveIntelligence > 0
                ? caster.General.EffectiveIntelligence : caster.General.Intelligence;
        else
            baseStat = caster.General.EffectiveStrength > 0
                ? caster.General.EffectiveStrength : caster.General.Strength;
        float value = baseStat * skill.Data.Coefficient;

        // 技能伤害加成 (被动)
        if (skill.Data.EffectType?.ToLower() == "damage" && caster.SkillDamageBonus > 0)
            value *= (1f + caster.SkillDamageBonus);

        // 获取目标
        var targets = ResolveTargets(caster, skill.Data, enemyArmy, friendlyArmy);

        // 应用效果
        ApplyEffect(skill.Data, value, targets, caster, enemyArmy, friendlyArmy, vfx);

        // 设置冷却
        skill.CooldownRoundsLeft = skill.CooldownRoundsTotal;

        // 设置武将状态
        caster.State = GeneralUnitState.CastingSkill;

        return true;
    }

    private List<Soldier> ResolveTargets(GeneralUnit caster, SkillData data,
                                          ArmyGroup enemyArmy, ArmyGroup friendlyArmy)
    {
        switch (data.TargetMode?.ToLower())
        {
            case "self":
                // 对己方士兵 (buff/heal)
                return friendlyArmy.GetAllAliveSoldiers()
                    .Where(s => s.Owner == caster)
                    .ToList();

            case "aoe_circle":
                // 全体敌方 (带半径限制可选)
                return enemyArmy.GetAllAliveSoldiers();

            case "aoe_line":
                // 对最近敌方武将的全部士兵
                var nearestEnemy = FindNearestEnemyUnit(caster, enemyArmy);
                if (nearestEnemy != null)
                    return nearestEnemy.Soldiers.Where(s => s.IsAlive).ToList();
                return enemyArmy.GetAllAliveSoldiers();

            case "singletarget":
            default:
                // 对最近敌方武将的士兵
                var target = FindNearestEnemyUnit(caster, enemyArmy);
                if (target != null)
                    return target.Soldiers.Where(s => s.IsAlive).ToList();
                return enemyArmy.GetAllAliveSoldiers();
        }
    }

    private GeneralUnit? FindNearestEnemyUnit(GeneralUnit caster, ArmyGroup enemyArmy)
    {
        GeneralUnit? nearest = null;
        float minDist = float.MaxValue;
        foreach (var eu in enemyArmy.Units)
        {
            if (eu.IsDefeated) continue;
            float d = Vector2.DistanceSquared(caster.GeneralPosition, eu.GeneralPosition);
            if (d < minDist) { minDist = d; nearest = eu; }
        }
        return nearest;
    }

    private void ApplyEffect(SkillData data, float value, List<Soldier> targets,
                              GeneralUnit caster, ArmyGroup enemyArmy, ArmyGroup friendlyArmy,
                              BattleVFXSystem vfx)
    {
        switch (data.EffectType?.ToLower())
        {
            case "damage":
                foreach (var s in targets)
                {
                    float dmg = value * (0.8f + (float)Random.Shared.NextDouble() * 0.4f);
                    s.TakeDamage(dmg);
                    SpawnSkillVFX(vfx, data, s.Position);
                }
                break;

            case "heal":
                // 治疗己方 - targets 应该是己方士兵
                var healTargets = friendlyArmy.GetAllAliveSoldiers();
                foreach (var s in healTargets)
                {
                    float heal = value * 0.5f;
                    s.HP = Math.Min(s.MaxHP, s.HP + heal);
                    vfx.AddHealText(s.Position, (int)heal);
                }
                vfx.ScreenFlash(new Color(100, 255, 150), 0.2f);
                break;

            case "morale":
                // 降低敌方士气
                foreach (var eu in enemyArmy.Units.Where(u => !u.IsDefeated))
                {
                    eu.Morale = Math.Max(0, eu.Morale + data.MoraleChange);
                }
                if (data.MoraleChange < 0)
                    vfx.ScreenFlash(new Color(180, 80, 80), 0.2f);
                else
                    vfx.ScreenFlash(new Color(100, 200, 255), 0.2f);
                break;

            case "buff":
                // 提升己方属性
                var buffOwner = targets.FirstOrDefault()?.Owner ?? caster;
                switch (data.BuffStat?.ToLower())
                {
                    case "attack":
                        buffOwner.AttackBuffMultiplier += data.BuffPercent;
                        break;
                    case "defense":
                        buffOwner.DefenseBuffMultiplier += data.BuffPercent;
                        break;
                    case "speed":
                        buffOwner.AttackBuffMultiplier += data.BuffPercent * 0.5f;
                        break;
                    default:
                        buffOwner.AttackBuffMultiplier += data.BuffPercent;
                        break;
                }
                vfx.ScreenFlash(new Color(255, 220, 100), 0.2f);
                break;

            default:
                // 默认当伤害处理
                foreach (var s in targets)
                {
                    float dmg = value * (0.8f + (float)Random.Shared.NextDouble() * 0.4f);
                    s.TakeDamage(dmg);
                }
                break;
        }
    }

    private void SpawnSkillVFX(BattleVFXSystem vfx, SkillData data, Vector2 pos)
    {
        string name = data.Name?.ToLower() ?? "";
        if (name.Contains("火") || name.Contains("fire"))
            vfx.SpawnFireEffect(pos, 6);
        else if (name.Contains("雷") || name.Contains("lightning"))
            vfx.SpawnLightningEffect(pos);
        else if (name.Contains("冰") || name.Contains("ice"))
            vfx.SpawnIceEffect(pos, 4);
        else
            vfx.SpawnHitSparks(pos, 6);
    }

    /// <summary>解析并应用被动技能到武将属性</summary>
    public void ResolvePassiveSkill(GeneralUnit unit, List<SkillData> allSkills)
    {
        var passive = FindSkill(unit.General.PassiveSkillId, allSkills);
        if (passive == null) return;

        switch (passive.BuffStat?.ToLower())
        {
            case "attack":
                unit.AttackBuffMultiplier += passive.BuffPercent;
                break;
            case "defense":
                unit.DefenseBuffMultiplier += passive.BuffPercent;
                break;
            case "morale_floor":
                unit.MoraleFloor = passive.BuffPercent * 100f;
                break;
            case "crit":
                unit.CritChance = passive.BuffPercent;
                break;
            case "dodge":
                unit.DodgeChance = passive.BuffPercent;
                break;
            case "regen":
                unit.RegenPercent = passive.BuffPercent;
                break;
            case "skill_damage":
                unit.SkillDamageBonus = passive.BuffPercent;
                break;
            default:
                if (passive.BuffPercent > 0)
                    unit.AttackBuffMultiplier += passive.BuffPercent;
                break;
        }
    }

    /// <summary>回合结束时为有回血被动的武将恢复HP</summary>
    public void ApplyRoundEndRegen(IEnumerable<GeneralUnit> units)
    {
        foreach (var unit in units)
        {
            if (unit.IsDefeated || unit.RegenPercent <= 0) continue;
            foreach (var s in unit.Soldiers.Where(s => s.IsAlive))
            {
                float heal = s.MaxHP * unit.RegenPercent;
                s.HP = Math.Min(s.MaxHP, s.HP + heal);
            }
        }
    }
}
