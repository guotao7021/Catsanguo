# 三国群英传5 - 游戏整体框架设计

## 文档说明

本文档是三国群英传5游戏的整体架构设计文本，用于指导后续开发。通过调整本文档中的配置和参数，可以实现游戏功能的扩展和平衡性调整。

---

## 一、游戏概述

### 1.1 游戏定位
- **平台**: Windows PC 单机游戏
- **类型**: 回合制策略RPG游戏
- **模式**: 完全离线单机运行，无需网络连接
- **发布形式**: Web版（浏览器直接运行）+ 可打包为桌面应用（Electron/Tauri）

### 1.2 核心玩法
- **大地图探索**: 城池连线地图，战略位置决定攻防路线
- **即时战斗**: 武将带领军队进行即时制战斗
- **内政管理**: 城池建设、资源管理、人才招募
- **武将收集**: 历史武将收集、培养、羁绊组合

### 1.3 技术栈
| 层级 | 技术选型 | 说明 |
|------|---------|------|
| 渲染引擎 | Phaser 3 | 2D游戏渲染，精灵管理，动画系统 |
| 开发语言 | TypeScript | 类型安全，大型项目必备 |
| UI方案 | DOM + Phaser混合 | DOM管理菜单UI，Phaser负责游戏渲染 |
| 状态管理 | 自定义事件总线 | 松耦合，高扩展性 |
| 数据存储 | IndexedDB + LocalStorage | 本地存档系统（无需网络） |
| 桌面打包 | Electron / Tauri | 可选：打包为Windows桌面应用 |
| 构建工具 | Vite / Webpack | 项目构建和热更新 |

### 1.4 单机运行说明
- **无网络依赖**: 游戏所有资源（图片、音频、配置）均打包在本地
- **本地存档**: 使用浏览器 IndexedDB 存储游戏进度，支持多存档位
- **隐私保护**: 玩家数据不上传任何服务器，完全本地化
- **启动方式**: 
  - Web版：直接打开 `index.html` 或通过本地HTTP服务器运行
  - 桌面版：双击打包后的 `.exe` 文件即可运行

---

## 二、整体架构设计

### 2.1 架构分层

```
┌─────────────────────────────────────────────────────┐
│                    UI Layer                         │
│  ┌───────────┐  ┌───────────┐  ┌────────────────┐  │
│  │ Map UI    │  │ Battle UI │  │ City/Menu UI   │  │
│  └───────────┘  └───────────┘  └────────────────┘  │
├─────────────────────────────────────────────────────┤
│                  Game Logic Layer                    │
│  ┌──────────┐  ┌──────────┐  ┌──────────────────┐  │
│  │ Map Sys  │  │Battle Sys│  │ Internal Affair  │  │
│  │ (Tile+   │  │(State    │  │ System           │  │
│  │  Graph)  │  │ Machine) │  │                  │  │
│  └──────────┘  └──────────┘  └──────────────────┘  │
├─────────────────────────────────────────────────────┤
│                  Entity Layer (ECS)                  │
│  ┌──────────┐  ┌──────────┐  ┌──────────────────┐  │
│  │Characters│  │  Troops  │  │     Cities       │  │
│  │(Entities)│  │(Entities)│  │    (Entities)    │  │
│  └──────────┘  └──────────┘  └──────────────────┘  │
├─────────────────────────────────────────────────────┤
│                Core Services Layer                   │
│  ┌────────┐ ┌──────────┐ ┌────────┐ ┌───────────┐  │
│  │EventBus│ │SaveManager│ │ Config │ │  Random   │  │
│  │        │ │(Memento) │ │ Tables │ │  Generator│  │
│  └────────┘ └──────────┘ └────────┘ └───────────┘  │
├─────────────────────────────────────────────────────┤
│                  Rendering Layer                     │
│  ┌──────────────┐  ┌─────────────┐  ┌───────────┐  │
│  │  Phaser 3    │  │  CSS/DOM    │  │   Audio   │  │
│  │  (Sprites,   │  │  (Panels,   │  │  (BGM,    │  │
│  │  Tilemap,    │  │  Buttons,   │  │   SFX)    │  │
│  │  Animation)  │  │  Text)      │  │           │  │
│  └──────────────┘  └─────────────┘  └───────────┘  │
└─────────────────────────────────────────────────────┘
```

