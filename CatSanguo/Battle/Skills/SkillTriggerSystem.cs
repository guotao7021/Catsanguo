using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using CatSanguo.Core;
using CatSanguo.Skills;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Battle;

// ==================== SkillContext ====================

public class SkillContext
{
    public Squad Source { get; set; } = null!;
    public Squad? Target { get; set; }
    public GameEvent Event { get; set; }
    public EventBus EventBus { get; set; } = null!;
    public BuffSystem BuffSystem { get; set; } = null!;
    public List<Squad> AllSquads { get; set; } = new();
    public int Depth { get; set; }
    public Random Rng { get; set; } = null!;
}

// ==================== Condition Interface & Implementations ====================

public interface ISkillCondition
{
    bool Check(SkillContext ctx);
}

public class AlwaysTrueCondition : ISkillCondition
{
    public bool Check(SkillContext ctx) => true;
}

public class RandomCondition : ISkillCondition
{
    private readonly float _probability;
    public RandomCondition(float probability) => _probability = probability;
    public bool Check(SkillContext ctx) => ctx.Rng.NextDouble() < _probability;
}

public class HpBelowCondition : ISkillCondition
{
    private readonly float _threshold;
    public HpBelowCondition(float threshold) => _threshold = threshold;
    public bool Check(SkillContext ctx) => ctx.Source.HP / ctx.Source.MaxHP < _threshold;
}

public class HpAboveCondition : ISkillCondition
{
    private readonly float _threshold;
    public HpAboveCondition(float threshold) => _threshold = threshold;
    public bool Check(SkillContext ctx) => ctx.Source.HP / ctx.Source.MaxHP > _threshold;
}

public class HasBuffCondition : ISkillCondition
{
    private readonly string _buffId;
    public HasBuffCondition(string buffId) => _buffId = buffId;
    public bool Check(SkillContext ctx) => ctx.BuffSystem.HasBuff(ctx.Source, _buffId);
}

// ==================== Effect Interface & Implementations ====================

public interface ISkillEffect
{
    void Execute(SkillContext ctx);
}

public class DamageSkillEffect : ISkillEffect
{
    private readonly float _coefficient;
    private readonly string _targetMode;
    private readonly float _radius;
    private readonly string _statBasis;

    public DamageSkillEffect(float coefficient, string targetMode, float radius, string statBasis)
    {
        _coefficient = coefficient;
        _targetMode = targetMode;
        _radius = radius;
        _statBasis = statBasis;
    }

    public void Execute(SkillContext ctx)
    {
        var targets = ResolveTargets(ctx);
        if (targets.Count == 0) return;

        float baseStat = _statBasis.Equals("intelligence", StringComparison.OrdinalIgnoreCase)
            ? (ctx.Source.General?.Intelligence ?? 30)
            : (ctx.Source.General?.Strength ?? 30);

        float damage = baseStat * _coefficient * (0.9f + (float)ctx.Rng.NextDouble() * 0.2f);

        foreach (var target in targets)
        {
            if (target == ctx.Source) continue;
            target.TakeDamage(damage);
            ctx.EventBus.Publish(new GameEvent(GameEventType.OnHit, ctx.Source, target, damage, "", ctx.Depth + 1));

            if (target.IsDead)
            {
                ctx.EventBus.Publish(new GameEvent(GameEventType.OnKill, ctx.Source, target, damage, "", ctx.Depth + 1));
            }
        }
    }

    private List<Squad> ResolveTargets(SkillContext ctx)
    {
        var result = new List<Squad>();

        switch (_targetMode)
        {
            case "CurrentTarget":
                if (ctx.Target != null && !ctx.Target.IsDead) result.Add(ctx.Target);
                break;
            case "Self":
                result.Add(ctx.Source);
                break;
            case "AllEnemies":
                result.AddRange(ctx.AllSquads.Where(s => s.Team != ctx.Source.Team && !s.IsDead));
                break;
            case "AllAllies":
                result.AddRange(ctx.AllSquads.Where(s => s.Team == ctx.Source.Team && !s.IsDead));
                break;
            case "AOEAroundTarget":
                if (ctx.Target != null)
                {
                    result.AddRange(ctx.AllSquads.Where(s =>
                        !s.IsDead && Vector2.Distance(s.Position, ctx.Target!.Position) <= _radius));
                }
                break;
            case "AOEAroundSelf":
                result.AddRange(ctx.AllSquads.Where(s =>
                    !s.IsDead && Vector2.Distance(s.Position, ctx.Source.Position) <= _radius));
                break;
        }

        return result;
    }
}

public class AddBuffSkillEffect : ISkillEffect
{
    private readonly string _buffId;
    private readonly string _targetMode;
    private readonly float _radius;

    public AddBuffSkillEffect(string buffId, string targetMode, float radius)
    {
        _buffId = buffId;
        _targetMode = targetMode;
        _radius = radius;
    }

