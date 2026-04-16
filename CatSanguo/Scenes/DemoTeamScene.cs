using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Battle;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.Systems;
using CatSanguo.UI;
using CatSanguo.UI.Battle;

namespace CatSanguo.Scenes;

public class DemoTeamScene : Scene
{
    private enum Phase { TeamSetup, TargetSelect }

    private readonly string _sourceCityId;

    private Texture2D _pixel = null!;
    private SpriteFontBase _font = null!;
    private SpriteFontBase _titleFont = null!;
    private SpriteFontBase _smallFont = null!;

    private TeamBuilder _teamBuilder = null!;
    private List<GeneralProgress> _allGenerals = new();
    private List<CityData> _enemyCities = new();

    private Phase _phase = Phase.TeamSetup;
    private int _selectedTargetIndex = -1;

    // Formations
    private static readonly FormationType[] AvailableFormations =
    {
        FormationType.Vanguard, FormationType.Archer, FormationType.Cavalry
    };

    // Buttons
    private Button _nextButton = null!;
    private Button _prevButton = null!;
    private Button _launchButton = null!;
    private Button _backButton = null!;

    // Status
    private string _statusMessage = "";
    private float _statusTimer;

    public DemoTeamScene(string sourceCityId)
    {
        _sourceCityId = sourceCityId;
    }

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.TitleFont;
        _smallFont = Game.SmallFont;

        _teamBuilder = GameRoot.Instance.Systems.Team;
        _allGenerals = GameState.Instance.GetAllUnlockedGenerals();

        // Gather non-player cities as potential targets
        var allCities = DataManager.Instance.AllCities;
        _enemyCities = allCities
            .Where(c => c.Owner.ToLower() != "player")
            .ToList();

        // Phase 1 buttons
        _nextButton = new Button("选择目标 >>", new Rectangle(GameSettings.ScreenWidth - 200, GameSettings.ScreenHeight - 55, 180, 45));
        _nextButton.NormalColor = new Color(60, 90, 120);
        _nextButton.HoverColor = new Color(80, 120, 160);
        _nextButton.OnClick = () =>
        {
            if (!_teamBuilder.IsReady)
            {
                ShowStatus("请至少选择一名武将!");
                return;
            }
            _phase = Phase.TargetSelect;
        };

        // Phase 2 buttons
        _prevButton = new Button("<< 返回编队", new Rectangle(200, GameSettings.ScreenHeight - 55, 180, 45));
        _prevButton.NormalColor = new Color(80, 70, 50);
        _prevButton.HoverColor = new Color(110, 95, 65);
        _prevButton.OnClick = () =>
        {
            _phase = Phase.TeamSetup;
            _selectedTargetIndex = -1;
        };

        _launchButton = new Button("出  征 !", new Rectangle(GameSettings.ScreenWidth - 200, GameSettings.ScreenHeight - 55, 180, 45));
        _launchButton.NormalColor = new Color(140, 50, 30);
        _launchButton.HoverColor = new Color(180, 70, 40);
        _launchButton.OnClick = OnLaunch;