### 2.2 核心设计模式

| 系统 | 主要模式 | 辅助模式 |
|------|---------|---------|
| 角色系统 | ECS (Entity-Component-System) | 工厂模式、数据驱动 |
| 战斗系统 | 状态机 + 命令模式 | 策略模式、观察者模式 |
| 地图系统 | Tile-based + 图结构 | 对象池、A*寻路 |
| 存档系统 | 备忘录模式 | 序列化/反序列化 |
| UI系统 | 组件化 + 状态驱动 | 观察者模式、单例模式 |
| 全局 | 事件总线 | 依赖注入 |

---

## 三、核心系统设计

### 3.1 武将/角色系统

#### 3.1.1 武将属性体系

```typescript
interface CharacterStats {
  // 基础属性 (1-100)
  strength: number;    // 武力 - 影响攻击力
  intelligence: number; // 智力 - 影响技能效果和策略
  command: number;     // 统率 - 影响带兵数量和防御
  politics: number;    // 政治 - 影响内政效率
  charm: number;       // 魅力 - 影响招降和招募
  
  // 战斗属性
  maxHp: number;       // 最大生命值
  attack: number;      // 攻击力
  defense: number;     // 防御力
  speed: number;       // 速度 - 影响行动顺序
  criticalRate: number; // 暴击率
  
  // 经验系统
  level: number;       // 等级
  experience: number;  // 经验值
  maxExperience: number;
}
```

#### 3.1.2 武将数据结构

```json
{
  "id": "zhangfei",
  "name": "张飞",
  "title": "万人敌",
  "rarity": "SSR",
  "faction": "shu",
  "avatar": "assets/heroes/zhangfei.png",
  
  "baseStats": {
    "strength": 98,
    "intelligence": 45,
    "command": 85,
    "politics": 30,
    "charm": 60
  },
  
  "growth": {
    "strength": 2.5,
    "intelligence": 1.2,
    "command": 2.0,
    "politics": 0.8,
    "charm": 1.0
  },
  
  "skills": ["roar", "intimidate", "charge"],
  "availableTroops": ["infantry", "cavalry", "archer"],
  
  "bondCharacters": ["liubei", "guanyu"],
  "bondSkill": "oath_of_the_peach_garden"
}
```

#### 3.1.3 技能系统

```typescript
enum SkillType {
  ACTIVE = 'active',      // 主动技能 - 需要手动释放
  PASSIVE = 'passive',    // 被动技能 - 自动生效
  BOND = 'bond',         // 羁绊技能 - 特定组合触发
  COMMAND = 'command'    // 指挥技能 - 影响军队
}

interface Skill {
  id: string;
  name: string;
  type: SkillType;
  description: string;
  icon: string;
  
  // 消耗
  cost?: {
    mp?: number;         // 魔法值消耗
    cooldown?: number;   // 冷却回合
  };
  
  // 效果
  effects: SkillEffect[];
  
  // 范围
  range?: {
    type: 'self' | 'ally' | 'enemy' | 'area';
    radius?: number;
  };
}

interface SkillEffect {
  type: 'damage' | 'heal' | 'buff' | 'debuff' | 'summon';
  value: number;
  duration?: number;     // 持续回合数
  stat?: keyof CharacterStats; // 影响的属性
}
```

#### 3.1.4 兵种系统

```typescript
interface TroopType {
  id: string;
  name: string;
  icon: string;
  
  // 克制关系
  strongAgainst: string[];  // 克制的兵种
  weakAgainst: string[];    // 被克制的兵种
  
  // 属性修正
  stats: {
    attack: number;    // 攻击修正
    defense: number;   // 防御修正
    speed: number;     // 速度修正
  };
  
  // 特殊能力
  abilities?: string[];
}

// 兵种克制矩阵示例
const TroopCounterMatrix = {
  infantry: { strongAgainst: ['cavalry'], weakAgainst: ['archer'] },
  cavalry: { strongAgainst: ['archer'], weakAgainst: ['infantry'] },
  archer: { strongAgainst: ['infantry'], weakAgainst: ['cavalry'] },
  chariot: { strongAgainst: ['infantry'], weakAgainst: ['cavalry'] },
  navy: { strongAgainst: ['infantry'], weakAgainst: ['archer'] }
};
```

