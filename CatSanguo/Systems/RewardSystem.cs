using System;
using System.Collections.Generic;
using System.Linq;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Systems;

public class BattleRewardResult
{
    public int Gold { get; set; }
    public int Food { get; set; }
    public int Wood { get; set; }
    public int Iron { get; set; }
    public int Merit { get; set; }
    public int XpPerGeneral { get; set; }
    public string Rating { get; set; } = "D";
}

public class RewardSystem
{
    public BattleRewardResult Calculate(
        bool isVictory, int playerSurvivors, int playerTotal,
        int enemyKills, int enemyTotal, float battleTime,
        string cityScale = "medium", int difficulty = 1)
    {
        int baseGold = 100, baseFood = 50, baseWood = 30, baseIron = 20, baseMerit = 50;

        if (!isVictory)
        {
            return new BattleRewardResult
            {
                Gold = baseGold / 2,
                Food = baseFood / 2,
                Wood = baseWood / 2,
                Iron = baseIron / 2,
                Merit = baseMerit / 3,
                XpPerGeneral = 50,
                Rating = "D"
            };
        }

        // 难度系数
        float difficultyMultiplier = 1.0f + (difficulty - 1) * 0.2f;

        // 表现评分
        float survivalRate = playerTotal > 0 ? (float)playerSurvivors / playerTotal : 0;
        float killRate = enemyTotal > 0 ? (float)enemyKills / enemyTotal : 0;

        string rating;
        float performanceMultiplier;

        if (survivalRate >= 1.0f && killRate >= 1.0f)
        {
            rating = "S";
            performanceMultiplier = 2.0f;
        }
        else if (survivalRate >= 0.75f && killRate >= 0.75f)
        {
            rating = "A";
            performanceMultiplier = 1.5f;
        }
        else if (survivalRate >= 0.5f && killRate >= 0.5f)
        {
            rating = "B";
            performanceMultiplier = 1.2f;
        }
        else if (killRate >= 0.3f)
        {
            rating = "C";
            performanceMultiplier = 1.0f;
        }
        else
        {
            rating = "D";
            performanceMultiplier = 0.8f;
        }

        // 城池规模加成
        float cityScaleBonus = cityScale switch
        {
            "huge" => 2.0f,
            "large" => 1.5f,
            "medium" => 1.0f,
            "small" => 0.7f,
            _ => 1.0f
        };

        float finalMultiplier = difficultyMultiplier * performanceMultiplier * cityScaleBonus;

        int gold = (int)(baseGold * finalMultiplier);
        int food = (int)(baseFood * finalMultiplier);
        int wood = (int)(baseWood * finalMultiplier);
        int iron = (int)(baseIron * finalMultiplier);
        int merit = (int)(baseMerit * finalMultiplier);

        // 歼灭加成
        if (killRate >= 1.0f)
        {
            gold = (int)(gold * 1.5f);
            merit += 30;
        }

        // 经验计算
        int baseXp = 200;
        int killXp = enemyKills * 50;
        int survivalXp = playerSurvivors * 30;
        int totalXp = baseXp + killXp + survivalXp;
        int xpPerGeneral = playerTotal > 0 ? totalXp / playerTotal : totalXp;

        return new BattleRewardResult
        {
            Gold = gold,
            Food = food,
            Wood = wood,
            Iron = iron,
            Merit = merit,
            XpPerGeneral = xpPerGeneral,
            Rating = rating
        };
    }

    public void Apply(string cityId, BattleRewardResult rewards, List<string> generalIds)
    {
        // 资源发放到城池
        var cp = GameState.Instance.GetCityProgress(cityId);
        if (cp != null)
        {
            cp.AddResource(ResourceType.Gold, rewards.Gold);
            cp.AddResource(ResourceType.Food, rewards.Food);
            cp.AddResource(ResourceType.Wood, rewards.Wood);
            cp.AddResource(ResourceType.Iron, rewards.Iron);
        }

        // 战功
        GameState.Instance.AddBattleMerit(rewards.Merit);

        // 经验
        foreach (var genId in generalIds)
        {
            GameState.Instance.AddGeneralExperience(genId, rewards.XpPerGeneral);
        }

        GameState.Instance.Save();
    }
}
