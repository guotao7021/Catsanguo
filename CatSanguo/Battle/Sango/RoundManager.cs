using System;
using System.Collections.Generic;
using CatSanguo.Core;

namespace CatSanguo.Battle.Sango;

/// <summary>
/// 回合管理器 - 管理回合制战斗的状态机、执行计时器、冷却递减
/// </summary>
public class RoundManager
{
    public int CurrentRound { get; private set; }
    public bool HasPlayerActed { get; set; }
    public float ExecutionTimer { get; private set; }
    public float ExecutionDuration => GameSettings.SangoExecutionDuration;

    /// <summary>执行阶段进度 (0~1)</summary>
    public float ExecutionProgress => ExecutionDuration > 0
        ? 1f - ExecutionTimer / ExecutionDuration
        : 1f;

    // AI技能队列
    private readonly List<(GeneralUnit caster, int skillIndex)> _aiSkillQueue = new();
    public IReadOnlyList<(GeneralUnit caster, int skillIndex)> AISkillQueue => _aiSkillQueue;

    /// <summary>开始指令阶段 (回合+1)</summary>
    public void BeginCommandPhase()
    {
        CurrentRound++;
        HasPlayerActed = false;
        _aiSkillQueue.Clear();
    }

    /// <summary>首次进入回合制时调用 (不递增回合)</summary>
    public void BeginFirstRound()
    {
        CurrentRound = 1;
        HasPlayerActed = false;
        _aiSkillQueue.Clear();
    }

    /// <summary>开始执行阶段</summary>
    public void BeginExecutionPhase()
    {
        ExecutionTimer = ExecutionDuration;
    }

    /// <summary>更新执行阶段计时器，返回true表示执行阶段结束</summary>
    public bool UpdateExecution(float dt)
    {
        ExecutionTimer -= dt;
        return ExecutionTimer <= 0;
    }

    /// <summary>回合结束时递减所有武将技能冷却</summary>
    public void TickCooldowns(IEnumerable<GeneralUnit> allUnits)
    {
        foreach (var unit in allUnits)
        {
            foreach (var skill in unit.ResolvedSkills)
            {
                if (skill.CooldownRoundsLeft > 0)
                    skill.CooldownRoundsLeft--;
            }
        }
    }

    /// <summary>AI队列添加技能</summary>
    public void EnqueueAISkill(GeneralUnit caster, int skillIndex)
    {
        _aiSkillQueue.Add((caster, skillIndex));
    }

    /// <summary>清空AI技能队列</summary>
    public void ClearAIQueue()
    {
        _aiSkillQueue.Clear();
    }
}
