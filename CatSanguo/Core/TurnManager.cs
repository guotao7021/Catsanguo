using System;
using System.Linq;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Core;

/// <summary>
/// 回合制时间管理器
/// 负责推进游戏时间（10天一回合），触发月末/季末事件
/// </summary>
public class TurnManager
{
    private readonly EventBus _eventBus;

    public TurnManager(EventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// 结束当前回合，推进到下一天
    /// </summary>
    public void EndTurn()
    {
        var gs = GameState.Instance;
        gs.TurnNumber++;

        // 重置所有城池的回合行动状态
        foreach (var cp in gs.GetAllCityProgress())
            cp.ResetTurnActions();

        // 推进日期（+10天，即一旬）
        var oldDate = gs.CurrentDate;
        gs.CurrentDate.AddDays(GameSettings.DaysPerTurn);

        // 触发回合结束事件
        _eventBus.Publish(new GameEvent(GameEventType.OnTurnEnd));

        // 城池驻军粮草消耗（先消耗再产出）
        ConsumeGarrisonGrain();

        // 建筑回合制产出
        ProduceBuildingResources();

        // 征兵处理
        ProcessRecruitment();

        // TODO: 处理远程策反任务
        // gs.ProcessSabotageMissions();

        // 检查是否跨月
        if (gs.CurrentDate.IsMonthEnd || gs.CurrentDate.Month != oldDate.Month)
        {
            TriggerMonthEnd();
        }

        // 检查是否跨季
        if (gs.CurrentDate.IsQuarterEnd)
        {
            TriggerQuarterEnd();
        }
    }

    /// <summary>
    /// 触发月末事件（经济迭代 + 俸禄扣除）
    /// </summary>
    private void TriggerMonthEnd()
    {
        _eventBus.Publish(new GameEvent(GameEventType.OnMonthEnd));

        var gs = GameState.Instance;

        // 遍历所有己方城池，执行月度经济结算
        foreach (var cityId in gs.OwnedCityIds)
        {
            var cityProgress = gs.GetCityProgress(cityId);
            if (cityProgress == null) continue;

            // 俸禄扣除（使用武将的实际Salary字段）
            int totalSalary = 0;
            foreach (var genId in cityProgress.GeneralIds)
            {
                var genProgress = gs.GetGeneralProgress(genId);
                if (genProgress != null)
                {
                    // 使用武将配置的俸禄值
                    totalSalary += genProgress.Data.Salary;
                }
            }

            // 扣除俸禄
            if (totalSalary > 0 && cityProgress.Gold >= totalSalary)
            {
                cityProgress.Gold -= totalSalary;
                System.Diagnostics.Debug.WriteLine($"[TurnManager] 城池{cityId}扣除俸禄{totalSalary}金币");
            }
            else if (totalSalary > 0)
            {
                // 金币不足，扣除忠诚度
                foreach (var genId in cityProgress.GeneralIds)
                {
                    var genProgress = gs.GetGeneralProgress(genId);
                    if (genProgress != null)
                    {
                        // 忠诚度下降幅度基于俸禄缺口比例
                        int loyaltyLoss = Math.Max(2, totalSalary / 10);
                        genProgress.Loyalty = Math.Max(0, genProgress.Loyalty - loyaltyLoss);
                        System.Diagnostics.Debug.WriteLine($"[TurnManager] 武将{genId}忠诚度-{loyaltyLoss}（金币不足）");
                    }
                }
            }

            // 官员加成：内政官增加资源产量
            if (!string.IsNullOrEmpty(cityProgress.InteriorOfficerId))
            {
                var officer = gs.GetGeneralProgress(cityProgress.InteriorOfficerId);
                if (officer != null)
                {
                    // 内政官政治值百分比加成
                    int bonus = officer.Data.Politics / 10;
                    cityProgress.AddResource(ResourceType.Gold, bonus);
                    cityProgress.AddResource(ResourceType.Food, bonus);
                }
            }

            // 军事官增加兵力恢复
            if (!string.IsNullOrEmpty(cityProgress.MilitaryOfficerId))
            {
                var officer = gs.GetGeneralProgress(cityProgress.MilitaryOfficerId);
                if (officer != null)
                {
                    var cd = DataManager.Instance.AllCities.FirstOrDefault(c => c.Id == cityId);
                    int mt = cd?.MaxTroops ?? CityScaleConfig.GetMaxTroops(cd?.CityScale ?? "medium");
                    int troopBonus = officer.Data.Command / 5;
                    cityProgress.CurrentTroops = Math.Min(
                        cityProgress.CurrentTroops + troopBonus,
                        mt
                    );
                }
            }
        }
    }

    /// <summary>
    /// 触发季末事件（军事迭代）
    /// </summary>
    private void TriggerQuarterEnd()
    {
        _eventBus.Publish(new GameEvent(GameEventType.OnQuarterEnd));

        var gs = GameState.Instance;

        // 季度兵力恢复（己方城池）
        foreach (var cityId in gs.OwnedCityIds)
        {
            var cityProgress = gs.GetCityProgress(cityId);
            if (cityProgress == null) continue;

            // 基础恢复 20 兵
            int baseRecovery = 20;

            // 太守统帅加成
            if (!string.IsNullOrEmpty(cityProgress.GovernorId))
            {
                var governor = gs.GetGeneralProgress(cityProgress.GovernorId);
                if (governor != null)
                {
                    baseRecovery += governor.Data.Command / 10;
                }
            }

            var qd = DataManager.Instance.AllCities.FirstOrDefault(c => c.Id == cityId);
            int qMaxTroops = qd?.MaxTroops ?? CityScaleConfig.GetMaxTroops(qd?.CityScale ?? "medium");

            cityProgress.CurrentTroops = Math.Min(
                cityProgress.CurrentTroops + baseRecovery,
                qMaxTroops
            );
        }
    }

    /// <summary>
    /// 获取当前回合显示文本
    /// </summary>
    public string GetTurnDisplayText()
    {
        var gs = GameState.Instance;
        return $"第{gs.TurnNumber}回合 - {gs.CurrentDate.ToDisplayString()}";
    }

    /// <summary>
    /// 城池驻军粮草消耗：每回合按驻军数/10扣粮，粮尽则逃兵
    /// </summary>
    private void ConsumeGarrisonGrain()
    {
        var gs = GameState.Instance;
        foreach (var cityId in gs.OwnedCityIds)
        {
            var cp = gs.GetCityProgress(cityId);
            if (cp == null || cp.CurrentTroops <= 0) continue;

            int grainCost = Math.Max(1, cp.CurrentTroops / 10);
            cp.Grain -= grainCost;

            if (cp.Grain < 0)
            {
                int desertCount = Math.Min(cp.CurrentTroops, Math.Abs(cp.Grain) * 2);
                cp.CurrentTroops -= desertCount;
                cp.Grain = 0;
            }
        }
    }

    /// <summary>
    /// 回合制建筑产出：根据建筑等级和城池规模计算每回合资源
    /// </summary>
    private void ProduceBuildingResources()
    {
        var gs = GameState.Instance;
        foreach (var cityId in gs.OwnedCityIds)
        {
            var cp = gs.GetCityProgress(cityId);
            if (cp == null) continue;

            var cityData = DataManager.Instance.AllCities.FirstOrDefault(c => c.Id == cityId);
            string cityScale = cityData?.CityScale ?? "medium";
            int maxTroops = cityData?.MaxTroops ?? CityScaleConfig.GetMaxTroops(cityScale);

            // 建筑资源产出
            var production = cp.GetTotalProduction(cityScale);
            foreach (var kvp in production)
            {
                if (kvp.Key == ResourceType.Food)
                    cp.Grain += kvp.Value; // Food 产出累加到 Grain
                else
                    cp.AddResource(kvp.Key, kvp.Value);
            }

            // 兵营特殊处理：产兵
            var barracks = cp.GetBuilding("barracks");
            if (barracks != null && barracks.Level > 0)
            {
                int troopGain = barracks.Level * 3;
                cp.CurrentTroops = Math.Min(cp.CurrentTroops + troopGain, maxTroops);
            }
        }
    }

    /// <summary>
    /// 征兵处理：对设置了征兵目标的城池，每回合自动消耗资源征兵
    /// </summary>
    private void ProcessRecruitment()
    {
        var gs = GameState.Instance;
        foreach (var cityId in gs.OwnedCityIds)
        {
            var cp = gs.GetCityProgress(cityId);
            if (cp == null || !cp.IsRecruiting) continue;
            if (cp.CurrentTroops >= cp.RecruitTarget)
            {
                cp.IsRecruiting = false;
                continue;
            }

            // 计算征兵量
            var barracks = cp.GetBuilding("barracks");
            int barracksLevel = barracks?.Level ?? 0;
            int recruitAmount = 10 + barracksLevel * 5;

            // 军事官加成
            if (!string.IsNullOrEmpty(cp.MilitaryOfficerId))
            {
                var officer = gs.GetGeneralProgress(cp.MilitaryOfficerId);
                if (officer != null)
                    recruitAmount += officer.Data.Command / 10;
            }

            // 不超过目标
            int needed = cp.RecruitTarget - cp.CurrentTroops;
            recruitAmount = Math.Min(recruitAmount, needed);

            // 不超过城池上限
            var cityData = DataManager.Instance.AllCities.FirstOrDefault(c => c.Id == cityId);
            int maxTroops = cityData?.MaxTroops ?? CityScaleConfig.GetMaxTroops(cityData?.CityScale ?? "medium");
            recruitAmount = Math.Min(recruitAmount, maxTroops - cp.CurrentTroops);

            if (recruitAmount <= 0) { cp.IsRecruiting = false; continue; }

            // 计算消耗
            int goldCost = recruitAmount * 2;
            int grainCost = recruitAmount;

            // 检查资源是否足够（不够则部分征兵）
            if (cp.Gold < goldCost || cp.Grain < grainCost)
            {
                int maxByGold = cp.Gold / 2;
                int maxByGrain = cp.Grain;
                recruitAmount = Math.Max(0, Math.Min(recruitAmount, Math.Min(maxByGold, maxByGrain)));
                if (recruitAmount <= 0) { cp.IsRecruiting = false; continue; }
                goldCost = recruitAmount * 2;
                grainCost = recruitAmount;
            }

            // 执行征兵
            cp.Gold -= goldCost;
            cp.Grain -= grainCost;
            cp.CurrentTroops += recruitAmount;

            if (cp.CurrentTroops >= cp.RecruitTarget)
                cp.IsRecruiting = false;
        }
    }
}
