# 猫三国 - Attribute属性系统设计文档

> 最后更新：2026-04-15（根据需求文档同步）

## 一、设计目标

### 必须满足
- 支持多来源叠加（基础 + 装备 + Buff + 技能 + 阵型）
- 支持加法 + 乘法修饰器
- 支持实时重算
- 与 Buff / Skill / Combat 完整解耦
- 支持6维武将属性（武力/智力/统帅/政治/经济/忠诚度）
- 属性既用于战斗也用于内政（全局作用域）

## 二、核心设计思想

### 1. 三层值模型

```
BaseValue（基础值）
   + Add（加法叠加）
   × Multiplier（乘法叠加）
   → FinalValue（最终值）
```

### 2. 来源统一抽象

所有属性修改 = Modifier（修正器）

### 3. 不直接改属性

所有变化必须通过 Modifier 系统

## 三、武将6维属性体系（核心扩展）

### 3.1 属性定义

| 属性 | 代码字段 | 范围 | 类型 | 作用域 |
|------|---------|------|------|--------|
| 武力 | Strength | 1~100 | 培养型 | 战斗 + 训练 |
| 智力 | Intelligence | 1~100 | 培养型 | 战斗技能 + 计策 + 搜索 |
| 统帅 | Command | 1~100 | 培养型 | 士气 + 带兵上限 + 征兵 |
| 政治 | Politics | 1~100 | 培养型 | 内政效率 + 建设 + 外交 |
| 魅力 | Charisma | 1~100 | 培养型 | 说服招募 + 外交 + 忠诚维系 |
| 忠诚度 | Loyalty | 0~100 | 动态型 | 俘获投降率 + 叛变概率 + 被策反风险 |

> **变更说明**：原"经济(Economics)"属性改为"魅力(Charisma)"。经济收入加成并入政治属性，魅力独立承担人际关系相关功能。

### 3.2 属性分类

**培养型属性**（武力/智力/统帅/政治/魅力）：
- 基础值由武将数据（generals.json）定义
- 通过战功升级时手动分配加点（每次升级只能选择**一项**属性+1点）
- 上限100，基础值决定武将特色方向

**动态型属性**（忠诚度）：
- 初始值由招募方式决定（自愿>说服>招降）
- 通过**势力君主经济赏赐**提升（金币/装备/俸禄）
- 受事件驱动浮动（赏赐/俸禄/战败/被俘）
- 忠诚度低于阈值时，可被敌对势力策反劝走
- 不通过Modifier系统修改，直接读写

### 3.3 武将升级与加点机制

#### 升级条件
- 武将通过参与战斗积累**战功**
- 战功达到升级所需阈值时可升级
- 升级消耗对应数量的战功

#### 加点规则
- 每次升级获得**1点属性点**
- 玩家从武力/智力/统帅/政治/魅力中**选择一项**加点
- **只能选择一项**，不可拆分
- 属性上限100，已满的属性不可选择

#### 加点策略参考
| 武将定位 | 推荐加点 | 说明 |
|---------|---------|------|
| 猛将型 | 武力 | 提升战斗伤害 |
| 军师型 | 智力 | 提升技能效果和搜索 |
| 统帅型 | 统帅 | 提升带兵上限和征兵 |
| 文官型 | 政治 | 提升内政和建设效率 |
| 外交型 | 魅力 | 提升说服/招募/外交成功率 |

### 3.4 属性作用映射

| 场景 | 使用属性 | 影响 |
|------|----------|------|
| 战斗伤害 | 武力 | 物理攻击力加成 |
| 技能效果 | 智力 | 技能伤害/命中率 |
| 部队士气 | 统帅 | 初始士气/士气恢复 |
| 带兵上限 | 统帅 | Squad最大兵力 |
| 资源产出 | 政治 | 内政官加成（含原经济功能） |
| 建设速度 | 政治 | 建筑升级效率 |
| 征兵效率 | 统帅+武力 | 军事官加成 |
| 搜索概率 | 智力+政治 | 搜索官加成 |
| 说服成功率 | 魅力（辅助） | 无羁绊时的说服基础成功率 |
| 外交成功率 | 魅力+政治 | 外交行动成功率 |
| 忠诚维系 | 魅力 | 减缓麾下武将忠诚度下降 |
| 俘获判定 | 速度+忠诚度 | 逃脱概率/投降概率 |
| 商业收入 | 政治 | 城池金币产出加成（原经济属性功能并入） |
| 策反抵抗 | 忠诚度+魅力 | 抵御敌方策反 |

