using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.UI;

namespace CatSanguo.Scenes;

public class CityDetailScene : Scene
{
    private string _cityId;
    private string _cityName;

    private Texture2D _pixel;
    private SpriteFontBase _font;
    private SpriteFontBase _titleFont;
    private SpriteFontBase _smallFont;

    private CityData _cityData = new();
    private CityProgress? _progress;
    private List<GeneralData> _allGenerals = new();

    private Button? _backButton;
    private Button? _recruitButton;
    private Button? _upgradeButton;
    private Button? _reinforceButton;

    private string _messageText = "";
    private float _messageTimer = 0f;

    private int _scrollOffset = 0;

    public CityDetailScene(string cityId, string cityName)
    {
        _cityId = cityId;
        _cityName = cityName;
    }

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.NotifyFont;
        _smallFont = Game.SmallFont;

        string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        _allGenerals = DataLoader.LoadList<GeneralData>(Path.Combine(dataPath, "generals.json"));

        // Find city data
        var cities = DataLoader.LoadList<CityData>(Path.Combine(dataPath, "cities.json"));
        _cityData = cities.FirstOrDefault(c => c.Id == _cityId) ?? new CityData { Name = _cityName };

        // Get or create progress
        _progress = GameState.Instance.GetCityProgress(_cityId);
        if (_progress == null)
        {
            _progress = GameState.Instance.GetOrCreateCityProgress(_cityData);
        }

