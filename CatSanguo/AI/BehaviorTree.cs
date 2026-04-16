using System;
using System.Collections.Generic;
using System.Linq;
using CatSanguo.Battle;

namespace CatSanguo.AI;

/// <summary>
/// 行为树节点状态
/// </summary>
public enum NodeStatus
{
    Success,
    Failure,
    Running
}

/// <summary>
/// 行为树节点基类
/// </summary>
public abstract class BTNode
{
    public string Name { get; set; } = "";

    public abstract NodeStatus Execute(BattleBlackboard blackboard, Squad squad);
}

/// <summary>
/// 顺序组合节点 - 按顺序执行子节点，直到有一个失败或全部成功
/// </summary>
public class SequenceNode : BTNode
{
    private readonly List<BTNode> _children = new();

    public SequenceNode(params BTNode[] children)
    {
        _children.AddRange(children);
    }

    public override NodeStatus Execute(BattleBlackboard blackboard, Squad squad)
    {
        foreach (var child in _children)
        {
            var status = child.Execute(blackboard, squad);
            if (status != NodeStatus.Success)
                return status;
        }
        return NodeStatus.Success;
    }
}

/// <summary>
/// 选择组合节点 - 按顺序执行子节点，直到有一个成功或全部失败
/// </summary>
public class SelectorNode : BTNode
{
    private readonly List<BTNode> _children = new();

    public SelectorNode(params BTNode[] children)
    {
        _children.AddRange(children);
    }

    public override NodeStatus Execute(BattleBlackboard blackboard, Squad squad)
    {
        foreach (var child in _children)
        {
            var status = child.Execute(blackboard, squad);
            if (status != NodeStatus.Failure)
                return status;
        }
        return NodeStatus.Failure;
    }
}

/// <summary>
/// 条件节点 - 检查条件是否满足
/// </summary>
public class ConditionNode : BTNode
{
    private readonly Func<BattleBlackboard, Squad, bool> _condition;

    public ConditionNode(Func<BattleBlackboard, Squad, bool> condition)
    {
        _condition = condition;
    }

    public override NodeStatus Execute(BattleBlackboard blackboard, Squad squad)
    {
        return _condition(blackboard, squad) ? NodeStatus.Success : NodeStatus.Failure;
    }
}

/// <summary>
/// 行动节点 - 执行具体行为
/// </summary>
public class ActionNode : BTNode
{
    private readonly Action<BattleBlackboard, Squad> _action;

    public ActionNode(Action<BattleBlackboard, Squad> action)
    {
        _action = action;
    }

    public override NodeStatus Execute(BattleBlackboard blackboard, Squad squad)
    {
        _action(blackboard, squad);
        return NodeStatus.Success;
    }
}

/// <summary>
/// 装饰器节点 - 包装单个子节点，添加额外逻辑
/// </summary>
public abstract class DecoratorNode : BTNode
{
    protected BTNode Child { get; }

    protected DecoratorNode(BTNode child)
    {
        Child = child;
    }
}

/// <summary>
/// 反相器 - 反转子节点的结果
/// </summary>
public class InverterNode : DecoratorNode
{
    public InverterNode(BTNode child) : base(child) { }

    public override NodeStatus Execute(BattleBlackboard blackboard, Squad squad)
    {
        var status = Child.Execute(blackboard, squad);
        return status switch
        {
            NodeStatus.Success => NodeStatus.Failure,
            NodeStatus.Failure => NodeStatus.Success,
            _ => status
        };
    }
}

/// <summary>
/// 重复执行器 - 重复执行子节点指定次数
/// </summary>
public class RepeaterNode : DecoratorNode
{
    private readonly int _maxIterations;
    private int _currentIteration;

    public RepeaterNode(BTNode child, int maxIterations = -1) : base(child)
    {
        _maxIterations = maxIterations;
    }

    public override NodeStatus Execute(BattleBlackboard blackboard, Squad squad)
    {
        _currentIteration = 0;
        while (_maxIterations < 0 || _currentIteration < _maxIterations)
        {
            var status = Child.Execute(blackboard, squad);
            if (status == NodeStatus.Failure)
                return NodeStatus.Failure;
            _currentIteration++;
        }
        return NodeStatus.Success;
    }
}
