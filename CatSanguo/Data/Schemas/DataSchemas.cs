using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace CatSanguo.Data.Schemas;

// ==================== 武将状态枚举 ====================
public enum GeneralStatus
{
    Recruited,  // 已招募 - 玩家麾下武将，可编入队伍出征
    Available,  // 在野 - 已发现但未招募的武将，可说服
    Captive     // 俘虏 - 战败被俘的武将，可招降
}

public class GeneralData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public int Strength { get; set; }
    public int Intelligence { get; set; }
    public int Leadership { get; set; }
    public int Speed { get; set; }
    public string ActiveSkillId { get; set; } = "";
    public string PassiveSkillId { get; set; } = "";
    public string PreferredFormation { get; set; } = "vanguard";
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
}

public class SkillData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "active"; // active or passive
    public string TargetMode { get; set; } = "SingleTarget";
    public float Coefficient { get; set; } = 1f;
    public float Radius { get; set; }
    public float Cooldown { get; set; } = 10f;
    public float CastTime { get; set; } = 0.5f;
    public string EffectType { get; set; } = "damage"; // damage, buff, morale, heal
    public string StatBasis { get; set; } = "strength"; // strength or intelligence
    public string BuffStat { get; set; } = "";
    public float BuffPercent { get; set; }
    public float BuffDuration { get; set; }
    public float MoraleChange { get; set; }
    
    // 技能等级系统
    public int MaxLevel { get; set; } = 10;
    public int LevelUpCost { get; set; } = 50;
    public float CooldownReductionPerLevel { get; set; } = 0.5f;
    public float CoefficientIncreasePerLevel { get; set; } = 0.1f;

    // 技能触发链系统 (用于新技能解析)
    public List<SkillTriggerData>? Triggers { get; set; }
}

public class SkillTriggerData
{
    public string Event { get; set; } = "";
    public List<SkillConditionData> Conditions { get; set; } = new();
    public List<SkillEffectData> Effects { get; set; } = new();
}

public class SkillConditionData
{
    public string Type { get; set; } = "";
    public float Value { get; set; }
    public string StringValue { get; set; } = "";
}

public class SkillEffectData
{
    public string Type { get; set; } = "";
    public float Value { get; set; }
    public float Coefficient { get; set; }
    public string BuffId { get; set; } = "";
    public string TargetMode { get; set; } = "CurrentTarget";
    public float Radius { get; set; }
    public string StatBasis { get; set; } = "strength";
}

public class StageData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Difficulty { get; set; } = 1;
    public int PlayerSlots { get; set; } = 2;
    public List<StageSquadData> EnemySquads { get; set; } = new();
    public List<string> PlayerGeneralPool { get; set; } = new();
    public List<string> UnlockReward { get; set; } = new();
    public string ConnectedCityId { get; set; } = "";
}

public class StageSquadData
{
    public string GeneralId { get; set; } = "";
    public string FormationType { get; set; } = "vanguard";
    public int SoldierCount { get; set; } = 30;
    public float PositionX { get; set; }
    public float PositionY { get; set; }
}

public class FormationData
{
    public string Type { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public float BaseHP { get; set; } = 500;
    public float BaseAttack { get; set; } = 30;
    public float BaseDefense { get; set; } = 30;
    public float BaseSpeed { get; set; } = 1f;
    public float AttackRange { get; set; } = 40;
    public float DefenseModifier { get; set; } = 1f;
    public float AttackModifier { get; set; } = 1f;
    public float SpeedModifier { get; set; } = 1f;
    public string LayoutPattern { get; set; } = "grid_3x3";
}

public class CityData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int GridX { get; set; }
    public int GridY { get; set; }
    public string Owner { get; set; } = "neutral"; // player, enemy_wei, enemy_wu, neutral
    public string CityType { get; set; } = "city"; // "city", "pass", "port"
    public string CityScale { get; set; } = "medium"; // "small", "medium", "large", "huge"
    public List<StageSquadData> Garrison { get; set; } = new();
    public string ConnectedStageId { get; set; } = "";
    public List<string> UnlockReward { get; set; } = new();
    public List<string> RequiredCityIds { get; set; } = new();

    // 城市连接 (道路网络)
    public List<string> ConnectedCityIds { get; set; } = new();

