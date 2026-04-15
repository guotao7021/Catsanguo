using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Data;

public class GeneralProgress
{
    public GeneralData Data { get; set; } = new();
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
    public bool IsUnlocked { get; set; } = false;
    public GeneralStatus Status { get; set; } = GeneralStatus.Recruited; // 武将状态
    public int LevelCap { get; set; } = 50;
    
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

    public int LevelUpCost => Level * 50;
    public int XpToNextLevel => Level * 100;
    public float XpProgressRatio => Level >= LevelCap ? 1f : (float)Experience / XpToNextLevel;
    public bool CanLevelUp => Experience >= XpToNextLevel && Level < LevelCap;

    public float GetEffectiveStat(int baseStat)
    {
        return baseStat * (1f + (Level - 1) * 0.03f);
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

    public bool TryLevelUpGeneral(string generalId)
    {
        if (!_playerGenerals.TryGetValue(generalId, out var progress)) return false;
        if (progress.Level >= progress.LevelCap) return false;
        int cost = progress.LevelUpCost;
        if (BattleMerit < cost) return false;

        BattleMerit -= cost;
        progress.Level++;
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
            return progress.GeneralIds.ToList();
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
        var result = new List<string>();

        // 1. 城池驻扎的武将
        var cityGenerals = GetCityGenerals(city.Id);
        result.AddRange(cityGenerals);

        // 2. 玩家编队中的武将（如果在己方城池）
        if (city.Owner == "player")
        {
            foreach (var genId in _currentSquad)
            {
                if (!result.Contains(genId))
                    result.Add(genId);
            }
        }

        return result;
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

    // ==================== 人才管理系统 ====================

    /// <summary>获取未发现的人才列表（未解锁的武将）</summary>
    public List<GeneralData> GetTalentPool(List<GeneralData> allGenerals)
    {
        return allGenerals.Where(g =>
        {
            var progress = GetGeneralProgress(g.Id);
            return progress == null || !progress.IsUnlocked;
        }).ToList();
    }

    /// <summary>发现人才 - 消耗战功随机解锁一个武将</summary>
    public bool DiscoverTalent(List<GeneralData> allGenerals, out string discoveredGeneralId, out string errorMsg)
    {
        discoveredGeneralId = "";
        errorMsg = "";

        var talentPool = GetTalentPool(allGenerals);
        if (talentPool.Count == 0)
        {
            errorMsg = "所有人才均已发现";
            return false;
        }

        int cost = 100; // 发现消耗战功
        if (BattleMerit < cost)
        {
            errorMsg = $"战功不足，需要{cost}战功";
            return false;
        }

        var random = new Random();
        var target = talentPool[random.Next(talentPool.Count)];

        BattleMerit -= cost;
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

        int cost = 200; // 说服消耗金币
        if (cityProgress.Gold < cost)
        {
            errorMsg = $"金币不足，需要{cost}金币";
            return false;
        }

        cityProgress.Gold -= cost;
        progress.Status = GeneralStatus.Recruited;

        // 加入城池武将列表
        if (!cityProgress.GeneralIds.Contains(generalId))
        {
            cityProgress.GeneralIds.Add(generalId);
        }

        Save();
        return true;
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
            var data = new GameStateSaveData
            {
                BattleMerit = BattleMerit,
                OwnedCityIds = _ownedCityIds.ToList(),
                CompletedStageIds = _completedStageIds.ToList(),
                CurrentSquad = _currentSquad.ToList(),
                Generals = _playerGenerals.Select(kvp => new GeneralSaveEntry
                {
                    Id = kvp.Key,
                    Level = kvp.Value.Level,
                    Experience = kvp.Value.Experience,
                    IsUnlocked = kvp.Value.IsUnlocked,
                    Status = kvp.Value.Status.ToString(),
                    SkillPoints = kvp.Value.SkillPoints,
                    EquippedItems = new Dictionary<string, string>(kvp.Value.EquippedItems),
                    LearnedSkillIds = kvp.Value.LearnedSkillIds.ToList(),
                    ActiveSkillSlot = kvp.Value.ActiveSkillSlot,
                    PassiveSkillSlot = kvp.Value.PassiveSkillSlot,
                    SkillLevels = new Dictionary<string, int>(kvp.Value.SkillLevels),
                    UnlockedSkillTreeNodes = kvp.Value.UnlockedSkillTreeNodes.ToList()
                }).ToList(),
                Cities = _cityProgress.Select(kvp => new CityProgressEntry
                {
                    CityId = kvp.Key,
                    Level = kvp.Value.Level,
                    Population = kvp.Value.Population,
                    Grain = kvp.Value.Grain,
                    CurrentTroops = kvp.Value.CurrentTroops,
                    LastProductionTickMs = kvp.Value.LastProductionTickMs,
                    GeneralIds = kvp.Value.GeneralIds.ToList()
                }).ToList(),
                Armies = _armies.ToList()
            };

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
                BattleMerit = data.BattleMerit;
                _ownedCityIds.Clear();
                _ownedCityIds.AddRange(data.OwnedCityIds);
                _completedStageIds.Clear();
                _completedStageIds.AddRange(data.CompletedStageIds);
                _currentSquad = data.CurrentSquad;

                foreach (var entry in data.Generals)
                {
                    if (_playerGenerals.TryGetValue(entry.Id, out var progress))
                    {
                        progress.Level = entry.Level;
                        progress.Experience = entry.Experience;
                        progress.IsUnlocked = entry.IsUnlocked;
                        // 加载Status字段（向后兼容）
                        if (!string.IsNullOrEmpty(entry.Status) && Enum.TryParse<GeneralStatus>(entry.Status, out var status))
                        {
                            progress.Status = status;
                        }
                        else if (progress.IsUnlocked)
                        {
                            // 旧存档没有Status字段，已解锁的默认为已招募
                            progress.Status = GeneralStatus.Recruited;
                        }
                        // 新字段 (向后兼容)
                        progress.SkillPoints = entry.SkillPoints;
                        progress.EquippedItems = entry.EquippedItems ?? new();
                        progress.LearnedSkillIds = entry.LearnedSkillIds ?? new();
                        progress.ActiveSkillSlot = entry.ActiveSkillSlot ?? "";
                        progress.PassiveSkillSlot = entry.PassiveSkillSlot ?? "";
                        progress.SkillLevels = entry.SkillLevels ?? new();
                        progress.UnlockedSkillTreeNodes = entry.UnlockedSkillTreeNodes ?? new();
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
                            GeneralIds = entry.GeneralIds ?? new List<string>()
                        };
                    }
                    else
                    {
                        // 更新已有的城池进度
                        _cityProgress[entry.CityId].GeneralIds = entry.GeneralIds ?? new List<string>();
                    }
                }

                _armies = data.Armies ?? new();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameState] Load failed: {ex.Message}");
        }
    }
}

