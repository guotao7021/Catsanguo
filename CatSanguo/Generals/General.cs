using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Generals;

public class General
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    
    // 6维属性
    public int Strength { get; set; }      // 武力
    public int Intelligence { get; set; }  // 智力
    public int Command { get; set; }       // 统帅
    public int Politics { get; set; }      // 政治
    public int Charisma { get; set; }      // 魅力
    public int Speed { get; set; }         // 速度
    public int Loyalty { get; set; } = 70; // 忠诚度
    
    public string ActiveSkillId { get; set; } = "";
    public string PassiveSkillId { get; set; } = "";
    public string PreferredFormation { get; set; } = "vanguard";
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
    
    // 历史登录
    public int AppearYear { get; set; } = 184;
    public string AppearCityId { get; set; } = "";
    public List<string> SpecialSkills { get; set; } = new();
    public int Salary { get; set; } = 10;

    // 装备系统
    public Dictionary<string, EquipmentData?> EquippedEquipment { get; set; } = new();
    
    // 技能系统
    public List<Skills.Skill> LearnedSkills { get; set; } = new();
    public Dictionary<string, int> SkillLevels { get; set; } = new();
    
    // 有效属性 (含装备和技能树加成)
    public int EffectiveStrength { get; private set; }
    public int EffectiveIntelligence { get; private set; }
    public int EffectiveCommand { get; private set; }
    public int EffectivePolitics { get; private set; }
    public int EffectiveCharisma { get; private set; }
    public int EffectiveSpeed { get; private set; }
    
    // 向后兼容
    public int Leadership
    {
        get => Command;
        set => Command = value;
    }
    public int EffectiveLeadership => EffectiveCommand;
    
    // 向后兼容 Economics
    public int Economics
    {
        get => Charisma;
        set => Charisma = value;
    }
    public int EffectiveEconomics => EffectiveCharisma;

    public void RecalculateEffectiveStats(List<EquipmentData> allEquipment, List<SkillTreeData> allSkillTrees, Data.GeneralProgress? progress = null)
    {
        int str = Strength;
        int intl = Intelligence;
        int cmd = Command;
        int pol = Politics;
        int cha = Charisma;
        int spd = Speed;

        // 装备加成
        foreach (var kvp in EquippedEquipment)
        {
            if (kvp.Value != null)
            {
                var equip = kvp.Value;
                switch (equip.StatType)
                {
                    case "strength": str += equip.StatBonus; break;
                    case "intelligence": intl += equip.StatBonus; break;
                    case "leadership": cmd += equip.StatBonus; break; // 向后兼容
                    case "command": cmd += equip.StatBonus; break;
                    case "politics": pol += equip.StatBonus; break;
                    case "charisma": cha += equip.StatBonus; break;
                    case "economics": cha += equip.StatBonus; break; // 向后兼容
                    case "speed": spd += equip.StatBonus; break;
                }
            }
        }

        // 技能树加成
        if (progress != null)
        {
            var tree = allSkillTrees.FirstOrDefault(st => st.GeneralId == Id);
            if (tree != null)
            {
                foreach (var nodeId in progress.UnlockedSkillTreeNodes)
                {
                    var node = tree.Nodes.FirstOrDefault(n => n.NodeId == nodeId);
                    if (node != null && node.NodeType == "stat")
                    {
                        int bonus = (int)node.StatValue;
                        switch (node.StatType)
                        {
                            case "strength": str += bonus; break;
                            case "intelligence": intl += bonus; break;
                            case "leadership": cmd += bonus; break; // 向后兼容
                            case "command": cmd += bonus; break;
                            case "politics": pol += bonus; break;
                            case "charisma": cha += bonus; break;
                            case "economics": cha += bonus; break; // 向后兼容
                            case "speed": spd += bonus; break;
                        }
                    }
                }
            }
        }

        EffectiveStrength = str;
        EffectiveIntelligence = intl;
        EffectiveCommand = cmd;
        EffectivePolitics = pol;
        EffectiveCharisma = cha;
        EffectiveSpeed = spd;
    }

    public static General FromData(GeneralData data)
    {
        return new General
        {
            Id = data.Id,
            Name = data.Name,
            Title = data.Title,
            Strength = data.Strength,
            Intelligence = data.Intelligence,
            Command = data.Command,
            Politics = data.Politics,
            Charisma = data.Charisma,
            Speed = data.Speed,
            Loyalty = data.Loyalty,
            AppearYear = data.AppearYear,
            AppearCityId = data.AppearCityId,
            SpecialSkills = data.SpecialSkills,
            Salary = data.Salary,
            ActiveSkillId = data.ActiveSkillId,
            PassiveSkillId = data.PassiveSkillId,
            PreferredFormation = data.PreferredFormation,
            Level = data.Level,
            Experience = data.Experience
        };
    }
}
