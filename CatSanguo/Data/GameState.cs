using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Data;

/// <summary>
/// 待处理的出征数据（用于Demo场景传递出征信息）
/// </summary>
public class PendingMarchData
{
    public string SourceCityId { get; set; } = "";
    public string TargetCityId { get; set; } = "";
    public List<string> GeneralIds { get; set; } = new();
    public string LeadGeneralName { get; set; } = "";
    public string LeadFormation { get; set; } = "idle";
}

public class GeneralProgress
{
    public GeneralData Data { get; set; } = new();
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
    public bool IsUnlocked { get; set; } = false;
    public GeneralStatus Status { get; set; } = GeneralStatus.Recruited; // 武将状态
    public int LevelCap { get; set; } = 50;
    
    // 新字段：忠诚度、当前所在城池、是否出征
    public int Loyalty { get; set; } = 70;
    public string CurrentCityId { get; set; } = "";
    public bool IsOnExpedition { get; set; } = false;
    
    // 装备系统
    public Dictionary<string, string> EquippedItems { get; set; } = new(); // slot -> equipmentId
    // 技能系统
    public List<string> LearnedSkillIds { get; set; } = new();
    public string ActiveSkillSlot { get; set; } = ""; // overrides Data.ActiveSkillId
    public string PassiveSkillSlot { get; set; } = ""; // overrides Data.PassiveSkillId
    public Dictionary<string, int> SkillLevels { get; set; } = new(); // skillId -> level
    // 技能树
    public int SkillPoints { get; set; } = 0;
    public List<string> UnlockedSkillTreeNodes { get; set; } = new();
    
    // 升级加点: 每次升级选择一项属性+1
    public Dictionary<string, int> BonusStats { get; set; } = new(); // statType -> bonus points

    public int LevelUpCost => Level * 50;
    public int XpToNextLevel => Level * 100;
    public float XpProgressRatio => Level >= LevelCap ? 1f : (float)Experience / XpToNextLevel;
    public bool CanLevelUp => Experience >= XpToNextLevel && Level < LevelCap;

    public float GetEffectiveStat(int baseStat, string statType = "")
    {
        int bonus = 0;
        if (!string.IsNullOrEmpty(statType) && BonusStats.TryGetValue(statType, out var b))
            bonus = b;
        return (baseStat + bonus) * (1f + (Level - 1) * 0.03f);
    }
    
    public int GetEffectiveStatWithEquipment(int baseStat, string statType, List<EquipmentData> allEquipment)
    {
        float stat = GetEffectiveStat(baseStat);
        
        // Add equipment bonuses
        foreach (var kvp in EquippedItems)
        {
            var equipId = kvp.Value;
            var equip = allEquipment.FirstOrDefault(e => e.Id == equipId);
            if (equip != null && equip.StatType == statType)
            {
                stat += equip.StatBonus;
            }
        }
        
        return (int)stat;
    }
    
    public string GetActiveSkillId()
    {
        return string.IsNullOrEmpty(ActiveSkillSlot) ? Data.ActiveSkillId : ActiveSkillSlot;
    }
    
    public string GetPassiveSkillId()
    {
        return string.IsNullOrEmpty(PassiveSkillSlot) ? Data.PassiveSkillId : PassiveSkillSlot;
    }
}

/// <summary>远程策反任务（说服其他城池的武将，消耗多个回合）</summary>
public class SabotageMission
{
    public string OfficerId { get; set; } = "";       // 执行策反的武将ID
    public string TargetGeneralId { get; set; } = ""; // 目标武将ID
    public string SourceCityId { get; set; } = "";    // 出发城池
    public string TargetCityId { get; set; } = "";    // 目标城池
    public int RemainingTurns { get; set; }            // 剩余回合数
    public int SuccessRate { get; set; }               // 预计成功率
}

public class CityProgress
{
    public string CityId { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Population { get; set; }
    public int Grain { get; set; }
    public int CurrentTroops { get; set; }
    public long LastProductionTickMs { get; set; }
    public List<string> GeneralIds { get; set; } = new(); // 城池驻扎的武将

    // ==================== 内政系统扩展 ====================
    // 资源存储
    public int Gold { get; set; } = 0;
    public int Food { get; set; } = 0;
    public int Wood { get; set; } = 0;
    public int Iron { get; set; } = 0;

    // 资源上限
    public int GoldCap { get; set; } = 9999;
    public int FoodCap { get; set; } = 9999;
    public int WoodCap { get; set; } = 9999;
    public int IronCap { get; set; } = 9999;

    // 建筑数据：buildingId -> Building
    public Dictionary<string, Building> Buildings { get; set; } = new();

    // ==================== 官员任命系统 (新) ====================
    public string GovernorId { get; set; } = "";           // 太守
    public string InteriorOfficerId { get; set; } = "";    // 内政官
    public string MilitaryOfficerId { get; set; } = "";    // 军事官
    public string SearchOfficerId { get; set; } = "";      // 搜索官

    // ==================== 征兵系统 ====================
    public int RecruitTarget { get; set; } = 0;    // 目标兵力
    public bool IsRecruiting { get; set; } = false; // 是否正在征兵

    // ==================== 回合行动限制 ====================
    public bool DiscoverUsedThisTurn { get; set; } = false;  // 本回合是否已发现人才
    public bool PersuadeUsedThisTurn { get; set; } = false;  // 本回合是否已说服人才
    public HashSet<string> ActedGeneralsThisTurn { get; set; } = new(); // 本回合已行动的武将

    public void ResetTurnActions()
    {
        DiscoverUsedThisTurn = false;
        PersuadeUsedThisTurn = false;
        ActedGeneralsThisTurn.Clear();
    }

    // 获取资源当前值
    public int GetResource(ResourceType type) => type switch
    {
        ResourceType.Gold => Gold,
        ResourceType.Food => Food,
        ResourceType.Wood => Wood,
        ResourceType.Iron => Iron,
        _ => 0
    };

    // 获取资源上限
    public int GetResourceCap(ResourceType type) => type switch
    {
        ResourceType.Gold => GoldCap,
        ResourceType.Food => FoodCap,
        ResourceType.Wood => WoodCap,
        ResourceType.Iron => IronCap,
        _ => 9999
    };

    // 增加资源
    public void AddResource(ResourceType type, int amount)
    {
        switch (type)
        {
            case ResourceType.Gold:
                Gold = System.Math.Min(Gold + amount, GoldCap);
                break;
            case ResourceType.Food:
                Food = System.Math.Min(Food + amount, FoodCap);
                break;
            case ResourceType.Wood:
                Wood = System.Math.Min(Wood + amount, WoodCap);
                break;
            case ResourceType.Iron:
                Iron = System.Math.Min(Iron + amount, IronCap);
                break;
        }
    }

    // 消耗资源，返回是否成功
    public bool ConsumeResource(ResourceType type, int amount)
    {
        int current = GetResource(type);
        if (current < amount) return false;

        switch (type)
        {
            case ResourceType.Gold:
                Gold -= amount;
                break;
            case ResourceType.Food:
                Food -= amount;
                break;
            case ResourceType.Wood:
                Wood -= amount;
                break;
            case ResourceType.Iron:
                Iron -= amount;
                break;
        }
        return true;
    }

    // 获取建筑
    public Building? GetBuilding(string buildingId)
    {
        return Buildings.TryGetValue(buildingId, out var b) ? b : null;
    }

    // 获取或创建建筑
    public Building GetOrCreateBuilding(string buildingId)
    {
        if (!Buildings.TryGetValue(buildingId, out var building))
        {
            var config = InteriorConfig.GetBuildingConfig(buildingId);
            building = new Building
            {
                Id = buildingId,
                Name = config?.Name ?? buildingId,
                Type = config?.Type ?? BuildingType.Resource,
                Level = 1,
                MaxLevel = config?.MaxLevel ?? 10
            };
            Buildings[buildingId] = building;
        }
        return building;
    }

    // 升级建筑
    public bool UpgradeBuilding(string buildingId, out string errorMsg)
    {
        errorMsg = "";
        var config = InteriorConfig.GetBuildingConfig(buildingId);
        if (config == null)
        {
            errorMsg = "建筑配置不存在";
            return false;
        }

        var building = GetOrCreateBuilding(buildingId);
        if (building.Level >= building.MaxLevel)
        {
            errorMsg = "建筑已达满级";
            return false;
        }

        int goldCost = InteriorConfig.CalculateUpgradeCost(config.GoldUpgradeCost, building.Level + 1);
        int foodCost = InteriorConfig.CalculateUpgradeCost(config.FoodUpgradeCost, building.Level + 1);
        int woodCost = InteriorConfig.CalculateUpgradeCost(config.WoodUpgradeCost, building.Level + 1);
        int ironCost = InteriorConfig.CalculateUpgradeCost(config.IronUpgradeCost, building.Level + 1);

        if (Gold < goldCost || Food < foodCost || Wood < woodCost || Iron < ironCost)
        {
            errorMsg = "资源不足";
            return false;
        }

        Gold -= goldCost;
        Food -= foodCost;
        Wood -= woodCost;
        Iron -= ironCost;

        building.Level++;
        return true;
    }

    // 获取建筑总产量
    public Dictionary<ResourceType, int> GetTotalProduction(string cityScale)
    {
        var production = new Dictionary<ResourceType, int>();
        foreach (var kvp in Buildings)
        {
            var config = InteriorConfig.GetBuildingConfig(kvp.Key);
            if (config != null && config.ProducesResource != ResourceType.Population)
            {
                int prod = InteriorConfig.CalculateProduction(config, kvp.Value.Level, cityScale);
                if (production.ContainsKey(config.ProducesResource))
                    production[config.ProducesResource] += prod;
                else
                    production[config.ProducesResource] = prod;
            }
        }
        return production;
    }
}

public class GameState
{
    private static GameState? _instance;
    public static GameState Instance
    {
        get
        {
            _instance ??= new GameState();
            return _instance;
        }
    }