    // 城防系统
    public int DefenseLevel { get; set; } = 1; // 城墙等级 1-5
    public int WallLevel { get; set; } = 1; // 城墙等级，影响攻城难度
    public float GarrisonDefenseBonus { get; set; } = 0f; // 驻军防御加成百分比 (e.g., 0.2 = +20% defense)

    // 城池资源属性
    public int Population { get; set; } = 100; // 城池人口
    public int Grain { get; set; } = 500; // 粮草储备
    public int MaxTroops { get; set; } = 200; // 兵力上限
    public int GrainProductionPerTick { get; set; } = 5; // 每tick粮草产出
    public int TroopProductionPerTick { get; set; } = 2; // 每tick兵力恢复
    public int RecruitCost { get; set; } = 100; // 招募武将战功消耗
    public int UpgradeCost { get; set; } = 200; // 升级城池基础战功消耗
}

// ==================== 武将系统扩展 ====================

public class EquipmentData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "weapon"; // weapon/armor/book/mount
    public int StatBonus { get; set; }
    public string Rarity { get; set; } = "common"; // common/rare/epic/legendary
    public string StatType { get; set; } = "strength"; // strength/intelligence/leadership/speed
}

public class BondData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> RequiredGeneralIds { get; set; } = new();
    public Dictionary<string, float> StatBonuses { get; set; } = new();
    public string Description { get; set; } = "";
}

public class SkillTreeNodeData
{
    public string NodeId { get; set; } = "";
    public string NodeType { get; set; } = "stat"; // stat/skill/passive
    public List<string> ParentNodeIds { get; set; } = new();
    public string StatType { get; set; } = "";
    public float StatValue { get; set; }
    public string UnlockSkillId { get; set; } = "";
    public int Cost { get; set; } = 1;
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public string Description { get; set; } = "";
}

public class SkillTreeData
{
    public string GeneralId { get; set; } = "";
    public List<SkillTreeNodeData> Nodes { get; set; } = new();
}

// ==================== 地图系统扩展 ====================

public class TerrainFeatureData
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "mountain"; // mountain, river, forest, mine, farm
    public float GridX { get; set; }
    public float GridY { get; set; }
    public bool IsResource { get; set; } = false;
    public string ResourceType { get; set; } = "grain"; // grain, troops, merit
    public int ResourceAmount { get; set; } = 0;
    public string Owner { get; set; } = "none";
}

public class ArmyTokenData
{
    public string Id { get; set; } = "";
    public List<string> GeneralIds { get; set; } = new();
    public string CurrentCityId { get; set; } = "";
    public string Team { get; set; } = "player";
}

public static class CityScaleConfig
{
    public static int GetMaxTroops(string scale) => scale switch
    {
        "small" => 200, "medium" => 400, "large" => 600, "huge" => 800, _ => 400
    };

    public static float GetProductionMultiplier(string scale) => scale switch
    {
        "small" => 0.6f, "medium" => 1.0f, "large" => 1.5f, "huge" => 2.0f, _ => 1.0f
    };

    public static int GetMaxGarrisonSlots(string scale) => scale switch
    {
        "small" => 1, "medium" => 2, "large" => 3, "huge" => 4, _ => 2
    };

    public static float GetIconScale(string scale) => scale switch
    {
        "small" => 0.7f, "medium" => 0.9f, "large" => 1.1f, "huge" => 1.3f, _ => 0.9f
    };
}

// ==================== 资源系统 ====================

public enum ResourceType
{
    Gold,       // 金币
    Food,       // 粮草
    Wood,       // 木材
    Iron,       // 铁矿
    Population  // 人口
}

public class ResourceData
{
    public int Current { get; set; }
    public int Max { get; set; } = 9999;
    public int ProductionPerHour { get; set; }
}

// ==================== 建筑系统 ====================

public enum BuildingType
{
    Resource,   // 资源类：农田、伐木场、矿场
    Military,   // 军事类：兵营、校场
    Functional, // 功能类：市场、仓库
    Tech        // 科技类：书院
}

public class Building
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public BuildingType Type { get; set; }
    public int Level { get; set; } = 1;
    public int MaxLevel { get; set; } = 10;
    public bool IsUpgrading { get; set; }
    public long UpgradeFinishTimeMs { get; set; }
}