## 四、战斗属性系统（已实现）

### 4.1 AttributeSet

在 BattleScene 中，每个 Squad 拥有 `AttributeSet`：

```csharp
public class AttributeSet
{
    // 基础属性（战斗用）
    public float BaseHp { get; set; }
    public float BaseAttack { get; set; }
    public float BaseDefense { get; set; }
    public float BaseSpeed { get; set; }
    
    // Modifier列表
    private List<Modifier> _modifiers;
    
    // 最终值计算
    public float GetFinalValue(AttrType type)
}
```

### 4.2 Modifier（已实现）

```csharp
public class Modifier
{
    public string SourceId { get; set; }     // Buff / Skill / Equipment
    public AttrType AttrType { get; set; }
    
    public ModifierType Type { get; set; }   // Add / Multiply
    
    public float Value { get; set; }
}

public enum ModifierType
{
    Add,       // 加法叠加
    Multiply   // 乘法叠加
}
```

### 4.3 属性初始化（BattleScene.InitializeSquadAttributes）

```csharp
private void InitializeSquadAttributes()
{
    // 从GeneralData读取基础属性（6维中的武力/智力/统帅映射到战斗属性）
    // 武力 → BaseAttack加成
    // 智力 → 技能系数加成
    // 统帅 → 初始士气/兵力上限
    // 从UnitConfigTable获取军种加成
    // 从FormationConfigTable获取阵型加成
    // 应用被动技能Modifier
}
```

## 五、重算逻辑

### 计算公式

```
FinalValue = (BaseValue + Sum(AddModifiers)) × Product(1 + MultiplyModifiers)
```

### 示例

```
攻击力计算：
Base = 100（军种基础 × 武力加成）
+ Buff加法: +20
× 技能乘法: ×1.5
= Final: (100 + 20) × 1.5 = 180
```

## 六、与Buff系统联动（已实现）

### Buff → Attribute

```
Buff添加 → 创建Modifier → AttributeSet.AddModifier()
Buff移除 → 删除Modifier → AttributeSet.RemoveModifier(sourceId)
```

### 示例：攻击+20%

```csharp
new Modifier {
    SourceId = "buff_attack_up",
    AttrType = AttrType.Attack,
    Type = ModifierType.Multiply,
    Value = 0.2f
}
```

## 七、与技能系统联动（已实现）

### 技能Effect → Modifier

```
释放技能 → 添加Buff → Buff添加Modifier → 属性变化
```

### 被动技能

```csharp
// BattleScene.ApplyPassive()
private void ApplyPassive(Squad squad, Skill passive)
{
    // 被动技能直接添加永久Modifier
}
```

## 八、武将基础属性（DataSchemas.cs）

```csharp
public class GeneralData
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Strength { get; set; }      // 武力（影响攻击）
    public int Intelligence { get; set; }  // 智力（影响技能）
    public int Command { get; set; }       // 统帅（影响士气/带兵）
    public int Politics { get; set; }      // 政治（影响内政/商业）
    public int Charisma { get; set; }      // 魅力（影响说服/外交/忠诚维系）
    public int Loyalty { get; set; }       // 忠诚度（动态）
    public int Speed { get; set; }         // 速度（影响行动）
    public string AppearYear { get; set; } // 历史登场年份
    public string AppearCity { get; set; } // 历史登场城池
    public string ForceId { get; set; }    // 所属势力
    public List<string> SpecialSkills { get; set; } // 特技列表
    // ...
}
```

> **注**: 原4属性扩展为6维 + Speed。Charisma（魅力）替代原Economics（经济），经济功能并入Politics。

### 属性来源链（战斗场景）

```
GeneralData基础属性（6维）
    → 武力/智力/统帅 映射为战斗属性
    + UnitConfigTable军种修正
    + FormationConfigTable阵型修正
    + 被动技能Modifier
    + Buff Modifier
    = Squad最终属性
```

### 属性来源链（内政场景）

