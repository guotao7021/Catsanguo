using System.Collections.Generic;
using System.Linq;
using CatSanguo.Core;

namespace CatSanguo.Battle;

public class MoraleSystem
{
    private float _evaluationTimer;
    private const float EvaluationInterval = 1.0f;

    public void Update(float deltaTime, List<Squad> allSquads)
    {
        _evaluationTimer += deltaTime;
        if (_evaluationTimer < EvaluationInterval) return;
        _evaluationTimer = 0;

        foreach (var squad in allSquads)
        {
            if (squad.IsDead) continue;
            EvaluateSquadMorale(squad, allSquads);
        }
    }

    private void EvaluateSquadMorale(Squad squad, List<Squad> allSquads)
    {
        float moraleChange = 0;

        // Count attackers targeting this squad
        int attackers = allSquads.Count(s => s.Team != squad.Team && s.IsActive && s.TargetSquad == squad);
        squad.AttackersCount = attackers;
        if (attackers >= 2) moraleChange -= 3;

        // HP low
        if (squad.HP / squad.MaxHP < 0.3f) moraleChange -= 2;

        // Army-wide troop ratio
        float allyTotalHP = allSquads.Where(s => s.Team == squad.Team && !s.IsDead).Sum(s => s.HP);
        float allyMaxHP = allSquads.Where(s => s.Team == squad.Team).Sum(s => s.MaxHP);
        if (allyMaxHP > 0 && allyTotalHP / allyMaxHP < 0.5f) moraleChange -= 2;

        // Recovery when out of combat
        if (squad.TimeSinceLastCombat > 5f) moraleChange += 1;

        // Apply passive morale effects (like Cao Cao's minimum morale)
        squad.Morale = System.Math.Clamp(squad.Morale + moraleChange, 0, 100);

        // Check for fleeing
        if (squad.Morale < GameSettings.MoraleCritical && squad.State != SquadState.Fleeing)
        {
            squad.State = SquadState.Fleeing;
        }
    }

    public void OnGeneralDeath(Squad deadGeneral, List<Squad> allSquads)
    {
        foreach (var squad in allSquads)
        {
            if (squad.Team == deadGeneral.Team && !squad.IsDead && squad != deadGeneral)
            {
                squad.Morale = System.Math.Max(0, squad.Morale - 15);
            }
        }
    }
}
