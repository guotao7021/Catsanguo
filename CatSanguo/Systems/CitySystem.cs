using System;
using System.Collections.Generic;
using System.Linq;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Systems;

public struct BuildingInfo
{
    public string Id;
    public string Name;
    public BuildingType Type;
    public int Level;
    public int MaxLevel;
    public int GoldCost;
    public int FoodCost;
    public int WoodCost;
    public int IronCost;
    public bool CanUpgrade;
    public ResourceType ProducesResource;
    public int CurrentProduction;
}

public struct CitySnapshot
{
    public string CityId;
    public string CityName;
    public int Gold, Food, Wood, Iron;
    public int GoldCap, FoodCap, WoodCap, IronCap;
    public Dictionary<ResourceType, int> Production;
    public List<BuildingInfo> Buildings;
}

public class CitySystem
{
    private string _activeCityId = "";
    private string _activeCityScale = "medium";
    private float _productionAccumulator;
    private const float ProductionInterval = 1f; // Demo加速: 每1秒产出一次

    public string ActiveCityId => _activeCityId;

    public void SetActiveCity(string cityId)
    {
        _activeCityId = cityId;
        _productionAccumulator = 0f;

        // 确定城池规模
        var cityData = DataManager.Instance.AllCities.FirstOrDefault(c => c.Id == cityId);
        _activeCityScale = cityData?.CityScale ?? "medium";
    }

    public void Update(float deltaSeconds)
    {
        if (string.IsNullOrEmpty(_activeCityId)) return;

        var cp = GameState.Instance.GetCityProgress(_activeCityId);
        if (cp == null) return;

        _productionAccumulator += deltaSeconds;
        if (_productionAccumulator >= ProductionInterval)
        {
            _productionAccumulator -= ProductionInterval;
            ProduceResources(cp);
        }
    }

    private void ProduceResources(CityProgress cp)
    {
        var production = cp.GetTotalProduction(_activeCityScale);
        foreach (var kvp in production)
        {
            cp.AddResource(kvp.Key, kvp.Value);
        }
    }

    public void CollectResources()
    {
        if (string.IsNullOrEmpty(_activeCityId)) return;
        var cp = GameState.Instance.GetCityProgress(_activeCityId);
        if (cp == null) return;

        cp.AddResource(ResourceType.Gold, 50);
        cp.AddResource(ResourceType.Food, 30);
        cp.AddResource(ResourceType.Wood, 20);
        cp.AddResource(ResourceType.Iron, 10);
    }

    public bool UpgradeBuilding(string buildingId, out string error)
    {
        error = "";
        if (string.IsNullOrEmpty(_activeCityId))
        {
            error = "没有激活的城池";
            return false;
        }

        var cp = GameState.Instance.GetCityProgress(_activeCityId);
        if (cp == null)
        {
            error = "城池进度不存在";
            return false;
        }

        return cp.UpgradeBuilding(buildingId, out error);
    }

    public CitySnapshot GetSnapshot()
    {
        var snapshot = new CitySnapshot
        {
            CityId = _activeCityId,
            CityName = "",
            Production = new Dictionary<ResourceType, int>(),
            Buildings = new List<BuildingInfo>()
        };

        if (string.IsNullOrEmpty(_activeCityId)) return snapshot;

        var cityData = DataManager.Instance.AllCities.FirstOrDefault(c => c.Id == _activeCityId);
        snapshot.CityName = cityData?.Name ?? _activeCityId;

        var cp = GameState.Instance.GetCityProgress(_activeCityId);
        if (cp == null) return snapshot;

        snapshot.Gold = cp.Gold;
        snapshot.Food = cp.Food;
        snapshot.Wood = cp.Wood;
        snapshot.Iron = cp.Iron;
        snapshot.GoldCap = cp.GoldCap;
        snapshot.FoodCap = cp.FoodCap;
        snapshot.WoodCap = cp.WoodCap;
        snapshot.IronCap = cp.IronCap;

        snapshot.Production = cp.GetTotalProduction(_activeCityScale);

        // 建筑列表
        foreach (var config in InteriorConfig.Buildings)
        {
            var building = cp.GetOrCreateBuilding(config.Id);
            int nextLevel = building.Level + 1;
            int goldCost = InteriorConfig.CalculateUpgradeCost(config.GoldUpgradeCost, nextLevel);
            int foodCost = InteriorConfig.CalculateUpgradeCost(config.FoodUpgradeCost, nextLevel);
            int woodCost = InteriorConfig.CalculateUpgradeCost(config.WoodUpgradeCost, nextLevel);
            int ironCost = InteriorConfig.CalculateUpgradeCost(config.IronUpgradeCost, nextLevel);

            bool canUpgrade = building.Level < building.MaxLevel
                && cp.Gold >= goldCost && cp.Food >= foodCost
                && cp.Wood >= woodCost && cp.Iron >= ironCost;

            int currentProd = config.BaseProduction > 0
                ? InteriorConfig.CalculateProduction(config, building.Level, _activeCityScale) : 0;

            snapshot.Buildings.Add(new BuildingInfo
            {
                Id = config.Id,
                Name = config.Name,
                Type = config.Type,
                Level = building.Level,
                MaxLevel = building.MaxLevel,
                GoldCost = goldCost,
                FoodCost = foodCost,
                WoodCost = woodCost,
                IronCost = ironCost,
                CanUpgrade = canUpgrade,
                ProducesResource = config.ProducesResource,
                CurrentProduction = currentProd
            });
        }

        return snapshot;
    }
}