```
GeneralData基础属性（6维）
    → 政治 用于内政效率/商业收入计算
    → 智力/政治 用于搜索
    → 魅力 用于说服/外交（优先羁绊，无羁绊时参考魅力）
    → 统帅/武力 用于军事管理
    → 忠诚度 用于俘获/叛变/策反判定
```

## 九、属性在各系统中的应用

### 战斗系统

| 属性 | 用途 |
|------|------|
| Attack（来自武力） | DamageCalculator 伤害计算 |
| Defense | 减伤计算 |
| Speed | 行动频率/移动速度 |
| HP | 生命值/存活判定 |
| CritRate | 暴击概率 |
| CritDamage | 暴击伤害倍率 |

### 内政系统

| 属性 | 用途 |
|------|------|
| 政治 | 内政官产量加成（含原经济功能） |
| 政治 + 统帅 | 太守效率加成 |
| 统帅 + 武力 | 军事官征兵加成 |
| 智力 + 政治 | 搜索官发现概率 |
| 魅力 | 说服成功率辅助（无羁绊时生效）/外交加成 |

### 俘获系统

| 属性 | 用途 |
|------|------|
| 速度 | 逃脱概率（越高越难抓） |
| 忠诚度 | 投降概率（越低越易降） |
| 魅力 | 招降成功率辅助 |

## 十、属性系统的扩展来源

| 来源 | 方式 | 状态 |
|------|------|------|
| 武将基础（6维） | GeneralData | 已实现5维，魅力待替换经济 |
| 升级加点 | 战功升级→选择1项属性+1 | 待实现 |
| 军种加成 | UnitConfigTable | 已实现 |
| 阵型加成 | FormationConfigTable | 已实现 |
| 被动技能 | ApplyPassive → Modifier | 已实现 |
| Buff效果 | BuffSystem → Modifier | 已实现 |
| 装备系统 | EquipItem → Modifier | 已实现框架 |
| 武将特技 | SpecialSkill → 内政/战斗加成 | 待实现 |
| 羁绊加成 | BondData → Modifier | 已实现（说服系统） |
| 官职加成 | OfficerRole → 内政Modifier | 待实现 |

## 十一、忠诚度特殊处理

忠诚度不走Modifier系统，因为其变化逻辑与战斗属性不同：

```
忠诚度变化触发点：
- 势力君主经济赏赐（核心提升手段）
- 每月俸禄结算
- 战斗胜利/失败
- 被俘虏/被释放
- 主公声望变化
```

### 忠诚度提升机制

忠诚度**只能通过势力君主经济赏赐提升**：
- 赏赐金币：按赏赐金额计算忠诚度提升
- 赏赐装备：按装备品质计算忠诚度提升
- 升职：获得更高官位时小幅提升

### 策反机制

忠诚度低于阈值时，敌对势力可通过外交手段策反：
- 忠诚度 < 50：可被策反，成功率 = (50 - 忠诚度) × 2%
- 忠诚度 < 30：高策反风险，成功率显著提升
- 策反成功后武将转入敌方势力

忠诚度直接读写 `GeneralProgress.Loyalty`，触发EventBus事件通知UI更新。

## 十二、后续扩展

| 方向 | 说明 |
|------|------|
| 衍生属性 | DPS = Attack × Speed × CritFactor |
| 属性上下限 | Clamp(finalValue, min, max) |
| 光环系统 | 给范围内队友添加Modifier |
| Dirty Flag优化 | 属性变化时标记脏，下次访问时重算 |
| 魅力影响君主光环 | 势力君主魅力值影响全体武将忠诚下降速率 |

## 十三、最终总结

Attribute系统 = 所有数值计算的唯一权威来源

武将属性从4维扩展为6维（武力/智力/统帅/政治/魅力/忠诚度），作用域从纯战斗扩展到全局（内政/外交/军事/俘获/说服）。升级时通过战功消耗获得属性点，玩家手动选择一项属性加点，体现培养策略。魅力替代原经济属性，主要影响说服/外交/忠诚维系。忠诚度通过势力君主经济赏赐提升，低忠诚武将存在被策反风险。战斗属性仍通过 AttributeSet + Modifier 系统计算，内政属性直接读取进行加成计算。