public class BuildingConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public BuildingType Type { get; set; }
    public int MaxLevel { get; set; } = 10;
    public ResourceType ProducesResource { get; set; } = ResourceType.Gold;
    public int BaseProduction { get; set; } = 10;  // 基础产量
    public int GoldUpgradeCost { get; set; } = 100; // 金币升级费用
    public int FoodUpgradeCost { get; set; } = 50;  // 粮草升级费用
    public int WoodUpgradeCost { get; set; } = 30;  // 木材升级费用
    public int IronUpgradeCost { get; set; } = 20;  // 铁矿升级费用
    public int UpgradeTimeSeconds { get; set; } = 60; // 升级时间（秒）
}

// 内政配置静态类
public static class InteriorConfig
{
    // 建筑配置表
    public static readonly List<BuildingConfig> Buildings = new()
    {
        // 资源类建筑
        new BuildingConfig { Id = "farm", Name = "农田", Type = BuildingType.Resource, ProducesResource = ResourceType.Food, BaseProduction = 20, GoldUpgradeCost = 100, UpgradeTimeSeconds = 30 },
        new BuildingConfig { Id = "lumber_mill", Name = "伐木场", Type = BuildingType.Resource, ProducesResource = ResourceType.Wood, BaseProduction = 15, GoldUpgradeCost = 80, UpgradeTimeSeconds = 30 },
        new BuildingConfig { Id = "iron_mine", Name = "铁矿", Type = BuildingType.Resource, ProducesResource = ResourceType.Iron, BaseProduction = 10, GoldUpgradeCost = 150, UpgradeTimeSeconds = 45 },

        // 军事类建筑
        new BuildingConfig { Id = "barracks", Name = "兵营", Type = BuildingType.Military, MaxLevel = 10, GoldUpgradeCost = 200, UpgradeTimeSeconds = 60 },
        new BuildingConfig { Id = "training_field", Name = "校场", Type = BuildingType.Military, MaxLevel = 10, GoldUpgradeCost = 150, UpgradeTimeSeconds = 45 },

        // 功能类建筑
        new BuildingConfig { Id = "market", Name = "集市", Type = BuildingType.Functional, ProducesResource = ResourceType.Gold, BaseProduction = 30, GoldUpgradeCost = 300, UpgradeTimeSeconds = 90 },
        new BuildingConfig { Id = "warehouse", Name = "仓库", Type = BuildingType.Functional, MaxLevel = 10, GoldUpgradeCost = 120, UpgradeTimeSeconds = 40 },

        // 科技类建筑
        new BuildingConfig { Id = "academy", Name = "书院", Type = BuildingType.Tech, MaxLevel = 5, GoldUpgradeCost = 500, UpgradeTimeSeconds = 120 },
    };

    // 获取建筑配置
    public static BuildingConfig? GetBuildingConfig(string buildingId)
    {
        return Buildings.FirstOrDefault(b => b.Id == buildingId);
    }

    // 计算等级产量：基础产量 × (1 + 等级^1.2) × 城池规模加成
    public static int CalculateProduction(BuildingConfig config, int level, string cityScale)
    {
        float scaleMultiplier = CityScaleConfig.GetProductionMultiplier(cityScale);
        return (int)(config.BaseProduction * System.Math.Pow(level, 1.2) * scaleMultiplier);
    }

    // 计算升级费用（随等级指数增长）
    public static int CalculateUpgradeCost(int baseCost, int targetLevel)
    {
        return (int)(baseCost * System.Math.Pow(1.5, targetLevel - 1));
    }
}

// ==================== 军种系统 ====================

/// <summary>军种类型枚举</summary>
public enum UnitType
{
    Infantry,       // 步兵 - 平衡单位
    Spearman,      // 枪兵 - 反骑兵
    ShieldInfantry, // 盾兵 - 抗远程
    Cavalry,        // 骑兵 - 机动突击
    HeavyCavalry,   // 重骑 - 冲锋爆发
    LightCavalry,   // 轻骑 - 机动收割
    Archer,         // 弓兵 - 远程输出
    Crossbowman,    // 强弩 - 高单体伤害
    Siege,          // 攻城器械 - 对建筑特攻
    Mage            // 法师 - AOE伤害
}

