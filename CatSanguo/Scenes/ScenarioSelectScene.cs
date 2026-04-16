using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.UI;
using CatSanguo.UI.Battle;
using SchemasGameDate = CatSanguo.Data.Schemas.GameDate;

namespace CatSanguo.Scenes;

/// <summary>
/// 经典三国志风格时期选择场景
/// 带边框的面板，列表式剧本选择，红色选中效果
/// </summary>
public class ScenarioSelectScene : Scene
{
    private Texture2D _pixel;
    private SpriteFontBase _font;
    private SpriteFontBase _titleFont;
    private ScenarioManager _scenarioManager;

    // 面板区域
    private Rectangle _panelRect;
    private int _panelPadding = 40;
    private int _originalPanelW = 500;
    private int _originalPanelH = 420;

    // 剧本按钮
    private List<ScenarioButton> _scenarioButtons = new();
    private Button _backButton;
    private ScenarioData? _selectedScenario;
    private int _selectedIndex = -1;

    // 势力选择阶段
    private bool _showFactionSelect = false;
    private List<Button> _factionButtons = new();
    private ScenarioFaction? _selectedFaction;
    private bool _skipNextFactionUpdate = false;

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.NotifyFont;

        _scenarioManager = GameRoot.Instance.ScenarioManager;
        string scenariosPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "scenarios.json");
        _scenarioManager.LoadScenarios(scenariosPath);

        // 面板位置先用默认高度，BuildScenarioButtons 会根据数量调整
        int panelW = 500;
        int panelH = 420;
        _panelRect = new Rectangle(
            (GameSettings.ScreenWidth - panelW) / 2,
            (GameSettings.ScreenHeight - panelH) / 2,
            panelW, panelH);

        BuildScenarioButtons();

        _backButton = new Button("返 回", new Rectangle(60, GameSettings.ScreenHeight - 80, 120, 45));
        _backButton.OnClick = () => Game.SceneManager.ChangeScene(new MainMenuScene());
    }

    private void BuildScenarioButtons()
    {
        _scenarioButtons.Clear();
        _selectedIndex = -1;

        var scenarios = _scenarioManager.AllScenarios;
        int count = Math.Max(scenarios.Count, 1);

        int spacing = 55;
        int btnH = 45;
        int topOffset = _panelPadding + 60;
        int bottomPadding = _panelPadding + 10;

        // 动态计算面板高度以容纳所有剧本按钮
        int neededHeight = topOffset + count * spacing + bottomPadding;
        int panelH = Math.Max(_originalPanelH, neededHeight);
        _originalPanelH = panelH;
        _panelRect = new Rectangle(
            (GameSettings.ScreenWidth - _originalPanelW) / 2,
            (GameSettings.ScreenHeight - panelH) / 2,
            _originalPanelW, panelH);

        int startY = _panelRect.Y + topOffset;
        int btnW = _panelRect.Width - _panelPadding * 2;

        for (int i = 0; i < scenarios.Count; i++)
        {
            var scenario = scenarios[i];
            var btn = new ScenarioButton(scenario.Name, new Rectangle(_panelRect.X + _panelPadding, startY + i * spacing, btnW, btnH), i);
            int index = i;
            btn.OnClick = () => SelectScenario(scenarios[index]);
            _scenarioButtons.Add(btn);
        }

        if (scenarios.Count == 0)
        {
            // 默认演示剧本
            var btn = new ScenarioButton("黄巾之乱 (演示)", new Rectangle(_panelRect.X + _panelPadding, startY, btnW, btnH), 0);
            btn.OnClick = () =>
            {
                _selectedScenario = new ScenarioData
                {
                    Id = "demo_huangjin",
                    Name = "黄巾之乱",
                    Description = "东汉末年，黄巾起义，天下大乱。",
                    StartDate = new SchemasGameDate(184, 1, 1),
                    Factions = new List<ScenarioFaction>
                    {
                        new ScenarioFaction
                        {
                            FactionId = "player",
                            FactionName = "玩家势力",
                            InitialCityIds = new List<string>(),
                            InitialGenerals = new List<ScenarioGeneralAllocation>(),
                            StartGold = 500,
                            StartFood = 300,
                            IsPlayable = true
                        }
                    }
                };
                _showFactionSelect = true;
                BuildFactionButtons();
            };
            _scenarioButtons.Add(btn);
        }
    }

    private void SelectScenario(ScenarioData scenario)
    {
        _selectedScenario = scenario;
        _selectedIndex = _scenarioButtons.FindIndex(b => b.ScenarioName == scenario.Name);
        _scenarioManager.SelectScenario(scenario.Id);
        _showFactionSelect = true;
        _skipNextFactionUpdate = true;
        BuildFactionButtons();
    }

    private void BuildFactionButtons()
    {
        _factionButtons.Clear();
        _selectedFaction = null;

        // Show ALL factions, not just playable ones
        var factions = _scenarioManager.GetAvailableFactions()
            .ToList();

        int btnH = 36;
        int spacing = 42;
        int btnW = _originalPanelW - _panelPadding * 2;

        // 动态计算面板高度以容纳所有势力按钮
        // 布局: padding(40) + subtitle(80) + buttons + bottom area(85)
        int neededHeight = _panelPadding + 80 + factions.Count * spacing + 85;
        int panelH = Math.Max(_originalPanelH, neededHeight);
        _panelRect = new Rectangle(
            (GameSettings.ScreenWidth - _originalPanelW) / 2,
            (GameSettings.ScreenHeight - panelH) / 2,
            _originalPanelW, panelH);

        int startY = _panelRect.Y + _panelPadding + 80;

        for (int i = 0; i < factions.Count; i++)
        {
            var faction = factions[i];
            var btn = new Button(faction.FactionName, new Rectangle(_panelRect.X + _panelPadding, startY + i * spacing, btnW, btnH));
            int index = i;
            btn.OnClick = () => SelectFaction(factions[index]);
            _factionButtons.Add(btn);
        }
    }

    private void SelectFaction(ScenarioFaction faction)
    {
        _selectedFaction = faction;
        _scenarioManager.SelectFaction(faction.FactionId);

        string errorMsg;
        if (_scenarioManager.StartGame(out errorMsg))
        {
            Game.SceneManager.ChangeScene(new WorldMapScene());
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ScenarioSelect] StartGame failed: {errorMsg}");
        }
    }

    public override void Update(GameTime gameTime)
    {
        _backButton.Update(Input);

        if (_showFactionSelect)
        {
            // 跳过切换后的第一帧，防止剧本点击穿透到势力按钮
            if (_skipNextFactionUpdate)
            {
                _skipNextFactionUpdate = false;
                return;
            }
            foreach (var btn in _factionButtons) btn.Update(Input);
        }
        else
        {
            foreach (var btn in _scenarioButtons) btn.Update(Input);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        // 深色背景
        GraphicsDevice.Clear(new Color(20, 15, 10));

        SpriteBatch.Begin();

        // 背景纹理（模拟古典屏风）
        DrawBackgroundPattern();

        if (!_showFactionSelect)
        {
            DrawScenarioSelection();
        }
        else
        {
            DrawFactionSelection();
        }

        _backButton.Draw(SpriteBatch, _font, _pixel);

        SpriteBatch.End();
    }

    private void DrawBackgroundPattern()
    {
        // 模拟古典屏风背景
        int tileSize = 40;
        for (int y = 0; y < GameSettings.ScreenHeight; y += tileSize)
        {
            for (int x = 0; x < GameSettings.ScreenWidth; x += tileSize)
            {
                bool isAlt = (x / tileSize + y / tileSize) % 2 == 0;
                Color tileColor = isAlt ? new Color(35, 25, 20) : new Color(40, 30, 22);
                SpriteBatch.Draw(_pixel, new Rectangle(x, y, tileSize, tileSize), tileColor);
            }
        }

        // 装饰边框线
        UIHelper.DrawBorder(SpriteBatch, _pixel,
            new Rectangle(30, 30, GameSettings.ScreenWidth - 60, GameSettings.ScreenHeight - 60),
            new Color(120, 90, 50), 2);
    }

    private void DrawScenarioSelection()
    {
        // 标题框
        DrawTitleBox("时 期 选 择");

        // 面板背景（带边框）
        UIHelper.DrawPanel(SpriteBatch, _pixel, _panelRect,
            new Color(50, 40, 35), new Color(150, 110, 60), 3);

        // 内部装饰线
        int innerPadding = 8;
        UIHelper.DrawBorder(SpriteBatch, _pixel,
            new Rectangle(_panelRect.X + innerPadding, _panelRect.Y + innerPadding,
                _panelRect.Width - innerPadding * 2, _panelRect.Height - innerPadding * 2),
            new Color(80, 60, 40), 1);

        // 剧本按钮
        foreach (var btn in _scenarioButtons)
            btn.Draw(SpriteBatch, _font, _pixel, _selectedIndex);

        // 剧本描述
        if (_selectedScenario != null)
        {
            string desc = _selectedScenario.Description;
            if (!string.IsNullOrEmpty(desc))
            {
                var descSize = _font.MeasureString(desc);
                int descY = _panelRect.Y + _panelRect.Height - _panelPadding - 30;
                SpriteBatch.DrawString(_font, desc,
                    new Vector2(_panelRect.X + _panelPadding, descY),
                    new Color(180, 150, 100));
            }
        }
    }

    private void DrawFactionSelection()
    {
        // 标题框
        DrawTitleBox(_selectedScenario?.Name ?? "选 择 势 力");

        // 面板背景
        UIHelper.DrawPanel(SpriteBatch, _pixel, _panelRect,
            new Color(50, 40, 35), new Color(150, 110, 60), 3);

        int innerPadding = 8;
        UIHelper.DrawBorder(SpriteBatch, _pixel,
            new Rectangle(_panelRect.X + innerPadding, _panelRect.Y + innerPadding,
                _panelRect.Width - innerPadding * 2, _panelRect.Height - innerPadding * 2),
            new Color(80, 60, 40), 1);

        // 势力选择提示
        string subtitle = "选 择 你 的 势 力";
        var subSize = _font.MeasureString(subtitle);
        SpriteBatch.DrawString(_font, subtitle,
            new Vector2(_panelRect.X + _panelPadding + (_panelRect.Width - _panelPadding * 2 - subSize.X) / 2,
                _panelRect.Y + _panelPadding + 30),
            new Color(200, 170, 120));

        // 势力按钮
        foreach (var btn in _factionButtons)
            btn.Draw(SpriteBatch, _font, _pixel);

        // 返回剧本选择按钮
        var backToScenario = new Button("返 回 剧 本", new Rectangle(
            _panelRect.X + _panelPadding,
            _panelRect.Y + _panelRect.Height - _panelPadding - 45,
            _panelRect.Width - _panelPadding * 2, 40));
        backToScenario.OnClick = () =>
        {
            _showFactionSelect = false;
            _panelRect = new Rectangle(
                (GameSettings.ScreenWidth - _originalPanelW) / 2,
                (GameSettings.ScreenHeight - _originalPanelH) / 2,
                _originalPanelW, _originalPanelH);
        };
        backToScenario.Update(Input);
        backToScenario.Draw(SpriteBatch, _font, _pixel);
    }

    private void DrawTitleBox(string title)
    {
        // 标题面板（类似三国志的标题框）
        int titleW = 220;
        int titleH = 50;
        int titleX = (GameSettings.ScreenWidth - titleW) / 2;
        int titleY = 40;

        Rectangle titleRect = new Rectangle(titleX, titleY, titleW, titleH);

        // 外框
        UIHelper.DrawPanel(SpriteBatch, _pixel, titleRect,
            new Color(80, 40, 40), new Color(200, 100, 80), 3);

        // 内框装饰
        UIHelper.DrawBorder(SpriteBatch, _pixel,
            new Rectangle(titleX + 4, titleY + 4, titleW - 8, titleH - 8),
            new Color(150, 70, 50), 1);

        // 标题文字
        var titleSize = _titleFont.MeasureString(title);
        SpriteBatch.DrawString(_titleFont, title,
            new Vector2(titleX + (titleW - titleSize.X) / 2, titleY + 10),
            new Color(255, 220, 150));
    }
}