#### 3.1.5 羁绊系统

```json
{
  "id": "oath_of_the_peach_garden",
  "name": "桃园结义",
  "requiredCharacters": ["liubei", "guanyu", "zhangfei"],
  "effects": [
    {
      "stat": "attack",
      "bonus": 0.15  // 15%攻击力加成
    },
    {
      "stat": "defense", 
      "bonus": 0.10  // 10%防御力加成
    }
  ]
}
```

### 3.2 战斗系统

#### 3.2.1 战斗状态机

```typescript
enum BattleState {
  INIT = 'init',           // 初始化
  DEPLOY = 'deploy',       // 布阵阶段
  PLAYER_TURN = 'player_turn',    // 玩家回合
  ENEMY_TURN = 'enemy_turn',      // 敌方回合
  SKILL_CAST = 'skill_cast',      // 技能释放
  DAMAGE_CALC = 'damage_calc',    // 伤害计算
  RESOLVE = 'resolve',     // 结算阶段
  VICTORY = 'victory',     // 胜利
  DEFEAT = 'defeat'        // 失败
}

interface BattleStateMachine {
  currentState: BattleState;
  states: Map<BattleState, BattleStateHandler>;
  
  transition(newState: BattleState): void;
  handleInput(input: BattleInput): void;
}
```

#### 3.2.2 命令系统

```typescript
interface Command {
  execute(): void;
  undo(): void;
  canExecute(): boolean;
}

class MoveCommand implements Command {
  constructor(
    private unit: Entity,
    private targetPosition: Position
  ) {}
  
  execute() {
    // 移动到目标位置
  }
  
  undo() {
    // 返回原位置
  }
  
  canExecute() {
    // 检查移动范围
    return true;
  }
}

class AttackCommand implements Command {
  constructor(
    private attacker: Entity,
    private target: Entity
  ) {}
  
  execute() {
    // 执行攻击逻辑
  }
  
  undo() {
    // 恢复生命值
  }
  
  canExecute() {
    // 检查攻击范围
    return true;
  }
}
```

#### 3.2.3 伤害计算公式

```typescript
interface DamageCalculation {
  // 基础伤害
  baseDamage = attacker.attack * skillMultiplier;
  
  // 属性修正
  statCorrection = (attacker.strength - target.defense) / 100;
  
  // 兵种克制
  troopCounter = getCounterMultiplier(attackerTroop, targetTroop);
  
  // 暴击判定
  critical = isCritical(attacker.criticalRate) ? 1.5 : 1.0;
  
  // 技能加成
  skillBonus = getSkillBonuses(attacker, target);
  
  // 随机因子 (0.9 ~ 1.1)
  randomFactor = 0.9 + Math.random() * 0.2;
  
  // 最终伤害
  finalDamage = baseDamage 
    * (1 + statCorrection) 
    * troopCounter 
    * critical 
    * (1 + skillBonus)
    * randomFactor;
    
  return Math.max(1, Math.floor(finalDamage));
}
```

#### 3.2.4 战斗场景配置

```typescript
interface BattleConfig {
  // 战场
  terrain: TerrainType;
  weather: WeatherType;
  
  // 参战方
  playerTeam: BattleUnit[];
  enemyTeam: BattleUnit[];
  
  // 规则
  maxTurns: number;          // 最大回合数
  retreatAllowed: boolean;   // 是否允许撤退
  captureCity: boolean;      // 是否攻城
  
  // 胜利条件
  victoryConditions: VictoryCondition[];
  
  // 失败条件
  defeatConditions: DefeatCondition[];
}

enum VictoryCondition {
  ALL_ENEMIES_DEFEATED,
  COMMANDER_DEFEATED,
  CITY_CAPTURED,
  SURVIVE_TURNS
}

enum DefeatCondition {
  ALL_UNITS_DEFEATED,
  COMMANDER_DEFEATED,
  TURNS_EXPIRED
}
```

### 3.3 地图系统

> **优先开发**: 地图系统作为游戏的核心展示界面，本阶段先实现基础展示功能。

#### 3.3.1 世界地图结构