    private readonly Dictionary<string, GeneralProgress> _playerGenerals = new();
    private readonly List<string> _ownedCityIds = new();
    private readonly Dictionary<string, CityProgress> _cityProgress = new();
    private readonly List<string> _completedStageIds = new();
    private List<string> _currentSquad = new();
    private List<ArmySaveEntry> _armies = new();

    public int BattleMerit { get; private set; }
    public IReadOnlyList<string> OwnedCityIds => _ownedCityIds.AsReadOnly();
    public IReadOnlyList<string> CompletedStageIds => _completedStageIds.AsReadOnly();
    public IReadOnlyList<string> CurrentSquad => _currentSquad.AsReadOnly();
    /// <summary>当前编队出征的武将配置（兵种、阵型、士兵数）</summary>
    public List<GeneralDeployEntry> CurrentDeployConfigs { get; set; } = new();
    public IReadOnlyList<ArmySaveEntry> SavedArmies => _armies.AsReadOnly();

    // ==================== 回合制与新系统字段 (新) ====================
    public Data.Schemas.GameDate CurrentDate { get; set; } = new Data.Schemas.GameDate(184, 1, 1);
    public string CurrentScenarioId { get; set; } = "";
    public string PlayerFactionId { get; set; } = "";
    public int TurnNumber { get; set; } = 1;
    public List<string> CaptiveGeneralIds { get; set; } = new();
    
    // 远程策反任务列表
    public List<SabotageMission> ActiveMissions { get; set; } = new();

    // 数据缓存
    private List<EquipmentData> _allEquipment = new();
    private List<BondData> _allBonds = new();
    private List<SkillTreeData> _allSkillTrees = new();
    
    public List<EquipmentData> AllEquipment => _allEquipment;
    public List<BondData> AllBonds => _allBonds;
    public List<SkillTreeData> AllSkillTrees => _allSkillTrees;

    private const int CityProductionInterval = 30; // seconds
    private const int MaxCityLevel = 5;
    private const int MaxWallLevel = 5;
    private const int MaxGrainCap = 9999;

    private string SavePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "game_state.json");
    private const int MaxSaveSlots = 6;

    private string GetSlotPath(int slot) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", $"save_slot_{slot}.json");

