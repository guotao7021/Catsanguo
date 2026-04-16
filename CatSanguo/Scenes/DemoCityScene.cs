using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.Systems;
using CatSanguo.UI;
using CatSanguo.UI.Battle;

namespace CatSanguo.Scenes;

public class DemoCityScene : Scene
{
    private readonly string? _cityIdOverride;

    private Texture2D _pixel = null!;
    private SpriteFontBase _font = null!;
    private SpriteFontBase _titleFont = null!;
    private SpriteFontBase _smallFont = null!;

    private CitySystem _citySystem = null!;

    private CitySnapshot _snapshot;

    // Buttons
    private Button _collectButton = null!;
    private Button _backButton = null!;
    private List<Button> _upgradeButtons = new();

    // UI state
    private string _statusMessage = "";
    private float _statusTimer;
    private float _resourceFlash;

    public DemoCityScene() : this(null) { }
    public DemoCityScene(string? cityIdOverride) { _cityIdOverride = cityIdOverride; }

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.TitleFont;
        _smallFont = Game.SmallFont;

        _citySystem = GameRoot.Instance.Systems.City;

        // Ensure city is set (prefer constructor override, fallback to GameRoot)
        string cityId = _cityIdOverride ?? GameRoot.Instance.GetDemoCityId();
        if (!string.IsNullOrEmpty(cityId))
            _citySystem.SetActiveCity(cityId);

        // Buttons
        _collectButton = new Button("收取资源", new Rectangle(20, 440, 180, 40));
        _collectButton.NormalColor = new Color(60, 100, 60);
        _collectButton.HoverColor = new Color(80, 130, 80);
        _collectButton.OnClick = () =>
        {
            _citySystem.CollectResources();
            ShowStatus("收取成功! 金+50 粮+30 木+20 铁+10");
            _resourceFlash = 0.5f;
        };

        _backButton = new Button("返回地图", new Rectangle(20, GameSettings.ScreenHeight - 55, 160, 45));
        _backButton.OnClick = () => Game.SceneManager.ChangeScene(new WorldMapScene());

