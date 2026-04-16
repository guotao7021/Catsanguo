using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.UI.Battle;

namespace CatSanguo.WorldMap;

public class FactionLegendPanel
{
    private const int PanelX = 5;
    private const int PanelY = 60;
    private const int PanelW = 230;
    private const int TitleH = 30;
    private const int RowH = 26;
    private const int CityRowH = 22;
    private const int Padding = 8;
    private const int ColorBlockW = 14;
    private const int ColorBlockH = 14;

    private const int GenRowH = 18;

    private List<FactionEntry> _factions = new();
    private int _expandedIndex = -1;
    private int _expandedCityFactionIdx = -1;
    private int _expandedCityIdx = -1;
    private bool _mouseInPanel = false;
    private List<GeneralData> _allGenerals = new();

    public Action<Vector2>? OnCityClicked { get; set; }
    public bool IsMouseInPanel => _mouseInPanel;

    public void Build(List<CityNode> cities, List<ScenarioFaction> scenarioFactions,
                      List<GeneralData> allGenerals)
    {
        _factions.Clear();
        _expandedIndex = -1;
        _expandedCityFactionIdx = -1;
        _expandedCityIdx = -1;
        _allGenerals = allGenerals;

        // Group cities by owner
        var cityByOwner = new Dictionary<string, List<CityInfo>>();
        foreach (var city in cities)
        {
            string owner = city.Data.Owner.ToLower();
            if (!cityByOwner.ContainsKey(owner))
                cityByOwner[owner] = new List<CityInfo>();
            cityByOwner[owner].Add(new CityInfo { CityId = city.Data.Id, Name = city.Data.Name, WorldPos = city.Center });
        }

        // Player faction first
        string playerFactionId = GameState.Instance.PlayerFactionId;
        var playerScenarioFaction = scenarioFactions.FirstOrDefault(f => f.FactionId == playerFactionId);

        if (cityByOwner.ContainsKey("player"))
        {
            string leaderName = ResolveLeaderName(playerScenarioFaction, allGenerals);
            _factions.Add(new FactionEntry
            {
                FactionId = "player",
                FactionName = playerScenarioFaction?.FactionName ?? "玩家势力",
                LeaderName = leaderName,
                FillColor = ProvinceRenderer.GetFactionSolidColor("player"),
                Cities = cityByOwner["player"]
            });
        }

        // Other factions (non-player, non-neutral)
        foreach (var sf in scenarioFactions)
        {
            if (sf.FactionId == playerFactionId) continue;

            // The owner key in CityData is the factionId (set by ScenarioManager)
            string ownerKey = sf.FactionId.ToLower();
            var factionCities = cityByOwner.ContainsKey(ownerKey)
                ? cityByOwner[ownerKey] : new List<CityInfo>();

            if (factionCities.Count == 0) continue;

            string leaderName = ResolveLeaderName(sf, allGenerals);
            _factions.Add(new FactionEntry
            {
                FactionId = sf.FactionId,
                FactionName = sf.FactionName,
                LeaderName = leaderName,
                FillColor = ProvinceRenderer.GetFactionSolidColor(sf.FactionId),
                Cities = factionCities
            });
        }

        // Neutral cities last
        if (cityByOwner.ContainsKey("neutral") && cityByOwner["neutral"].Count > 0)
        {
            _factions.Add(new FactionEntry
            {
                FactionId = "neutral",
                FactionName = "无主城池",
                LeaderName = "",
                FillColor = ProvinceRenderer.GetFactionSolidColor("neutral"),
                Cities = cityByOwner["neutral"]
            });
        }
    }

    private string ResolveLeaderName(ScenarioFaction? sf, List<GeneralData> allGenerals)
    {
        if (sf == null) return "";
        if (!string.IsNullOrEmpty(sf.LeaderId))
        {
            var gen = allGenerals.FirstOrDefault(g => g.Id == sf.LeaderId);
            if (gen != null) return gen.Name;
        }
        // Fallback: first general in initialGenerals
        if (sf.InitialGenerals.Count > 0)
        {
            var gen = allGenerals.FirstOrDefault(g => g.Id == sf.InitialGenerals[0].GeneralId);
            if (gen != null) return gen.Name;
        }
        return sf.FactionName;
    }