    /// <summary>获取所有存档槽位信息</summary>
    public List<SaveSlotInfo> GetSaveSlotInfos()
    {
        var list = new List<SaveSlotInfo>();
        for (int i = 1; i <= MaxSaveSlots; i++)
        {
            var path = GetSlotPath(i);
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<GameStateSaveData>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (data != null)
                    {
                        list.Add(new SaveSlotInfo
                        {
                            SlotIndex = i,
                            IsEmpty = false,
                            ScenarioId = data.CurrentScenarioId,
                            FactionId = data.PlayerFactionId,
                            TurnNumber = data.TurnNumber,
                            CityCount = data.OwnedCityIds.Count,
                            GameDateYear = data.CurrentDateYear,
                            GameDateMonth = data.CurrentDateMonth,
                            SaveTime = File.GetLastWriteTime(path)
                        });
                        continue;
                    }
                }
                catch { }
            }
            list.Add(new SaveSlotInfo { SlotIndex = i, IsEmpty = true });
        }
        return list;
    }

    /// <summary>保存到指定槽位</summary>
    public bool SaveToSlot(int slot)
    {
        if (slot < 1 || slot > MaxSaveSlots) return false;
        try
        {
            var data = BuildSaveData();
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            });
            var dir = Path.GetDirectoryName(GetSlotPath(slot));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(GetSlotPath(slot), json);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameState] SaveToSlot({slot}) failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>从指定槽位加载</summary>
    public bool LoadFromSlot(int slot)
    {
        if (slot < 1 || slot > MaxSaveSlots) return false;
        var path = GetSlotPath(slot);
        if (!File.Exists(path)) return false;
        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<GameStateSaveData>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (data != null)
            {
                ApplySaveData(data);
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameState] LoadFromSlot({slot}) failed: {ex.Message}");
        }
        return false;
    }

    /// <summary>删除指定槽位</summary>
    public bool DeleteSlot(int slot)
    {
        if (slot < 1 || slot > MaxSaveSlots) return false;
        var path = GetSlotPath(slot);
        if (File.Exists(path))
        {
            File.Delete(path);
            return true;
        }
        return false;
    }

    public void Initialize(List<GeneralData> allGenerals)
    {
        foreach (var gen in allGenerals)
        {
            if (!_playerGenerals.ContainsKey(gen.Id))
            {
                _playerGenerals[gen.Id] = new GeneralProgress
                {
                    Data = gen,
                    IsUnlocked = true, // Default all unlocked for demo
                    Level = 1,
                    Experience = 0
                };
            }
        }

        if (_currentSquad.Count == 0)
        {
            _currentSquad = allGenerals.Take(3).Select(g => g.Id).ToList();
        }

        Load();
    }

    public void InitializeForDemo(List<CityData> allCities)
    {
        // 1. 找到第一个 player 城池
        var playerCity = allCities.FirstOrDefault(c => c.Owner == "player") ?? allCities.FirstOrDefault();
        if (playerCity != null)
        {
            if (!_ownedCityIds.Contains(playerCity.Id))
                _ownedCityIds.Add(playerCity.Id);

            // 2. 初始化城池进度
            var cp = GetOrCreateCityProgress(playerCity);
            if (cp.Gold < 500) cp.Gold = 500;
            if (cp.Food < 300) cp.Food = 300;
            if (cp.Wood < 200) cp.Wood = 200;
            if (cp.Iron < 100) cp.Iron = 100;
        }

        // 3. 确保有编队
        var unlocked = GetAllUnlockedGenerals();
        if (_currentSquad.Count == 0 && unlocked.Count > 0)
            _currentSquad = unlocked.Take(3).Select(g => g.Data.Id).ToList();

        // 4. 初始战功
        if (BattleMerit < 200) BattleMerit = 200;

        Save();
    }

    public GeneralProgress? GetGeneralProgress(string generalId)
    {
        return _playerGenerals.TryGetValue(generalId, out var progress) ? progress : null;
    }

    public List<GeneralProgress> GetAllUnlockedGenerals()
    {
        return _playerGenerals.Values.Where(g => g.IsUnlocked).ToList();
    }

    public List<GeneralData> GetCurrentSquadData(List<GeneralData> allGenerals)
    {
        var result = new List<GeneralData>();
        foreach (var id in _currentSquad)
        {
            var baseData = allGenerals.FirstOrDefault(g => g.Id == id);
            var progress = GetGeneralProgress(id);
            if (baseData != null && progress != null)
            {
                var leveled = new GeneralData
                {
                    Id = baseData.Id,
                    Name = baseData.Name,
                    Title = baseData.Title,
                    Strength = (int)progress.GetEffectiveStat(baseData.Strength),
                    Intelligence = (int)progress.GetEffectiveStat(baseData.Intelligence),
                    Leadership = (int)progress.GetEffectiveStat(baseData.Leadership),
                    Speed = (int)progress.GetEffectiveStat(baseData.Speed),
                    ActiveSkillId = baseData.ActiveSkillId,
                    PassiveSkillId = baseData.PassiveSkillId,
                    PreferredFormation = baseData.PreferredFormation,
                    Level = progress.Level,
                    Experience = progress.Experience
                };
                result.Add(leveled);
            }
        }
        return result;
    }

    public void AddBattleMerit(int amount)
    {
        BattleMerit += amount;
    }

    private static readonly HashSet<string> ValidStatTypes = new()
    {
        "strength", "intelligence", "command", "politics", "charisma"
    };
    
    public bool TryLevelUpGeneral(string generalId, string statType)
    {
        if (!ValidStatTypes.Contains(statType)) return false;
        if (!_playerGenerals.TryGetValue(generalId, out var progress)) return false;
        if (progress.Level >= progress.LevelCap) return false;
        int cost = progress.LevelUpCost;
        if (BattleMerit < cost) return false;

        BattleMerit -= cost;
        progress.Level++;
        progress.BonusStats[statType] = progress.BonusStats.GetValueOrDefault(statType) + 1;
        Save();
        return true;
    }

    public void SetCurrentSquad(List<string> generalIds)
    {
        _currentSquad = generalIds.Take(3).ToList();
        Save();
    }

    public bool OwnsCity(string cityId)
    {
        return _ownedCityIds.Contains(cityId);
    }

    public void AddOwnedCity(string cityId)
    {
        if (!_ownedCityIds.Contains(cityId))
        {
            _ownedCityIds.Add(cityId);
            Save();
        }
    }

    /// <summary>
    /// 新游戏开始时重置所有状态（清除旧存档数据）
    /// </summary>
    public void ResetForNewGame()
    {
        _ownedCityIds.Clear();
        _completedStageIds.Clear();
        _currentSquad.Clear();
        _cityProgress.Clear();
        _armies.Clear();
        BattleMerit = 0;
        CurrentDate = new Data.Schemas.GameDate(184, 1, 1);
        CurrentScenarioId = "";
        PlayerFactionId = "";
        TurnNumber = 1;
        CaptiveGeneralIds = new();
        CurrentDeployConfigs = new();

        // 重置所有武将状态
        foreach (var kv in _playerGenerals)
        {
            kv.Value.Status = GeneralStatus.Available;
            kv.Value.Level = 1;
            kv.Value.Experience = 0;
            kv.Value.Loyalty = 50;
            kv.Value.CurrentCityId = "";
            kv.Value.IsOnExpedition = false;
            kv.Value.IsUnlocked = false;
        }
    }

    public CityProgress? GetCityProgress(string cityId)
    {
        return _cityProgress.TryGetValue(cityId, out var progress) ? progress : null;
    }

    public CityProgress GetOrCreateCityProgress(CityData cityData)
    {
        if (_cityProgress.TryGetValue(cityData.Id, out var progress))
            return progress;

        // 根据城池规模设置资源上限
        int baseCap = cityData.CityScale switch
        {
            "small" => 1000,
            "medium" => 3000,
            "large" => 6000,
            "huge" => 10000,
            _ => 3000
        };

        progress = new CityProgress
        {
            CityId = cityData.Id,
            Level = 1,
            Population = cityData.Population,
            Grain = cityData.Grain,
            CurrentTroops = cityData.MaxTroops,
            LastProductionTickMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            // 初始资源（根据城池规模）
            Gold = baseCap / 2,
            Food = baseCap,
            Wood = baseCap / 2,
            Iron = baseCap / 3,
            // 资源上限
            GoldCap = baseCap * 2,
            FoodCap = baseCap * 3,
            WoodCap = baseCap * 2,
            IronCap = baseCap
        };

        // 如果是己方城池，初始化基础建筑
        if (cityData.Owner == "player")
        {
            // 添加当前编队武将
            if (_currentSquad.Count > 0)
            {
                progress.GeneralIds = _currentSquad.ToList();
            }

            // 初始化基础建筑
            foreach (var config in InteriorConfig.Buildings)
            {
                progress.Buildings[config.Id] = new Building
                {
                    Id = config.Id,
                    Name = config.Name,
                    Type = config.Type,
                    Level = 1,
                    MaxLevel = config.MaxLevel
                };
            }
        }

        _cityProgress[cityData.Id] = progress;
        return progress;
    }

    public List<CityProgress> GetAllCityProgress()
    {
        return _cityProgress.Values.ToList();
    }

    // ==================== 城池武将管理 ====================

    public List<string> GetCityGenerals(string cityId)
    {
        var progress = GetCityProgress(cityId);
        if (progress != null)
        {
            // 双重验证：同时检查 CityProgress.GeneralIds 和 GeneralProgress.CurrentCityId
            return progress.GeneralIds
                .Where(genId => GetGeneralProgress(genId)?.CurrentCityId == cityId)
                .ToList();
        }
        return new List<string>();
    }

    public void SetCityGenerals(string cityId, List<string> generalIds)
    {
        var progress = GetOrCreateCityProgress(new CityData { Id = cityId });
        progress.GeneralIds = generalIds.ToList();
    }

    public void AddGeneralToCity(string cityId, string generalId)
    {
        var progress = GetOrCreateCityProgress(new CityData { Id = cityId });
        if (!progress.GeneralIds.Contains(generalId))
            progress.GeneralIds.Add(generalId);
    }

    public void RemoveGeneralFromCity(string cityId, string generalId)
    {
        var progress = GetCityProgress(cityId);
        if (progress != null)
            progress.GeneralIds.Remove(generalId);
    }

    // ==================== 内政系统 ====================

    /// <summary>获取城池资源</summary>
    public int GetCityResource(string cityId, ResourceType type)
    {
        var progress = GetCityProgress(cityId);
        return progress?.GetResource(type) ?? 0;
    }

    /// <summary>获取城池资源上限</summary>
    public int GetCityResourceCap(string cityId, ResourceType type)
    {
        var progress = GetCityProgress(cityId);
        return progress?.GetResourceCap(type) ?? 9999;
    }

    /// <summary>获取城池建筑列表</summary>
    public List<Building> GetCityBuildings(string cityId)
    {
        var progress = GetCityProgress(cityId);
        return progress?.Buildings.Values.ToList() ?? new List<Building>();
    }

    /// <summary>升级城池建筑</summary>
    public bool TryUpgradeBuilding(string cityId, string buildingId)
    {
        var progress = GetCityProgress(cityId);
        if (progress == null) return false;

        bool result = progress.UpgradeBuilding(buildingId, out _);
        if (result) Save();
        return result;
    }

    /// <summary>获取城池总产量</summary>
    public Dictionary<ResourceType, int> GetCityProduction(string cityId, string cityScale)
    {
        var progress = GetCityProgress(cityId);
        return progress?.GetTotalProduction(cityScale) ?? new Dictionary<ResourceType, int>();
    }

    /// <summary>更新城池资源生产（每秒调用）</summary>
    public void UpdateCityProduction(string cityId, string cityScale, double deltaSeconds)
    {
        var progress = GetCityProgress(cityId);
        if (progress == null) return;

        var production = progress.GetTotalProduction(cityScale);
        foreach (var kvp in production)
        {
            // 每秒产量 = 每小时产量 / 3600
            int amount = (int)(kvp.Value * deltaSeconds / 3600.0);
            if (amount > 0)
            {
                progress.AddResource(kvp.Key, amount);
            }
        }

        // 更新 last tick（每分钟保存一次）
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - progress.LastProductionTickMs > 60000)
        {
            progress.LastProductionTickMs = now;
            Save();
        }
    }

    /// <summary>消耗城池资源（用于招兵等）</summary>
    public bool ConsumeCityResource(string cityId, ResourceType type, int amount)
    {
        var progress = GetCityProgress(cityId);
        if (progress == null) return false;

        bool result = progress.ConsumeResource(type, amount);
        if (result) Save();
        return result;
    }

    /// <summary>添加城池资源</summary>
    public void AddCityResource(string cityId, ResourceType type, int amount)
    {
        var progress = GetOrCreateCityProgress(new CityData { Id = cityId });
        progress.AddResource(type, amount);
        Save();
    }

    public List<string> GetAvailableGeneralsForCity(CityData city)
    {
        // 出征时只能选择驻守在本城池的武将
        return GetCityGenerals(city.Id);
    }

    public void RunProductionTick()
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var cityId in _ownedCityIds)
        {
            var progress = GetCityProgress(cityId);
            if (progress == null) continue;

            // Find template data for production rates
            // (caller should pass city data, but we use defaults if not found)
            int grainPerTick = 5;
            int troopPerTick = 2;
            int maxTroops = 200;
            int wallLevel = 1;
            int cityLevel = progress.Level;

            // Calculate production
            float levelMultiplier = 1f + (cityLevel - 1) * 0.2f;
            float wallMultiplier = 1f + (wallLevel - 1) * 0.1f;

            progress.Grain += (int)(grainPerTick * levelMultiplier);
            progress.Grain = Math.Min(progress.Grain, MaxGrainCap);

            progress.CurrentTroops += (int)(troopPerTick * wallMultiplier);
            progress.CurrentTroops = Math.Min(progress.CurrentTroops, maxTroops);

            // Slow population growth
            int maxPop = 500; // default cap
            int popGrowth = Math.Min(5, (maxPop - progress.Population) / 10);
            if (popGrowth > 0) progress.Population += popGrowth;

            progress.LastProductionTickMs = nowMs;
        }
    }

    public bool RecruitGeneral(string cityId, string generalId, List<GeneralData> allGenerals)
    {
        var progress = GetCityProgress(cityId);
        if (progress == null) return false;

        var genData = allGenerals.FirstOrDefault(g => g.Id == generalId);
        if (genData == null) return false;

        int cost = GetRecruitCost(cityId, genData);
        if (BattleMerit < cost) return false;

        // If already unlocked, skip
        if (_playerGenerals.TryGetValue(generalId, out var gp) && gp.IsUnlocked)
            return false;

        BattleMerit -= cost;
        if (_playerGenerals.TryGetValue(generalId, out var genProgress))
        {
            genProgress.IsUnlocked = true;
        }
        Save();
        return true;
    }

    private int GetRecruitCost(string cityId, GeneralData genData)
    {
        // Base cost from city template, scaled by general stats
        var progress = GetCityProgress(cityId);
        int baseCost = 100; // default
        if (progress != null)
        {
            baseCost = 80 + (int)(genData.Strength * 0.5f);
        }
        return baseCost;
    }

    public bool UpgradeCity(string cityId)
    {
        var progress = GetCityProgress(cityId);
        if (progress == null || progress.Level >= MaxCityLevel) return false;

        int cost = progress.Level * 200; // scales with level
        if (BattleMerit < cost) return false;

        BattleMerit -= cost;
        progress.Level++;
        Save();
        return true;
    }

    public bool ReinforceCity(string cityId, int troopAmount)
    {
        var progress = GetCityProgress(cityId);
        if (progress == null) return false;

        int grainCost = troopAmount; // 1 grain = 1 troop
        if (progress.Grain < grainCost) return false;

        int maxTroops = 500; // default cap
        if (progress.CurrentTroops >= maxTroops) return false;

        progress.Grain -= grainCost;
        progress.CurrentTroops = Math.Min(progress.CurrentTroops + troopAmount, maxTroops);
        Save();
        return true;
    }

    // ==================== 武将系统扩展方法 ====================

    public void LoadGameData()
    {
        try
        {
            var dataDir = AppDomain.CurrentDomain.BaseDirectory;
            _allEquipment = DataLoader.Load<List<EquipmentData>>(Path.Combine(dataDir, "Data", "equipment.json")) ?? new();
            _allBonds = DataLoader.Load<List<BondData>>(Path.Combine(dataDir, "Data", "bonds.json")) ?? new();
            _allSkillTrees = DataLoader.Load<List<SkillTreeData>>(Path.Combine(dataDir, "Data", "skill_trees.json")) ?? new();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameState] LoadGameData failed: {ex.Message}");
        }
    }

    public void AddGeneralExperience(string generalId, int xpAmount)
    {
        if (!_playerGenerals.TryGetValue(generalId, out var progress)) return;
        
        progress.Experience += xpAmount;
        
        // Auto level up if enough XP
        while (progress.CanLevelUp)
        {
            progress.Experience -= progress.XpToNextLevel;
            progress.Level++;
            System.Diagnostics.Debug.WriteLine($"[GameState] {generalId} leveled up to {progress.Level}");
        }
        
        Save();
    }

    public void AddSkillPoints(string generalId, int amount)
    {
        if (!_playerGenerals.TryGetValue(generalId, out var progress)) return;
        progress.SkillPoints += amount;
        Save();
    }

    public bool EquipItem(string generalId, string slotType, string equipmentId)
    {
        if (!_playerGenerals.TryGetValue(generalId, out var progress)) return false;
        if (!_allEquipment.Any(e => e.Id == equipmentId && e.Type == slotType)) return false;
        
        progress.EquippedItems[slotType] = equipmentId;
        Save();
        return true;
    }

    public bool UnequipItem(string generalId, string slotType)
    {
        if (!_playerGenerals.TryGetValue(generalId, out var progress)) return false;
        bool removed = progress.EquippedItems.Remove(slotType);
        if (removed) Save();
        return removed;
    }

    public bool LearnSkill(string generalId, string skillId)
    {
        if (!_playerGenerals.TryGetValue(generalId, out var progress)) return false;
        if (progress.LearnedSkillIds.Contains(skillId)) return false;
        
        // Check if general can learn this skill (from skill tree or default pool)
        var treeNodes = _allSkillTrees.FirstOrDefault(st => st.GeneralId == generalId);
        bool canLearn = treeNodes?.Nodes.Any(n => n.UnlockSkillId == skillId) == true;
        
        if (!canLearn) return false;
        
        progress.LearnedSkillIds.Add(skillId);
        Save();
        return true;
    }

    public bool SwapActiveSkill(string generalId, string skillId)
    {
        if (!_playerGenerals.TryGetValue(generalId, out var progress)) return false;
        if (!progress.LearnedSkillIds.Contains(skillId)) return false;
        
        progress.ActiveSkillSlot = skillId;
        Save();
        return true;
    }

    public bool SwapPassiveSkill(string generalId, string skillId)
    {
        if (!_playerGenerals.TryGetValue(generalId, out var progress)) return false;
        if (!progress.LearnedSkillIds.Contains(skillId)) return false;
        
        progress.PassiveSkillSlot = skillId;
        Save();
        return true;
    }

    public bool LevelUpSkill(string generalId, string skillId)
    {
        if (!_playerGenerals.TryGetValue(generalId, out var progress)) return false;
        if (!progress.SkillLevels.ContainsKey(skillId))
            progress.SkillLevels[skillId] = 1;
        
        int currentLevel = progress.SkillLevels[skillId];
        int cost = currentLevel * 50; // Skill level up cost
        
        if (BattleMerit < cost) return false;
        
        BattleMerit -= cost;
        progress.SkillLevels[skillId]++;
        Save();
        return true;
    }

    public bool UnlockSkillTreeNode(string generalId, string nodeId)
    {
        if (!_playerGenerals.TryGetValue(generalId, out var progress)) return false;
        if (progress.UnlockedSkillTreeNodes.Contains(nodeId)) return false;
        
        var tree = _allSkillTrees.FirstOrDefault(st => st.GeneralId == generalId);
        var node = tree?.Nodes.FirstOrDefault(n => n.NodeId == nodeId);
        if (node == null) return false;
        
        // Check prerequisites
        foreach (var parentId in node.ParentNodeIds)
        {
            if (!progress.UnlockedSkillTreeNodes.Contains(parentId))
                return false;
        }
        
        if (progress.SkillPoints < node.Cost) return false;
        
        progress.SkillPoints -= node.Cost;
        progress.UnlockedSkillTreeNodes.Add(nodeId);
        
        // Apply node bonuses
        if (node.NodeType == "stat")
        {
            // Stats will be calculated dynamically in battle
        }
        else if (node.NodeType == "skill" && !string.IsNullOrEmpty(node.UnlockSkillId))
        {
            if (!progress.LearnedSkillIds.Contains(node.UnlockSkillId))
                progress.LearnedSkillIds.Add(node.UnlockSkillId);
        }
        
        Save();
        return true;
    }

    public List<BondData> GetActiveBonds()
    {
        var activeBonds = new List<BondData>();
        var squadGenerals = _currentSquad.ToHashSet();
        
        foreach (var bond in _allBonds)
        {
            if (bond.RequiredGeneralIds.All(id => squadGenerals.Contains(id)))
            {
                activeBonds.Add(bond);
            }
        }
        
        return activeBonds;
    }

    public Dictionary<string, float> GetBondBonusesForSquad()
    {
        var bonuses = new Dictionary<string, float>();
        var activeBonds = GetActiveBonds();
        
        foreach (var bond in activeBonds)
        {
            foreach (var kvp in bond.StatBonuses)
            {
                if (!bonuses.ContainsKey(kvp.Key))
                    bonuses[kvp.Key] = 0f;
                bonuses[kvp.Key] += kvp.Value;
            }
        }
        
        return bonuses;
    }

    // ==================== 军队持久化 ====================

    public void SaveArmyState(List<ArmySaveEntry> armies)
    {
        _armies = armies.ToList();
        Save();
    }

    public List<ArmySaveEntry> GetSavedArmies()
    {
        return _armies.ToList();
    }

    // ==================== 忠诚度赏赐系统 ====================
    
    /// <summary>赏赐武将金币以提升忠诚度（每10金币+1忠诚度）</summary>
    public bool GrantReward(string generalId, string cityId, int goldAmount, out string msg)
    {
        msg = "";
        var progress = GetGeneralProgress(generalId);
        if (progress == null) { msg = "武将不存在"; return false; }
        
        var city = GetCityProgress(cityId);
        if (city == null) { msg = "城池不存在"; return false; }
        if (city.Gold < goldAmount) { msg = "金币不足"; return false; }
        
        int loyaltyGain = goldAmount / 10;
        if (loyaltyGain <= 0) { msg = "金额太少（至少10金币）"; return false; }
        
        city.Gold -= goldAmount;
        int oldLoyalty = progress.Loyalty;
        progress.Loyalty = Math.Min(100, progress.Loyalty + loyaltyGain);
        int actualGain = progress.Loyalty - oldLoyalty;
        Save();
        msg = $"忠诚度+{actualGain} (当前{progress.Loyalty})";
        return true;
    }

    // ==================== 远程策反系统 ====================
    
    /// <summary>发起远程策反任务（说服其他城池的武将，需要多回合）</summary>
    public bool StartSabotageMission(string officerId, string targetGeneralId, string sourceCityId, string targetCityId, out string msg)
    {
        msg = "";
        
        // 检查执行官是否存在且已招募
        var officerProgress = GetGeneralProgress(officerId);
        if (officerProgress == null || officerProgress.Status != GeneralStatus.Recruited)
        {
            msg = "执行官不可用";
            return false;
        }
        
        // 检查执行官是否已在执行任务
        if (ActiveMissions.Any(m => m.OfficerId == officerId))
        {
            msg = "该武将正在执行策反任务";
            return false;
        }
        
        // 检查是否已有针对同一目标的任务
        if (ActiveMissions.Any(m => m.TargetGeneralId == targetGeneralId))
        {
            msg = "已有针对该目标的策反任务";
            return false;
        }
        
        // 计算成功率和所需回合数(基础2回合)
        int successRate = CalcPersuadeSuccessRate(officerId, targetGeneralId);
        int requiredTurns = 2;
        
        var mission = new SabotageMission
        {
            OfficerId = officerId,
            TargetGeneralId = targetGeneralId,
            SourceCityId = sourceCityId,
            TargetCityId = targetCityId,
            RemainingTurns = requiredTurns,
            SuccessRate = successRate
        };
        
        ActiveMissions.Add(mission);
        
        // 标记武将为已行动
        var cityProgress = GetCityProgress(sourceCityId);
        cityProgress?.ActedGeneralsThisTurn.Add(officerId);
        
        Save();
        msg = $"策反任务已启动 (预计{requiredTurns}回合, 成功率{successRate}%)";
        return true;
    }
    
    /// <summary>每回合处理策反任务（由TurnManager调用）</summary>
    public void ProcessSabotageMissions()
    {
        var completedMissions = new List<SabotageMission>();
        
        foreach (var mission in ActiveMissions)
        {
            mission.RemainingTurns--;
            
            if (mission.RemainingTurns <= 0)
            {
                // 判定成功/失败
                var random = new Random();
                bool success = random.Next(100) < mission.SuccessRate;
                
                if (success)
                {
                    // 策反成功：将目标武将招募到出发城池
                    var targetProgress = GetGeneralProgress(mission.TargetGeneralId);
                    if (targetProgress != null)
                    {
                        targetProgress.Status = GeneralStatus.Recruited;
                        targetProgress.CurrentCityId = mission.SourceCityId;
                        
                        var cityProgress = GetCityProgress(mission.SourceCityId);
                        if (cityProgress != null && !cityProgress.GeneralIds.Contains(mission.TargetGeneralId))
                        {
                            cityProgress.GeneralIds.Add(mission.TargetGeneralId);
                        }
                        
                        // 从原城池移除
                        var targetCity = GetCityProgress(mission.TargetCityId);
                        targetCity?.GeneralIds.Remove(mission.TargetGeneralId);
                    }
                    System.Diagnostics.Debug.WriteLine($"[Sabotage] 策反成功: {mission.TargetGeneralId} -> {mission.SourceCityId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Sabotage] 策反失败: {mission.TargetGeneralId}");
                }
                
                completedMissions.Add(mission);
            }
        }
        
        foreach (var m in completedMissions)
            ActiveMissions.Remove(m);
        
        Save();
    }
    
    /// <summary>检查武将是否正在执行策反任务</summary>
    public bool IsOnSabotageMission(string generalId)
    {
        return ActiveMissions.Any(m => m.OfficerId == generalId);
    }

    // ==================== 人才管理系统 ====================

    /// <summary>获取未发现的人才列表（未解锁的武将，考虑历史年份和城池约束）</summary>
    public List<GeneralData> GetTalentPool(List<GeneralData> allGenerals, string searchCityId = "")
    {
        string playerFactionId = PlayerFactionId;
        int currentYear = CurrentDate.Year;
        return allGenerals.Where(g =>
        {
            var progress = GetGeneralProgress(g.Id);
            if (progress != null && progress.IsUnlocked) return false;
            // 过滤：排除属于其他势力的武将（只能发现本势力和无归属的武将）
            if (!string.IsNullOrEmpty(g.ForceId) && g.ForceId != playerFactionId)
                return false;
            // 历史年份约束：武将未到登场年份则不可发现
            if (currentYear < g.AppearYear)
                return false;
            // 城池约束：如果武将有指定登场城市，只能在该城市发现
            if (!string.IsNullOrEmpty(g.AppearCityId) && !string.IsNullOrEmpty(searchCityId)
                && g.AppearCityId != searchCityId)
                return false;
            return true;
        }).ToList();
    }

    /// <summary>发现人才 - 随机解锁一个武将（基于城池和历史约束）</summary>
    public bool DiscoverTalent(List<GeneralData> allGenerals, string cityId, out string discoveredGeneralId, out string errorMsg)
    {
        discoveredGeneralId = "";
        errorMsg = "";

        var talentPool = GetTalentPool(allGenerals, cityId);
        if (talentPool.Count == 0)
        {
            errorMsg = "所有人才均已发现";
            return false;
        }

        var random = new Random();
        var target = talentPool[random.Next(talentPool.Count)];

        if (_playerGenerals.TryGetValue(target.Id, out var progress))
        {
            progress.IsUnlocked = true;
            progress.Status = GeneralStatus.Available; // 设置为在野状态
        }
        else
        {
            _playerGenerals[target.Id] = new GeneralProgress
            {
                Data = target,
                IsUnlocked = true,
                Status = GeneralStatus.Available,
                Level = 1,
                Experience = 0
            };
        }

        discoveredGeneralId = target.Id;
        Save();
        return true;
    }

    /// <summary>获取在野武将列表（已发现但未招募）</summary>
    public List<GeneralProgress> GetAvailableTalents()
    {
        return _playerGenerals.Values.Where(g => g.IsUnlocked && g.Status == GeneralStatus.Available).ToList();
    }

    /// <summary>说服在野武将 - 消耗金币招募</summary>
    public bool PersuadeTalent(string generalId, string cityId, out string errorMsg)
    {
        errorMsg = "";

        if (!_playerGenerals.TryGetValue(generalId, out var progress))
        {
            errorMsg = "武将不存在";
            return false;
        }

        if (!progress.IsUnlocked || progress.Status != GeneralStatus.Available)
        {
            errorMsg = "该武将不是在野状态";
            return false;
        }

        var cityProgress = GetCityProgress(cityId);
        if (cityProgress == null)
        {
            errorMsg = "城池不存在";
            return false;
        }

        // 需要搜索官
        if (string.IsNullOrEmpty(cityProgress.SearchOfficerId))
        {
            errorMsg = "请先分配搜索官";
            return false;
        }

        // 计算成功率
        int successRate = CalcPersuadeSuccessRate(cityProgress.SearchOfficerId, generalId);
        var random = new Random();
        if (random.Next(100) >= successRate)
        {
            errorMsg = $"说服失败 (成功率{successRate}%)";
            return false;
        }

        progress.Status = GeneralStatus.Recruited;
        progress.CurrentCityId = cityId;

        // 加入城池武将列表
        if (!cityProgress.GeneralIds.Contains(generalId))
        {
            cityProgress.GeneralIds.Add(generalId);
        }

        // 被说服的人才当前回合标记为已行动，下回合解锁
        cityProgress.ActedGeneralsThisTurn.Add(generalId);

        Save();
        return true;
    }

    /// <summary>计算说服成功率：羁绊优先，魅力兜底，忠诚度修正</summary>
    public int CalcPersuadeSuccessRate(string searchOfficerId, string targetGeneralId)
    {
        int baseRate = 10;
        var officer = _playerGenerals.TryGetValue(searchOfficerId, out var sp) ? sp.Data : null;
        var targetProgress = GetGeneralProgress(targetGeneralId);
        var targetData = targetProgress?.Data;

        // 1. 羁绊加成 (最高优先级, +50%)
        int bondBonus = 0;
        foreach (var bond in _allBonds)
        {
            if (bond.RequiredGeneralIds.Contains(searchOfficerId) &&
                bond.RequiredGeneralIds.Contains(targetGeneralId))
            {
                bondBonus = 50;
                break;
            }
        }
        // 同势力加成 (+20%, 仅当无直接羁绊时)
        if (bondBonus == 0 && officer != null && targetData != null
            && !string.IsNullOrEmpty(officer.ForceId) && officer.ForceId == targetData.ForceId)
        {
            bondBonus = 20;
        }

        // 2. 魅力修正 (兜底因素, Charisma/5, 最高+20%)
        int charismaBonus = officer != null ? officer.Charisma / 5 : 0;

        // 3. 忠诚度修正 (目标忠诚度越低越容易说服)
        int targetLoyalty = targetProgress?.Loyalty ?? targetData?.Loyalty ?? 70;
        int loyaltyBonus = (100 - targetLoyalty) / 5;

        return Math.Clamp(baseRate + bondBonus + charismaBonus + loyaltyBonus, 5, 95);
    }

    /// <summary>获取俘虏武将列表</summary>
    public List<GeneralProgress> GetCaptives()
    {
        return _playerGenerals.Values.Where(g => g.Status == GeneralStatus.Captive).ToList();
    }

    /// <summary>招降俘虏 - 消耗战功招募</summary>
    public bool RecruitCaptive(string generalId, out string errorMsg)
    {
        errorMsg = "";

        if (!_playerGenerals.TryGetValue(generalId, out var progress))
        {
            errorMsg = "武将不存在";
            return false;
        }

        if (progress.Status != GeneralStatus.Captive)
        {
            errorMsg = "该武将不是俘虏";
            return false;
        }

        int cost = 150; // 招降消耗战功
        if (BattleMerit < cost)
        {
            errorMsg = $"战功不足，需要{cost}战功";
            return false;
        }

        BattleMerit -= cost;
        progress.Status = GeneralStatus.Recruited;
        Save();
        return true;
    }

    /// <summary>添加俘虏（战斗胜利后调用）</summary>
    public void AddCaptive(string generalId)
    {
        if (_playerGenerals.TryGetValue(generalId, out var progress))
        {
            progress.Status = GeneralStatus.Captive;
            Save();
        }
    }

    // ==================== 编队操作方法 ====================

    /// <summary>尝试添加武将到编队</summary>
    public bool TryAddToSquad(string generalId, out string errorMsg)
    {
        errorMsg = "";

        if (!_playerGenerals.TryGetValue(generalId, out var progress))
        {
            errorMsg = "武将不存在";
            return false;
        }

        if (!progress.IsUnlocked || progress.Status != GeneralStatus.Recruited)
        {
            errorMsg = "武将未招募";
            return false;
        }

        if (_currentSquad.Contains(generalId))
        {
            errorMsg = "武将已在编队中";
            return false;
        }

        if (_currentSquad.Count >= 3)
        {
            errorMsg = "编队已满（最多3人）";
            return false;
        }

        _currentSquad.Add(generalId);
        Save();
        return true;
    }

    /// <summary>从编队移除武将</summary>
    public bool TryRemoveFromSquad(string generalId)
    {
        bool removed = _currentSquad.Remove(generalId);
        if (removed) Save();
        return removed;
    }

    // ==================== 原有方法 ====================

    public void UnlockGenerals(List<string> generalIds)
    {
        bool changed = false;
        foreach (var id in generalIds)
        {
            if (_playerGenerals.TryGetValue(id, out var progress) && !progress.IsUnlocked)
            {
                progress.IsUnlocked = true;
                changed = true;
            }
        }
        if (changed) Save();
    }

    public void MarkStageCompleted(string stageId)
    {
        if (!_completedStageIds.Contains(stageId))
        {
            _completedStageIds.Add(stageId);
            Save();
        }
    }

    public void Save()
    {
        try
        {
            var data = BuildSaveData();
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            });
            File.WriteAllText(SavePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameState] Save failed: {ex.Message}");
        }
    }

    public void Load()
    {
        if (!File.Exists(SavePath)) return;

        try
        {
            var json = File.ReadAllText(SavePath);
            var data = JsonSerializer.Deserialize<GameStateSaveData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data != null)
            {
                ApplySaveData(data);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameState] Load failed: {ex.Message}");
        }
    }

    private GameStateSaveData BuildSaveData()
    {
        return new GameStateSaveData
        {
            BattleMerit = BattleMerit,
            OwnedCityIds = _ownedCityIds.ToList(),
            CompletedStageIds = _completedStageIds.ToList(),
            CurrentSquad = _currentSquad.ToList(),
            CurrentDateYear = CurrentDate.Year,
            CurrentDateMonth = CurrentDate.Month,
            CurrentDateDay = CurrentDate.Day,
            CurrentScenarioId = CurrentScenarioId,
            PlayerFactionId = PlayerFactionId,
            TurnNumber = TurnNumber,
            CaptiveGeneralIds = CaptiveGeneralIds.ToList(),
            Generals = _playerGenerals.Select(kvp => new GeneralSaveEntry
            {
                Id = kvp.Key,
                Level = kvp.Value.Level,
                Experience = kvp.Value.Experience,
                IsUnlocked = kvp.Value.IsUnlocked,
                Status = kvp.Value.Status.ToString(),
                Loyalty = kvp.Value.Loyalty,
                CurrentCityId = kvp.Value.CurrentCityId,
                IsOnExpedition = kvp.Value.IsOnExpedition,
                SkillPoints = kvp.Value.SkillPoints,
                EquippedItems = new Dictionary<string, string>(kvp.Value.EquippedItems),
                LearnedSkillIds = kvp.Value.LearnedSkillIds.ToList(),
                ActiveSkillSlot = kvp.Value.ActiveSkillSlot,
                PassiveSkillSlot = kvp.Value.PassiveSkillSlot,
                SkillLevels = new Dictionary<string, int>(kvp.Value.SkillLevels),
                UnlockedSkillTreeNodes = kvp.Value.UnlockedSkillTreeNodes.ToList(),
                BonusStats = new Dictionary<string, int>(kvp.Value.BonusStats)
            }).ToList(),
            Cities = _cityProgress.Select(kvp => new CityProgressEntry
            {
                CityId = kvp.Key,
                Level = kvp.Value.Level,
                Population = kvp.Value.Population,
                Grain = kvp.Value.Grain,
                CurrentTroops = kvp.Value.CurrentTroops,
                LastProductionTickMs = kvp.Value.LastProductionTickMs,
                GeneralIds = kvp.Value.GeneralIds.ToList(),
                GovernorId = kvp.Value.GovernorId,
                InteriorOfficerId = kvp.Value.InteriorOfficerId,
                MilitaryOfficerId = kvp.Value.MilitaryOfficerId,
                SearchOfficerId = kvp.Value.SearchOfficerId,
                Gold = kvp.Value.Gold,
                Food = kvp.Value.Food,
                Wood = kvp.Value.Wood,
                Iron = kvp.Value.Iron,
                Buildings = kvp.Value.Buildings.Select(b => new BuildingSaveEntry
                {
                    Id = b.Key,
                    Name = b.Value.Name,
                    Level = b.Value.Level,
                    MaxLevel = b.Value.MaxLevel,
                    Category = b.Value.Category.ToString()
                }).ToList(),
                RecruitTarget = kvp.Value.RecruitTarget,
                IsRecruiting = kvp.Value.IsRecruiting
            }).ToList(),
            Armies = _armies.ToList(),
            ActiveMissions = ActiveMissions.ToList()
        };
    }

    private void ApplySaveData(GameStateSaveData data)
    {
        BattleMerit = data.BattleMerit;
        _ownedCityIds.Clear();
        _ownedCityIds.AddRange(data.OwnedCityIds);
        _completedStageIds.Clear();
        _completedStageIds.AddRange(data.CompletedStageIds);
        _currentSquad = data.CurrentSquad;

        if (data.CurrentDateYear > 0)
            CurrentDate = new Data.Schemas.GameDate(data.CurrentDateYear, data.CurrentDateMonth, data.CurrentDateDay);
        CurrentScenarioId = data.CurrentScenarioId ?? "";
        PlayerFactionId = data.PlayerFactionId ?? "";
        TurnNumber = data.TurnNumber > 0 ? data.TurnNumber : 1;
        CaptiveGeneralIds = data.CaptiveGeneralIds ?? new();

        foreach (var entry in data.Generals)
        {
            if (_playerGenerals.TryGetValue(entry.Id, out var progress))
            {
                progress.Level = entry.Level;
                progress.Experience = entry.Experience;
                progress.IsUnlocked = entry.IsUnlocked;
                if (!string.IsNullOrEmpty(entry.Status) && Enum.TryParse<GeneralStatus>(entry.Status, out var status))
                    progress.Status = status;
                else if (progress.IsUnlocked)
                    progress.Status = GeneralStatus.Recruited;
                progress.Loyalty = entry.Loyalty;
                progress.CurrentCityId = entry.CurrentCityId ?? "";
                progress.IsOnExpedition = entry.IsOnExpedition;
                progress.SkillPoints = entry.SkillPoints;
                progress.EquippedItems = entry.EquippedItems ?? new();
                progress.LearnedSkillIds = entry.LearnedSkillIds ?? new();
                progress.ActiveSkillSlot = entry.ActiveSkillSlot ?? "";
                progress.PassiveSkillSlot = entry.PassiveSkillSlot ?? "";
                progress.SkillLevels = entry.SkillLevels ?? new();
                progress.UnlockedSkillTreeNodes = entry.UnlockedSkillTreeNodes ?? new();
                progress.BonusStats = entry.BonusStats ?? new();
            }
        }

        foreach (var entry in data.Cities)
        {
            if (!_cityProgress.ContainsKey(entry.CityId))
            {
                _cityProgress[entry.CityId] = new CityProgress
                {
                    CityId = entry.CityId,
                    Level = entry.Level,
                    Population = entry.Population,
                    Grain = entry.Grain,
                    CurrentTroops = entry.CurrentTroops,
                    LastProductionTickMs = entry.LastProductionTickMs,
                    GeneralIds = entry.GeneralIds ?? new List<string>(),
                    GovernorId = entry.GovernorId ?? "",
                    InteriorOfficerId = entry.InteriorOfficerId ?? "",
                    MilitaryOfficerId = entry.MilitaryOfficerId ?? "",
                    SearchOfficerId = entry.SearchOfficerId ?? "",
                    Gold = entry.Gold,
                    Food = entry.Food,
                    Wood = entry.Wood,
                    Iron = entry.Iron,
                    RecruitTarget = entry.RecruitTarget,
                    IsRecruiting = entry.IsRecruiting
                };
            }
            else
            {
                var cp = _cityProgress[entry.CityId];
                cp.Level = entry.Level;
                cp.Population = entry.Population;
                cp.Grain = entry.Grain;
                cp.CurrentTroops = entry.CurrentTroops;
                cp.LastProductionTickMs = entry.LastProductionTickMs;
                cp.GeneralIds = entry.GeneralIds ?? new List<string>();
                cp.GovernorId = entry.GovernorId ?? "";
                cp.InteriorOfficerId = entry.InteriorOfficerId ?? "";
                cp.MilitaryOfficerId = entry.MilitaryOfficerId ?? "";
                cp.SearchOfficerId = entry.SearchOfficerId ?? "";
                cp.Gold = entry.Gold;
                cp.Food = entry.Food;
                cp.Wood = entry.Wood;
                cp.Iron = entry.Iron;
                cp.RecruitTarget = entry.RecruitTarget;
                cp.IsRecruiting = entry.IsRecruiting;
            }

            // 恢复建筑数据
            var cityProg = _cityProgress[entry.CityId];
            if (entry.Buildings != null && entry.Buildings.Count > 0)
            {
                cityProg.Buildings.Clear();
                foreach (var b in entry.Buildings)
                {
                    var building = new Data.Schemas.Building
                    {
                        Id = b.Id,
                        Name = b.Name,
                        Level = b.Level,
                        MaxLevel = b.MaxLevel
                    };
                    if (Enum.TryParse<Data.Schemas.BuildingCategory>(b.Category, out var cat))
                        building.Category = cat;
                    cityProg.Buildings[b.Id] = building;
                }
            }
        }

        _armies = data.Armies ?? new();
        ActiveMissions = data.ActiveMissions ?? new();
    }
}

