using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using CatSanguo.Battle;
using CatSanguo.Skills;

namespace CatSanguo.AI;

/// <summary>
/// 效用AI评分器，用于评估技能释放目标和移动目标的价值。
/// 基于多维度因素综合评分：血量、距离、威胁度、技能匹配度等。
/// </summary>
public class UtilityScorer
{
    /// <summary>
    /// 评估技能释放的价值
    /// </summary>
    /// <returns>评分 > 0 则应该释放技能</returns>
    public static float EvaluateSkillUse(Squad caster, Skill skill, BattleBlackboard blackboard)
    {
        float score = 0f;

        // 1. 技能就绪度（CD越近评分越高）
        float readiness = skill.IsReady ? 1.0f : 0f;
        score += readiness * 30f;

        // 2. 目标数量评估
        var targets = blackboard.FindSkillTargets(caster, skill);
        if (targets.Count == 0) return 0f;

        score += targets.Count * 15f;

        // 3. 目标血量评估（优先打残血/优先打满血AOE）
        if (skill.TargetMode == SkillTargetMode.AOE_Circle ||
            skill.TargetMode == SkillTargetMode.AOE_Line)
        {
            float totalHPRatio = targets.Sum(t => t.HP / t.MaxHP);
            score += totalHPRatio * 10f; // AOE优先打血量多的
        }
        else if (skill.TargetMode == SkillTargetMode.SingleTarget)
        {
            var target = targets.FirstOrDefault();
            if (target != null)
            {
                float hpRatio = target.HP / target.MaxHP;
                score += (1f - hpRatio) * 25f; // 单体优先打残血
            }
        }

        // 4. 战术价值评估
        if (caster.HP / caster.MaxHP < 0.3f && skill.TargetMode == SkillTargetMode.Self)
        {
            score += 40f; // 低血量时自评技能价值大幅提升
        }

        return score;
    }

    /// <summary>
    /// 评估移动目标的价值
    /// </summary>
    public static float EvaluateMoveTarget(Squad squad, Squad target, BattleBlackboard blackboard)
    {
        float score = 0f;

        // 1. 距离评分（越近越好）
        float dist = Vector2.Distance(squad.Position, target.Position);
        float distScore = 1f / Math.Max(dist / 100f, 1f);
        score += distScore * 30f;

        // 2. 血量评分（优先攻击残血）
        float hpRatio = target.HP / target.MaxHP;
        score += (1f - hpRatio) * 25f;

        // 3. 威胁评分（优先攻击近威胁）
        var nearestThreat = blackboard.GetNearestThreat(squad);
        if (nearestThreat == target)
        {
            score += 20f;
        }

        // 4. 集火评分（如果队友也在攻击这个目标，加分）
        int allyTargetingCount = blackboard.PlayerSquads.Count(s =>
            s.TargetSquad == target && s != squad);
        score += allyTargetingCount * 5f;

        return score;
    }

    /// <summary>
    /// 评估撤退的必要性
    /// </summary>
    public static float EvaluateRetreatNeed(Squad squad, BattleBlackboard blackboard)
    {
        float score = 0f;

        // 1. 血量极低
        float hpRatio = squad.HP / squad.MaxHP;
        if (hpRatio < 0.2f) score += 50f;
        else if (hpRatio < 0.4f) score += 30f;

        // 2. 被集火
        int enemyTargetingCount = blackboard.EnemySquads.Count(s =>
            Vector2.DistanceSquared(s.Position, squad.Position) < 10000); // 100单位内
        score += enemyTargetingCount * 10f;

        // 3. 友军劣势
        if (blackboard.IsEnemyAdvantage) score += 20f;

        return score;
    }

    /// <summary>
    /// 评估姿态切换
    /// </summary>
    public static AIPosture EvaluateOptimalPosture(Squad squad, BattleBlackboard blackboard)
    {
        float aggressiveScore = 0f;
        float defensiveScore = 0f;

        // 我方优势 -> 激进
        if (blackboard.IsPlayerAdvantage) aggressiveScore += 30f;
        if (blackboard.EnemyAliveCount <= 1) aggressiveScore += 20f;

        // 敌方优势 -> 防守
        if (blackboard.IsEnemyAdvantage) defensiveScore += 30f;
        if (blackboard.PlayerAliveCount <= 1) defensiveScore += 40f;

        // 低血量 -> 防守
        if (squad.HP / squad.MaxHP < 0.3f) defensiveScore += 25f;

        // 满状态 -> 激进
        if (squad.HP / squad.MaxHP > 0.8f) aggressiveScore += 15f;

        return aggressiveScore >= defensiveScore ? AIPosture.Aggressive : AIPosture.Defensive;
    }

    /// <summary>
    /// 选择最佳技能目标
    /// </summary>
    public static List<Squad>? SelectBestSkillTargets(Squad caster, BattleBlackboard blackboard)
    {
        if (caster.ActiveSkill == null || !caster.ActiveSkill.IsReady)
            return null;

        float score = EvaluateSkillUse(caster, caster.ActiveSkill, blackboard);
        if (score < 20f) return null; // 阈值：评分不够就不放

        return blackboard.FindSkillTargets(caster, caster.ActiveSkill);
    }

    /// <summary>
    /// 选择最佳移动目标
    /// </summary>
    public static Squad? SelectBestMoveTarget(Squad squad, BattleBlackboard blackboard)
    {
        Squad? best = null;
        float bestScore = float.MinValue;

        foreach (var enemy in blackboard.EnemySquads)
        {
            float score = EvaluateMoveTarget(squad, enemy, blackboard);
            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return best;
    }
}
