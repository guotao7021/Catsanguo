一、Demo目标定义（必须明确）
🎯 Demo必须包含
✅ 一个城市（内政）
✅ 一场战斗（阵容 + 阵形 + AI）
✅ 一套完整战斗表现（动画 + UI）
✅ 战斗结算 → 回到内政
✅ 可循环（游戏Loop）
🎯 Demo流程（用户体验）
进入游戏
 → 收资源
 → 升级建筑
 → 编队
 → 选择阵形
 → 进入战斗
 → 战斗表现
 → 结算奖励
 → 回城
🧱 二、整体架构（运行级）
GameRoot
 ├── GameStateManager（状态机）
 ├── SceneManager（场景控制）
 ├── DataManager（数据）
 ├── SystemManager（系统集合）

--------------------------------

SystemManager
 ├── CitySystem（内政）
 ├── BattleSystem（战斗逻辑）
 ├── CombatPresentation（表现）
 ├── UISystem（UI）
 ├── RewardSystem（奖励）
🧠 三、游戏状态机（核心）
🎯 状态流转
public enum GameState
{
    Login,
    City,
    Battle,
    Result
}
🎯 控制流程
switch (state)
{
    case City:
        EnterCity();
        break;
    case Battle:
        EnterBattle();
        break;
}
🎮 四、核心模块整合
4.1 内政模块（CitySystem）
🎯 功能
资源生产
建筑升级
编队入口
🎯 对外接口
public class CitySystem
{
    public ResourceData GetResources();
    public void UpgradeBuilding(int id);
}
4.2 编队系统（TeamBuilder）
🎯 输入
武将
军种
阵形
🎯 输出
public class BattleTeam
{
    public List<Unit> units;
    public FormationType formation;
}
4.3 战斗系统（BattleSystem）
🎯 逻辑层（无表现）
public class BattleSystem
{
    public void StartBattle(BattleTeam a, BattleTeam b);
    public void Tick(float dt);
}
🎯 输出
CombatEvent流
4.4 表现系统（Presentation）
🎯 接收事件
OnCombatEvent(CombatEvent e)
{
    PlayAnimation(e);
    PlayVFX(e);
    ShowDamage(e);
}
4.5 UI系统（BattleUI）
🎯 功能
技能按钮
血条
阵形切换
4.6 奖励系统（RewardSystem）
🎯 计算奖励
public Reward Calculate(BattleResult result)
{
    return new Reward { gold = 100 };
}
🔄 五、完整数据流（关键）
CitySystem（资源）
   ↓
TeamBuilder（编队）
   ↓
BattleSystem（逻辑）
   ↓
CombatEvent（事件）
   ↓
Presentation（表现）
   ↓
BattleResult（结果）
   ↓
RewardSystem（奖励）
   ↓
CitySystem（资源增加）
⚔️ 六、战斗执行流程（Tick级）
void Update()
{
    battle.Tick(Time.deltaTime);
}
Tick内部
1. AI决策
2. 技能触发
3. Buff更新
4. 伤害计算
5. 生成事件
🎬 七、场景结构（Unity）
🎯 Scene划分
1️⃣ CityScene
建筑UI
资源UI
2️⃣ BattleScene
战场
单位Prefab
战斗UI
🧩 八、Prefab结构（关键）
单位Prefab
Unit
 ├── Model
 ├── Animator
 ├── UnitView
 ├── HPBar
⚙️ 九、关键代码结构（建议）
目录结构
Scripts/
 ├── Core/
 ├── Battle/
 ├── City/
 ├── UI/
 ├── Data/
🧪 十、最小可运行版本（MVP）
🎯 必须实现
内政
金币增长
战斗
3v3战斗
自动攻击
表现
简单动画
飘字
UI
战斗按钮
结算界面
⚡ 十一、性能目标
🎯 Demo要求
30~60 FPS
支持 20~50单位
🧠 十二、调试工具（强烈建议）
🎯 Debug面板
当前状态：Battle
单位数量：20
FPS：60