/// <summary>军种标签（用于更灵活的克制计算）</summary>
[Flags]
public enum UnitTag
{
    None = 0,
    Melee = 1,      // 近战
    Ranged = 2,     // 远程
    Cavalry = 4,    // 骑兵类
    Heavy = 8,      // 重型
    Light = 16,     // 轻型
    Infantry = 32,   // 步兵类
    Siege = 64      // 攻城
}

/// <summary>军种配置数据</summary>
public class UnitConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public UnitType Type { get; set; }
    public UnitTag Tags { get; set; }

    // 基础属性倍率（相对于步兵1.0）
    public float AttackMultiplier { get; set; } = 1.0f;
    public float DefenseMultiplier { get; set; } = 1.0f;
    public float SpeedMultiplier { get; set; } = 1.0f;
    public float HPMultiplier { get; set; } = 1.0f;

    // 攻击范围
    public float AttackRange { get; set; } = 40f;

    // 特殊能力
    public bool CanPierce { get; set; } = false;      // 穿透攻击
    public bool CanSplash { get; set; } = false;      // AOE溅射
    public bool HasCounter { get; set; } = false;    // 反击能力
}

/// <summary>军种克制关系配置</summary>
public static class UnitCounterConfig
{
    // 克制表：[攻击方, 防守方] = 倍率
    // 1.0 = 无克制, >1.0 = 克制, <1.0 = 被克
    private static readonly float[,] _counterTable = new float[10, 10];

    static UnitCounterConfig()
    {
        // 初始化克制表 (行=攻击方, 列=防守方)
        // 索引: 0=步兵, 1=枪兵, 2=盾兵, 3=骑兵, 4=重骑, 5=轻骑, 6=弓兵, 7=强弩, 8=攻城, 9=法师

        // 默认1.0
        for (int i = 0; i < 10; i++)
            for (int j = 0; j < 10; j++)
                _counterTable[i, j] = 1.0f;

        // 步兵克制
        _counterTable[0, 2] = 1.1f;  // 步兵 > 盾兵
        _counterTable[0, 9] = 1.2f;  // 步兵 > 法师

        // 枪兵克制（反骑专精）
        _counterTable[1, 3] = 1.5f;  // 枪兵 >> 骑兵
        _counterTable[1, 4] = 1.6f;  // 枪兵 >> 重骑
        _counterTable[1, 5] = 1.2f;  // 枪兵 > 轻骑
        _counterTable[1, 0] = 0.9f;  // 枪兵 < 步兵

        // 盾兵（抗远程）
        _counterTable[2, 6] = 1.4f;  // 盾兵 >> 弓兵
        _counterTable[2, 7] = 1.3f;  // 盾兵 > 强弩
        _counterTable[2, 3] = 1.0f;  // 盾兵 = 骑兵
        _counterTable[2, 0] = 1.0f;  // 盾兵 = 步兵

        // 骑兵
        _counterTable[3, 6] = 1.3f;  // 骑兵 > 弓兵
        _counterTable[3, 0] = 1.2f;  // 骑兵 > 步兵
        _counterTable[3, 1] = 0.6f;  // 骑兵 << 枪兵
        _counterTable[3, 2] = 1.0f;  // 骑兵 = 盾兵

        // 重骑（冲锋爆发）
        _counterTable[4, 6] = 1.5f;  // 重骑 >> 弓兵
        _counterTable[4, 0] = 1.3f;  // 重骑 > 步兵
        _counterTable[4, 1] = 0.5f;  // 重骑 << 枪兵
        _counterTable[4, 2] = 0.6f;  // 重骑 < 盾兵
        _counterTable[4, 9] = 1.4f;  // 重骑 > 法师

        // 轻骑（机动收割）
        _counterTable[5, 6] = 1.2f;  // 轻骑 > 弓兵
        _counterTable[5, 1] = 0.8f;  // 轻骑 < 枪兵
        _counterTable[5, 9] = 1.3f;  // 轻骑 > 法师

        // 弓兵
        _counterTable[6, 0] = 1.2f;  // 弓兵 > 步兵
        _counterTable[6, 3] = 0.7f;  // 弓兵 < 骑兵
        _counterTable[6, 2] = 0.6f;  // 弓兵 << 盾兵
        _counterTable[6, 4] = 0.7f;  // 弓兵 < 重骑

        // 强弩
        _counterTable[7, 3] = 1.4f;  // 强弩 > 骑兵
        _counterTable[7, 4] = 1.3f;  // 强弩 > 重骑
        _counterTable[7, 2] = 0.7f;  // 强弩 < 盾兵
        _counterTable[7, 9] = 1.2f;  // 强弩 > 法师

        // 攻城器械
        _counterTable[8, 8] = 1.5f;  // 攻城 > 攻城
        _counterTable[8, 9] = 1.3f;  // 攻城 > 法师

        // 法师
        _counterTable[9, 0] = 1.2f;  // 法师 > 步兵
        _counterTable[9, 3] = 1.1f;  // 法师 > 骑兵
        _counterTable[9, 2] = 0.8f;  // 法师 < 盾兵
    }