        RefreshSnapshot();
        BuildUpgradeButtons();
    }

    private void RefreshSnapshot()
    {
        _snapshot = _citySystem.GetSnapshot();
    }

    private void BuildUpgradeButtons()
    {
        _upgradeButtons.Clear();
        for (int i = 0; i < _snapshot.Buildings.Count; i++)
        {
            int idx = i;
            var btn = new Button("升级", new Rectangle(0, 0, 60, 26));
            btn.NormalColor = new Color(80, 70, 50);
            btn.HoverColor = new Color(110, 95, 65);
            btn.OnClick = () =>
            {
                if (idx < _snapshot.Buildings.Count)
                {
                    var b = _snapshot.Buildings[idx];
                    if (_citySystem.UpgradeBuilding(b.Id, out string error))
                    {
                        ShowStatus($"{b.Name} 升级到 Lv{b.Level + 1}!");
                        RefreshSnapshot();
                        BuildUpgradeButtons();
                    }
                    else
                    {
                        ShowStatus(error);
                    }
                }
            };
            _upgradeButtons.Add(btn);
        }
    }

    private void ShowStatus(string msg)
    {
        _statusMessage = msg;
        _statusTimer = 2.5f;
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Refresh snapshot each frame for live resource updates
        RefreshSnapshot();

        // Status timer
        if (_statusTimer > 0) _statusTimer -= dt;
        if (_resourceFlash > 0) _resourceFlash -= dt;

        // Buttons
        _collectButton.Update(Input);
        _backButton.Update(Input);

        // Upgrade buttons - update positions based on layout
        int buildStartY = 115;
        for (int i = 0; i < _upgradeButtons.Count && i < _snapshot.Buildings.Count; i++)
        {
            var b = _snapshot.Buildings[i];
            int bx = 280;
            int by = buildStartY + i * 55;
            _upgradeButtons[i].Bounds = new Rectangle(bx + 470, by + 8, 60, 26);
            if (b.Level < b.MaxLevel)
                _upgradeButtons[i].Update(Input);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(35, 28, 22));
        SpriteBatch.Begin();

        DrawBackground();
        DrawTopBar();
        DrawResourcePanel();
        DrawBuildingPanel();
        DrawBottomBar();
        DrawStatusMessage();

        SpriteBatch.End();
    }

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
        // Top bar
        SpriteBatch.Draw(_pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, 50), new Color(25, 20, 14, 230));
        SpriteBatch.Draw(_pixel, new Rectangle(0, 50, GameSettings.ScreenWidth, 1), UIHelper.PanelBorder);

        // City name
        string title = $"内政 - {_snapshot.CityName}";
        SpriteBatch.DrawString(_titleFont, title, new Vector2(15, 4), UIHelper.TitleText);

        // Phase indicator
        SpriteBatch.DrawString(_smallFont, "[Demo 模式]", new Vector2(GameSettings.ScreenWidth - 100, 18), new Color(120, 100, 70));
    }

    private void DrawResourcePanel()
    {
        int px = 15, py = 60, pw = 250, ph = 430;
        UIHelper.DrawPanel(SpriteBatch, _pixel, new Rectangle(px, py, pw, ph), UIHelper.PanelBg, UIHelper.PanelBorder, 2);

        SpriteBatch.DrawString(_font, "资 源", new Vector2(px + 100, py + 8), UIHelper.TitleText);
        SpriteBatch.Draw(_pixel, new Rectangle(px + 10, py + 35, pw - 20, 1), UIHelper.PanelBorder);

        int ry = py + 45;
        Color flashColor = _resourceFlash > 0 ? new Color(255, 255, 200) : UIHelper.BodyText;

        DrawResourceRow("金币", _snapshot.Gold, _snapshot.GoldCap, new Color(255, 215, 0), ry, px + 15);
        ry += 45;
        DrawResourceRow("粮草", _snapshot.Food, _snapshot.FoodCap, new Color(120, 200, 80), ry, px + 15);
        ry += 45;
        DrawResourceRow("木材", _snapshot.Wood, _snapshot.WoodCap, new Color(160, 120, 60), ry, px + 15);
        ry += 45;
        DrawResourceRow("铁矿", _snapshot.Iron, _snapshot.IronCap, new Color(150, 150, 170), ry, px + 15);

        // Collect button
        ry += 60;
        _collectButton.Bounds = new Rectangle(px + 10, ry, pw - 20, 40);
        _collectButton.Draw(SpriteBatch, _font, _pixel);

        // Production rates
        ry += 55;
        SpriteBatch.DrawString(_smallFont, "每秒产出:", new Vector2(px + 15, ry), new Color(140, 120, 90));
        ry += 22;
        foreach (var kvp in _snapshot.Production)
        {
            string resName = kvp.Key switch
            {
                ResourceType.Gold => "金",
                ResourceType.Food => "粮",
                ResourceType.Wood => "木",
                ResourceType.Iron => "铁",
                _ => "?"
            };
            Color resColor = kvp.Key switch
            {
                ResourceType.Gold => new Color(255, 215, 0),
                ResourceType.Food => new Color(120, 200, 80),
                ResourceType.Wood => new Color(160, 120, 60),
                ResourceType.Iron => new Color(150, 150, 170),
                _ => Color.White
            };
            SpriteBatch.DrawString(_smallFont, $"  {resName} +{kvp.Value}", new Vector2(px + 15, ry), resColor);
            ry += 20;
        }
    }

    private void DrawResourceRow(string name, int value, int cap, Color color, int y, int x)
    {
        SpriteBatch.DrawString(_font, name, new Vector2(x, y), color);
        SpriteBatch.DrawString(_font, $"{value}", new Vector2(x + 60, y), UIHelper.BodyText);
        SpriteBatch.DrawString(_smallFont, $"/{cap}", new Vector2(x + 60 + _font.MeasureString($"{value}").X + 2, y + 4), new Color(100, 90, 70));

        // Progress bar
        float ratio = cap > 0 ? Math.Clamp((float)value / cap, 0, 1) : 0;
        int barW = 200, barH = 6;
        SpriteBatch.Draw(_pixel, new Rectangle(x, y + 28, barW, barH), new Color(20, 15, 10));
        if (ratio > 0)
            SpriteBatch.Draw(_pixel, new Rectangle(x, y + 28, (int)(barW * ratio), barH), color * 0.7f);
    }

    private void DrawBuildingPanel()
    {
        int px = 280, py = 60, pw = GameSettings.ScreenWidth - 295, ph = 430;
        UIHelper.DrawPanel(SpriteBatch, _pixel, new Rectangle(px, py, pw, ph), UIHelper.PanelBg, UIHelper.PanelBorder, 2);

        SpriteBatch.DrawString(_font, "建 筑", new Vector2(px + pw / 2 - 20, py + 8), UIHelper.TitleText);
        SpriteBatch.Draw(_pixel, new Rectangle(px + 10, py + 35, pw - 20, 1), UIHelper.PanelBorder);

        int buildStartY = py + 45;
        for (int i = 0; i < _snapshot.Buildings.Count; i++)
        {
            var b = _snapshot.Buildings[i];
            int by = buildStartY + i * 55;

            // Background highlight on hover
            Rectangle rowRect = new Rectangle(px + 5, by, pw - 10, 50);
            if (Input.IsMouseInRect(rowRect))
                SpriteBatch.Draw(_pixel, rowRect, new Color(60, 50, 38));

            // Building name and level
            Color typeColor = b.Type switch
            {
                BuildingType.Resource => new Color(120, 200, 80),
                BuildingType.Military => new Color(200, 100, 80),
                BuildingType.Functional => new Color(100, 160, 220),
                BuildingType.Tech => new Color(180, 140, 220),
                _ => UIHelper.BodyText
            };

            SpriteBatch.DrawString(_font, b.Name, new Vector2(px + 15, by + 5), typeColor);
            SpriteBatch.DrawString(_smallFont, $"Lv.{b.Level}", new Vector2(px + 90, by + 8), UIHelper.BodyText);

            // Production info
            if (b.CurrentProduction > 0)
            {
                string prodText = $"+{b.CurrentProduction}/s";
                SpriteBatch.DrawString(_smallFont, prodText, new Vector2(px + 130, by + 8), new Color(160, 150, 100));
            }

            // Upgrade cost
            if (b.Level < b.MaxLevel)
            {
                string costText = $"金{b.GoldCost}";
                if (b.FoodCost > 0) costText += $" 粮{b.FoodCost}";
                SpriteBatch.DrawString(_smallFont, costText, new Vector2(px + 15, by + 28), new Color(120, 110, 85));

                // Upgrade button
                if (i < _upgradeButtons.Count)
                {
                    var btn = _upgradeButtons[i];
                    if (b.CanUpgrade)
                    {
                        btn.NormalColor = new Color(80, 100, 60);
                        btn.HoverColor = new Color(100, 130, 70);
                    }
                    else
                    {
                        btn.NormalColor = new Color(60, 55, 45);
                        btn.HoverColor = new Color(60, 55, 45);
                    }
                    btn.Draw(SpriteBatch, _smallFont, _pixel);
                }
            }
            else
            {
                SpriteBatch.DrawString(_smallFont, "满级", new Vector2(px + 470, by + 12), new Color(200, 180, 80));
            }
        }
    }

    private void DrawBottomBar()
    {
        SpriteBatch.Draw(_pixel, new Rectangle(0, GameSettings.ScreenHeight - 65, GameSettings.ScreenWidth, 65), new Color(25, 20, 14, 220));
        SpriteBatch.Draw(_pixel, new Rectangle(0, GameSettings.ScreenHeight - 65, GameSettings.ScreenWidth, 1), UIHelper.PanelBorder);

        _backButton.Draw(SpriteBatch, _font, _pixel);

        // Hint
        SpriteBatch.DrawString(_smallFont, "管理城池资源与建筑 | 返回地图进行军事行动",
            new Vector2(GameSettings.ScreenWidth / 2 - 160, GameSettings.ScreenHeight - 25),
            new Color(100, 85, 65));
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
}
