using System;
using System.Collections.Generic;
using System.Linq;
using CatSanguo.Battle;

namespace CatSanguo.AI;

/// <summary>
/// AI姿态 - 决定AI的整体行为倾向
/// </summary>
public enum AIPosture
{
    /// <summary>激进：优先攻击，追击残血，积极使用技能</summary>
    Aggressive,
    /// <summary>防守：优先自保，保持阵型，节省技能</summary>
    Defensive,
    /// <summary>均衡：根据态势动态调整</summary>
    Balanced
}

/// <summary>
/// AI指令 - 行为树执行的具体命令
/// </summary>
public class AICommand
{
    public AICommandType Type { get; set; }
    public Squad? Target { get; set; }
    public List<Squad>? SkillTargets { get; set; }
    public AIPosture Posture { get; set; } = AIPosture.Balanced;

    public enum AICommandType
    {
        Attack,
        UseSkill,
        Retreat,
        HoldPosition,
        Flee
    }
}

/// <summary>
/// AI指挥官，使用行为树+效用AI进行高级决策。
/// 替代原有的简单启发式AI，提供战略规划能力。
/// </summary>
public class AICommander
{
    private readonly BattleBlackboard _blackboard = new();
    private readonly BTNode _behaviorTree;
    private readonly Team _team;
    private readonly int _difficulty;
    private float _thinkTimer;

    // 难度配置
    private readonly float _thinkInterval;
    private readonly float _utilityThreshold;
    private readonly bool _enableCoordination;

    public AIPosture CurrentPosture { get; private set; } = AIPosture.Balanced;

    public AICommander(Team team, int difficulty = 1)
    {
        _team = team;
        _difficulty = difficulty;

        // 难度影响思考频率和协调能力
        (_thinkInterval, _utilityThreshold, _enableCoordination) = difficulty switch
        {
            1 => (2.0f, 60f, false),   // 简单：慢思考，高阈值，无协作
            2 => (1.0f, 40f, true),    // 普通：中等
            _ => (0.5f, 20f, true),    // 困难：快思考，低阈值，有协作
        };

        // 构建行为树
        _behaviorTree = BuildBehaviorTree();
    }

    /// <summary>
    /// 构建行为树
    /// </summary>
    private BTNode BuildBehaviorTree()
    {
        // 根节点：选择器（按优先级尝试不同策略）
        return new SelectorNode(
            // 1. 紧急撤退（血量极低且被集火）
            new SequenceNode(
                new ConditionNode((bb, squad) => UtilityScorer.EvaluateRetreatNeed(squad, bb) > 60f),
                new ActionNode((bb, squad) => ExecuteRetreat(squad))
            ),

            // 2. 技能释放（效用评分足够）
            new SequenceNode(
                new ConditionNode((bb, squad) => squad.ActiveSkill != null && squad.ActiveSkill.IsReady),
                new ConditionNode((bb, squad) =>
                    UtilityScorer.EvaluateSkillUse(squad, squad.ActiveSkill!, bb) > _utilityThreshold),
                new ActionNode((bb, squad) => ExecuteSkillUse(squad, bb))
            ),

            // 3. 选择目标攻击
            new SequenceNode(
                new ConditionNode((bb, squad) => squad.State != SquadState.UsingSkill),
                new ActionNode((bb, squad) => ExecuteAttack(squad, bb))
            ),

            // 4. 默认：待机
            new ActionNode((bb, squad) => ExecuteHold(squad))
        );
    }

    /// <summary>
    /// 更新AI决策
    /// </summary>
    public void Update(float deltaTime, List<Squad> allSquads)
    {
        _thinkTimer += deltaTime;
        if (_thinkTimer < _thinkInterval) return;
        _thinkTimer = 0;

        // 刷新黑板
        _blackboard.Refresh(allSquads, _team);

        // 更新全局姿态
        CurrentPosture = UtilityScorer.EvaluateOptimalPosture(
            _blackboard.PlayerSquads.FirstOrDefault() ?? new Squad(),
            _blackboard
        );

        // 遍历所有己方武将执行行为树
        var mySquads = _blackboard.PlayerSquads;
        foreach (var squad in mySquads)
        {
            // 执行行为树
            _behaviorTree.Execute(_blackboard, squad);

            // 困难难度：协调集火
            if (_enableCoordination)
            {
                CoordinateFocusFire(squad);
            }
        }
    }

    private void ExecuteRetreat(Squad squad)
    {
        // 寻找最近的安全位置（远离敌人，靠近队友）
        var nearestEnemy = _blackboard.GetNearestThreat(squad);
        if (nearestEnemy == null) return;

        // 撤退方向：远离最近敌人
        var retreatDir = squad.Position - nearestEnemy.Position;
        retreatDir = Microsoft.Xna.Framework.Vector2.Normalize(retreatDir);

        // 移动到安全距离
        var targetPos = squad.Position + retreatDir * 200f;

        // 临时设置一个假目标让武将移动
        squad.TargetSquad = null;
        squad.State = SquadState.Fleeing;
    }

    private void ExecuteSkillUse(Squad squad, BattleBlackboard blackboard)
    {
        var targets = UtilityScorer.SelectBestSkillTargets(squad, blackboard);
        if (targets != null && targets.Count > 0)
        {
            squad.UseSkill(targets);
        }
    }

    private void ExecuteAttack(Squad squad, BattleBlackboard blackboard)
    {
        var target = UtilityScorer.SelectBestMoveTarget(squad, blackboard);
        if (target != null)
        {
            squad.TargetSquad = target;

            // 根据姿态调整攻击策略
            if (CurrentPosture == AIPosture.Aggressive && squad.ActiveSkill?.IsReady == true)
            {
                // 激进姿态：更积极使用技能
                if (UtilityScorer.EvaluateSkillUse(squad, squad.ActiveSkill, blackboard) > _utilityThreshold * 0.7f)
                {
                    ExecuteSkillUse(squad, blackboard);
                }
            }
        }
    }

    private void ExecuteHold(Squad squad)
    {
        // 待机状态：保持当前位置
        if (squad.State == SquadState.Engaging)
        {
            // 如果正在交战，保持当前目标
            return;
        }

        squad.State = SquadState.Idle;
    }

    private void CoordinateFocusFire(Squad squad)
    {
        // 寻找被最多队友瞄准的敌人
        var targetCounts = new Dictionary<Squad, int>();

        foreach (var ally in _blackboard.PlayerSquads)
        {
            if (ally.TargetSquad != null && ally != squad)
            {
                if (!targetCounts.ContainsKey(ally.TargetSquad))
                    targetCounts[ally.TargetSquad] = 0;
                targetCounts[ally.TargetSquad]++;
            }
        }

        if (targetCounts.Count > 0)
        {
            var focusTarget = targetCounts.OrderByDescending(x => x.Value).First().Key;
            // 如果当前没有目标，加入集火
            if (squad.TargetSquad == null)
            {
                squad.TargetSquad = focusTarget;
            }
        }
    }
}
