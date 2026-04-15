using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CatSanguo.Core;

namespace CatSanguo.Battle;

// ==================== Data Schemas ====================

public class BuffConfigData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public float Duration { get; set; }
    public int MaxStack { get; set; } = 1;
    public string StackMode { get; set; } = "Stack";
    public float TickInterval { get; set; }
    public bool IsDebuff { get; set; }
    public List<BuffEffectData> Effects { get; set; } = new();
}

public class BuffEffectData
{
    public string Type { get; set; } = "";
    public string AttrType { get; set; } = "";
    public string ModifierType { get; set; } = "Multiply";
    public float Value { get; set; }
    public string TargetBuffId { get; set; } = "";
    public float Probability { get; set; }
}

// ==================== Buff Instance ====================

public class BuffInstance
{
    public string BuffId { get; set; }
    public string InstanceId { get; set; }
    public Squad? Caster { get; set; }
    public Squad Target { get; set; } = null!;
    public float Duration { get; set; }
    public float Elapsed { get; set; }
    public float TickTimer { get; set; }
    public int StackCount { get; set; } = 1;
    public BuffConfigData Config { get; set; } = null!;
    public bool PendingRemoval { get; set; }

    public bool IsExpired => Duration > 0 && Elapsed >= Duration;
}

// ==================== Buff Effect Interface & Implementations ====================

public interface IBuffEffect
{
    void OnAdd(BuffInstance buff, Squad target, Squad? caster);
    void OnRemove(BuffInstance buff, Squad target);
    void OnTick(BuffInstance buff, Squad target, Squad? caster);
    void OnEvent(BuffInstance buff, Squad target, Squad? caster, GameEvent evt);
}

public class ModifyAttributeEffect : IBuffEffect
{
    private readonly AttrType _attr;
    private readonly ModifierOp _op;
    private readonly float _value;

    public ModifyAttributeEffect(string attrType, string modifierType, float value)
    {
        _attr = Enum.TryParse<AttrType>(attrType, true, out var a) ? a : AttrType.Attack;
        _op = modifierType.Equals("Add", StringComparison.OrdinalIgnoreCase) ? ModifierOp.Add : ModifierOp.Multiply;
        _value = value;
    }

    public void OnAdd(BuffInstance buff, Squad target, Squad? caster)
    {
        var mod = new Modifier(buff.InstanceId, _attr, _op, _value * buff.StackCount);
        target.Attributes.AddModifier(mod);
    }

    public void OnRemove(BuffInstance buff, Squad target)
    {
        target.Attributes.RemoveModifiersBySource(buff.InstanceId);
    }

    public void OnTick(BuffInstance buff, Squad target, Squad? caster)
    {
        // Refresh modifier with current stack count
        target.Attributes.RemoveModifiersBySource(buff.InstanceId);
        var mod = new Modifier(buff.InstanceId, _attr, _op, _value * buff.StackCount);
        target.Attributes.AddModifier(mod);
    }

    public void OnEvent(BuffInstance buff, Squad target, Squad? caster, GameEvent evt) { }
}

public class DamageOverTimeEffect : IBuffEffect
{
    private readonly float _damage;
    private readonly EventBus _eventBus;

    public DamageOverTimeEffect(float damage, EventBus eventBus)
    {
        _damage = damage;
        _eventBus = eventBus;
    }

    public void OnAdd(BuffInstance buff, Squad target, Squad? caster) { }

    public void OnRemove(BuffInstance buff, Squad target) { }

    public void OnTick(BuffInstance buff, Squad target, Squad? caster)
    {
        float dmg = _damage * buff.StackCount;
        target.HP = Math.Max(0, target.HP - dmg);
        _eventBus.Publish(new GameEvent(GameEventType.OnDamaged, target, null, dmg, buff.BuffId));
    }

    public void OnEvent(BuffInstance buff, Squad target, Squad? caster, GameEvent evt) { }
}

public class HealOverTimeEffect : IBuffEffect
{
    private readonly float _heal;

    public HealOverTimeEffect(float heal)
    {
        _heal = heal;
    }

    public void OnAdd(BuffInstance buff, Squad target, Squad? caster) { }

    public void OnRemove(BuffInstance buff, Squad target) { }

