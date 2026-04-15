using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using CatSanguo.Battle;
using CatSanguo.Core;

namespace CatSanguo.AI;

public class BattleAI
{
    private float _thinkTimer;
    private readonly float _thinkInterval;
    private readonly Team _team;

    public BattleAI(Team team, int difficulty = 1)
    {
        _team = team;
        _thinkInterval = difficulty switch
        {
            1 => 2.0f,
            2 => 1.0f,
            _ => 0.5f
        };
    }

    public void Update(float deltaTime, List<Squad> allSquads)
    {
        _thinkTimer += deltaTime;
        if (_thinkTimer < _thinkInterval) return;
        _thinkTimer = 0;

        var mySquads = allSquads.Where(s => s.Team == _team && s.IsActive).ToList();
        var enemySquads = allSquads.Where(s => s.Team != _team && s.IsActive).ToList();

        if (enemySquads.Count == 0) return;

        foreach (var squad in mySquads)
        {
            // Try using skill
            if (squad.ActiveSkill != null && squad.ActiveSkill.IsReady && squad.State == SquadState.Engaging)
            {
                var skillTargets = GetSkillTargets(squad, allSquads);
                if (skillTargets.Count > 0)
                {
                    squad.UseSkill(skillTargets);
                    continue;
                }
            }

            // Select target using heuristic
            squad.TargetSquad = SelectBestTarget(squad, enemySquads);
        }
    }

    private Squad? SelectBestTarget(Squad squad, List<Squad> enemies)
    {
        Squad? best = null;
        float bestScore = float.MinValue;

        foreach (var enemy in enemies)
        {
            float dist = Vector2.Distance(squad.Position, enemy.Position);
            float distScore = 1f / Math.Max(dist, 1f);
            float hpScore = 1f - (enemy.HP / enemy.MaxHP);
            float score = 0.6f * distScore * 1000f + 0.4f * hpScore;

            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return best;
    }

    private List<Squad> GetSkillTargets(Squad caster, List<Squad> allSquads)
    {
        if (caster.ActiveSkill == null) return new();

        var skill = caster.ActiveSkill;
        var targets = new List<Squad>();

        switch (skill.TargetMode)
        {
            case Skills.SkillTargetMode.SingleTarget:
                if (caster.TargetSquad != null && !caster.TargetSquad.IsDead)
                    targets.Add(caster.TargetSquad);
                break;

            case Skills.SkillTargetMode.AOE_Circle:
                var enemySquads = allSquads.Where(s => s.Team != caster.Team && s.IsActive);
                if (caster.TargetSquad != null)
                {
                    targets.AddRange(enemySquads.Where(s =>
                        Vector2.Distance(s.Position, caster.TargetSquad.Position) <= skill.Radius));
                }
                break;

            case Skills.SkillTargetMode.Self:
                targets.AddRange(allSquads.Where(s => s.Team == caster.Team && s.IsActive));
                break;

            case Skills.SkillTargetMode.AOE_Line:
                if (caster.TargetSquad != null)
                {
                    Vector2 dir = Vector2.Normalize(caster.TargetSquad.Position - caster.Position);
                    targets.AddRange(allSquads.Where(s => s.Team != caster.Team && s.IsActive)
                        .Where(s =>
                        {
                            Vector2 toTarget = s.Position - caster.Position;
                            float dot = Vector2.Dot(Vector2.Normalize(toTarget), dir);
                            float dist = toTarget.Length();
                            return dot > 0.7f && dist < skill.Radius;
                        }));
                }
                break;
        }

        return targets;
    }
}