```typescript
interface WorldMap {
  // 图结构 - 战略层
  graph: {
    nodes: Map<string, MapNode>;  // 城池/据点
    edges: MapEdge[];             // 连接路径
  };
  
  // 瓦片地图 - 战术层
  tileMap: {
    width: number;
    height: number;
    tiles: Tile[][];
    terrainLayers: TerrainLayer[];
  };
  
  // 战争迷雾
  fogOfWar: boolean[][];
  
  // 当前事件
  events: MapEvent[];
}

interface MapNode {
  id: string;
  type: 'city' | 'fortress' | 'village' | 'resource';
  name: string;
  position: { x: number; y: number };
  
  // 城池属性
  city?: {
    level: number;           // 城池等级 (1-5)
    owner: string;           // 所属势力
    troops: number;          // 守军数量
    resources: {
      gold: number;
      grain: number;
      iron: number;
    };
    buildings: Building[];
  };
}

interface MapEdge {
  from: string;
  to: string;
  distance: number;
  terrain: TerrainType;
  movementCost: number;
}
```

#### 3.3.2 地形系统

```typescript
enum TerrainType {
  PLAIN = 'plain',           // 平原 - 无修正
  FOREST = 'forest',         // 森林 - 步兵+10%防御
  MOUNTAIN = 'mountain',     // 山地 - 通行困难
  RIVER = 'river',           // 河流 - 需要桥梁/船只
  DESERT = 'desert',         // 沙漠 - 移动力下降
  SWAMP = 'swamp',           // 沼泽 - 骑兵无法通行
  CITY = 'city'              // 城池 - 恢复点
}

interface TerrainModifier {
  terrain: TerrainType;
  
  // 移动修正
  movementCost: {
    infantry: number;
    cavalry: number;
    archer: number;
    chariot: number;
  };
  
  // 战斗修正
  combatBonus: {
    attack?: number;
    defense?: number;
    evasion?: number;
  };
}
```

#### 3.3.3 路径查找

```typescript
interface Pathfinding {
  // A* 算法 - 战术层移动
  findPath(
    start: Position, 
    end: Position, 
    terrain: TerrainType[][]
  ): Position[];
  
  // Dijkstra 算法 - 战略层最短路径
  findShortestPath(
    startNode: string, 
    endNode: string,
    graph: WorldMapGraph
  ): string[];
}
```

#### 3.3.4 地图展示系统详细规格（阶段零）

```typescript
// 地图展示系统 - MVP版本规格

interface MapDisplayConfig {
  // 画布设置
  canvas: {
    width: 1920;
    height: 1080;
    backgroundColor: 0x2a1f1a;  // 古铜色背景
  };
  
  // 相机设置
  camera: {
    minZoom: 0.5;      // 最小缩放
    maxZoom: 2.0;      // 最大缩放
    dragEnabled: true;  // 允许拖拽
  };
  
  // 城池显示
  cityNode: {
    size: 48;          // 城池图标大小
    fontSize: 14;      // 城池名称字号
    colorsByFaction: {
      player: 0x3498db;    // 蓝色 - 玩家
      enemy: 0xe74c3c;     // 红色 - 敌方
      neutral: 0x95a5a6;   // 灰色 - 中立
    };
  };
  
  // 连接线
  connectionLine: {
    width: 3;
    color: 0x8b7355;    // 棕色
    style: 'solid';     // 实线
  };
  
  // 战争迷雾
  fogOfWar: {
    enabled: true;
    unexploredColor: 0x000000;
    opacity: 0.7;
  };
  
  // 小地图
  minimap: {
    width: 200;
    height: 150;
    position: { x: 20, y: 20 };
    borderColor: 0xffffff;
  };
}
```

**地图展示系统 MVP 功能清单**:

| 功能 | 优先级 | 说明 |
|------|--------|------|
| 地图画布渲染 | P0 | Phaser 游戏画布初始化 |
| 城池节点显示 | P0 | 各势力城池图标和名称 |
| 连接线绘制 | P0 | 城池间连线 |
| 相机缩放拖拽 | P0 | 鼠标滚轮缩放，拖拽移动 |
| 点击选中城池 | P1 | 显示城池信息面板 |
| 势力颜色区分 | P1 | 不同势力不同颜色 |
| 战争迷雾 | P2 | 未探索区域遮罩 |
| 小地图 | P2 | 右上角小地图导航 |
| 动画效果 | P3 | 城池呼吸动画，连接线流动 |

