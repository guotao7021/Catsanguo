using System;
using System.Collections.Generic;
using CatSanguo.Core;

namespace CatSanguo.Battle;

public class BattleContext
{
    public EventBus EventBus { get; }
    public BuffSystem BuffSystem { get; }
    public SkillTriggerSystem SkillTriggerSystem { get; }
    public List<Squad> AllSquads { get; set; } = new();
    public Random Rng { get; }

    public BattleContext(EventBus eventBus, BuffSystem buffSystem, SkillTriggerSystem skillTriggerSystem)
    {
        EventBus = eventBus;
        BuffSystem = buffSystem;
        SkillTriggerSystem = skillTriggerSystem;
        Rng = new Random();
    }
}
