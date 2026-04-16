using System.Collections.Generic;
using System.Linq;

namespace CatSanguo.Battle.Sango;

public class ArmyGroup
{
    public Team Team { get; }
    public List<GeneralUnit> Units { get; } = new();
    public GeneralUnit? Commander => Units.FirstOrDefault();

    public ArmyGroup(Team team)
    {
        Team = team;
    }

    /// <summary>全军所有活跃士兵</summary>
    public List<Soldier> GetAllAliveSoldiers()
    {
        return Units.SelectMany(u => u.Soldiers.Where(s => s.IsAlive)).ToList();
    }

    /// <summary>当前总兵力</summary>
    public int GetTotalAlive()
    {
        return Units.Sum(u => u.AliveSoldierCount);
    }

    /// <summary>初始总兵力</summary>
    public int GetTotalMax()
    {
        return Units.Sum(u => u.InitialSoldierCount);
    }

    /// <summary>全军是否溃败</summary>
    public bool IsDefeated()
    {
        return Units.All(u => u.IsDefeated);
    }

    public void Update(float dt)
    {
        foreach (var unit in Units)
        {
            unit.Update(dt);
        }
    }
}
