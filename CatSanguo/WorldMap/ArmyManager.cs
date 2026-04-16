using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;

namespace CatSanguo.WorldMap;

public class ArmyManager
{
    private readonly List<ArmyToken> _armies = new();
    private ArmyToken? _selectedArmy;
    private Dictionary<string, CityNode> _cityLookup = new();
    private List<CityNode> _allCityNodes = new();
    private float _time;
    private bool _isAnimPhase;

    public ArmyToken? SelectedArmy => _selectedArmy;
    public IReadOnlyList<ArmyToken> Armies => _armies;
    public List<ArmyToken> ArmiesList => _armies;
    public bool IsAnimPhase => _isAnimPhase;
    public Dictionary<string, CityNode> CityLookup => _cityLookup;

    // Events
    public event Action<ArmyToken, string>? OnArmyArrived;

    public void Initialize(List<CityNode> cityNodes, List<GeneralData> allGenerals)
    {
        _allCityNodes = cityNodes;
        _cityLookup = cityNodes.ToDictionary(c => c.Data.Id, c => c);

        _armies.Clear();
        _selectedArmy = null;

        // Create player army from current squad at first owned city
        var playerCityIds = GameState.Instance.OwnedCityIds.ToList();
        var squad = GameState.Instance.CurrentSquad;
        if (squad.Count > 0 && playerCityIds.Count > 0)
        {
            string startCity = playerCityIds[0];
            var leadGen = allGenerals.FirstOrDefault(g => g.Id == squad[0]);
            var token = new ArmyToken
            {
                Id = "player_main",
                GeneralIds = squad.ToList(),
                LeadGeneralName = GetGeneralName(squad[0], allGenerals),
                LeadFormation = leadGen?.PreferredFormation ?? "vanguard",
                Team = "player",
                CurrentCityId = startCity
            };
            token.UpdateStationaryPosition(_cityLookup);
            _armies.Add(token);
        }

        // Create enemy army tokens from enemy city garrisons (all enemy factions)
        foreach (var city in cityNodes.Where(c => c.Data.Owner.StartsWith("enemy") && c.Data.Garrison.Count > 0))
        {
            var leadGenId = city.Data.Garrison[0].GeneralId;
            var leadGenData = allGenerals.FirstOrDefault(g => g.Id == leadGenId);
            var token = new ArmyToken
            {
                Id = $"enemy_{city.Data.Id}",
                GeneralIds = city.Data.Garrison.Select(g => g.GeneralId).ToList(),
                LeadGeneralName = GetGeneralName(leadGenId, allGenerals),
                LeadFormation = leadGenData?.PreferredFormation ?? city.Data.Garrison[0].FormationType,
                Team = city.Data.Owner, // Inherit full faction: "enemy_wei" or "enemy_wu"
                CurrentCityId = city.Data.Id
            };
            token.UpdateStationaryPosition(_cityLookup);
            _armies.Add(token);
        }
    }

    public void Update(float dt, InputManager input, Vector2 worldMousePos)
    {
        _time += dt;

        // 动画阶段：仅更新动画，不处理输入
        if (_isAnimPhase)
        {
            bool anyAnimating = false;
            foreach (var army in _armies)
            {
                army.UpdateAnimation(dt);
                if (army.IsAnimating) anyAnimating = true;
            }
            if (!anyAnimating)
            {
                _isAnimPhase = false;
            }
            return;
        }

        // Handle click input (using world-space mouse position)
        if (input.IsMouseClicked())
        {
            HandleClick(worldMousePos);
        }

        if (input.IsRightMouseClicked())
        {
            _selectedArmy = null;
        }
    }

    /// <summary>
    /// 回合结束时推进所有行军中的军队
    /// </summary>
    public void AdvanceAllArmies(int days)
    {
        foreach (var army in _armies.ToList())
        {
            if (!army.IsMoving) continue;

            var oldPos = army.ScreenPosition;
            int remaining = days;
            bool stopped = false;

            while (remaining > 0 && army.IsMoving && !stopped)
            {
                var result = army.AdvanceToNextCity(remaining);
                if (result == null) break;

                var (cityId, isFinal, daysUsed) = result.Value;
                remaining -= daysUsed;

                if (!_cityLookup.TryGetValue(cityId, out var cityNode)) break;

                string owner = cityNode.Data.Owner.ToLower();
                bool isFriendly = MapPathfinder.IsFriendly(owner, army.Team);

                if (isFinal)
                {
                    HandleArmyArrivalTurnBased(army, cityId, true);
                    stopped = true;
                }
                else if (!isFriendly && !(owner == "neutral" && !cityNode.Data.Garrison.Any()))
                {
                    // 遇到敌方城池 - 停下触发战斗
                    HandleArmyArrivalTurnBased(army, cityId, true);
                    stopped = true;
                }
                else if (owner == "neutral" && !cityNode.Data.Garrison.Any())
                {
                    // 空中立城池 - 占领并继续
                    if (army.Team == "player")
                    {
                        cityNode.Data.Owner = "player";
                        GameState.Instance.AddOwnedCity(cityId);
                        OnArmyArrived?.Invoke(army, cityId);
                    }
                }
                // 友方城池 - 直接通过
            }

            // 设置动画
            var newPos = army.ComputeMarchPosition(_cityLookup);
            if (Vector2.Distance(oldPos, newPos) > 1f)
            {
                army.StartAnimation(newPos);
            }
            else
            {
                army.ScreenPosition = newPos;
            }
        }

        _isAnimPhase = _armies.Any(a => a.IsAnimating);
    }

