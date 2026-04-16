using System;
using System.Collections.Generic;
using System.Linq;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.Generals;

namespace CatSanguo.Core;

/// <summary>
/// 历史武将登录管理器
/// 根据剧本年份和武将的 AppearYear 判断武将是否已登场
/// </summary>
public class GeneralAppearanceManager
{
    private readonly List<GeneralData> _allGenerals;

    public GeneralAppearanceManager(List<GeneralData> allGenerals)
    {
        _allGenerals = allGenerals;
    }

    /// <summary>
    /// 检查武将在指定年份是否已登场
    /// </summary>
    public bool IsGeneralAppeared(string generalId, int currentYear)
    {
        var general = _allGenerals.FirstOrDefault(g => g.Id == generalId);
        if (general == null) return false;

        return currentYear >= general.AppearYear;
    }

    /// <summary>
    /// 获取当前年份已登场的所有武将
    /// </summary>
    public List<GeneralData> GetAppearedGenerals(int currentYear)
    {
        return _allGenerals
            .Where(g => currentYear >= g.AppearYear)
            .ToList();
    }

    /// <summary>
    /// 获取当前年份未登场但即将登场的武将（未来5年内）
    /// </summary>
    public List<GeneralData> GetUpcomingGenerals(int currentYear, int yearsAhead = 5)
    {
        return _allGenerals
            .Where(g => g.AppearYear > currentYear && g.AppearYear <= currentYear + yearsAhead)
            .OrderBy(g => g.AppearYear)
            .ToList();
    }

    /// <summary>
    /// 获取在指定城市登场的武将
    /// </summary>
    public List<GeneralData> GetGeneralsAppearingInCity(string cityId)
    {
        return _allGenerals
            .Where(g => g.AppearCityId == cityId)
            .ToList();
    }

    /// <summary>
    /// 根据剧本启动时，筛选出应登场的武将
    /// </summary>
    public List<General> FilterAppearedGeneralsForScenario(ScenarioData scenario)
    {
        int startYear = scenario.StartDate.Year;
        var appearedIds = GetAppearedGenerals(startYear).Select(g => g.Id).ToHashSet();

        var result = new List<General>();
        foreach (var generalData in _allGenerals)
        {
            if (appearedIds.Contains(generalData.Id))
            {
                result.Add(General.FromData(generalData));
            }
        }

        return result;
    }

    /// <summary>
    /// 获取武将的登场信息
    /// </summary>
    public string GetAppearanceInfo(string generalId)
    {
        var general = _allGenerals.FirstOrDefault(g => g.Id == generalId);
        if (general == null) return "未知武将";

        var gs = GameState.Instance;
        int currentYear = gs.CurrentDate.Year;

        if (currentYear >= general.AppearYear)
        {
            return $"已登场（{general.AppearYear}年，{general.AppearCityId}）";
        }
        else
        {
            int yearsUntil = general.AppearYear - currentYear;
            return $"{yearsUntil}年后登场（{general.AppearYear}年，{general.AppearCityId}）";
        }
    }

    /// <summary>
    /// 检查是否有新武将应该在本回合登场
    /// </summary>
    public List<GeneralData> CheckNewAppearances(int previousYear, int currentYear)
    {
        return _allGenerals
            .Where(g => g.AppearYear > previousYear && g.AppearYear <= currentYear)
            .ToList();
    }
}
