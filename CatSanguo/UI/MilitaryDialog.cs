using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;

namespace CatSanguo.UI;

/// <summary>
/// 军事出征子对话框
/// 负责编队管理、武将选择、出征确认
/// </summary>
public class MilitaryDialog : DialogBase
{
    private enum MilitaryPhase
    {
        MainMenu,       // 军事主菜单：出征/武将培养
        Deploy,         // 编队管理
        SelectTarget,   // 选择目标城池
        Confirm         // 确认行军
    }

    private MilitaryPhase _phase = MilitaryPhase.MainMenu;

    // UI按钮
    private Button _deployBtn = null!;
    private Button _generalRosterBtn = null!;
    private Button _backBtn = null!;
    private Button _confirmBtn = null!;
    private Button _selectGeneralBtn = null!;
    private Button _confirmDeployBtn = null!;

    // 动态按钮
    private List<Button> _generalButtons = new();
    private List<Button> _squadRemoveButtons = new();
    private List<Button> _selectGeneralButtons = new();

    // 数据
    private CityData? _sourceCity;
    private List<string> _cityGenerals = new();
    private List<GeneralDeployCard> _deployCards = new();
    private bool _isSelectingGenerals;
    private List<string> _selectedGenerals = new();

    // 回调
    public Action? OnOpenGeneralRoster { get; set; }
    public Action<List<string>, List<GeneralDeployEntry>, CityData>? OnLaunchArmy { get; set; }
    public Func<string, string>? GetGeneralName { get; set; }

    // 渲染资源
    private Texture2D? _pixel;
    private SpriteFontBase? _font;
    private SpriteFontBase? _titleFont;

    /// <summary>
    /// 初始化对话框
    /// </summary>
    public void Initialize(Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont)
    {
        _pixel = pixel;
        _font = font;
        _titleFont = titleFont;
        InitButtons();
    }

    private void InitButtons()
    {
        int cx = GameSettings.ScreenWidth / 2;
        int cy = GameSettings.ScreenHeight / 2;
        int btnW = 140;
        int btnH = 45;

        _deployBtn = new Button("出 征", new Rectangle(cx - btnW - 10, cy - 30, btnW, btnH));
        _deployBtn.NormalColor = new Color(120, 50, 30);
        _deployBtn.HoverColor = new Color(160, 70, 40);
        _deployBtn.OnClick = () => _phase = MilitaryPhase.Deploy;

        _generalRosterBtn = new Button("武将培养", new Rectangle(cx + 10, cy - 30, btnW, btnH));
        _generalRosterBtn.NormalColor = new Color(50, 80, 50);
        _generalRosterBtn.HoverColor = new Color(70, 110, 70);
        _generalRosterBtn.OnClick = () => OnOpenGeneralRoster?.Invoke();

        _backBtn = new Button("返 回", new Rectangle(20, 20, 100, 40));
        _backBtn.NormalColor = new Color(50, 50, 50);
        _backBtn.HoverColor = new Color(70, 70, 70);
        _backBtn.OnClick = () =>
        {
            if (_phase == MilitaryPhase.MainMenu)
                Close();
            else
                _phase = MilitaryPhase.MainMenu;
        };

        _confirmBtn = new Button("确认出击", new Rectangle(GameSettings.ScreenWidth - 160, GameSettings.ScreenHeight - 70, 140, 45));
        _confirmBtn.NormalColor = new Color(100, 50, 30);
        _confirmBtn.HoverColor = new Color(140, 70, 40);
        _confirmBtn.OnClick = ConfirmLaunchArmy;
    }

    public void OpenForCity(CityData city, List<string> cityGenerals, List<GeneralData> allGenerals)
    {
        _sourceCity = city;
        _cityGenerals = cityGenerals;
        _phase = MilitaryPhase.MainMenu;
        _deployCards.Clear();
        _selectedGenerals.Clear();
        Open();
    }

    public void UpdateCustom(float dt, InputManager input)
    {
        if (_pixel == null || _font == null) return;

        switch (_phase)
        {
            case MilitaryPhase.MainMenu:
                UpdateMainMenuCustom(input);
                break;
            case MilitaryPhase.Deploy:
                UpdateDeployCustom(input);
                break;
        }

        _backBtn?.Update(input);
        _confirmBtn?.Update(input);
    }