### 3.4 内政系统

#### 3.4.1 内政指令

```typescript
interface InternalAffairCommand {
  type: InternalAffairType;
  target: string;
  cost: {
    gold?: number;
    grain?: number;
    turns?: number;
  };
  effect: InternalAffairEffect;
}

enum InternalAffairType {
  DEVELOP = 'develop',           // 开发 - 提升城池规模
  FARM = 'farm',                 // 农耕 - 增加粮食
  COMMERCE = 'commerce',         // 商业 - 增加金钱
  SEARCH = 'search',             // 搜索 - 寻找人才/宝物
  RECRUIT = 'recruit',           // 征兵 - 招募士兵
  TRAIN = 'train',               // 训练 - 提升士兵士气
  DIPLOMACY = 'diplomacy',       // 外交 - 与其他势力交涉
  BUILD = 'build',               // 建设 - 建造设施
  RESEARCH = 'research'          // 研究 - 解锁科技
}
```

#### 3.4.2 城池管理

```typescript
interface City {
  id: string;
  name: string;
  position: { x: number; y: number };
  
  // 基础属性
  level: number;                 // 等级 (1-5)
  owner: string;                 // 所属势力
  population: number;            // 人口
  
  // 资源
  resources: {
    gold: number;
    grain: number;
    iron: number;
  };
  
  // 设施
  buildings: {
    type: BuildingType;
    level: number;
  }[];
  
  // 驻军
  garrison: {
    commander: string;
    troops: number;
    morale: number;
  };
  
  // 官员
  officials: {
    governor: string;
    general: string[];
    advisor: string;
  };
  
  // 每回合产出
  income: {
    gold: number;
    grain: number;
  };
}

enum BuildingType {
  FARM = 'farm',             // 农田
  MARKET = 'market',         // 市场
  BARRACKS = 'barracks',     // 兵营
  ACADEMY = 'academy',       // 学院
  WALL = 'wall',             // 城墙
  TOWER = 'tower',           // 箭塔
  STOREHOUSE = 'storehouse'  // 仓库
}
```

### 3.5 存档系统

#### 3.5.1 存档结构

```typescript
interface GameSave {
  metadata: {
    version: string;
    timestamp: number;
    playTime: number;
    saveSlot: number;
  };
  
  state: GameStateSnapshot;
}

interface GameStateSnapshot {
  // 玩家信息
  player: {
    name: string;
    faction: string;
    difficulty: Difficulty;
  };
  
  // 当前进度
  progress: {
    chapter: number;
    turn: number;
    phase: GamePhase;
  };
  
  // 武将数据
  characters: CharacterSaveData[];
  
  // 城池数据
  cities: CitySaveData[];
  
  // 军队数据
  armies: ArmySaveData[];
  
  // 世界状态
  world: {
    controlledNodes: string[];
    discoveredAreas: string[];
    completedEvents: string[];
  };
  
  // 资源
  resources: {
    gold: number;
    grain: number;
    iron: number;
  };
}
```

#### 3.5.2 存档管理器

```typescript
class SaveManager {
  // 创建存档
  createSave(slot: number, data: GameStateSnapshot): void;
  
  // 加载存档
  loadSave(slot: number): GameStateSnapshot;
  
  // 删除存档
  deleteSave(slot: number): void;
  
  // 获取存档列表
  getSaveList(): SaveMetadata[];
  
  // 快速存档
  quickSave(): void;
  
  // 快速读档
  quickLoad(): void;
  
  // 自动存档
  autoSave(): void;
}
```

### 3.6 UI系统

#### 3.6.1 屏幕管理

```typescript
enum ScreenType {
  TITLE = 'title',               // 标题画面
  MAIN_MENU = 'main_menu',       // 主菜单
  MAP = 'map',                   // 大地图
  BATTLE = 'battle',             // 战斗画面
  CITY = 'city',                 // 城池管理
  CHARACTER = 'character',       // 武将信息
  INVENTORY = 'inventory',       // 物品栏
  SETTINGS = 'settings',         // 设置
  SAVE_LOAD = 'save_load'        // 存档/读档
}

interface ScreenManager {
  currentScreen: ScreenType;
  screenStack: ScreenType[];
  
  pushScreen(screen: ScreenType): void;
  popScreen(): void;
  replaceScreen(screen: ScreenType): void;
}
```