        CreateButtons();
    }

    private void CreateButtons()
    {
        int btnX = GameSettings.ScreenWidth - 100;
        int btnY = 20;

        _backButton = new Button("返 回", new Rectangle(btnX, btnY, 80, 40));
        _backButton.NormalColor = new Color(60, 30, 30);
        _backButton.HoverColor = new Color(80, 40, 40);
        _backButton.OnClick = () => Game.SceneManager.ChangeScene(new WorldMapScene());

        // Action buttons in middle panel
        int actionX = 390;
        int actionY = 120;
        int btnW = 260;
        int btnH = 45;

        int recruitCost = GetRecruitCost();
        _recruitButton = new Button($"招募武将 (-{recruitCost}战功)", new Rectangle(actionX, actionY, btnW, btnH));
        _recruitButton.NormalColor = new Color(50, 40, 30);
        _recruitButton.HoverColor = new Color(70, 60, 40);
        _recruitButton.OnClick = RecruitRandomGeneral;

        int upgradeCost = _progress!.Level * _cityData.UpgradeCost;
        _upgradeButton = new Button($"升级城池 (-{upgradeCost}战功)", new Rectangle(actionX, actionY + 60, btnW, btnH));
        _upgradeButton.NormalColor = new Color(50, 40, 30);
        _upgradeButton.HoverColor = new Color(70, 60, 40);
        _upgradeButton.OnClick = UpgradeCity;

        _reinforceButton = new Button($"征兵 50人 (-50粮草)", new Rectangle(actionX, actionY + 120, btnW, btnH));
        _reinforceButton.NormalColor = new Color(50, 40, 30);
        _reinforceButton.HoverColor = new Color(70, 60, 40);
        _reinforceButton.OnClick = ReinforceCity;
    }

    private int GetRecruitCost()
    {
        return 80 + (_progress?.Level ?? 1) * 20;
    }

    private void RecruitRandomGeneral()
    {
        if (_progress == null) return;

        int cost = GetRecruitCost();
        if (GameState.Instance.BattleMerit < cost)
        {
            ShowMessage("战功不足！");
            return;
        }

        // Find locked generals
        var allLocked = _allGenerals.Where(g =>
        {
            var gp = GameState.Instance.GetGeneralProgress(g.Id);
            return gp != null && !gp.IsUnlocked;
        }).ToList();

        if (allLocked.Count == 0)
        {
            ShowMessage("所有武将均已解锁！");
            return;
        }

        // Recruit first locked general
        var target = allLocked[0];
        if (GameState.Instance.RecruitGeneral(_cityId, target.Id, _allGenerals))
        {
            ShowMessage($"成功招募 {target.Name}！");
            CreateButtons(); // Refresh button states
        }
        else
        {
            ShowMessage("招募失败！");
        }
    }

    private void UpgradeCity()
    {
        if (_progress == null) return;

        if (_progress.Level >= 5)
        {
            ShowMessage("城池已满级！");
            return;
        }

        int cost = _progress.Level * _cityData.UpgradeCost;
        if (GameState.Instance.BattleMerit < cost)
        {
            ShowMessage("战功不足！");
            return;
        }

        if (GameState.Instance.UpgradeCity(_cityId))
        {
            ShowMessage($"城池升至 {_progress.Level} 级！");
            CreateButtons();
        }
    }

    private void ReinforceCity()
    {
        if (_progress == null) return;

        if (GameState.Instance.ReinforceCity(_cityId, 50))
        {
            ShowMessage("征兵 50 人成功！");
        }
        else
        {
            ShowMessage("粮草不足或兵力已满！");
        }
    }

    private void ShowMessage(string msg)
    {
        _messageText = msg;
        _messageTimer = 3f;
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _backButton?.Update(Input);
        _recruitButton?.Update(Input);
        _upgradeButton?.Update(Input);
        _reinforceButton?.Update(Input);

        // Update message timer
        if (_messageTimer > 0)
        {
            _messageTimer -= dt;
            if (_messageTimer <= 0) _messageText = "";
        }

        // Handle escape key
        if (Input.IsKeyPressed(Keys.Escape))
        {
            Game.SceneManager.ChangeScene(new WorldMapScene());
        }

        // Refresh progress data
        _progress = GameState.Instance.GetCityProgress(_cityId);
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(30, 25, 18));
        SpriteBatch.Begin();

        // Background
        DrawBackground();

        // Left panel - City Info (x=30, y=70, w=320, h=580)
        DrawLeftPanel();

        // Middle panel - Actions (x=370, y=70, w=300, h=580)
        DrawMiddlePanel();

        // Right panel - Available Generals (x=690, y=70, w=560, h=580)
        DrawRightPanel();

        // HUD
        DrawHUD();

        // Buttons
        _backButton?.Draw(SpriteBatch, _font, _pixel);
        _recruitButton?.Draw(SpriteBatch, _font, _pixel);
        _upgradeButton?.Draw(SpriteBatch, _font, _pixel);
        _reinforceButton?.Draw(SpriteBatch, _font, _pixel);

        // Message overlay
        if (_messageTimer > 0)
        {
            float alpha = Math.Min(1f, _messageTimer);
            var msgSize = _titleFont.MeasureString(_messageText);
            float msgX = GameSettings.ScreenWidth / 2 - msgSize.X / 2;
            float msgY = GameSettings.ScreenHeight / 2 - msgSize.Y / 2;

            SpriteBatch.Draw(_pixel,
                new Rectangle((int)msgX - 20, (int)msgY - 10, (int)msgSize.X + 40, (int)msgSize.Y + 20),
                new Color((byte)0, (byte)0, (byte)0, (byte)(180 * alpha)));
            SpriteBatch.DrawString(_titleFont, _messageText,
                new Vector2(msgX, msgY),
                new Color(255, 220, 100) * alpha);
        }

        SpriteBatch.End();
    }

    private void DrawBackground()
    {
        for (int y = 0; y < GameSettings.ScreenHeight; y += 4)
        {
            float t = (float)y / GameSettings.ScreenHeight;
            byte r = (byte)MathHelper.Lerp(35, 25, t);
            byte g = (byte)MathHelper.Lerp(30, 20, t);
            byte b = (byte)MathHelper.Lerp(20, 15, t);
            SpriteBatch.Draw(_pixel, new Rectangle(0, y, GameSettings.ScreenWidth, 4), new Color(r, g, b));
        }
    }

    private void DrawLeftPanel()
    {
        int px = 30, py = 70, pw = 320, ph = 580;

        // Panel background
        SpriteBatch.Draw(_pixel, new Rectangle(px, py, pw, ph), new Color(40, 35, 28));
        DrawBorder(new Rectangle(px, py, pw, ph), new Color(100, 85, 60), 2);

        if (_progress == null) return;

        // City name
        SpriteBatch.DrawString(_titleFont, _cityName,
            new Vector2(px + 20, py + 15), new Color(220, 190, 130));

        // Level badge
        string levelText = $"等级 {_progress.Level}";
        Color levelColor = _progress.Level >= 4 ? new Color(255, 180, 60)
                       : _progress.Level >= 3 ? new Color(180, 160, 130)
                       : new Color(130, 120, 100);
        SpriteBatch.DrawString(_font, levelText,
            new Vector2(px + 20, py + 55), levelColor);

        // Population
        DrawResourceBar(px + 20, py + 95, "人口", _progress.Population, 500, new Color(80, 200, 120));

        // Grain
        DrawResourceBar(px + 20, py + 160, "粮草", _progress.Grain, _cityData.Grain * 2, new Color(200, 160, 80));

        // Troops
        int maxTroops = _cityData.MaxTroops;
        DrawResourceBar(px + 20, py + 225, "兵力", _progress.CurrentTroops, maxTroops, new Color(200, 80, 80));

        // Production rates
        int lineY = py + 300;
        SpriteBatch.DrawString(_font, "── 产出速率 ──",
            new Vector2(px + 20, lineY), new Color(140, 130, 110));

        float levelMult = 1f + (_progress.Level - 1) * 0.2f;
        float wallMult = 1f + (_cityData.WallLevel - 1) * 0.1f;

        int grainRate = (int)(_cityData.GrainProductionPerTick * levelMult);
        int troopRate = (int)(_cityData.TroopProductionPerTick * wallMult);

        SpriteBatch.DrawString(_smallFont, $"+{grainRate} 粮草/30秒",
            new Vector2(px + 25, lineY + 30), new Color(200, 160, 80));
        SpriteBatch.DrawString(_smallFont, $"+{troopRate} 兵力/30秒",
            new Vector2(px + 25, lineY + 55), new Color(200, 80, 80));
    }

    private void DrawResourceBar(int x, int y, string label, int current, int max, Color barColor)
    {
        float ratio = max > 0 ? MathHelper.Clamp((float)current / max, 0, 1) : 0;
        int barW = 260, barH = 16;

        // Label
        SpriteBatch.DrawString(_font, $"{label} {current}/{max}",
            new Vector2(x, y), new Color(180, 160, 120));

        // Bar background
        SpriteBatch.Draw(_pixel, new Rectangle(x, y + 22, barW, barH), new Color(20, 15, 10));

        // Bar fill
        int fillW = (int)(barW * ratio);
        if (fillW > 0)
        {
            SpriteBatch.Draw(_pixel, new Rectangle(x, y + 22, fillW, barH), barColor);
            // Top highlight
            SpriteBatch.Draw(_pixel, new Rectangle(x, y + 22, fillW, 2), barColor * 1.3f);
        }

        // Border
        DrawBorder(new Rectangle(x, y + 22, barW, barH), new Color(60, 50, 40), 1);
    }

    private void DrawMiddlePanel()
    {
        int px = 370, py = 70, pw = 300, ph = 580;

        // Panel background
        SpriteBatch.Draw(_pixel, new Rectangle(px, py, pw, ph), new Color(40, 35, 28));
        DrawBorder(new Rectangle(px, py, pw, ph), new Color(100, 85, 60), 2);

        if (_progress == null) return;

        // Title
        SpriteBatch.DrawString(_titleFont, "管理操作",
            new Vector2(px + 20, py + 15), new Color(220, 190, 130));

        // City properties section
        int infoY = py + 320;
        SpriteBatch.DrawString(_font, "── 城池属性 ──",
            new Vector2(px + 20, infoY), new Color(140, 130, 110));

        float levelMult = 1f + (_progress.Level - 1) * 0.2f;
        float wallMult = 1f + (_cityData.WallLevel - 1) * 0.1f;
        int grainRate = (int)(_cityData.GrainProductionPerTick * levelMult);
        int troopRate = (int)(_cityData.TroopProductionPerTick * wallMult);

        int lineY = infoY + 35;
        SpriteBatch.DrawString(_smallFont, $"城防: {_cityData.DefenseLevel}级",
            new Vector2(px + 25, lineY), new Color(160, 150, 120));
        SpriteBatch.DrawString(_smallFont, $"城墙: {_cityData.WallLevel}级",
            new Vector2(px + 25, lineY + 25), new Color(160, 150, 120));
        SpriteBatch.DrawString(_smallFont, $"驻军加成: +{_cityData.GarrisonDefenseBonus:P0}",
            new Vector2(px + 25, lineY + 50), new Color(160, 150, 120));
        SpriteBatch.DrawString(_smallFont, $"产出: +{grainRate}粮/刻  +{troopRate}兵/刻",
            new Vector2(px + 25, lineY + 75), new Color(160, 150, 120));
        SpriteBatch.DrawString(_smallFont, $"招募消耗: {GetRecruitCost()} 战功",
            new Vector2(px + 25, lineY + 100), new Color(160, 150, 120));
        SpriteBatch.DrawString(_smallFont, $"升级消耗: {_progress.Level * _cityData.UpgradeCost} 战功",
            new Vector2(px + 25, lineY + 125), new Color(160, 150, 120));
    }

    private void DrawRightPanel()
    {
        int px = 690, py = 70, pw = 560, ph = 580;

        // Panel background
        SpriteBatch.Draw(_pixel, new Rectangle(px, py, pw, ph), new Color(40, 35, 28));
        DrawBorder(new Rectangle(px, py, pw, ph), new Color(100, 85, 60), 2);

        // Title
        SpriteBatch.DrawString(_titleFont, "可招募武将",
            new Vector2(px + 20, py + 15), new Color(220, 190, 130));

        // General cards grid
        int cardW = 160, cardH = 90;
        int gapX = 12, gapY = 12;
        int startX = px + 15;
        int startY = py + 60;
        int cols = 3;

        int i = 0;
        foreach (var gen in _allGenerals)
        {
            var gp = GameState.Instance.GetGeneralProgress(gen.Id);
            bool isUnlocked = gp?.IsUnlocked ?? false;

            int col = i % cols;
            int row = i / cols;
            int cx = startX + col * (cardW + gapX);
            int cy = startY + row * (cardH + gapY) - _scrollOffset;

            // Skip if off screen
            if (cy < py + 50 || cy > py + ph - 20) { i++; continue; }

            // Card background
            Color bgColor = isUnlocked ? new Color(50, 45, 35) : new Color(35, 30, 25);
            SpriteBatch.Draw(_pixel, new Rectangle(cx, cy, cardW, cardH), bgColor);
            DrawBorder(new Rectangle(cx, cy, cardW, cardH),
                isUnlocked ? new Color(80, 70, 50) : new Color(50, 45, 35), 1);

            // General name
            Color nameColor = isUnlocked ? new Color(150, 140, 120) : new Color(100, 90, 80);
            SpriteBatch.DrawString(_smallFont, isUnlocked ? $"{gen.Name} (已解锁)" : gen.Name,
                new Vector2(cx + 8, cy + 5), nameColor);

            // Stat bars
            DrawMiniStatBar(cx + 8, cy + 28, "武", gen.Strength, 100, new Color(200, 100, 80), isUnlocked);
            DrawMiniStatBar(cx + 8, cy + 44, "智", gen.Intelligence, 100, new Color(100, 160, 200), isUnlocked);
            DrawMiniStatBar(cx + 8, cy + 60, "统", gen.Leadership, 100, new Color(100, 200, 100), isUnlocked);

            i++;
        }
    }

    private void DrawMiniStatBar(int x, int y, string label, int value, int max, Color barColor, bool enabled)
    {
        float ratio = max > 0 ? MathHelper.Clamp((float)value / max, 0, 1) : 0;
        int barW = 120, barH = 10;

        SpriteBatch.DrawString(_smallFont, label,
            new Vector2(x, y), enabled ? new Color(180, 170, 140) : new Color(80, 70, 60));

        SpriteBatch.Draw(_pixel, new Rectangle(x + 18, y + 2, barW, barH), new Color(20, 15, 10));

        int fillW = (int)(barW * ratio);
        if (fillW > 0)
        {
            Color finalColor = enabled ? barColor : barColor * 0.4f;
            SpriteBatch.Draw(_pixel, new Rectangle(x + 18, y + 2, fillW, barH), finalColor);
        }

        SpriteBatch.DrawString(_smallFont, $"{value}",
            new Vector2(x + barW + 22, y), enabled ? new Color(160, 150, 120) : new Color(70, 60, 50));
    }

    private void DrawHUD()
    {
        // Top bar
        SpriteBatch.Draw(_pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, 50), new Color(25, 20, 14, 230));
        SpriteBatch.Draw(_pixel, new Rectangle(0, 50, GameSettings.ScreenWidth, 1), new Color(80, 65, 45));

        // Title
        SpriteBatch.DrawString(_font, $"猫三国 · {_cityName} 管理",
            new Vector2(15, 12), new Color(220, 190, 130));

        // Battle merit
        SpriteBatch.DrawString(_font, $"战功: {GameState.Instance.BattleMerit}",
            new Vector2(400, 12), new Color(255, 200, 80));
    }

    private void DrawBorder(Rectangle rect, Color color, int thickness)
    {
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
