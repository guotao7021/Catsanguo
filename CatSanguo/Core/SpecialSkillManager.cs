using System;
using System.Collections.Generic;
using System.Linq;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.Generals;

namespace CatSanguo.Core;

/// <summary>
/// 武将特技管理器
/// 负责管理武将特技的触发和效果计算
/// </summary>
public class SpecialSkillManager
{
    private readonly EventBus _eventBus;

    public SpecialSkillManager(EventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// 获取武将的特技效果描述
    /// </summary>
    public string GetSkillDescription(string skillId)
    {
        return skillId switch
        {
            // 关羽特技
            "green_dragon_blade" => "青龙偃月：攻击时有15%概率造成1.5倍伤害",
            "single_rider" => "千里独行/单骑救主：移动距离+2，撤退成功率+20%，单独出征时统帅+10",

            // 张飞特技
            "war_cry" => "战吼：战斗开始时使敌方全体士气-10%",
            "bridge_stand" => "据水断桥：防守时防御力+30%",

            // 诸葛亮特技
            "eight_diagrams" => "八卦阵：每10秒切换攻防状态，防御态减伤30%",
            "borrow_east_wind" => "借东风：火系技能伤害+50%",

            // 曹操特技
            "ambush_tactic" => "奇袭：首次攻击伤害+40%",
            "poem_inspire" => "短歌行：回合开始时己方全体士气+15%",

            // 张辽特技
            "raid_charge" => "突袭：冲锋伤害+30%，可穿透后排",
            "cavalry_command" => "骑兵指挥：骑兵单位攻击+15%",

            // 刘备特技
            "benevolent_rule" => "仁德：所在城池忠诚度下降速度-50%",
            "oath_brothers" => "桃园结义：与关羽/张飞同队时，全属性+5",

            // 孙权特技
            "river_defense" => "长江天险：在水域附近作战时防御+25%",
            "talent_recruit" => "求贤若渴：招募武将时金币消耗-20%",

            // 周瑜特技
            "fire_command" => "火攻：火系技能范围+50%，伤害+25%",
            "music_mastery" => "曲有误周郎顾：降低敌方武将智力效果10%",

            // 赵云特技
            "seven_in_seven_out" => "七进七出：被包围时攻击力+40%",

            // 貂蝉特技
            "chain_scheme" => "连环计：使两个敌方武将互相攻击概率+15%",
            "moon_dance" => "闭月：每回合恢复自身5%兵力",

            // 吕布特技
            "horse_riding_archery" => "骑射：骑兵形态下可远程攻击",
            "god_of_war" => "战神：武力+10，但忠诚度下降速度+30%",

            // 司马懿特技
            "wolf_look" => "狼顾：被攻击时有20%概率闪避",
            "patience_scheme" => "隐忍：回合数越多，智力加成越高（每回合+1，上限+20）",

            _ => "未知特技"
        };
    }

    /// <summary>
    /// 获取武将拥有的特技列表
    /// </summary>
    public List<string> GetGeneralSkills(string generalId)
    {
        var gs = GameState.Instance;
        var progress = gs.GetGeneralProgress(generalId);
        if (progress == null) return new List<string>();

        var generalData = progress.Data;
        return generalData.SpecialSkills.ToList();
    }

    /// <summary>
    /// 计算武将特技带来的战斗加成
    /// </summary>
    public CombatBonus CalculateCombatBonus(General general, List<General> allies, List<General> enemies)
    {
        var bonus = new CombatBonus();

        foreach (var skillId in general.SpecialSkills)
        {
            ApplySkillBonus(skillId, general, allies, enemies, bonus);
        }

        return bonus;
    }

    private void ApplySkillBonus(string skillId, General general, List<General> allies, List<General> enemies, CombatBonus bonus)
    {
        switch (skillId)
        {
            case "green_dragon_blade":
                bonus.CritChance += 0.15f;
                bonus.CritMultiplier += 0.5f;
                break;

            case "single_rider":
                bonus.RetreatSuccessRate += 0.2f;
                break;

            case "war_cry":
                bonus.EnemyMoraleReduction += 0.1f;
                break;

            case "bridge_stand":
                bonus.DefenseBonus += 0.3f;
                break;

            case "eight_diagrams":
                bonus.HasPhaseSwitch = true;
                bonus.PhaseSwitchInterval = 10f;
                bonus.DefensePhaseReduction = 0.3f;
                break;

            case "borrow_east_wind":
                bonus.FireDamageBonus += 0.5f;
                break;

            case "ambush_tactic":
                bonus.FirstStrikeBonus += 0.4f;
                break;

            case "poem_inspire":
                bonus.AlliedMoraleBonus += 0.15f;
                break;

            case "raid_charge":
                bonus.ChargeDamageBonus += 0.3f;
                bonus.CanPierce = true;
                break;

            case "cavalry_command":
                bonus.CavalryAttackBonus += 0.15f;
                break;

            case "benevolent_rule":
                bonus.LoyaltyDecayReduction += 0.5f;
                break;

            case "oath_brothers":
                bool hasGuanyu = allies.Any(a => a.Id == "guanyu") || general.Id == "guanyu";
                bool hasZhangFei = allies.Any(a => a.Id == "zhangfei") || general.Id == "zhangfei";
                if (hasGuanyu && hasZhangFei)
                {
                    bonus.AllStatBonus += 5;
                }
                break;

            case "river_defense":
                bonus.RiverDefenseBonus += 0.25f;
                break;

            case "talent_recruit":
                bonus.RecruitCostReduction += 0.2f;
                break;

            case "fire_command":
                bonus.FireDamageBonus += 0.25f;
                bonus.FireRadiusBonus += 0.5f;
                break;

            case "music_mastery":
                bonus.EnemyIntelligenceReduction += 0.1f;
                break;

            case "seven_in_seven_out":
                bonus.SurroundedAttackBonus += 0.4f;
                break;

            case "chain_scheme":
                bonus.EnemyFriendlyFireChance += 0.15f;
                break;

            case "moon_dance":
                bonus.SelfHealRate += 0.05f;
                break;

            case "horse_riding_archery":
                bonus.CavalryRangedAttack = true;
                break;

            case "god_of_war":
                bonus.StrengthBonus += 10;
                bonus.LoyaltyDecayIncrease += 0.3f;
                break;

            case "wolf_look":
                bonus.DodgeChance += 0.2f;
                break;

            case "patience_scheme":
                // 这个需要在回合制中动态计算，这里先返回0
                bonus.IntelligenceBonus += 0; // 运行时计算
                break;
        }
    }

    /// <summary>
    /// 计算司马懿"隐忍"特技的智力加成
    /// </summary>
    public int CalculatePatienceSchemeBonus(string generalId)
    {
        if (generalId != "simayi") return 0;

        var gs = GameState.Instance;
        int turnCount = gs.TurnNumber;
        return Math.Min(turnCount, 20); // 每回合+1，上限+20
    }

    /// <summary>
    /// 检查武将是否拥有指定特技
    /// </summary>
    public bool HasSkill(string generalId, string skillId)
    {
        var gs = GameState.Instance;
        var progress = gs.GetGeneralProgress(generalId);
        if (progress == null) return false;

        return progress.Data.SpecialSkills.Contains(skillId);
    }
}

/// <summary>
/// 战斗加成数据类
/// </summary>
public class CombatBonus
{
    // 攻击相关
    public float CritChance { get; set; } = 0f;
    public float CritMultiplier { get; set; } = 0f;
    public float FirstStrikeBonus { get; set; } = 0f;
    public float ChargeDamageBonus { get; set; } = 0f;
    public int StrengthBonus { get; set; } = 0;
    public int AllStatBonus { get; set; } = 0;
    public int IntelligenceBonus { get; set; } = 0;
    public float SurroundedAttackBonus { get; set; } = 0f;
    public float CavalryAttackBonus { get; set; } = 0f;
    public bool CanPierce { get; set; } = false;
    public bool CavalryRangedAttack { get; set; } = false;

    // 防御相关
    public float DefenseBonus { get; set; } = 0f;
    public float DodgeChance { get; set; } = 0f;
    public float RiverDefenseBonus { get; set; } = 0f;

    // 士气相关
    public float EnemyMoraleReduction { get; set; } = 0f;
    public float AlliedMoraleBonus { get; set; } = 0f;

    // 火系相关
    public float FireDamageBonus { get; set; } = 0f;
    public float FireRadiusBonus { get; set; } = 0f;

    // 特技机制
    public bool HasPhaseSwitch { get; set; } = false;
    public float PhaseSwitchInterval { get; set; } = 10f;
    public float DefensePhaseReduction { get; set; } = 0.3f;

    // 撤退/俘获
    public float RetreatSuccessRate { get; set; } = 0f;

    // 忠诚度
    public float LoyaltyDecayReduction { get; set; } = 0f;
    public float LoyaltyDecayIncrease { get; set; } = 0f;

    // 外交/招募
    public float RecruitCostReduction { get; set; } = 0f;

    // 敌方干扰
    public float EnemyIntelligenceReduction { get; set; } = 0f;
    public float EnemyFriendlyFireChance { get; set; } = 0f;

    // 治疗
    public float SelfHealRate { get; set; } = 0f;
}