public class SaveSlotInfo
{
    public int SlotIndex { get; set; }
    public bool IsEmpty { get; set; } = true;
    public string ScenarioId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public int TurnNumber { get; set; }
    public int CityCount { get; set; }
    public int GameDateYear { get; set; }
    public int GameDateMonth { get; set; }
    public DateTime SaveTime { get; set; }
}

public class GameStateSaveData
{
    public int BattleMerit { get; set; }
    public List<string> OwnedCityIds { get; set; } = new();
    public List<string> CompletedStageIds { get; set; } = new();
    public List<string> CurrentSquad { get; set; } = new();
    
    // 回合制与新系统
    public int CurrentDateYear { get; set; }
    public int CurrentDateMonth { get; set; }
    public int CurrentDateDay { get; set; }
    public string CurrentScenarioId { get; set; } = "";
    public string PlayerFactionId { get; set; } = "";
    public int TurnNumber { get; set; }
    public List<string> CaptiveGeneralIds { get; set; } = new();
    
    public List<GeneralSaveEntry> Generals { get; set; } = new();
    public List<CityProgressEntry> Cities { get; set; } = new();
    public List<ArmySaveEntry> Armies { get; set; } = new();
    public List<SabotageMission> ActiveMissions { get; set; } = new();
}

public class GeneralSaveEntry
{
    public string Id { get; set; } = "";
    public int Level { get; set; }
    public int Experience { get; set; }
    public bool IsUnlocked { get; set; }
    public string Status { get; set; } = "Recruited"; // 武将状态
    // 新字段
    public int Loyalty { get; set; } = 70;
    public string CurrentCityId { get; set; } = "";
    public bool IsOnExpedition { get; set; } = false;
    public int SkillPoints { get; set; } = 0;
    public Dictionary<string, string> EquippedItems { get; set; } = new();
    public List<string> LearnedSkillIds { get; set; } = new();
    public string ActiveSkillSlot { get; set; } = "";
    public string PassiveSkillSlot { get; set; } = "";
    public Dictionary<string, int> SkillLevels { get; set; } = new();
    public List<string> UnlockedSkillTreeNodes { get; set; } = new();
    public Dictionary<string, int> BonusStats { get; set; } = new();
}

