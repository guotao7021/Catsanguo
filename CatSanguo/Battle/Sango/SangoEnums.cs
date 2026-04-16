namespace CatSanguo.Battle.Sango;

/// <summary>三国群英传2风格战斗阶段</summary>
public enum SangoBattlePhase
{
    Deploy,     // 部署武将
    Countdown,  // 倒计时
    Charge,     // 双方士兵对冲
    Melee,          // 混战 (过渡状态)
    RoundCommand,   // 回合指令阶段 (暂停, 玩家下令)
    RoundExecution, // 回合执行阶段 (自动战斗)
    Duel,           // 武将单挑 (子阶段)
    Result          // 结算
}

/// <summary>个体士兵状态</summary>
public enum SoldierState
{
    Idle,       // 待机
    Charging,   // 冲锋中
    Fighting,   // 近战中
    Shooting,   // 远程射击 (弓兵)
    Dying,      // 播放死亡动画
    Dead        // 已死亡
}

/// <summary>武将带兵单位状态</summary>
public enum GeneralUnitState
{
    Deploying,      // 部署中
    Charging,       // 冲锋
    InCombat,       // 混战中
    CastingSkill,   // 释放武将技
    InDuel,         // 单挑中
    Retreating,     // 溃败撤退
    Defeated        // 已败
}