#### 3.6.2 UI组件系统

```typescript
interface UIComponent {
  id: string;
  type: ComponentType;
  position: { x: number; y: number };
  size: { width: number; height: number };
  
  // 数据绑定
  dataBinding?: {
    source: string;
    transform?: (value: any) => any;
  };
  
  // 事件处理
  events?: {
    onClick?: () => void;
    onHover?: () => void;
  };
  
  // 样式
  style?: CSSProperties;
}

enum ComponentType {
  BUTTON = 'button',
  PANEL = 'panel',
  TEXT = 'text',
  IMAGE = 'image',
  PROGRESS_BAR = 'progress_bar',
  LIST = 'list',
  GRID = 'grid',
  TOOLTIP = 'tooltip',
  MODAL = 'modal'
}
```

### 3.7 事件系统

#### 3.7.1 事件总线

```typescript
enum GameEvents {
  // 战斗事件
  BATTLE_START = 'battle:start',
  BATTLE_END = 'battle:end',
  UNIT_ATTACKED = 'unit:attacked',
  UNIT_DIED = 'unit:died',
  SKILL_USED = 'skill:used',
  
  // 地图事件
  CITY_CAPTURED = 'city:captured',
  NODE_ENTERED = 'node:entered',
  EVENT_TRIGGERED = 'event:triggered',
  
  // 回合事件
  TURN_START = 'turn:start',
  TURN_END = 'turn:end',
  PHASE_CHANGED = 'phase:changed',
  
  // 武将事件
  CHARACTER_RECRUITED = 'character:recruited',
  CHARACTER_LEVEL_UP = 'character:level_up',
  BOND_ACTIVATED = 'bond:activated',
  
  // 资源事件
  RESOURCE_CHANGED = 'resource:changed',
  CITY_INCOME = 'city:income',
  
  // UI事件
  SCREEN_CHANGED = 'screen:changed',
  MENU_OPENED = 'menu:opened',
  MENU_CLOSED = 'menu:closed'
}

interface EventBus {
  on(event: GameEvents, handler: EventHandler): void;
  off(event: GameEvents, handler: EventHandler): void;
  emit(event: GameEvents, data?: any): void;
  once(event: GameEvents, handler: EventHandler): void;
}
```

---

## 四、配置表系统

### 4.1 武将配置表

```json
// config/characters.json
[
  {
    "id": "liubei",
    "name": "刘备",
    "faction": "shu",
    "rarity": "SSR",
    "baseStats": { "strength": 75, "intelligence": 80, "command": 90, "politics": 85, "charm": 95 }
  },
  {
    "id": "guanyu",
    "name": "关羽", 
    "faction": "shu",
    "rarity": "SSR",
    "baseStats": { "strength": 97, "intelligence": 75, "command": 92, "politics": 60, "charm": 85 }
  }
]
```

### 4.2 技能配置表

```json
// config/skills.json
[
  {
    "id": "roar",
    "name": "咆哮",
    "type": "active",
    "cost": { "mp": 30, "cooldown": 2 },
    "effects": [{ "type": "damage", "value": 150, "range": { "type": "area", "radius": 2 } }]
  }
]
```

### 4.3 兵种配置表

```json
// config/troops.json
[
  {
    "id": "infantry",
    "name": "步兵",
    "strongAgainst": ["cavalry"],
    "weakAgainst": ["archer"],
    "stats": { "attack": 1.0, "defense": 1.2, "speed": 0.8 }
  }
]
```

### 4.4 地图配置表

```json
// config/maps.json
{
  "chapter1": {
    "name": "黄巾之乱",
    "nodes": [
      { "id": "zhuo_commandery", "name": "涿郡", "type": "city", "position": { "x": 100, "y": 200 } }
    ],
    "edges": [
      { "from": "zhuo_commandery", "to": "ye_city", "distance": 5, "terrain": "plain" }
    ]
  }
}
```

---

## 五、项目目录结构