    public static int GetTypeIndex(UnitType type) => (int)type;

    public static float GetCounterMultiplier(UnitType attacker, UnitType defender)
    {
        return _counterTable[GetTypeIndex(attacker), GetTypeIndex(defender)];
    }

    /// <summary>根据标签获取克制倍率（用于技能效果）</summary>
    public static float GetTagCounterMultiplier(UnitTag attackerTags, UnitTag defenderTags)
    {
        float multiplier = 1.0f;

        // 骑兵克制远程
        if (attackerTags.HasFlag(UnitTag.Cavalry) && defenderTags.HasFlag(UnitTag.Ranged))
            multiplier *= 1.2f;

        // 重型克制轻型
        if (attackerTags.HasFlag(UnitTag.Heavy) && defenderTags.HasFlag(UnitTag.Light))
            multiplier *= 1.15f;

        // 远程克制近战（如果有先手优势）
        if (attackerTags.HasFlag(UnitTag.Ranged) && !defenderTags.HasFlag(UnitTag.Ranged))
            multiplier *= 1.1f;

        return multiplier;
    }
}

/// <summary>军种配置表</summary>
public static class UnitConfigTable
{
    public static readonly Dictionary<UnitType, UnitConfig> Units = new()
    {
        [UnitType.Infantry] = new UnitConfig
        {
            Id = "infantry", Name = "步兵", Type = UnitType.Infantry,
            Tags = UnitTag.Melee | UnitTag.Infantry,
            AttackMultiplier = 1.0f, DefenseMultiplier = 1.0f,
            SpeedMultiplier = 1.0f, HPMultiplier = 1.0f,
            AttackRange = 40f, HasCounter = true
        },
        [UnitType.Spearman] = new UnitConfig
        {
            Id = "spearman", Name = "枪兵", Type = UnitType.Spearman,
            Tags = UnitTag.Melee | UnitTag.Infantry,
            AttackMultiplier = 1.1f, DefenseMultiplier = 0.9f,
            SpeedMultiplier = 0.9f, HPMultiplier = 1.1f,
            AttackRange = 50f, HasCounter = true
        },
        [UnitType.ShieldInfantry] = new UnitConfig
        {
            Id = "shield", Name = "盾兵", Type = UnitType.ShieldInfantry,
            Tags = UnitTag.Melee | UnitTag.Heavy | UnitTag.Infantry,
            AttackMultiplier = 0.7f, DefenseMultiplier = 1.8f,
            SpeedMultiplier = 0.7f, HPMultiplier = 1.5f,
            AttackRange = 35f
        },
        [UnitType.Cavalry] = new UnitConfig
        {
            Id = "cavalry", Name = "骑兵", Type = UnitType.Cavalry,
            Tags = UnitTag.Melee | UnitTag.Cavalry | UnitTag.Light,
            AttackMultiplier = 1.2f, DefenseMultiplier = 0.8f,
            SpeedMultiplier = 1.5f, HPMultiplier = 1.0f,
            AttackRange = 45f, CanPierce = true
        },
        [UnitType.HeavyCavalry] = new UnitConfig
        {
            Id = "heavy_cavalry", Name = "重骑", Type = UnitType.HeavyCavalry,
            Tags = UnitTag.Melee | UnitTag.Cavalry | UnitTag.Heavy,
            AttackMultiplier = 1.5f, DefenseMultiplier = 1.2f,
            SpeedMultiplier = 1.0f, HPMultiplier = 1.5f,
            AttackRange = 50f, CanPierce = true
        },
        [UnitType.LightCavalry] = new UnitConfig
        {
            Id = "light_cavalry", Name = "轻骑", Type = UnitType.LightCavalry,
            Tags = UnitTag.Melee | UnitTag.Cavalry | UnitTag.Light,
            AttackMultiplier = 1.0f, DefenseMultiplier = 0.6f,
            SpeedMultiplier = 2.0f, HPMultiplier = 0.8f,
            AttackRange = 40f, CanPierce = true
        },
        [UnitType.Archer] = new UnitConfig
        {
            Id = "archer", Name = "弓兵", Type = UnitType.Archer,
            Tags = UnitTag.Ranged,
            AttackMultiplier = 1.1f, DefenseMultiplier = 0.6f,
            SpeedMultiplier = 1.0f, HPMultiplier = 0.8f,
            AttackRange = 150f, CanSplash = true
        },
        [UnitType.Crossbowman] = new UnitConfig
        {
            Id = "crossbow", Name = "强弩", Type = UnitType.Crossbowman,
            Tags = UnitTag.Ranged,
            AttackMultiplier = 1.5f, DefenseMultiplier = 0.5f,
            SpeedMultiplier = 0.8f, HPMultiplier = 0.7f,
            AttackRange = 200f
        },
        [UnitType.Siege] = new UnitConfig
        {
            Id = "siege", Name = "攻城车", Type = UnitType.Siege,
            Tags = UnitTag.Siege | UnitTag.Ranged | UnitTag.Heavy,
            AttackMultiplier = 0.8f, DefenseMultiplier = 0.4f,
            SpeedMultiplier = 0.3f, HPMultiplier = 2.0f,
            AttackRange = 300f, CanSplash = true
        },
        [UnitType.Mage] = new UnitConfig
        {
            Id = "mage", Name = "术士", Type = UnitType.Mage,
            Tags = UnitTag.Ranged,
            AttackMultiplier = 1.3f, DefenseMultiplier = 0.5f,
            SpeedMultiplier = 0.9f, HPMultiplier = 0.7f,
            AttackRange = 180f, CanSplash = true
        }
    };