/// <summary>
/// 剧本按钮（支持选中高亮）
/// </summary>
public class ScenarioButton
{
    public Rectangle Bounds { get; set; }
    public string ScenarioName { get; set; }
    public int Index { get; set; }
    public Color NormalColor { get; set; } = new Color(70, 55, 40);
    public Color HoverColor { get; set; } = new Color(100, 75, 50);
    public Color SelectedColor { get; set; } = new Color(180, 60, 50); // 红色选中
    public Color TextColor { get; set; } = new Color(240, 220, 180);
    public Color BorderColor { get; set; } = new Color(150, 120, 80);
    public bool IsHovered { get; private set; }
    public bool Enabled { get; set; } = true;
    public Action? OnClick { get; set; }

    public ScenarioButton(string name, Rectangle bounds, int index)
    {
        ScenarioName = name;
        Bounds = bounds;
        Index = index;
    }

    public void Update(InputManager input)
    {
        if (!Enabled) { IsHovered = false; return; }
        IsHovered = input.IsMouseInRect(Bounds);
        if (IsHovered && input.IsMouseClicked())
            OnClick?.Invoke();
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFontBase font, Texture2D pixel, int selectedIndex)
    {
        bool isSelected = Index == selectedIndex;
        bool isDisabled = !Enabled;

        // 背景色
        Color bgColor = isDisabled ? new Color(30, 30, 30) :
            isSelected ? SelectedColor :
            IsHovered ? HoverColor : NormalColor;

        // 边框色
        Color borderColor = isDisabled ? new Color(50, 50, 50) :
            isSelected ? new Color(255, 100, 80) : BorderColor;

        // 文字色
        Color textColor = isDisabled ? Color.Gray * 0.6f :
            isSelected ? new Color(255, 240, 220) : TextColor;

        // 绘制按钮背景
        spriteBatch.Draw(pixel, Bounds, bgColor);

        // 绘制边框
        UIHelper.DrawBorder(spriteBatch, pixel, Bounds, borderColor, isSelected ? 3 : 2);

        // 绘制文字（居中）
        var textSize = font.MeasureString(ScenarioName);
        Vector2 textPos = new Vector2(
            Bounds.X + (Bounds.Width - textSize.X) / 2,
            Bounds.Y + (Bounds.Height - textSize.Y) / 2);

        spriteBatch.DrawString(font, ScenarioName, textPos, textColor);
    }
}