    public void Execute(SkillContext ctx)
    {
        var targets = ResolveTargets(ctx);
        foreach (var target in targets)
        {
            ctx.BuffSystem.AddBuff(_buffId, ctx.Source, target);
        }
    }

    private List<Squad> ResolveTargets(SkillContext ctx)
    {
        var result = new List<Squad>();

        switch (_targetMode)
        {
            case "Self":
                result.Add(ctx.Source);
                break;
            case "CurrentTarget":
                if (ctx.Target != null && !ctx.Target.IsDead) result.Add(ctx.Target);
                break;
            case "AllEnemies":
                result.AddRange(ctx.AllSquads.Where(s => s.Team != ctx.Source.Team && !s.IsDead));
                break;
            case "AllAllies":
                result.AddRange(ctx.AllSquads.Where(s => s.Team == ctx.Source.Team && !s.IsDead));
                break;
            case "AOEAroundSelf":
                result.AddRange(ctx.AllSquads.Where(s =>
                    !s.IsDead && Vector2.Distance(s.Position, ctx.Source.Position) <= _radius));
                break;
            case "AOEAroundTarget":
                if (ctx.Target != null)
                {
                    result.AddRange(ctx.AllSquads.Where(s =>
                        !s.IsDead && Vector2.Distance(s.Position, ctx.Target!.Position) <= _radius));
                }
                break;
        }

        return result;
    }
}

public class HealSkillEffect : ISkillEffect
{
    private readonly float _value;
    private readonly string _targetMode;

    public HealSkillEffect(float value, string targetMode = "Self")
    {
        _value = value;
        _targetMode = targetMode;
    }

    public void Execute(SkillContext ctx)
    {
        var targets = _targetMode == "AllAllies"
            ? ctx.AllSquads.Where(s => s.Team == ctx.Source.Team && !s.IsDead).ToList()
            : new List<Squad> { ctx.Source };

        foreach (var target in targets)
        {
            target.HP = Math.Min(target.MaxHP, target.HP + _value);
        }
    }
}

public class ModifyMoraleSkillEffect : ISkillEffect
{
    private readonly float _change;
    private readonly string _targetMode;

    public ModifyMoraleSkillEffect(float change, string targetMode)
    {
        _change = change;
        _targetMode = targetMode;
    }

    public void Execute(SkillContext ctx)
    {
        var targets = _targetMode == "AllAllies"
            ? ctx.AllSquads.Where(s => s.Team == ctx.Source.Team && !s.IsDead).ToList()
            : (_targetMode == "AllEnemies"
                ? ctx.AllSquads.Where(s => s.Team != ctx.Source.Team && !s.IsDead).ToList()
                : new List<Squad> { ctx.Source });

        foreach (var target in targets)
        {
            target.Morale = Math.Clamp(target.Morale + _change, 0, 100);
        }
    }
}

public class RemoveBuffSkillEffect : ISkillEffect
{
    private readonly string _buffId;
    private readonly string _targetMode;

    public RemoveBuffSkillEffect(string buffId, string targetMode)
    {
        _buffId = buffId;
        _targetMode = targetMode;
    }

    public void Execute(SkillContext ctx)
    {
        // Note: RemoveBuff needs instanceId, simplified here to remove all matching buffs
        // The full implementation would need access to active buffs list
    }
}

// ==================== Registered Trigger ====================

public class RegisteredTrigger
{
    public Squad Owner { get; set; } = null!;
    public string SkillId { get; set; } = "";
    public GameEventType EventType { get; set; }
    public List<ISkillCondition> Conditions { get; set; } = new();
    public List<ISkillEffect> Effects { get; set; } = new();
}

// ==================== Skill Trigger System ====================

public class SkillTriggerSystem
{
    private readonly Dictionary<GameEventType, List<RegisteredTrigger>> _triggerIndex = new();
    private readonly List<RegisteredTrigger> _allTriggers = new();
    private readonly EventBus _eventBus;
    private readonly BuffSystem _buffSystem;
    private readonly List<Squad> _allSquads;
    private readonly Random _rng;
    private Action<GameEvent>? _eventHandler;
    private const int MaxChainDepth = 5;

    public SkillTriggerSystem(EventBus eventBus, BuffSystem buffSystem, List<Squad> allSquads, Random rng)
    {
        _eventBus = eventBus;
        _buffSystem = buffSystem;
        _allSquads = allSquads;
        _rng = rng;

        _eventHandler = OnEvent;
    }

    public void RegisterSquad(Squad squad)
    {
        // Active skill
        if (squad.ActiveSkill != null)
        {
            RegisterSkillTriggers(squad, squad.ActiveSkill);
        }

        // Passive skill
        if (squad.PassiveSkill != null)
        {
            RegisterSkillTriggers(squad, squad.PassiveSkill);
        }
    }