    public static UnitConfig? GetConfig(UnitType type) =>
        Units.TryGetValue(type, out var config) ? config : null;
}

// ==================== 阵型系统 ====================

/// <summary>阵型类型枚举</summary>
public enum BattleFormation
{
    // 防御型
    FishScale,      // 鱼鳞阵 - 前排减伤，分摊伤害
    Square,         // 方阵 - 全体防御+20%

    // 进攻型
    Wedge,          // 锥形阵(锋矢) - 前排伤害+50%，穿透
    LongSnake,      // 长蛇阵 - 移动速度+30%，连续攻击
    CraneWing,      // 鹤翼阵 - 侧翼伤害+30%，包围加成

    // 战术型
    EightTrigrams,  // 八卦阵 - 状态切换(防/攻/控)
    CrescentMoon,    // 偃月阵 - 集中输出，叠加伤害
    Circle,         // 环形阵 - 中心目标伤害+40%
    Vanguard,       // 先锋阵 - 默认进攻阵型
}

/// <summary>阵型阶段（八卦阵专用）</summary>
public enum FormationPhase
{
    Defense,    // 防御态：减伤+30%
    Attack,     // 攻击态：伤害+25%
    Control     // 控制态：概率眩晕
}

/// <summary>阵型配置数据</summary>
public class FormationConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    // 分类
    public string Category { get; set; } = "attack"; // "defense", "attack", "tactical"

    // 空间结构描述
    public string Structure { get; set; } = "";

    // 数值加成
    public float DefenseBonus { get; set; } = 0f;
    public float AttackBonus { get; set; } = 0f;
    public float SpeedBonus { get; set; } = 0f;
    public float DamageReduction { get; set; } = 0f;

    // 特殊能力
    public bool HasDamageShare { get; set; } = false;      // 伤害分摊
    public bool HasPierce { get; set; } = false;           // 穿透攻击
    public bool HasSurroundBonus { get; set; } = false;    // 包围加成
    public bool HasChainAttack { get; set; } = false;      // 连锁攻击
    public bool HasPhaseSwitch { get; set; } = false;       // 状态切换
    public bool HasStackDamage { get; set; } = false;      // 叠加伤害
}

