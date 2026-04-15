using System;
using System.Collections.Generic;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Skills;

public enum SkillTargetMode
{
    SingleTarget,
    AOE_Circle,
    AOE_Line,
    Self
}

public class Skill
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; }
    public SkillTargetMode TargetMode { get; set; }
    public float Coefficient { get; set; } = 1f;
    public float Radius { get; set; }
    public float Cooldown { get; set; }
    public float CastTime { get; set; }
    public string EffectType { get; set; } = "damage";
    public string StatBasis { get; set; } = "strength";
    public string BuffStat { get; set; } = "";
    public float BuffPercent { get; set; }
    public float BuffDuration { get; set; }
    public float MoraleChange { get; set; }

    // 技能等级系统
    public int Level { get; set; } = 1;
    public int MaxLevel { get; set; } = 10;
    public int Xp { get; set; } = 0;
    public int XpToNextLevel => Level * 80;
    public bool CanLevelUp => Level < MaxLevel && Xp >= XpToNextLevel;

    // 有效系数和冷却 (受等级影响)
    public float EffectiveCoefficient => Coefficient + (Level - 1) * CoefficientIncreasePerLevel;
    public float EffectiveCooldown => Math.Max(1.0f, Cooldown - (Level - 1) * CooldownReductionPerLevel);

    // 从 SkillData 获取的等级相关属性
    private float CoefficientIncreasePerLevel { get; set; } = 0.1f;
    private float CooldownReductionPerLevel { get; set; } = 0.5f;
    private int LevelUpCost { get; set; } = 50;

    // 技能触发链系统
    public List<SkillTriggerData>? Triggers { get; set; }
    public SkillData? Data { get; set; }  // 引用原始数据，用于SkillTriggerSystem

    // Runtime state
    public float CurrentCooldown { get; set; }
    public bool IsReady => CurrentCooldown <= 0;

    public void Update(float deltaTime)
    {
        if (CurrentCooldown > 0)
            CurrentCooldown -= deltaTime;
    }

    public void Activate()
    {
        CurrentCooldown = EffectiveCooldown;
    }
    
    public void AddXp(int amount)
    {
        Xp += amount;
        while (CanLevelUp)
        {
            Xp -= XpToNextLevel;
            Level++;
        }
    }

    public static Skill FromData(SkillData data, int level = 1)
    {
        SkillTargetMode mode = data.TargetMode switch
        {
            "AOE_Circle" => SkillTargetMode.AOE_Circle,
            "AOE_Line" => SkillTargetMode.AOE_Line,
            "Self" => SkillTargetMode.Self,
            _ => SkillTargetMode.SingleTarget
        };

        return new Skill
        {
            Id = data.Id,
            Name = data.Name,
            Description = data.Description,
            IsActive = data.Type == "active",
            TargetMode = mode,
            Coefficient = data.Coefficient,
            Radius = data.Radius,
            Cooldown = data.Cooldown,
            CastTime = data.CastTime,
            EffectType = data.EffectType,
            StatBasis = data.StatBasis,
            BuffStat = data.BuffStat,
            BuffPercent = data.BuffPercent,
            BuffDuration = data.BuffDuration,
            MoraleChange = data.MoraleChange,
            Triggers = data.Triggers,
            Data = data,
            Level = level
        };
    }
}