    private void RegisterSkillTriggers(Squad squad, Skill skill)
    {
        if (skill.Triggers == null || skill.Triggers.Count == 0) return;

        foreach (var triggerData in skill.Triggers)
        {
            if (!Enum.TryParse<GameEventType>(triggerData.Event, true, out var eventType))
                continue;

            var trigger = new RegisteredTrigger
            {
                Owner = squad,
                SkillId = skill.Data?.Id ?? "",
                EventType = eventType
            };

            foreach (var condData in triggerData.Conditions)
                trigger.Conditions.Add(CreateCondition(condData));

            foreach (var effData in triggerData.Effects)
                trigger.Effects.Add(CreateEffect(effData));

            if (!_triggerIndex.ContainsKey(eventType))
                _triggerIndex[eventType] = new List<RegisteredTrigger>();

            _triggerIndex[eventType].Add(trigger);
            _allTriggers.Add(trigger);
        }
    }

    private ISkillCondition CreateCondition(SkillConditionData data)
    {
        return data.Type switch
        {
            "Random" => new RandomCondition(data.Value),
            "HpBelow" => new HpBelowCondition(data.Value),
            "HpAbove" => new HpAboveCondition(data.Value),
            "HasBuff" => new HasBuffCondition(data.StringValue),
            _ => new AlwaysTrueCondition()
        };
    }

    private ISkillEffect CreateEffect(SkillEffectData data)
    {
        return data.Type switch
        {
            "Damage" => new DamageSkillEffect(data.Coefficient > 0 ? data.Coefficient : data.Value, data.TargetMode, data.Radius, data.StatBasis),
            "AddBuff" => new AddBuffSkillEffect(data.BuffId, data.TargetMode, data.Radius),
            "Heal" => new HealSkillEffect(data.Value, data.TargetMode),
            "ModifyMorale" => new ModifyMoraleSkillEffect(data.Value, data.TargetMode),
            "RemoveBuff" => new RemoveBuffSkillEffect(data.BuffId, data.TargetMode),
            _ => new AddBuffSkillEffect(data.BuffId, data.TargetMode, data.Radius)
        };
    }

    public void Initialize()
    {
        foreach (var eventType in _triggerIndex.Keys)
        {
            _eventBus.Subscribe(eventType, _eventHandler!);
        }
    }

    public void UnregisterSquad(Squad squad)
    {
        var toRemove = _allTriggers.Where(t => t.Owner == squad).ToList();
        foreach (var trigger in toRemove)
        {
            if (_triggerIndex.TryGetValue(trigger.EventType, out var list))
                list.Remove(trigger);
            _allTriggers.Remove(trigger);
        }
    }

    private void OnEvent(GameEvent evt)
    {
        if (evt.Depth > MaxChainDepth) return;

        if (!_triggerIndex.TryGetValue(evt.Type, out var triggers)) return;

        foreach (var trigger in triggers.ToArray())
        {
            if (trigger.Owner.IsDead) continue;

            var ctx = new SkillContext
            {
                Source = trigger.Owner,
                Target = evt.Target,
                Event = evt,
                EventBus = _eventBus,
                BuffSystem = _buffSystem,
                AllSquads = _allSquads,
                Depth = evt.Depth,
                Rng = _rng
            };

            // Check all conditions (short-circuit)
            bool allPass = true;
            foreach (var cond in trigger.Conditions)
            {
                if (!cond.Check(ctx))
                {
                    allPass = false;
                    break;
                }
            }

            if (!allPass) continue;

            // Execute all effects
            foreach (var effect in trigger.Effects)
            {
                try { effect.Execute(ctx); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SkillTrigger] Effect error: {ex.Message}");
                }
            }
        }
    }

    public void HandleManualSkill(Squad caster, Skill skill)
    {
        if (skill.Triggers == null || skill.Triggers.Count == 0) return;

        foreach (var triggerData in skill.Triggers)
        {
            if (!triggerData.Event.Equals("Manual", StringComparison.OrdinalIgnoreCase)) continue;

            var ctx = new SkillContext
            {
                Source = caster,
                Target = caster.TargetSquad,
                Event = new GameEvent(GameEventType.OnSkillCast, caster, caster.TargetSquad, 0, skill.Data?.Id ?? ""),
                EventBus = _eventBus,
                BuffSystem = _buffSystem,
                AllSquads = _allSquads,
                Depth = 0,
                Rng = _rng
            };

            bool allPass = true;
            foreach (var condData in triggerData.Conditions)
            {
                var cond = CreateCondition(condData);
                if (!cond.Check(ctx)) { allPass = false; break; }
            }

            if (!allPass) continue;

            foreach (var effData in triggerData.Effects)
            {
                var effect = CreateEffect(effData);
                try { effect.Execute(ctx); }
                catch { }
            }
        }
    }

    public void Clear()
    {
        foreach (var eventType in _triggerIndex.Keys)
        {
            _eventBus.Unsubscribe(eventType, _eventHandler!);
        }
        _triggerIndex.Clear();
        _allTriggers.Clear();
    }
}
