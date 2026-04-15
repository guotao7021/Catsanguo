using System;
using System.Collections.Generic;

namespace CatSanguo.Core;

public enum GameEventType
{
    OnAttack,
    OnHit,
    OnKill,
    OnDamaged,
    OnBuffAdded,
    OnBuffRemoved,
    OnSkillCast,
    OnBattleStart,
    OnMoraleBreak
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
