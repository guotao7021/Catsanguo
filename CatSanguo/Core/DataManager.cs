using System;
using System.Collections.Generic;
using System.IO;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Core;

public class DataManager
{
    private static DataManager? _instance;
    public static DataManager Instance => _instance ?? throw new InvalidOperationException("DataManager not initialized");

    public List<GeneralData> AllGenerals { get; private set; } = new();
    public List<SkillData> AllSkills { get; private set; } = new();
    public List<StageData> AllStages { get; private set; } = new();
    public List<FormationData> AllFormations { get; private set; } = new();
    public List<CityData> AllCities { get; private set; } = new();
    public List<EquipmentData> AllEquipment { get; private set; } = new();
    public List<BondData> AllBonds { get; private set; } = new();
    public List<SkillTreeData> AllSkillTrees { get; private set; } = new();

    public static DataManager Create()
    {
        _instance = new DataManager();
        return _instance;
    }

    public void LoadAll()
    {
        string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

        AllGenerals = DataLoader.LoadList<GeneralData>(Path.Combine(dataPath, "generals.json"));
        AllSkills = DataLoader.LoadList<SkillData>(Path.Combine(dataPath, "skills.json"));
        AllStages = DataLoader.LoadList<StageData>(Path.Combine(dataPath, "stages.json"));
        AllFormations = DataLoader.LoadList<FormationData>(Path.Combine(dataPath, "formations.json"));
        AllEquipment = DataLoader.LoadList<EquipmentData>(Path.Combine(dataPath, "equipment.json"));

        string citiesPath = Path.Combine(dataPath, "cities.json");
        if (File.Exists(citiesPath))
            AllCities = DataLoader.LoadList<CityData>(citiesPath);

        string bondsPath = Path.Combine(dataPath, "bonds.json");
        if (File.Exists(bondsPath))
            AllBonds = DataLoader.LoadList<BondData>(bondsPath);

        string skillTreesPath = Path.Combine(dataPath, "skill_trees.json");
        if (File.Exists(skillTreesPath))
            AllSkillTrees = DataLoader.LoadList<SkillTreeData>(skillTreesPath);
    }
}
