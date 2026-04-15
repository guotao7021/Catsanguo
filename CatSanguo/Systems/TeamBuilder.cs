using System;
using System.Collections.Generic;
using System.Linq;
using CatSanguo.Core;
using CatSanguo.Battle;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Systems;

public class TeamBuilder
{
    private readonly List<string> _selectedIds = new();
    private readonly Dictionary<string, FormationType> _formations = new();

    public IReadOnlyList<string> SelectedIds => _selectedIds;
    public bool IsReady => _selectedIds.Count > 0;

    public void ToggleGeneral(string generalId)
    {
        if (_selectedIds.Contains(generalId))
        {
            _selectedIds.Remove(generalId);
        }
        else if (_selectedIds.Count < 3)
        {
            _selectedIds.Add(generalId);
        }
    }

    public void SetFormation(string generalId, FormationType formation)
    {
        _formations[generalId] = formation;
    }

    public FormationType GetFormation(string generalId)
    {
        return _formations.TryGetValue(generalId, out var f) ? f : FormationType.Vanguard;
    }

    public List<GeneralData> GetSquadForBattle()
    {
        var result = new List<GeneralData>();
        var allGenerals = DataManager.Instance.AllGenerals;

        foreach (var id in _selectedIds)
        {
            var baseData = allGenerals.FirstOrDefault(g => g.Id == id);
            var progress = GameState.Instance.GetGeneralProgress(id);
            if (baseData == null || progress == null) continue;

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
                Level = progress.Level,
                Experience = progress.Experience
            };

            // 使用编队中设置的阵形覆盖默认值
            if (_formations.TryGetValue(id, out var formation))
                leveled.PreferredFormation = formation.ToString();
            else
                leveled.PreferredFormation = baseData.PreferredFormation;

            result.Add(leveled);
        }

        return result;
    }

    public void SyncFromGameState()
    {
        _selectedIds.Clear();
        foreach (var id in GameState.Instance.CurrentSquad)
        {
            _selectedIds.Add(id);
        }

        // 初始化阵形
        foreach (var id in _selectedIds)
        {
            var gen = DataManager.Instance.AllGenerals.FirstOrDefault(g => g.Id == id);
            if (gen != null && !string.IsNullOrEmpty(gen.PreferredFormation))
            {
                if (Enum.TryParse<FormationType>(gen.PreferredFormation, true, out var ft))
                    _formations[id] = ft;
            }
        }
    }

    public void SyncToGameState()
    {
        GameState.Instance.SetCurrentSquad(_selectedIds.ToList());
    }
}