    public void OnTick(BuffInstance buff, Squad target, Squad? caster)
    {
        float heal = _heal * buff.StackCount;
        target.HP = Math.Min(target.MaxHP, target.HP + heal);
    }

    public void OnEvent(BuffInstance buff, Squad target, Squad? caster, GameEvent evt) { }
}

public class StunEffect : IBuffEffect
{
    public void OnAdd(BuffInstance buff, Squad target, Squad? caster)
    {
        target.IsStunned = true;
        if (target.State == SquadState.Idle || target.State == SquadState.Engaging)
        {
            // Keep current position when stunned
        }
    }

    public void OnRemove(BuffInstance buff, Squad target)
    {
        target.IsStunned = false;
    }

    public void OnTick(BuffInstance buff, Squad target, Squad? caster) { }
    public void OnEvent(BuffInstance buff, Squad target, Squad? caster, GameEvent evt) { }
}

public class SilenceEffect : IBuffEffect
{
    public void OnAdd(BuffInstance buff, Squad target, Squad? caster)
    {
        target.IsSilenced = true;
    }

    public void OnRemove(BuffInstance buff, Squad target)
    {
        target.IsSilenced = false;
    }

    public void OnTick(BuffInstance buff, Squad target, Squad? caster) { }
    public void OnEvent(BuffInstance buff, Squad target, Squad? caster, GameEvent evt) { }
}

public class OnHitApplyBuffEffect : IBuffEffect
{
    private readonly string _buffId;
    private readonly float _probability;
    private readonly BuffSystem _buffSystem;

    public OnHitApplyBuffEffect(string buffId, float probability, BuffSystem buffSystem)
    {
        _buffId = buffId;
        _probability = probability;
        _buffSystem = buffSystem;
    }

    public void OnAdd(BuffInstance buff, Squad target, Squad? caster) { }
    public void OnRemove(BuffInstance buff, Squad target) { }
    public void OnTick(BuffInstance buff, Squad target, Squad? caster) { }

    public void OnEvent(BuffInstance buff, Squad target, Squad? caster, GameEvent evt)
    {
        if (evt.Type == GameEventType.OnHit && evt.Source == target)
        {
            if (Random.Shared.NextDouble() < _probability && evt.Target != null)
            {
                _buffSystem.AddBuff(_buffId, target, evt.Target);
            }
        }
    }
}

// ==================== Buff System ====================

public class BuffSystem
{
    private readonly List<BuffInstance> _activeBuffs = new();
    private readonly Dictionary<string, BuffConfigData> _configs = new();
    private readonly EventBus _eventBus;
    private readonly Dictionary<string, List<IBuffEffect>> _effectCache = new();
    private int _instanceCounter;

