using System;
using System.Collections.Generic;

namespace CatSanguo.Core;

public enum GameEventType
{
    // 战斗事件
    OnAttack,
    OnHit,
    OnKill,
    OnDamaged,
    OnBuffAdded,
    OnBuffRemoved,
    OnSkillCast,
    OnBattleStart,
    OnMoraleBreak,
    
    // 撤退/俘获事件（新）
    OnRetreat,   // 武将撤退（替代原阵亡）
    OnCapture,   // 武将被俘获
    
    // 回合制事件（新）
    OnTurnEnd,      // 回合结束
    OnMonthEnd,     // 月末（经济迭代）
    OnQuarterEnd    // 季末（兵力迭代）
}

public struct GameEvent
{
    public GameEventType Type;
    public Battle.Squad? Source;
    public Battle.Squad? Target;
    public float Value;
    public string Tag;
    public int Depth;

    public GameEvent(GameEventType type, Battle.Squad? source = null, Battle.Squad? target = null,
        float value = 0f, string tag = "", int depth = 0)
    {
        Type = type;
        Source = source;
        Target = target;
        Value = value;
        Tag = tag;
        Depth = depth;
    }
}

public class EventBus
{
    private readonly Dictionary<GameEventType, List<Action<GameEvent>>> _handlers = new();
    private const int MaxChainDepth = 5;

    public void Subscribe(GameEventType type, Action<GameEvent> handler)
    {
        if (!_handlers.ContainsKey(type))
            _handlers[type] = new List<Action<GameEvent>>();
        if (!_handlers[type].Contains(handler))
            _handlers[type].Add(handler);
    }

    public void Unsubscribe(GameEventType type, Action<GameEvent> handler)
    {
        if (_handlers.TryGetValue(type, out var list))
            list.Remove(handler);
    }

    public void Publish(GameEvent evt)
    {
        if (evt.Depth > MaxChainDepth) return;

        if (_handlers.TryGetValue(evt.Type, out var list))
        {
            foreach (var handler in list.ToArray())
            {
                try { handler(evt); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EventBus] Handler error: {ex.Message}");
                }
            }
        }
    }

    public void Clear()
    {
        _handlers.Clear();
    }
}
