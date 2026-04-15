using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace CatSanguo.Battle;

public enum BattleEventType
{
    SkillUsed,
    GeneralDeath,
    MoraleBreak,
    BattleEnd
}

public class BattleEvent
{
    public float Time { get; set; }
    public string Description { get; set; } = "";
    public BattleEventType Type { get; set; }
    public Color Color { get; set; } = Color.White;
}

public class BattleEventLog
{
    private readonly List<BattleEvent> _events = new();

    public IReadOnlyList<BattleEvent> Events => _events;

    public void Add(float time, string description, BattleEventType type)
    {
        Color color = type switch
        {
            BattleEventType.SkillUsed => new Color(255, 230, 100),
            BattleEventType.GeneralDeath => new Color(255, 80, 80),
            BattleEventType.MoraleBreak => new Color(200, 150, 50),
            BattleEventType.BattleEnd => new Color(100, 220, 100),
            _ => Color.White
        };

        _events.Add(new BattleEvent
        {
            Time = time,
            Description = description,
            Type = type,
            Color = color
        });
    }

    public List<BattleEvent> GetRecent(int count)
    {
        return _events.Skip(System.Math.Max(0, _events.Count - count)).ToList();
    }
}