public class GameStateSaveData
{
    public int BattleMerit { get; set; }
    public List<string> OwnedCityIds { get; set; } = new();
    public List<string> CompletedStageIds { get; set; } = new();
    public List<string> CurrentSquad { get; set; } = new();
    public List<GeneralSaveEntry> Generals { get; set; } = new();
    public List<CityProgressEntry> Cities { get; set; } = new();
    public List<ArmySaveEntry> Armies { get; set; } = new();
}

public class GeneralSaveEntry
{
    public string Id { get; set; } = "";
    public int Level { get; set; }
    public int Experience { get; set; }
    public bool IsUnlocked { get; set; }
    public string Status { get; set; } = "Recruited"; // 武将状态
    // 新字段
    public int SkillPoints { get; set; } = 0;
    public Dictionary<string, string> EquippedItems { get; set; } = new();
    public List<string> LearnedSkillIds { get; set; } = new();
    public string ActiveSkillSlot { get; set; } = "";
    public string PassiveSkillSlot { get; set; } = "";
    public Dictionary<string, int> SkillLevels { get; set; } = new();
    public List<string> UnlockedSkillTreeNodes { get; set; } = new();
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
}

public class ArmySaveEntry
{
    public string Id { get; set; } = "";
    public List<string> GeneralIds { get; set; } = new();
    public List<GeneralDeployEntry> GeneralConfigs { get; set; } = new();
    public string CurrentCityId { get; set; } = "";
    public string Team { get; set; } = "player";
}
