# 猫三国 - Buffer系统设计文档

> 最后更新：2026-04-15（根据实际代码同步）

## 一、Buff系统设计目标

### 核心能力
- 支持任意效果组合
- 支持叠加 / 覆盖 / 刷新
- 支持触发式（OnHit / OnKill）
- 支持持续效果（DOT / HOT）
- 支持控制效果（眩晕 / 沉默）
- 支持属性修改（攻击 / 防御）

## 二、核心设计思想

### 1. Buff ≠ 状态机
Buff是"效果容器"，不是行为控制器

### 2. 所有效果 = Effect组件
```
Buff = 数据 + 多个Effect
```

### 3. 事件驱动
```
OnAdd / OnTick / OnRemove / OnEvent
```

## 三、核心架构设计（已实现）

### 3.1 BuffSystem 在战斗中的位置

```
BattleScene
 ├── EventBus（事件发布）
 ├── BuffSystem（Buff管理）
 ├── SkillTriggerSystem（技能触发）
 └── BattleContext（连接所有子系统）
```

### 3.2 Buff生命周期

```
创建(Add) → 应用效果(OnAdd) → Tick循环(OnTick) → 过期移除(OnRemove)
```

### 3.3 Tick流程

```
BuffSystem.Tick(deltaTime):
  for each activeBuff:
    elapsed += dt
    
    if 到tickInterval时间:
      执行 OnTick效果
    
    if duration结束:
      执行 OnRemove
      删除Buff
```

## 四、Buff数据结构

### Buff实例

```csharp
public class BuffInstance
{
    public string BuffId { get; set; }
    public string CasterId { get; set; }
    public string TargetId { get; set; }
    
    public float Duration { get; set; }
    public float Elapsed { get; set; }
    
    public int StackCount { get; set; }
}
```

### Buff配置（JSON）

```json
{
  "id": "buff_poison",
  "name": "中毒",
  "duration": 5,
  "maxStack": 3,
  "tickInterval": 1,
  "effects": [
    { "type": "DamageOverTime", "value": 10 }
  ]
}
```

## 五、Effect类型

### 分类

| 类型 | 说明 | 示例 |
|------|------|------|
| 属性类(Modifier) | 通过AttributeSet修改属性 | 增伤、减防、攻速 |
| 行为类(Control) | 控制效果 | 眩晕、沉默、禁止移动 |
| 触发类(Trigger) | 响应事件 | OnHit触发、OnKill触发 |
| 持续类(Periodic) | 周期性效果 | DOT(持续伤害)、HOT(回血) |

### Effect接口

```csharp
public interface IBuffEffect
{
    void OnAdd(BuffContext ctx);     // Buff添加时
    void OnRemove(BuffContext ctx);  // Buff移除时
    void OnTick(BuffContext ctx);    // 每个Tick间隔
}
```

## 六、核心Effect实现

### 6.1 DOT伤害（持续伤害）

```csharp
// 每tick对目标造成固定伤害
OnTick: target.hp -= damage;
```

### 6.2 属性修改

```csharp
// 添加时创建Modifier，移除时删除
OnAdd: target.AttributeSet.AddModifier(new Modifier { ... });
OnRemove: target.AttributeSet.RemoveModifier(sourceId);
```

### 6.3 控制效果（眩晕）

```csharp
OnAdd: target.AddTag(UnitTag.Stunned);
OnRemove: target.RemoveTag(UnitTag.Stunned);
```

## 七、叠加机制

### 支持3种模式

| 模式 | 说明 | 示例 |
|------|------|------|
| Stack | 叠加层数 | 中毒最多3层 |
| Refresh | 刷新时间 | 重新施加→duration重置 |
| Replace | 覆盖 | 强Buff覆盖弱Buff |

## 八、与其他系统联动

### 与技能系统联动（通过EventBus）

```
释放技能 → EventBus.OnSkillCast
   → SkillTriggerSystem检查触发
   → Effect执行: BuffSystem.AddBuff()
   → Buff应用: OnAdd → Modifier添加
```

### 与属性系统联动

```
Buff添加 → OnAdd → AttributeSet.AddModifier()
Buff移除 → OnRemove → AttributeSet.RemoveModifier()
→ 属性自动重算
```

### 与战斗AI联动

```
BattleAI在决策时考虑：
  - 目标身上有什么Buff
  - 是否有控制效果
  - 是否需要解除Buff
```

## 九、BattleScene中的集成

```csharp
// BattleScene中的Buff系统
private BuffSystem _buffSystem;

// 在BattleScene.Enter()中初始化
_buffSystem = new BuffSystem();
_battleContext.BuffSystem = _buffSystem;

// 在BattleScene.UpdateFighting()中每帧更新
_buffSystem.Tick(deltaTime);
```

## 十、与战斗表现联动

| Buff事件 | 视觉表现 |
|----------|----------|
| Buff添加 | EventBus → 飘字提示 |
| DOT伤害 | FloatingTextManager → 伤害数字 |
| Buff移除 | 状态图标消失 |
| 控制效果 | Squad动画变化 |

## 十一、后续扩展

| 方向 | 优先级 | 说明 |
|------|--------|------|
| Buff图标显示 | 高 | Squad上方显示当前Buff图标 |
| Buff详情面板 | 中 | 点击查看Buff效果和剩余时间 |
| 免疫系统 | 中 | 特定Buff对某些状态免疫 |
| Buff转移 | 低 | 技能将Buff从一个目标转移到另一个 |
| Buff净化 | 中 | 移除目标负面Buff |

## 十二、最终能力总结

| 能力 | 状态 |
|------|------|
| DOT/HOT | 已实现 |
| 控制技能 | 已实现 |
| 属性修改 | 已实现（通过Modifier） |
| 多层叠加 | 已实现 |
| 技能联动 | 已实现（EventBus驱动） |
| 生命周期管理 | 已实现 |

Buff系统 = 所有状态效果的统一容器

当前已在BattleScene中集成BuffSystem，通过EventBus与SkillTriggerSystem联动，支持DOT/HOT、控制效果、属性修改等核心功能。Buff通过Modifier系统与AttributeSet交互，确保属性计算的一致性。