/// <summary>阵型配置表</summary>
public static class FormationConfigTable
{
    public static readonly Dictionary<BattleFormation, FormationConfig> Formations = new()
    {
        // 防御型阵型
        [BattleFormation.FishScale] = new FormationConfig
        {
            Id = "fish_scale", Name = "鱼鳞阵",
            Description = "密集排列，层层推进。前排减伤40%，伤害分摊全队。",
            Category = "defense",
            Structure = "[前前前]\n [中中中]\n [后后后]",
            DefenseBonus = 0.2f, DamageReduction = 0.4f,
            HasDamageShare = true
        },
        [BattleFormation.Square] = new FormationConfig
        {
            Id = "square", Name = "方阵",
            Description = "罗马军团风格。全体防御+20%，阵型稳定抗冲击。",
            Category = "defense",
            Structure = "[中中中中]\n[中中中中]",
            DefenseBonus = 0.2f, DamageReduction = 0.1f
        },

        // 进攻型阵型
        [BattleFormation.Wedge] = new FormationConfig
        {
            Id = "wedge", Name = "锥形阵",
            Description = "集中突破一点。前排伤害+50%，可穿透攻击后排。",
            Category = "attack",
            Structure = "   [尖]\n  [中中]\n[后后后]",
            AttackBonus = 0.3f, SpeedBonus = 0.1f,
            HasPierce = true
        },
        [BattleFormation.LongSnake] = new FormationConfig
        {
            Id = "long_snake", Name = "长蛇阵",
            Description = "灵活延展如蛇。移动速度+30%，连续攻击链。",
            Category = "attack",
            Structure = "[1]-[2]-[3]-[4]",
            SpeedBonus = 0.3f, AttackBonus = 0.1f,
            HasChainAttack = true
        },
        [BattleFormation.CraneWing] = new FormationConfig
        {
            Id = "crane_wing", Name = "鹤翼阵",
            Description = "两翼展开包围。侧翼伤害+30%，夹击目标额外+25%。",
            Category = "attack",
            Structure = "[翼]   [翼]\n  [中中]\n    [后]",
            AttackBonus = 0.2f, SpeedBonus = 0.1f,
            HasSurroundBonus = true
        },

        // 战术型阵型
        [BattleFormation.EightTrigrams] = new FormationConfig
        {
            Id = "eight_trigrams", Name = "八卦阵",
            Description = "阴阳变化之道。每10秒切换状态：防御/攻击/控制。",
            Category = "tactical",
            Structure = "[坎][坤][震][巽]\n[艮][乾][兑][离]",
            DefenseBonus = 0.1f, AttackBonus = 0.1f,
            HasPhaseSwitch = true
        },
        [BattleFormation.CrescentMoon] = new FormationConfig
        {
            Id = "crescent_moon", Name = "偃月阵",
            Description = "半月包围形态。攻击同一目标叠加伤害，最多+50%。",
            Category = "tactical",
            Structure = "[  弯  ]\n[中中中]\n [  后]",
            AttackBonus = 0.15f, DefenseBonus = 0.1f,
            HasStackDamage = true
        },
        [BattleFormation.Circle] = new FormationConfig
        {
            Id = "circle", Name = "环形阵",
            Description = "包围敌人。中心目标伤害+40%，防守反击。",
            Category = "tactical",
            Structure = "[前]\n[中][中]\n[后]",
            DefenseBonus = 0.15f, AttackBonus = 0.2f,
            HasSurroundBonus = true
        },
        [BattleFormation.Vanguard] = new FormationConfig
        {
            Id = "vanguard", Name = "先锋阵",
            Description = "标准进攻阵型。无特殊效果，但攻防均衡。",
            Category = "attack",
            Structure = "[前前]\n[中中]\n[后后]"
        }
    };

    public static FormationConfig? GetConfig(BattleFormation formation) =>
        Formations.TryGetValue(formation, out var config) ? config : null;
}

// ==================== 武将出征配置 ====================
public class GeneralDeployEntry
{
    public string GeneralId { get; set; } = "";
    public UnitType UnitType { get; set; } = UnitType.Infantry;
    public BattleFormation BattleFormation { get; set; } = BattleFormation.Vanguard;
    public int SoldierCount { get; set; } = 30;
}