    public BuffSystem(EventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void LoadConfigs(string dataPath)
    {
        if (!File.Exists(dataPath)) return;

        var json = File.ReadAllText(dataPath);
        var configs = JsonSerializer.Deserialize<List<BuffConfigData>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (configs == null) return;
        foreach (var cfg in configs)
            _configs[cfg.Id] = cfg;
    }

    public void AddBuff(string buffId, Squad caster, Squad target)
    {
        if (!_configs.TryGetValue(buffId, out var config)) return;

        // Find existing buff on target
        var existing = _activeBuffs.FirstOrDefault(b => b.BuffId == buffId && b.Target == target);

        if (existing != null)
        {
            switch (config.StackMode)
            {
                case "Refresh":
                    existing.Elapsed = 0;
                    break;
                case "Stack":
                    if (existing.StackCount < config.MaxStack)
                        existing.StackCount++;
                    else
                        existing.Elapsed = 0;
                    break;
                case "Replace":
                    RemoveBuff(existing.InstanceId);
                    CreateNewBuff(config, caster, target);
                    _eventBus.Publish(new GameEvent(GameEventType.OnBuffAdded, caster, target, 0, buffId));
                    return;
            }
            // Refresh: just reset elapsed
            _eventBus.Publish(new GameEvent(GameEventType.OnBuffAdded, caster, target, 0, buffId));
        }
        else
        {
            CreateNewBuff(config, caster, target);
            _eventBus.Publish(new GameEvent(GameEventType.OnBuffAdded, caster, target, 0, buffId));
        }
    }

    private void CreateNewBuff(BuffConfigData config, Squad caster, Squad target)
    {
        var instance = new BuffInstance
        {
            BuffId = config.Id,
            InstanceId = $"{config.Id}_{_instanceCounter++}",
            Caster = caster,
            Target = target,
            Duration = config.Duration,
            Elapsed = 0,
            TickTimer = 0,
            StackCount = 1,
            Config = config
        };

        var effects = CreateEffects(config, target);
        instance.Config = config;

        _activeBuffs.Add(instance);

        // Call OnAdd for each effect
        foreach (var effect in effects)
            effect.OnAdd(instance, target, caster);
    }

    private List<IBuffEffect> CreateEffects(BuffConfigData config, Squad target)
    {
        var key = $"{config.Id}_{target.GetHashCode()}";
        if (_effectCache.TryGetValue(key, out var cached))
            return cached;

        var effects = new List<IBuffEffect>();
        foreach (var effectData in config.Effects)
        {
            IBuffEffect effect = effectData.Type switch
            {
                "ModifyAttribute" => new ModifyAttributeEffect(effectData.AttrType, effectData.ModifierType, effectData.Value),
                "DamageOverTime" => new DamageOverTimeEffect(effectData.Value, _eventBus),
                "HealOverTime" => new HealOverTimeEffect(effectData.Value),
                "Stun" => new StunEffect(),
                "Silence" => new SilenceEffect(),
                "OnHitApplyBuff" => new OnHitApplyBuffEffect(effectData.TargetBuffId, effectData.Probability, this),
                _ => new ModifyAttributeEffect("Attack", "Multiply", 0)
            };
            effects.Add(effect);
        }

        _effectCache[key] = effects;
        return effects;
    }

    public void RemoveBuff(string instanceId)
    {
        var buff = _activeBuffs.FirstOrDefault(b => b.InstanceId == instanceId);
        if (buff == null) return;

        // Call OnRemove for each effect
        var effects = _activeBuffs.Where(b => b.InstanceId == instanceId).SelectMany(b =>
            CreateEffects(b.Config, b.Target)).ToList();

        foreach (var effect in effects)
            effect.OnRemove(buff, buff.Target);

        _activeBuffs.Remove(buff);
        _eventBus.Publish(new GameEvent(GameEventType.OnBuffRemoved, buff.Caster, buff.Target, 0, buff.BuffId));
    }

    public void Update(float dt)
    {
        foreach (var buff in _activeBuffs)
        {
            if (buff.PendingRemoval) continue;

            buff.Elapsed += dt;
            if (buff.Config.TickInterval > 0)
            {
                buff.TickTimer += dt;
                if (buff.TickTimer >= buff.Config.TickInterval)
                {
                    var effects = CreateEffects(buff.Config, buff.Target);
                    foreach (var effect in effects)
                        effect.OnTick(buff, buff.Target, buff.Caster);
                    buff.TickTimer = 0;
                }
            }

            if (buff.IsExpired)
            {
                buff.PendingRemoval = true;
                var effects = CreateEffects(buff.Config, buff.Target);
                foreach (var effect in effects)
                    effect.OnRemove(buff, buff.Target);
            }
        }

        // Remove pending
        _activeBuffs.RemoveAll(b => b.PendingRemoval);
    }

    public List<BuffInstance> GetBuffsOn(Squad target)
    {
        return _activeBuffs.Where(b => b.Target == target).ToList();
    }

    public bool HasBuff(Squad target, string buffId)
    {
        return _activeBuffs.Any(b => b.BuffId == buffId && b.Target == target);
    }

    public void Clear()
    {
        foreach (var buff in _activeBuffs.ToArray())
            RemoveBuff(buff.InstanceId);
        _activeBuffs.Clear();
        _effectCache.Clear();
    }

    public void OnGameEvent(GameEvent evt)
    {
        foreach (var buff in _activeBuffs)
        {
            var effects = CreateEffects(buff.Config, buff.Target);
            foreach (var effect in effects)
                effect.OnEvent(buff, buff.Target, buff.Caster, evt);
        }
    }
}