```
Sanguo/
├── src/
│   ├── core/                    # 核心系统
│   │   ├── engine.ts           # 游戏引擎入口
│   │   ├── event-bus.ts        # 事件总线
│   │   ├── save-manager.ts     # 存档管理器
│   │   ├── config-loader.ts    # 配置加载器
│   │   └── random.ts           # 随机数生成器
│   │
│   ├── entities/                # ECS实体和组件
│   │   ├── components/
│   │   │   ├── stats.ts        # 属性组件
│   │   │   ├── skills.ts       # 技能组件
│   │   │   ├── troops.ts       # 兵种组件
│   │   │   └── position.ts     # 位置组件
│   │   ├── character.ts        # 武将实体
│   │   ├── troop.ts            # 军队实体
│   │   └── city.ts             # 城池实体
│   │
│   ├── systems/                 # 游戏系统
│   │   ├── combat-system.ts    # 战斗系统
│   │   ├── map-system.ts       # 地图系统
│   │   ├── internal-affair.ts  # 内政系统
│   │   ├── ai-system.ts        # AI系统
│   │   └── bond-system.ts      # 羁绊系统
│   │
│   ├── ui/                      # UI系统
│   │   ├── screens/
│   │   │   ├── map-screen.ts
│   │   │   ├── battle-screen.ts
│   │   │   └── city-screen.ts
│   │   ├── components/
│   │   │   ├── button.ts
│   │   │   ├── panel.ts
│   │   │   └── tooltip.ts
│   │   └── ui-manager.ts
│   │
│   ├── scenes/                  # Phaser场景
│   │   ├── boot-scene.ts       # 加载场景
│   │   ├── menu-scene.ts       # 菜单场景
│   │   ├── map-scene.ts        # 地图场景
│   │   └── battle-scene.ts     # 战斗场景
│   │
│   └── utils/                   # 工具函数
│       ├── pathfinding.ts      # 路径查找
│       ├── damage-calc.ts      # 伤害计算
│       └── helpers.ts
│
├── config/                      # 配置表 (JSON)
│   ├── characters.json         # 武将配置
│   ├── skills.json             # 技能配置
│   ├── troops.json             # 兵种配置
│   ├── maps.json               # 地图配置
│   └── items.json              # 物品配置
│
├── assets/                      # 资源文件
│   ├── images/                  # 图片资源
│   │   ├── heroes/             # 武将立绘
│   │   ├── ui/                 # UI元素
│   │   ├── map/                # 地图素材
│   │   └── battle/             # 战斗素材
│   ├── audio/                   # 音频资源
│   │   ├── bgm/                # 背景音乐
│   │   └── sfx/                # 音效
│   └── data/                    # 数据文件
│
├── public/                      # 静态资源
│   └── index.html
│
├── tests/                       # 测试文件
│   ├── unit/                   # 单元测试
│   └── integration/            # 集成测试
│
├── package.json
├── tsconfig.json
├── webpack.config.js
└── README.md
```

---

## 六、扩展指南

### 6.1 添加新武将
1. 在 `config/characters.json` 中添加武将数据
2. 准备武将立绘，放入 `assets/images/heroes/`
3. 如需特殊技能，在 `config/skills.json` 添加技能配置
4. 游戏自动加载新配置

### 6.2 添加新技能
1. 在 `config/skills.json` 定义技能效果
2. 在 `src/systems/combat-system.ts` 实现技能逻辑（如需要）
3. 配置技能图标和动画素材
4. 测试技能效果

### 6.3 添加新地图
1. 在 `config/maps.json` 定义地图节点和连接
2. 准备地图素材，更新 `src/scenes/map-scene.ts`
3. 配置地图事件和剧情
4. 测试路径查找和移动

### 6.4 调整游戏平衡
1. 修改武将成长率：调整 `characters.json` 中的 `growth` 值
2. 修改兵种克制：调整 `troops.json` 中的克制关系
3. 修改伤害公式：调整 `src/utils/damage-calc.ts` 中的系数
4. 修改内政效率：调整 `config/cities.json` 中的产出值

### 6.5 添加新功能
1. 新系统：在 `src/systems/` 创建新系统
2. 新组件：在 `src/entities/components/` 添加新组件
3. 新UI：在 `src/ui/` 创建新界面
4. 注册事件：在 `GameEvents` 枚举中添加事件类型
5. 在配置表中添加相关配置

---

