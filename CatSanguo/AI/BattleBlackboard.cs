using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using CatSanguo.Battle;

namespace CatSanguo.AI;

/// <summary>
/// 战场共享黑板，用于AI决策系统共享战场信息。
/// 避免重复查询，提高决策效率。
/// </summary>
public class BattleBlackboard
{
    // 战场信息
    public List<Squad> PlayerSquads { get; set; } = new();
    public List<Squad> EnemySquads { get; set; } = new();
    public List<Squad> AllSquads { get; set; } = new();

    // 态势评估
    public float PlayerTotalHP { get; set; }
    public float EnemyTotalHP { get; set; }
    public float PlayerTotalSoldiers { get; set; }
    public float EnemyTotalSoldiers { get; set; }
    public int PlayerAliveCount { get; set; }
    public int EnemyAliveCount { get; set; }

    // 优势评估
    public bool IsPlayerAdvantage => PlayerTotalHP > EnemyTotalHP * 1.2f;
    public bool IsEnemyAdvantage => EnemyTotalHP > PlayerTotalHP * 1.2f;
    public bool IsEvenMatch => !IsPlayerAdvantage && !IsEnemyAdvantage;

    // 最近威胁
    private readonly Dictionary<Squad, Squad> _nearestThreats = new();

    /// <summary>
    /// 刷新黑板数据
    /// </summary>
    public void Refresh(List<Squad> allSquads, Team aiTeam)
    {
        AllSquads = allSquads;
        PlayerSquads = allSquads.Where(s => s.Team == aiTeam && s.IsActive).ToList();
        EnemySquads = allSquads.Where(s => s.Team != aiTeam && s.IsActive).ToList();

        PlayerTotalHP = PlayerSquads.Sum(s => s.HP);
        EnemyTotalHP = EnemySquads.Sum(s => s.HP);
        PlayerTotalSoldiers = PlayerSquads.Sum(s => s.SoldierCount);
        EnemyTotalSoldiers = EnemySquads.Sum(s => s.SoldierCount);
        PlayerAliveCount = PlayerSquads.Count;
        EnemyAliveCount = EnemySquads.Count;

        // 计算最近威胁
        _nearestThreats.Clear();
        foreach (var squad in PlayerSquads)
        {
            var nearest = FindNearestEnemy(squad);
            if (nearest != null)
                _nearestThreats[squad] = nearest;
        }
    }

    /// <summary>
    /// 获取指定武将的最近威胁
    /// </summary>
    public Squad? GetNearestThreat(Squad squad)
    {
        return _nearestThreats.TryGetValue(squad, out var threat) ? threat : null;
    }

    /// <summary>
    /// 查找最近敌方武将
    /// </summary>
    public Squad? FindNearestEnemy(Squad from)
    {
        Squad? nearest = null;
        float minDist = float.MaxValue;

        foreach (var enemy in EnemySquads)
        {
            float dist = Vector2.DistanceSquared(from.Position, enemy.Position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 查找血量最低的敌方武将
    /// </summary>
    public Squad? FindWeakestEnemy()
    {
        return EnemySquads.OrderBy(s => s.HP / s.MaxHP).FirstOrDefault();
    }

    /// <summary>
    /// 查找指定范围内的所有敌人
    /// </summary>
    public List<Squad> FindEnemiesInRange(Vector2 center, float range)
    {
        return EnemySquads.Where(s =>
            Vector2.DistanceSquared(s.Position, center) <= range * range
        ).ToList();
    }

    /// <summary>
    /// 查找可释放技能的目标
    /// </summary>
    public List<Squad> FindSkillTargets(Squad caster, Skills.Skill skill)
    {
        var targets = new List<Squad>();

        switch (skill.TargetMode)
        {
            case Skills.SkillTargetMode.SingleTarget:
                var target = FindWeakestEnemy();
                if (target != null) targets.Add(target);
                break;

            case Skills.SkillTargetMode.AOE_Circle:
                targets.AddRange(FindAOETargets(caster, skill.Radius));
                break;

            case Skills.SkillTargetMode.Self:
                targets.Add(caster);
                break;

            case Skills.SkillTargetMode.AOE_Line:
                targets.AddRange(FindLineTargets(caster, skill.Radius));
                break;
        }

        return targets;
    }

    private List<Squad> FindAOETargets(Squad caster, float radius)
    {
        List<Squad> bestTargets = new();
        int bestCount = 0;

        // 尝试以每个敌人为中心，找最大命中数
        foreach (var center in EnemySquads)
        {
            var hits = EnemySquads.Where(s =>
                Vector2.DistanceSquared(s.Position, center.Position) <= radius * radius
            ).ToList();

            if (hits.Count > bestCount)
            {
                bestCount = hits.Count;
                bestTargets = hits;
            }
        }

        return bestTargets;
    }

    private List<Squad> FindLineTargets(Squad caster, float range)
    {
        var targets = new List<Squad>();
        var nearest = GetNearestThreat(caster);
        if (nearest == null) return targets;

        Vector2 dir = Vector2.Normalize(nearest.Position - caster.Position);
        return EnemySquads.Where(s =>
        {
            Vector2 toTarget = s.Position - caster.Position;
            float dot = Vector2.Dot(Vector2.Normalize(toTarget), dir);
            float dist = toTarget.Length();
            return dot > 0.7f && dist < range;
        }).ToList();
    }
}
