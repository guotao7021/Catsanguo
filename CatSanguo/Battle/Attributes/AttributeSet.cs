using System;
using System.Collections.Generic;
using System.Linq;

namespace CatSanguo.Battle;

public enum AttrType
{
    MaxHP,
    Attack,
    Defense,
    Speed,
    CritRate,
    CritDamage,
    AttackRange
}

public enum ModifierOp
{
    Add,
    Multiply,
    Override
}

public struct Modifier
{
    public string SourceId;
    public AttrType Attr;
    public ModifierOp Op;
    public float Value;
    public int Priority;

    public Modifier(string sourceId, AttrType attr, ModifierOp op, float value, int priority = 0)
    {
        SourceId = sourceId;
        Attr = attr;
        Op = op;
        Value = value;
        Priority = priority;
    }
}

public class AttributeSet
{
    private readonly Dictionary<AttrType, float> _baseValues = new();
    private readonly List<Modifier> _modifiers = new();
    private readonly Dictionary<AttrType, float> _finalValues = new();
    private bool _dirty = true;

    public AttributeSet()
    {
        foreach (AttrType attr in Enum.GetValues(typeof(AttrType)))
            _baseValues[attr] = 0f;
    }

    public void SetBase(AttrType attr, float value)
    {
        _baseValues[attr] = value;
        _dirty = true;
    }

    public float GetBase(AttrType attr)
    {
        return _baseValues.TryGetValue(attr, out var v) ? v : 0f;
    }

    public void AddModifier(Modifier mod)
    {
        _modifiers.Add(mod);
        _dirty = true;
    }

    public void RemoveModifiersBySource(string sourceId)
    {
        _modifiers.RemoveAll(m => m.SourceId == sourceId);
        _dirty = true;
    }

    public float GetValue(AttrType attr)
    {
        if (_dirty) Recalculate();
        return _finalValues.TryGetValue(attr, out var v) ? v : 0f;
    }

    public void Recalculate()
    {
        foreach (AttrType attr in Enum.GetValues(typeof(AttrType)))
        {
            float @base = _baseValues.TryGetValue(attr, out var b) ? b : 0f;
            float addSum = 0f;
            float multSum = 0f;

            var relevantMods = _modifiers
                .Where(m => m.Attr == attr)
                .OrderBy(m => m.Priority);

            float? overrideValue = null;
            int overridePriority = -1;

            foreach (var mod in relevantMods)
            {
                switch (mod.Op)
                {
                    case ModifierOp.Add:
                        addSum += mod.Value;
                        break;
                    case ModifierOp.Multiply:
                        multSum += mod.Value;
                        break;
                    case ModifierOp.Override:
                        if (mod.Priority >= overridePriority)
                        {
                            overrideValue = mod.Value;
                            overridePriority = mod.Priority;
                        }
                        break;
                }
            }

            float final;
            if (overrideValue.HasValue)
                final = overrideValue.Value;
            else
                final = (@base + addSum) * (1f + multSum);

            _finalValues[attr] = final;
        }

        _dirty = false;
    }

    public void Clear()
    {
        _modifiers.Clear();
        _dirty = true;
    }

    public void MarkDirty() => _dirty = true;
}