    private void HandleArmyArrivalTurnBased(ArmyToken army, string cityId, bool isFinalDest)
    {
        if (!_cityLookup.TryGetValue(cityId, out var cityNode)) return;

        string owner = cityNode.Data.Owner.ToLower();
        bool isFriendly = MapPathfinder.IsFriendly(owner, army.Team);

        if (isFriendly)
        {
            if (isFinalDest)
            {
                army.StopAtCity(cityId);
                army.UpdateStationaryPosition(_cityLookup);
            }
        }
        else if (owner == "neutral" && !cityNode.Data.Garrison.Any())
        {
            if (army.Team == "player")
            {
                cityNode.Data.Owner = "player";
                GameState.Instance.AddOwnedCity(cityId);
                OnArmyArrived?.Invoke(army, cityId);
            }
            if (isFinalDest)
            {
                army.StopAtCity(cityId);
                army.UpdateStationaryPosition(_cityLookup);
            }
        }
        else
        {
            army.StopAtCity(cityId);
            army.UpdateStationaryPosition(_cityLookup);
            OnArmyArrived?.Invoke(army, cityId);
        }
    }

    private void HandleClick(Vector2 mousePos)
    {
        // First: check if clicking on an army token
        foreach (var army in _armies.Where(a => a.Team == "player"))
        {
            if (army.GetBounds().Contains(mousePos.ToPoint()))
            {
                _selectedArmy = army;
                army.IsSelected = true;
                foreach (var other in _armies.Where(a => a != army))
                    other.IsSelected = false;
                return;
            }
        }

        // Second: if army is selected, check if clicking on a city (set as destination)
        if (_selectedArmy != null && !_selectedArmy.IsMoving)
        {
            foreach (var city in _allCityNodes)
            {
                if (city.Bounds.Contains(mousePos.ToPoint()))
                {
                    TryMoveSelectedArmy(city.Data.Id);
                    return;
                }
            }
        }

        // Clicked empty space - deselect
        _selectedArmy = null;
        foreach (var army in _armies) army.IsSelected = false;
    }

    private void TryMoveSelectedArmy(string targetCityId)
    {
        if (_selectedArmy == null || _selectedArmy.CurrentCityId == null) return;
        if (_selectedArmy.CurrentCityId == targetCityId) return;

        // Use team-aware pathfinding (passes with enemy garrison block path)
        var path = MapPathfinder.FindPath(_selectedArmy.CurrentCityId, targetCityId, _allCityNodes, _selectedArmy.Team);
        if (path.Count >= 2)
        {
            _selectedArmy.StartMove(path, _cityLookup);
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font)
    {
        // Draw path preview for selected army
        if (_selectedArmy != null && _selectedArmy.IsMoving && _selectedArmy.MovePath != null)
        {
            DrawPathPreview(sb, pixel, _selectedArmy.MovePath);
        }

        // Draw all armies
        foreach (var army in _armies)
        {
            army.Draw(sb, pixel, font, _time);
        }
    }

    public void DrawPathPreview(SpriteBatch sb, Texture2D pixel, List<string> path)
    {
        Color pathColor = new Color(255, 220, 100, 80);
        for (int i = 0; i < path.Count - 1; i++)
        {
            if (_cityLookup.TryGetValue(path[i], out var from) &&
                _cityLookup.TryGetValue(path[i + 1], out var to))
            {
                DrawLine(sb, pixel, from.Center, to.Center, pathColor, 2);
            }
        }
    }

    public List<ArmyToken> GetPlayerArmies()
    {
        return _armies.Where(a => a.Team == "player").ToList();
    }

    public ArmyToken? GetArmyAtCity(string cityId)
    {
        return _armies.FirstOrDefault(a => a.CurrentCityId == cityId);
    }

    public void RemoveArmy(string armyId)
    {
        var army = _armies.FirstOrDefault(a => a.Id == armyId);
        if (army != null)
        {
            _armies.Remove(army);
            if (_selectedArmy == army) _selectedArmy = null;
        }
    }

    private static string GetGeneralName(string generalId, List<GeneralData> allGenerals)
    {
        return allGenerals.FirstOrDefault(g => g.Id == generalId)?.Name ?? "?";
    }

    private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, Color color, int thickness)
    {
        Vector2 diff = end - start;
        float length = diff.Length();
        if (length < 1) return;
        float angle = MathF.Atan2(diff.Y, diff.X);
        sb.Draw(pixel, start, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0);
    }
}