## 七、开发规范

### 7.1 命名规范
- 文件名：kebab-case (如 `combat-system.ts`)
- 类名：PascalCase (如 `BattleStateMachine`)
- 变量/函数：camelCase (如 `calculateDamage`)
- 常量：UPPER_SNAKE_CASE (如 `MAX_LEVEL`)
- 枚举：PascalCase (如 `BattleState`)

### 7.2 代码规范
- 使用 TypeScript 严格模式
- 所有公开API必须有类型定义
- 配置数据与逻辑分离
- 使用事件系统解耦各模块
- 关键逻辑添加注释

### 7.3 版本控制
- 主版本：重大架构改动
- 次版本：新功能添加
- 修订版：Bug修复和平衡性调整

---

## 八、后续开发计划

> **开发优先级说明**: 地图系统作为游戏的核心展示界面，优先开发，用于验证游戏框架和视觉效果。

### 阶段零：地图展示系统（优先开发）
- [x] 游戏框架文档设计
- [ ] 项目初始化和环境搭建
- [ ] Phaser 3 基础配置
- [ ] 世界地图数据结构设计
- [ ] 地图渲染与交互
  - [ ] 城池节点显示
  - [ ] 连接线绘制
  - [ ] 点击选中交互
  - [ ] 相机缩放与拖拽
- [ ] 势力颜色区分
- [ ] 战争迷雾效果
- [ ] 小地图组件

### 阶段一：核心框架
- [ ] 核心系统（事件总线、配置加载）
- [ ] ECS框架搭建
- [ ] 存档系统基础
- [ ] 基础UI框架

### 阶段二：武将系统
- [ ] 武将数据结构和组件
- [ ] 技能系统
- [ ] 兵种系统
- [ ] 羁绊系统

### 阶段三：战斗系统
- [ ] 战斗状态机
- [ ] 命令系统
- [ ] 伤害计算
- [ ] AI逻辑

### 阶段四：内政系统
- [ ] 内政指令
- [ ] 城池管理
- [ ] 资源系统
- [ ] 建筑系统

### 阶段五：完善和优化
- [ ] 存档系统完善
- [ ] UI美化
- [ ] 音效和音乐
- [ ] 性能优化
- [ ] 测试和Bug修复

---

## 九、技术要点

### 9.1 性能优化
- 使用对象池管理频繁创建的对象（投射物、特效）
- 瓦片地图使用分块加载
- AI计算使用 Web Worker
- 资源懒加载和预加载结合

### 9.2 数据安全
- 存档数据添加校验和
- 配置文件使用版本号管理
- 敏感数据加密存储

### 9.3 可扩展性
- 所有游戏数据配置化
- 使用插件架构设计核心系统
- 事件驱动实现模块解耦
- 提供模组(MOD)接口

---

**文档版本**: 1.1  
**创建日期**: 2026-04-13  
**最后更新**: 2026-04-13  
**维护者**: Qoder

---

## 附录：快速启动地图开发

### A.1 最小可运行版本

仅需以下文件即可运行地图展示:

```
Sanguo/
├── public/
│   └── index.html              # 入口HTML
├── src/
│   ├── main.ts                 # 入口文件
│   ├── scenes/
│   │   └── MapScene.ts         # 地图场景
│   └── config/
│       └── map-data.ts         # 地图配置数据
├── package.json
└── vite.config.ts              # Vite配置
```

### A.2 快速验证清单

1. 创建 `public/index.html`
2. 配置 `package.json` + TypeScript + Phaser
3. 创建基础 `MapScene` 场景
4. 添加测试地图数据（3-5个城池节点）
5. 运行 `npm run dev` 验证地图显示

### A.3 示例地图数据

```typescript
// src/config/map-data.ts
export const TEST_MAP_DATA = {
  nodes: [
    { id: 'zhuo', name: '涿郡', x: 200, y: 300, faction: 'player', level: 1 },
    { id: 'ye', name: '邺城', x: 400, y: 250, faction: 'enemy', level: 2 },
    { id: 'luoyang', name: '洛阳', x: 600, y: 350, faction: 'neutral', level: 3 },
  ],
  edges: [
    { from: 'zhuo', to: 'ye' },
    { from: 'ye', to: 'luoyang' },
  ]
};
```