    public void Update(InputManager input)
    {
        int panelH = ComputePanelHeight();
        var panelRect = new Rectangle(PanelX, PanelY, PanelW, panelH);
        _mouseInPanel = input.IsMouseInRect(panelRect);

        if (!_mouseInPanel) return;
        if (!input.IsMouseClicked()) return;

        var mousePos = input.MousePosition;
        int y = PanelY + TitleH;

        for (int i = 0; i < _factions.Count; i++)
        {
            var factionRowRect = new Rectangle(PanelX, y, PanelW, RowH);
            y += RowH;

            if (factionRowRect.Contains(mousePos.ToPoint()))
            {
                // Toggle expand/collapse
                _expandedIndex = (_expandedIndex == i) ? -1 : i;
                return;
            }

            // If this faction is expanded, check city rows
            if (_expandedIndex == i)
            {
                var entry = _factions[i];
                // Skip leader name row (must match Draw logic)
                int detailY = y;
                if (!string.IsNullOrEmpty(entry.LeaderName) && entry.FactionId != "neutral")
                    detailY += CityRowH;
                // Separator
                detailY += 4;

                for (int c = 0; c < entry.Cities.Count; c++)
                {
                    var cityRect = new Rectangle(PanelX + Padding, detailY, PanelW - Padding * 2, CityRowH);
                    if (cityRect.Contains(mousePos.ToPoint()))
                    {
                        // 切换该城池的武将展示
                        if (_expandedCityFactionIdx == i && _expandedCityIdx == c)
                        {
                            _expandedCityFactionIdx = -1;
                            _expandedCityIdx = -1;
                        }
                        else
                        {
                            _expandedCityFactionIdx = i;
                            _expandedCityIdx = c;
                        }
                        OnCityClicked?.Invoke(entry.Cities[c].WorldPos);
                        return;
                    }
                    detailY += CityRowH;

                    // 跳过已展开的武将行
                    if (_expandedCityFactionIdx == i && _expandedCityIdx == c)
                    {
                        var genNames = GetCityGeneralNames(entry.Cities[c].CityId);
                        detailY += genNames.Count > 0 ? genNames.Count * GenRowH : GenRowH;
                    }
                }

                y = detailY + 4;
            }
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont)
    {
        int panelH = ComputePanelHeight();
        var panelRect = new Rectangle(PanelX, PanelY, PanelW, panelH);

        // Background
        sb.Draw(pixel, panelRect, new Color(25, 22, 18, 220));
        // Border
        UIHelper.DrawBorder(sb, pixel, panelRect, new Color(100, 80, 50), 2);

        // Title
        var titleText = "势 力 图 例";
        var titleSize = smallFont.MeasureString(titleText);
        sb.DrawString(smallFont, titleText,
            new Vector2(PanelX + (PanelW - titleSize.X) / 2, PanelY + 6),
            new Color(220, 190, 130));

        // Separator under title
        sb.Draw(pixel, new Rectangle(PanelX + Padding, PanelY + TitleH - 2, PanelW - Padding * 2, 1),
            new Color(80, 65, 45));

        int y = PanelY + TitleH;

        for (int i = 0; i < _factions.Count; i++)
        {
            var entry = _factions[i];
            bool isExpanded = (_expandedIndex == i);
            bool isHovered = _mouseInPanel && new Rectangle(PanelX, y, PanelW, RowH)
                .Contains(Microsoft.Xna.Framework.Input.Mouse.GetState().Position);

            // Row background on hover
            if (isHovered || isExpanded)
            {
                sb.Draw(pixel, new Rectangle(PanelX + 2, y, PanelW - 4, RowH),
                    isExpanded ? new Color(60, 50, 40, 200) : new Color(50, 42, 35, 150));
            }

            // Color block
            int blockY = y + (RowH - ColorBlockH) / 2;
            sb.Draw(pixel, new Rectangle(PanelX + Padding, blockY, ColorBlockW, ColorBlockH), entry.FillColor);
            UIHelper.DrawBorder(sb, pixel,
                new Rectangle(PanelX + Padding, blockY, ColorBlockW, ColorBlockH),
                new Color(80, 70, 55), 1);

            // Faction name
            string displayName = entry.FactionName;
            if (displayName.Length > 6) displayName = displayName.Substring(0, 6);
            sb.DrawString(smallFont, displayName,
                new Vector2(PanelX + Padding + ColorBlockW + 6, y + 4),
                new Color(220, 200, 160));

            // City count
            string countText = $"[{entry.Cities.Count}城]";
            var countSize = smallFont.MeasureString(countText);
            sb.DrawString(smallFont, countText,
                new Vector2(PanelX + PanelW - Padding - countSize.X, y + 4),
                new Color(160, 145, 110));

            y += RowH;

            // Expanded detail area
            if (isExpanded)
            {
                // Leader name
                if (!string.IsNullOrEmpty(entry.LeaderName) && entry.FactionId != "neutral")
                {
                    sb.DrawString(smallFont, $"君主: {entry.LeaderName}",
                        new Vector2(PanelX + Padding + 4, y + 2),
                        new Color(200, 175, 120));
                    y += CityRowH;
                }

                // Separator
                sb.Draw(pixel, new Rectangle(PanelX + Padding + 4, y, PanelW - Padding * 2 - 8, 1),
                    new Color(70, 58, 40));
                y += 4;

                // City list
                for (int c = 0; c < entry.Cities.Count; c++)
                {
                    var cityRect = new Rectangle(PanelX + Padding, y, PanelW - Padding * 2, CityRowH);
                    bool cityHover = _mouseInPanel && cityRect.Contains(
                        Microsoft.Xna.Framework.Input.Mouse.GetState().Position);
                    bool cityExpanded = (_expandedCityFactionIdx == i && _expandedCityIdx == c);

                    if (cityHover || cityExpanded)
                    {
                        sb.Draw(pixel, cityRect, new Color(80, 65, 45, 150));
                    }

                    string cityIndicator = cityExpanded ? "▼" : "▶";
                    sb.DrawString(smallFont, $" {cityIndicator} {entry.Cities[c].Name}",
                        new Vector2(PanelX + Padding + 8, y + 2),
                        cityHover ? new Color(255, 230, 160) : new Color(190, 175, 140));

                    y += CityRowH;

                    // 展开的武将列表
                    if (cityExpanded)
                    {
                        var genNames = GetCityGeneralNames(entry.Cities[c].CityId);
                        if (genNames.Count == 0)
                        {
                            sb.DrawString(smallFont, "   (无武将)",
                                new Vector2(PanelX + Padding + 16, y + 1),
                                new Color(130, 120, 100));
                            y += GenRowH;
                        }
                        else
                        {
                            foreach (var gn in genNames)
                            {
                                sb.DrawString(smallFont, $"   - {gn}",
                                    new Vector2(PanelX + Padding + 16, y + 1),
                                    new Color(170, 200, 160));
                                y += GenRowH;
                            }
                        }
                    }
                }

                y += 4;
            }
        }
    }

    private int ComputePanelHeight()
    {
        int h = TitleH;
        for (int i = 0; i < _factions.Count; i++)
        {
            h += RowH;
            if (_expandedIndex == i)
            {
                var entry = _factions[i];
                if (!string.IsNullOrEmpty(entry.LeaderName) && entry.FactionId != "neutral")
                    h += CityRowH;
                h += 4; // separator
                for (int c = 0; c < entry.Cities.Count; c++)
                {
                    h += CityRowH;
                    if (_expandedCityFactionIdx == i && _expandedCityIdx == c)
                    {
                        var genNames = GetCityGeneralNames(entry.Cities[c].CityId);
                        h += genNames.Count > 0 ? genNames.Count * GenRowH : GenRowH;
                    }
                }
                h += 4; // bottom padding
            }
        }
        // Clamp to screen height
        int maxH = GameSettings.ScreenHeight - PanelY - 40;
        return Math.Min(h + 4, maxH);
    }

    private List<string> GetCityGeneralNames(string cityId)
    {
        var names = new List<string>();
        var cityProgress = GameState.Instance.GetCityProgress(cityId);
        if (cityProgress == null) return names;

        foreach (var genId in cityProgress.GeneralIds)
        {
            var gp = GameState.Instance.GetGeneralProgress(genId);
            if (gp == null || !gp.IsUnlocked) continue;
            var gen = _allGenerals.FirstOrDefault(g => g.Id == genId);
            if (gen != null)
                names.Add(gen.Name);
        }
        return names;
    }

    private struct FactionEntry
    {
        public string FactionId;
        public string FactionName;
        public string LeaderName;
        public Color FillColor;
        public List<CityInfo> Cities;
    }

    private struct CityInfo
    {
        public string CityId;
        public string Name;
        public Vector2 WorldPos;
    }
}