    private void UpdateMainMenuCustom(InputManager input)
    {
        _deployBtn?.Update(input);
        _generalRosterBtn?.Update(input);
    }

    private void UpdateDeployCustom(InputManager input)
    {
        _selectGeneralBtn?.Update(input);
        _confirmDeployBtn?.Update(input);

        foreach (var btn in _generalButtons) btn.Update(input);
        foreach (var btn in _squadRemoveButtons) btn.Update(input);
        foreach (var btn in _selectGeneralButtons) btn.Update(input);
    }

    public void DrawCustom(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont)
    {
        if (_pixel == null || _font == null || _titleFont == null) return;

        // 绘制对话框背景
        DrawDialogBackground(sb, pixel, font, titleFont);

        switch (_phase)
        {
            case MilitaryPhase.MainMenu:
                DrawMainMenu(sb, pixel, font, titleFont);
                break;
            case MilitaryPhase.Deploy:
                DrawDeploy(sb, pixel, font, titleFont);
                break;
        }

        _backBtn?.Draw(sb, font, pixel);
        if (_phase != MilitaryPhase.MainMenu)
            _confirmBtn?.Draw(sb, font, pixel);
    }

    private void DrawDialogBackground(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont)
    {
        int panelW = 600;
        int panelH = 400;
        int panelX = GameSettings.ScreenWidth / 2 - panelW / 2;
        int panelY = GameSettings.ScreenHeight / 2 - panelH / 2;

        sb.Draw(pixel, new Rectangle(panelX, panelY, panelW, panelH), new Color(30, 25, 18, 240));
        sb.Draw(pixel, new Rectangle(panelX, panelY, panelW, 3), new Color(100, 80, 50));
        sb.Draw(pixel, new Rectangle(panelX, panelY + panelH - 2, panelW, 2), new Color(60, 50, 40));
    }

    private void DrawMainMenu(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont)
    {
        string title = "军 事";
        var titleSize = titleFont.MeasureString(title);
        sb.DrawString(titleFont, title,
            new Vector2(GameSettings.ScreenWidth / 2 - titleSize.X / 2, GameSettings.ScreenHeight / 2 - 80),
            new Color(220, 190, 130));

        _deployBtn?.Draw(sb, font, pixel);
        _generalRosterBtn?.Draw(sb, font, pixel);
    }

    private void DrawDeploy(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont)
    {
        string title = "出征编队";
        var titleSize = titleFont.MeasureString(title);
        sb.DrawString(titleFont, title,
            new Vector2(GameSettings.ScreenWidth / 2 - titleSize.X / 2, GameSettings.ScreenHeight / 2 - 100),
            new Color(220, 190, 130));

        // Draw squad info
        int y = GameSettings.ScreenHeight / 2 - 60;
        sb.DrawString(font, $"当前城池: {_sourceCity?.Name}",
            new Vector2(GameSettings.ScreenWidth / 2 - 100, y), new Color(180, 160, 120));

        // Draw available generals
        y += 40;
        sb.DrawString(font, "可用武将:", new Vector2(GameSettings.ScreenWidth / 2 - 150, y), new Color(160, 140, 100));

        foreach (var btn in _generalButtons) btn.Draw(sb, font, pixel);
    }

    private void ConfirmLaunchArmy()
    {
        if (_sourceCity == null || _selectedGenerals.Count == 0) return;

        var deployEntries = new List<GeneralDeployEntry>();
        foreach (var genId in _selectedGenerals)
        {
            deployEntries.Add(new GeneralDeployEntry
            {
                GeneralId = genId,
                SoldierCount = 30,
                BattleFormation = Data.Schemas.BattleFormation.Vanguard
            });
        }

        OnLaunchArmy?.Invoke(_selectedGenerals, deployEntries, _sourceCity);
        Close();
    }

    public override void Close()
    {
        _phase = MilitaryPhase.MainMenu;
        _deployCards.Clear();
        _selectedGenerals.Clear();
        base.Close();
    }
}