public class CityProgressEntry
{
    public string CityId { get; set; } = "";
    public int Level { get; set; }
    public int Population { get; set; }
    public int Grain { get; set; }
    public int CurrentTroops { get; set; }
    public long LastProductionTickMs { get; set; }
    public List<string> GeneralIds { get; set; } = new();
    
    // 官员任命
    public string GovernorId { get; set; } = "";
    public string InteriorOfficerId { get; set; } = "";
    public string MilitaryOfficerId { get; set; } = "";
    public string SearchOfficerId { get; set; } = "";

    // 资源存储
    public int Gold { get; set; }
    public int Food { get; set; }
    public int Wood { get; set; }
    public int Iron { get; set; }

    // 建筑列表
    public List<BuildingSaveEntry> Buildings { get; set; } = new();

    // 征兵系统
    public int RecruitTarget { get; set; }
    public bool IsRecruiting { get; set; }
}

public class BuildingSaveEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; } = 1;
    public int MaxLevel { get; set; } = 10;
    public string Category { get; set; } = "Agriculture";
}

public class ArmySaveEntry
{
    public string Id { get; set; } = "";
    public List<string> GeneralIds { get; set; } = new();
    public List<GeneralDeployEntry> GeneralConfigs { get; set; } = new();
    public string CurrentCityId { get; set; } = "";
    public string Team { get; set; } = "player";
    public string OriginCityId { get; set; } = "";

    // 行军状态 (回合制)
    public string? TargetCityId { get; set; }
    public List<string>? MovePath { get; set; }
    public int CurrentSegmentIndex { get; set; }
    public int[]? DaysPerSegment { get; set; }
    public int DaysElapsedInSegment { get; set; }
    public int TotalDaysRemaining { get; set; }
}
