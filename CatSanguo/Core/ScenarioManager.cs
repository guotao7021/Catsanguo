using System;
using System.Collections.Generic;
using System.Linq;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Core;

/// <summary>
/// 事件剧本管理器
/// 负责加载剧本数据、选择剧本、选择势力、启动游戏
/// </summary>
public class ScenarioManager
{
    private List<ScenarioData> _allScenarios = new();
    private ScenarioData? _selectedScenario;
    private ScenarioFaction? _selectedFaction;

    public IReadOnlyList<ScenarioData> AllScenarios => _allScenarios.AsReadOnly();
    public ScenarioData? SelectedScenario => _selectedScenario;
    public ScenarioFaction? SelectedFaction => _selectedFaction;

    /// <summary>
    /// 加载所有剧本数据
    /// </summary>
    public void LoadScenarios(string scenariosPath)
    {
        try
        {
            _allScenarios = DataLoader.Load<List<ScenarioData>>(scenariosPath) ?? new();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScenarioManager] Load scenarios failed: {ex.Message}");
            _allScenarios = new();
        }
    }

    /// <summary>
    /// 选择剧本
    /// </summary>
    public bool SelectScenario(string scenarioId)
    {
        var scenario = _allScenarios.FirstOrDefault(s => s.Id == scenarioId);
        if (scenario == null) return false;

        _selectedScenario = scenario;
        _selectedFaction = null;
        return true;
    }

    /// <summary>
    /// 选择势力
    /// </summary>
    public bool SelectFaction(string factionId)
    {
        if (_selectedScenario == null) return false;

        var faction = _selectedScenario.Factions.FirstOrDefault(f => f.FactionId == factionId);
        if (faction == null) return false;

        _selectedFaction = faction;
        return true;
    }

    /// <summary>
    /// 获取当前剧本的可用势力列表
    /// </summary>
    public List<ScenarioFaction> GetAvailableFactions()
    {
        return _selectedScenario?.Factions ?? new();
    }

    /// <summary>
    /// 启动游戏（应用剧本配置）
    /// </summary>
    public bool StartGame(out string errorMsg)
    {
        errorMsg = "";

        if (_selectedScenario == null)
        {
            errorMsg = "未选择剧本";
            return false;
        }

        if (_selectedFaction == null)
        {
            errorMsg = "未选择势力";
            return false;
        }

        var gs = GameState.Instance;

        // 新游戏：清空所有旧存档数据，防止旧数据残留
        gs.ResetForNewGame();

        // 设置游戏状态
        gs.CurrentScenarioId = _selectedScenario.Id;
        gs.PlayerFactionId = _selectedFaction.FactionId;
        gs.TurnNumber = 1;
        gs.CurrentDate = _selectedScenario.StartDate;

        // 初始化所有势力的城池归属（更新 CityData.Owner）
        // 先重置所有城池为中立，再按剧本分配
        var allCities = DataManager.Instance.AllCities;
        foreach (var city in allCities)
        {
            city.Owner = "neutral";
            city.Garrison.Clear();
        }

        foreach (var faction in _selectedScenario.Factions)
        {
            bool isPlayer = faction.FactionId == _selectedFaction.FactionId;
            foreach (var cityId in faction.InitialCityIds)
            {
                var cityData = allCities.FirstOrDefault(c => c.Id == cityId);
                if (cityData != null)
                {
                    cityData.Owner = isPlayer ? "player" : faction.FactionId;
                }

                if (isPlayer)
                {
                    gs.AddOwnedCity(cityId);
                }
            }
        }

        // 设置所有势力武将的 ForceId（先于初始化完成，确保所有武将知道自己属于哪个势力）
        foreach (var faction in _selectedScenario.Factions)
        {
            foreach (var allocation in faction.InitialGenerals)
            {
                var genData = DataManager.Instance.AllGenerals.FirstOrDefault(g => g.Id == allocation.GeneralId);
                if (genData != null)
                    genData.ForceId = faction.FactionId;
            }
        }

        // 初始化所有势力武将（包括玩家和AI）
        var allGenerals = DataManager.Instance.AllGenerals;
        foreach (var faction in _selectedScenario.Factions)
        {
            bool isPlayer = faction.FactionId == _selectedFaction.FactionId;
            foreach (var allocation in faction.InitialGenerals)
            {
                var genData = allGenerals.FirstOrDefault(g => g.Id == allocation.GeneralId);
                if (genData == null) continue;

                var progress = gs.GetGeneralProgress(allocation.GeneralId);
                if (progress == null)
                {
                    // 通过初始化添加
                    gs.Initialize(new List<Data.Schemas.GeneralData> { genData });
                    progress = gs.GetGeneralProgress(allocation.GeneralId);
                }

                // 无论新建或已存在，统一设置剧本分配属性
                if (progress != null)
                {
                    progress.IsUnlocked = true;
                    progress.Status = GeneralStatus.Recruited;
                    progress.Loyalty = allocation.InitialLoyalty;
                    progress.CurrentCityId = allocation.AssignedCityId;
                    progress.Level = allocation.InitialLevel;
                }

                // 将武将分配到起始城池
                if (!string.IsNullOrEmpty(allocation.AssignedCityId))
                {
                    gs.AddGeneralToCity(allocation.AssignedCityId, allocation.GeneralId);
                }
            }
        }

        // 初始化编队（取前3个武将）
        var recruitedGenerals = _selectedFaction.InitialGenerals
            .Take(3)
            .Select(a => a.GeneralId)
            .ToList();
        gs.SetCurrentSquad(recruitedGenerals);

        // 初始化城池资源
        foreach (var cityId in _selectedFaction.InitialCityIds)
        {
            var cityData = DataManager.Instance.AllCities.FirstOrDefault(c => c.Id == cityId);
            if (cityData != null)
            {
                var cityProgress = gs.GetOrCreateCityProgress(cityData);
                cityProgress.Gold = _selectedFaction.StartGold;
                cityProgress.Food = _selectedFaction.StartFood;
                cityProgress.Wood = _selectedFaction.StartFood / 2;
                cityProgress.Iron = _selectedFaction.StartFood / 3;
            }
        }

        gs.Save();
        return true;
    }
}