        _backButton = new Button("返回地图", new Rectangle(20, GameSettings.ScreenHeight - 55, 160, 45));
        _backButton.OnClick = () => Game.SceneManager.ChangeScene(new WorldMapScene());
    }

    private void OnLaunch()
    {
        if (_selectedTargetIndex < 0 || _selectedTargetIndex >= _enemyCities.Count)
        {
            ShowStatus("请先选择目标城池!");
            return;
        }

        var target = _enemyCities[_selectedTargetIndex];
        var selectedIds = _teamBuilder.SelectedIds.ToList();
        if (selectedIds.Count == 0)
        {
            ShowStatus("没有选择武将!");
            return;
        }

        // Find lead general name
        string leadId = selectedIds[0];
        var leadGen = _allGenerals.FirstOrDefault(g => g.Data.Id == leadId);
        string leadName = leadGen?.Data.Name ?? "Unknown";
        string leadFormation = _teamBuilder.GetFormation(leadId).ToString().ToLower();

        // Write PendingMarchData
        GameRoot.Instance.PendingMarch = new PendingMarchData
        {
            SourceCityId = _sourceCityId,
            TargetCityId = target.Id,
            GeneralIds = selectedIds,
            LeadGeneralName = leadName,
            LeadFormation = leadFormation
        };

        // Sync team to game state
        _teamBuilder.SyncToGameState();

        // Return to world map; ProcessPendingMarch will handle army creation
        Game.SceneManager.ChangeScene(new WorldMapScene());
    }

    private void ShowStatus(string msg)
    {
        _statusMessage = msg;
        _statusTimer = 2.5f;
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_statusTimer > 0) _statusTimer -= dt;

        _backButton.Update(Input);

        if (_phase == Phase.TeamSetup)
            UpdateTeamPhase();
        else
            UpdateTargetPhase();
    }

    private void UpdateTeamPhase()
    {
        _nextButton.Update(Input);

        if (!Input.IsMouseClicked()) return;

        Vector2 mp = Input.MousePosition;

        // General card clicks
        int panelX = 30, genStartY = 105;
        for (int i = 0; i < _allGenerals.Count && i < 8; i++)
        {
            Rectangle cardRect = new Rectangle(panelX, genStartY + i * 65, 580, 55);
            if (cardRect.Contains(mp.ToPoint()))
            {
                _teamBuilder.ToggleGeneral(_allGenerals[i].Data.Id);
                return;
            }
        }

        // Formation clicks (below general list)
        int formY = genStartY + Math.Min(_allGenerals.Count, 8) * 65 + 15;
        for (int i = 0; i < AvailableFormations.Length; i++)
        {
            Rectangle fRect = new Rectangle(panelX + i * 200, formY + 25, 185, 38);
            if (fRect.Contains(mp.ToPoint()))
            {
                foreach (var id in _teamBuilder.SelectedIds)
                    _teamBuilder.SetFormation(id, AvailableFormations[i]);
                ShowStatus($"全军阵形切换为 {GetFormationName(AvailableFormations[i])}");
                return;
            }
        }
    }

    private void UpdateTargetPhase()
    {
        _prevButton.Update(Input);
        _launchButton.Update(Input);

        if (!Input.IsMouseClicked()) return;

        Vector2 mp = Input.MousePosition;
        int startY = 110;
        for (int i = 0; i < _enemyCities.Count && i < 10; i++)
        {
            Rectangle row = new Rectangle(30, startY + i * 50, GameSettings.ScreenWidth - 60, 42);
            if (row.Contains(mp.ToPoint()))
            {
                _selectedTargetIndex = i;
                return;
            }
        }
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(35, 28, 22));
        SpriteBatch.Begin();

        DrawBackground();
        DrawTopBar();

        if (_phase == Phase.TeamSetup)
            DrawTeamPhase();
        else
            DrawTargetPhase();

        DrawBottomBar();
        DrawStatusMessage();

        SpriteBatch.End();
    }

    // ==================== Drawing helpers ====================

    private void DrawBackground()
    {
        for (int y = 0; y < GameSettings.ScreenHeight; y += 4)
        {
            float t = (float)y / GameSettings.ScreenHeight;
            byte r = (byte)MathHelper.Lerp(42, 30, t);
            byte g = (byte)MathHelper.Lerp(36, 24, t);
            byte b = (byte)MathHelper.Lerp(28, 18, t);
            SpriteBatch.Draw(_pixel, new Rectangle(0, y, GameSettings.ScreenWidth, 4), new Color(r, g, b));
        }
    }

    private void DrawTopBar()
    {
        SpriteBatch.Draw(_pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, 50), new Color(25, 20, 14, 230));
        SpriteBatch.Draw(_pixel, new Rectangle(0, 50, GameSettings.ScreenWidth, 1), UIHelper.PanelBorder);

        string title = _phase == Phase.TeamSetup ? "编队出征 - 选择武将" : "编队出征 - 选择目标";
        SpriteBatch.DrawString(_titleFont, title, new Vector2(15, 4), UIHelper.TitleText);

        // Phase indicators
        int indX = GameSettings.ScreenWidth - 260;
        Color p1Color = _phase == Phase.TeamSetup ? new Color(220, 180, 80) : new Color(100, 90, 70);
        Color p2Color = _phase == Phase.TargetSelect ? new Color(220, 180, 80) : new Color(100, 90, 70);
        SpriteBatch.DrawString(_smallFont, "[1]编队", new Vector2(indX, 18), p1Color);
        SpriteBatch.DrawString(_smallFont, " > ", new Vector2(indX + 60, 18), new Color(80, 70, 55));
        SpriteBatch.DrawString(_smallFont, "[2]目标", new Vector2(indX + 80, 18), p2Color);
        SpriteBatch.DrawString(_smallFont, "[Demo]", new Vector2(GameSettings.ScreenWidth - 65, 18), new Color(120, 100, 70));
    }

    // ==================== Phase 1: Team Setup ====================

    private void DrawTeamPhase()
    {
        int panelX = 20, panelY = 58, panelW = 600;
        int panelH = GameSettings.ScreenHeight - 130;
        UIHelper.DrawPanel(SpriteBatch, _pixel, new Rectangle(panelX, panelY, panelW, panelH),
            UIHelper.PanelBg, UIHelper.PanelBorder, 2);

        SpriteBatch.DrawString(_font, "可用武将", new Vector2(panelX + 15, panelY + 10), UIHelper.TitleText);
        SpriteBatch.DrawString(_smallFont, $"已选: {_teamBuilder.SelectedIds.Count}/3",
            new Vector2(panelX + panelW - 100, panelY + 12), new Color(180, 160, 100));
        SpriteBatch.Draw(_pixel, new Rectangle(panelX + 10, panelY + 35, panelW - 20, 1), UIHelper.PanelBorder);

        int genStartY = panelY + 45;
        int displayCount = Math.Min(_allGenerals.Count, 8);

        for (int i = 0; i < displayCount; i++)
        {
            var gp = _allGenerals[i];
            string genId = gp.Data.Id;
            bool selected = _teamBuilder.SelectedIds.Contains(genId);
            int cy = genStartY + i * 65;

            Rectangle cardRect = new Rectangle(panelX + 10, cy, panelW - 20, 55);

            // Card background
            Color cardBg = selected ? new Color(60, 55, 40) : new Color(42, 38, 30);
            bool hovered = Input.IsMouseInRect(cardRect);
            if (hovered && !selected) cardBg = new Color(50, 46, 35);
            SpriteBatch.Draw(_pixel, cardRect, cardBg);

            // Selection indicator
            if (selected)
            {
                SpriteBatch.Draw(_pixel, new Rectangle(cardRect.X, cardRect.Y, 4, cardRect.Height), new Color(220, 180, 80));
                SpriteBatch.DrawString(_font, "\u2713", new Vector2(cardRect.Right - 35, cy + 14), new Color(80, 200, 80));
            }

            // Border
            UIHelper.DrawBorder(SpriteBatch, _pixel, cardRect, selected ? new Color(180, 150, 70) : new Color(70, 60, 45), 1);

            // General info
            SpriteBatch.DrawString(_font, gp.Data.Name, new Vector2(panelX + 25, cy + 5), UIHelper.TitleText);
            SpriteBatch.DrawString(_smallFont, $"Lv.{gp.Level}", new Vector2(panelX + 25, cy + 30), UIHelper.BodyText);

            string stats = $"武{(int)gp.GetEffectiveStat(gp.Data.Strength)} " +
                           $"智{(int)gp.GetEffectiveStat(gp.Data.Intelligence)} " +
                           $"统{(int)gp.GetEffectiveStat(gp.Data.Leadership)}";
            SpriteBatch.DrawString(_smallFont, stats, new Vector2(panelX + 100, cy + 30), new Color(140, 130, 110));

            // Formation display for selected
            if (selected)
            {
                var ft = _teamBuilder.GetFormation(genId);
                string ftName = GetFormationName(ft);
                SpriteBatch.DrawString(_smallFont, ftName, new Vector2(panelX + 300, cy + 8), new Color(160, 140, 100));
            }

            // Title
            if (!string.IsNullOrEmpty(gp.Data.Title))
                SpriteBatch.DrawString(_smallFont, gp.Data.Title, new Vector2(panelX + 130, cy + 8), new Color(120, 110, 90));
        }

        // Formation selection
        int formY = genStartY + displayCount * 65 + 5;
        SpriteBatch.DrawString(_font, "阵 形", new Vector2(panelX + 15, formY), UIHelper.TitleText);
        SpriteBatch.Draw(_pixel, new Rectangle(panelX + 10, formY + 25, panelW - 20, 1), new Color(60, 50, 40));

        for (int i = 0; i < AvailableFormations.Length; i++)
        {
            Rectangle fRect = new Rectangle(panelX + 10 + i * 200, formY + 30, 185, 38);

            bool isHover = Input.IsMouseInRect(fRect);
            Color bg = isHover ? new Color(70, 60, 45) : new Color(50, 44, 35);
            SpriteBatch.Draw(_pixel, fRect, bg);
            UIHelper.DrawBorder(SpriteBatch, _pixel, fRect, new Color(90, 75, 55), 1);

            string name = GetFormationName(AvailableFormations[i]);
            Vector2 nameSize = _smallFont.MeasureString(name);
            SpriteBatch.DrawString(_smallFont, name,
                new Vector2(fRect.X + (fRect.Width - nameSize.X) / 2, fRect.Y + (fRect.Height - nameSize.Y) / 2),
                new Color(200, 180, 130));
        }

        // Right side: selected squad summary
        int sumX = 640, sumY = 58, sumW = GameSettings.ScreenWidth - 660, sumH = panelH;
        UIHelper.DrawPanel(SpriteBatch, _pixel, new Rectangle(sumX, sumY, sumW, sumH),
            UIHelper.PanelBg, UIHelper.PanelBorder, 2);

        SpriteBatch.DrawString(_font, "出征编队", new Vector2(sumX + 15, sumY + 10), UIHelper.TitleText);
        SpriteBatch.Draw(_pixel, new Rectangle(sumX + 10, sumY + 35, sumW - 20, 1), UIHelper.PanelBorder);

        int sy = sumY + 50;
        if (_teamBuilder.SelectedIds.Count == 0)
        {
            SpriteBatch.DrawString(_smallFont, "尚未选择武将", new Vector2(sumX + 20, sy), new Color(120, 110, 90));
            SpriteBatch.DrawString(_smallFont, "点击左侧武将卡片进行选择", new Vector2(sumX + 20, sy + 22), new Color(100, 90, 75));
        }
        else
        {
            foreach (var id in _teamBuilder.SelectedIds)
            {
                var gp = _allGenerals.FirstOrDefault(g => g.Data.Id == id);
                if (gp == null) continue;

                SpriteBatch.DrawString(_font, gp.Data.Name, new Vector2(sumX + 20, sy), UIHelper.TitleText);
                var ft = _teamBuilder.GetFormation(id);
                SpriteBatch.DrawString(_smallFont, GetFormationName(ft), new Vector2(sumX + 120, sy + 4), new Color(160, 140, 100));

                string statsLine = $"武{(int)gp.GetEffectiveStat(gp.Data.Strength)} " +
                                   $"智{(int)gp.GetEffectiveStat(gp.Data.Intelligence)} " +
                                   $"统{(int)gp.GetEffectiveStat(gp.Data.Leadership)} " +
                                   $"速{(int)gp.GetEffectiveStat(gp.Data.Speed)}";
                SpriteBatch.DrawString(_smallFont, statsLine, new Vector2(sumX + 20, sy + 28), new Color(140, 130, 110));
                sy += 60;
            }
        }

        // Source city info
        sy = sumY + sumH - 60;
        SpriteBatch.Draw(_pixel, new Rectangle(sumX + 10, sy - 5, sumW - 20, 1), new Color(60, 50, 40));
        var sourceCity = DataManager.Instance.AllCities.FirstOrDefault(c => c.Id == _sourceCityId);
        string sourceName = sourceCity?.Name ?? _sourceCityId;
        SpriteBatch.DrawString(_smallFont, $"出发城池: {sourceName}", new Vector2(sumX + 20, sy + 5), new Color(120, 180, 120));
    }

    // ==================== Phase 2: Target Selection ====================

    private void DrawTargetPhase()
    {
        int px = 20, py = 58, pw = GameSettings.ScreenWidth - 40;
        int ph = GameSettings.ScreenHeight - 130;
        UIHelper.DrawPanel(SpriteBatch, _pixel, new Rectangle(px, py, pw, ph),
            UIHelper.PanelBg, UIHelper.PanelBorder, 2);

        SpriteBatch.DrawString(_font, "选择攻击目标", new Vector2(px + 15, py + 10), UIHelper.TitleText);

        // Squad summary in top-right
        string squadInfo = string.Join(", ", _teamBuilder.SelectedIds.Select(id =>
        {
            var gp = _allGenerals.FirstOrDefault(g => g.Data.Id == id);
            return gp?.Data.Name ?? id;
        }));
        SpriteBatch.DrawString(_smallFont, $"出战: {squadInfo}",
            new Vector2(px + pw - 400, py + 12), new Color(160, 140, 100));

        SpriteBatch.Draw(_pixel, new Rectangle(px + 10, py + 35, pw - 20, 1), UIHelper.PanelBorder);

        // Column headers
        int headerY = py + 40;
        SpriteBatch.DrawString(_smallFont, "城池名", new Vector2(px + 30, headerY), new Color(140, 120, 90));
        SpriteBatch.DrawString(_smallFont, "势力", new Vector2(px + 220, headerY), new Color(140, 120, 90));
        SpriteBatch.DrawString(_smallFont, "类型", new Vector2(px + 380, headerY), new Color(140, 120, 90));
        SpriteBatch.DrawString(_smallFont, "规模", new Vector2(px + 500, headerY), new Color(140, 120, 90));
        SpriteBatch.DrawString(_smallFont, "驻军", new Vector2(px + 620, headerY), new Color(140, 120, 90));
        SpriteBatch.Draw(_pixel, new Rectangle(px + 10, headerY + 18, pw - 20, 1), new Color(60, 50, 40));

        int startY = headerY + 22;
        int displayCount = Math.Min(_enemyCities.Count, 10);

        for (int i = 0; i < displayCount; i++)
        {
            var city = _enemyCities[i];
            int ry = startY + i * 50;
            bool selected = i == _selectedTargetIndex;

            Rectangle rowRect = new Rectangle(px + 5, ry, pw - 10, 42);
            bool hovered = Input.IsMouseInRect(rowRect);

            // Row background
            Color rowBg = selected ? new Color(70, 55, 35) :
                          hovered ? new Color(55, 48, 35) : new Color(42, 38, 30);
            SpriteBatch.Draw(_pixel, rowRect, rowBg);

            if (selected)
            {
                UIHelper.DrawBorder(SpriteBatch, _pixel, rowRect, new Color(220, 180, 80), 1);
                SpriteBatch.Draw(_pixel, new Rectangle(rowRect.X, rowRect.Y, 4, rowRect.Height), new Color(220, 180, 80));
            }

            // City name
            Color nameColor = selected ? UIHelper.TitleText : UIHelper.BodyText;
            SpriteBatch.DrawString(_font, city.Name, new Vector2(px + 30, ry + 8), nameColor);

            // Owner / faction
            string ownerText = city.Owner.ToLower() switch
            {
                "enemy_wei" => "魏",
                "enemy_wu" => "吴",
                "neutral" => "中立",
                _ => city.Owner
            };
            Color ownerColor = city.Owner.ToLower() switch
            {
                "enemy_wei" => new Color(100, 130, 200),
                "enemy_wu" => new Color(200, 80, 80),
                "neutral" => new Color(160, 160, 140),
                _ => UIHelper.BodyText
            };
            SpriteBatch.DrawString(_font, ownerText, new Vector2(px + 220, ry + 8), ownerColor);

            // Type
            string typeText = city.CityType switch
            {
                "pass" => "关隘",
                "port" => "港口",
                _ => "城池"
            };
            SpriteBatch.DrawString(_smallFont, typeText, new Vector2(px + 380, ry + 12), new Color(140, 130, 110));

            // Scale
            string scaleText = city.CityScale switch
            {
                "small" => "小",
                "large" => "大",
                "huge" => "巨",
                _ => "中"
            };
            SpriteBatch.DrawString(_smallFont, scaleText, new Vector2(px + 500, ry + 12), new Color(140, 130, 110));

            // Garrison count
            int garrisonCount = city.Garrison?.Count ?? 0;
            string garText = garrisonCount > 0 ? $"{garrisonCount}队" : "无";
            Color garColor = garrisonCount > 2 ? new Color(200, 80, 80) :
                             garrisonCount > 0 ? new Color(200, 170, 80) : new Color(100, 100, 90);
            SpriteBatch.DrawString(_smallFont, garText, new Vector2(px + 620, ry + 12), garColor);
        }

        if (_enemyCities.Count == 0)
        {
            SpriteBatch.DrawString(_font, "没有可攻击的城池", new Vector2(px + pw / 2 - 80, startY + 50), new Color(160, 140, 100));
        }
    }

    // ==================== Bottom Bar & Status ====================

    private void DrawBottomBar()
    {
        SpriteBatch.Draw(_pixel, new Rectangle(0, GameSettings.ScreenHeight - 65, GameSettings.ScreenWidth, 65), new Color(25, 20, 14, 220));
        SpriteBatch.Draw(_pixel, new Rectangle(0, GameSettings.ScreenHeight - 65, GameSettings.ScreenWidth, 1), UIHelper.PanelBorder);

        _backButton.Draw(SpriteBatch, _font, _pixel);

        if (_phase == Phase.TeamSetup)
        {
            _nextButton.Draw(SpriteBatch, _font, _pixel);
            SpriteBatch.DrawString(_smallFont, "点击武将卡片选择/取消 | 选阵形后点击下一步",
                new Vector2(GameSettings.ScreenWidth / 2 - 170, GameSettings.ScreenHeight - 25),
                new Color(100, 85, 65));
        }
        else
        {
            _prevButton.Draw(SpriteBatch, _font, _pixel);
            _launchButton.Draw(SpriteBatch, _font, _pixel);
            SpriteBatch.DrawString(_smallFont, "选择目标城池后点击出征",
                new Vector2(GameSettings.ScreenWidth / 2 - 80, GameSettings.ScreenHeight - 25),
                new Color(100, 85, 65));
        }
    }

    private void DrawStatusMessage()
    {
        if (_statusTimer > 0 && !string.IsNullOrEmpty(_statusMessage))
        {
            float alpha = Math.Min(_statusTimer, 1f);
            Vector2 msgSize = _font.MeasureString(_statusMessage);
            float msgX = (GameSettings.ScreenWidth - msgSize.X) / 2;
            float msgY = GameSettings.ScreenHeight / 2 - 30;

            SpriteBatch.Draw(_pixel, new Rectangle((int)msgX - 20, (int)msgY - 8, (int)msgSize.X + 40, 40),
                new Color(40, 35, 25) * alpha);
            UIHelper.DrawBorder(SpriteBatch, _pixel, new Rectangle((int)msgX - 20, (int)msgY - 8, (int)msgSize.X + 40, 40),
                new Color(200, 170, 80) * alpha, 1);
            SpriteBatch.DrawString(_font, _statusMessage, new Vector2(msgX, msgY), new Color(255, 230, 130) * alpha);
        }
    }

    private static string GetFormationName(FormationType ft) => ft switch
    {
        FormationType.Vanguard => "先锋阵",
        FormationType.Archer => "弓兵阵",
        FormationType.Cavalry => "骑兵阵",
        _ => ft.ToString()
    };
}
