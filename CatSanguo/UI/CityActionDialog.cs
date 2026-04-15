using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.WorldMap;

namespace CatSanguo.UI;

// ==================== 城池操作对话框 ====================

public enum CityActionPhase
{
    Main,           // 主选项：军事/内政
    // 军事子菜单
    MilitaryMain,    // 军事：出征
    MilitaryDeploy,  // 军事出征：编队管理
    MilitarySelectTarget, // 选择目标城池
    MilitaryConfirm,    // 确认行军
    SelectGeneral,  // 武将选择模式（多选加入编队）
    // 内政子菜单
    InteriorMain,   // 内政：经济/防御/建筑/人才
    InteriorEconomy,    // 经济开发
    InteriorDefense,    // 城池防御
    InteriorBuilding,   // 城池建筑
    TalentManage,   // 人才管理
}

enum TalentSubTab { Discover, Persuade, Recruit }

// ==================== 选择器（显示当前值+左右切换） ====================
class Selector
{
    public Rectangle Bounds { get; set; }
    public List<string> Items { get; set; } = new();
    public int SelectedIndex { get; set; }
    public Color NormalColor = new Color(50, 45, 40);
    public Color BorderColor = new Color(80, 70, 50);
    public Color BtnColor = new Color(70, 60, 50);
    public Color BtnHoverColor = new Color(90, 80, 70);

    public string SelectedValue => SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : "";

    public void Update(InputManager input)
    {
        var mousePos = input.MousePosition.ToPoint();
        int btnW = 28;
        int h = Bounds.Height;

        // 左按钮
        var leftBtn = new Rectangle(Bounds.X, Bounds.Y, btnW, h);
        if (leftBtn.Contains(mousePos) && input.IsMouseClicked())
        {
            SelectedIndex = (SelectedIndex - 1 + Items.Count) % Items.Count;
            return;
        }

        // 右按钮
        var rightBtn = new Rectangle(Bounds.Right - btnW, Bounds.Y, btnW, h);
        if (rightBtn.Contains(mousePos) && input.IsMouseClicked())
        {
            SelectedIndex = (SelectedIndex + 1) % Items.Count;
            return;
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, InputManager input)
    {
        var mousePos = input.MousePosition.ToPoint();
        int btnW = 28;
        int h = Bounds.Height;
        bool leftHover = new Rectangle(Bounds.X, Bounds.Y, btnW, h).Contains(mousePos);
        bool rightHover = new Rectangle(Bounds.Right - btnW, Bounds.Y, btnW, h).Contains(mousePos);

        // 背景
        sb.Draw(pixel, Bounds, NormalColor);
        DrawBorderRect(sb, pixel, Bounds, BorderColor, 2);

        // 左按钮
        sb.Draw(pixel, new Rectangle(Bounds.X, Bounds.Y, btnW, h), leftHover ? BtnHoverColor : BtnColor);
        sb.DrawString(font, "<", new Vector2(Bounds.X + 8, Bounds.Y + 4), new Color(230, 210, 170));

        // 右按钮
        sb.Draw(pixel, new Rectangle(Bounds.Right - btnW, Bounds.Y, btnW, h), rightHover ? BtnHoverColor : BtnColor);
        sb.DrawString(font, ">", new Vector2(Bounds.Right - btnW + 8, Bounds.Y + 4), new Color(230, 210, 170));

        // 中间文字
        if (!string.IsNullOrEmpty(SelectedValue))
        {
            var textSize = font.MeasureString(SelectedValue);
            var textX = Bounds.X + btnW + (Bounds.Width - btnW * 2 - textSize.X) / 2;
            var textY = Bounds.Y + (Bounds.Height - textSize.Y) / 2;
            sb.DrawString(font, SelectedValue, new Vector2(textX, textY), new Color(255, 230, 160));
        }
    }

    private void DrawBorderRect(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}

// ==================== 武将出征卡片 ====================
class GeneralDeployCard
{
    public GeneralData Data { get; set; } = null!;
    public bool IsSelected { get; set; } = false;
    public string SelectedUnit { get; set; } = "步兵";
    public string SelectedFormation { get; set; } = "先锋";
    public int SoldierCount { get; set; } = 30;

    public int UnitIndex
    {
        get
        {
            var units = new[] { "步兵", "枪兵", "盾兵", "骑兵", "重骑", "轻骑", "弓兵", "强弩", "攻城", "法师" };
            for (int i = 0; i < units.Length; i++) if (units[i] == SelectedUnit) return i;
            return 0;
        }
    }

    public int FormationIndex
    {
        get
        {
            var formations = new[] { "先锋", "鱼鳞", "方阵", "锥形", "长蛇", "鹤翼", "八卦", "偃月", "环形" };
            for (int i = 0; i < formations.Length; i++) if (formations[i] == SelectedFormation) return i;
            return 0;
        }
    }
}

public class CityActionDialog
{
    // 状态
    public CityActionPhase Phase { get; private set; } = CityActionPhase.Main;
    public CityData? SourceCity { get; private set; }
    /// <summary>仅用于 MilitarySelectTarget 阶段临时显示选中的武将名</summary>
    public string? SelectedGeneralId { get; private set; }
    public CityData? TargetCity { get; private set; }
    public List<string>? MovePath { get; private set; }
    public bool IsSelectingTarget => Phase == CityActionPhase.MilitarySelectTarget;
    public bool IsActive => SourceCity != null;

    // SelectGeneral 模式多选
    private List<GeneralDeployCard> _deployCards = new();
    // 人才管理子标签
    private TalentSubTab _currentTalentTab = TalentSubTab.Discover;
    // 引用所有武将数据
    private List<GeneralData> _allGeneralsRef = new();

    // UI组件
    private Button _militaryBtn = null!;
    private Button _interiorBtn = null!;
    private Button _talentBtn = null!;      // 人才管理（主菜单级别）
    private Button _cancelBtn = null!;
    private Button _backBtn = null!;
    private Button _confirmBtn = null!;

    // 军事子菜单按钮
    private Button _deployBtn = null!;      // 出征
    private Button _generalRosterBtn = null!; // 武将培养
    private Button _selectGeneralBtn = null!; // 选择武将（编队出征页面）
    private Button _confirmDeployBtn = null!; // 确认出击（编队出征页面）

    // 内政子菜单按钮
    private Button _economyBtn = null!;     // 经济开发
    private Button _defenseBtn = null!;     // 城池防御
    private Button _buildingBtn = null!;    // 城池建筑
    private Button _talentManageBtn = null!; // 人才管理

    // 动态按钮列表
    private List<Button> _generalButtons = new();
    private List<Button> _squadRemoveButtons = new();       // 编队武将旁的移除按钮
    private List<Button> _selectGeneralButtons = new();     // 武将选择模式的可选武将按钮
    private List<Button> _talentDiscoverButtons = new();    // 发现人才列表按钮
    private List<Button> _talentPersuadeButtons = new();    // 说服在野列表按钮
    private List<Button> _talentRecruitButtons = new();     // 招降俘虏列表按钮
    private List<Button> _talentTabButtons = new();         // 三个Tab按钮

    // 建筑相关
    private List<Button> _buildingButtons = new();

    // 回调
    private Action? _onClose;
    private Action<List<string>, List<GeneralDeployEntry>, CityData>? _onLaunchArmy;
    private Action? _onOpenGeneralRoster;
    private Func<string, string>? _getGeneralName;

    // 数据
    private List<string> _availableGenerals = new();
    private List<string> _cityGenerals = new();  // 城池驻军武将

    // 世界空间（用于目标选择）
    public Vector2 WorldMousePos { get; set; }

    public void Initialize(Func<string, string> getGeneralName, SpriteFontBase font, SpriteFontBase titleFont, Action? onOpenGeneralRoster = null)
    {
        _getGeneralName = getGeneralName;
        _onOpenGeneralRoster = onOpenGeneralRoster;
        InitButtons();
    }

    private void InitButtons()
    {
        int cx = GameSettings.ScreenWidth / 2;
        int cy = GameSettings.ScreenHeight / 2;
        int btnW = 105;
        int btnH = 46;
        int spacing = 14;

        // 主菜单按钮（三列等间距布局：军事、内政、人才）
        // 计算起始位置，确保三个按钮整体居中
        int totalWidth = btnW * 3 + spacing * 2;
        int startX = cx - totalWidth / 2;
        
        _militaryBtn = new Button("军 事", new Rectangle(startX, cy, btnW, btnH));
        _militaryBtn.NormalColor = new Color(130, 65, 42);    // 暖红 - 军事主题
        _militaryBtn.HoverColor = new Color(160, 85, 55);
        _militaryBtn.BorderColor = new Color(175, 135, 85);

        _interiorBtn = new Button("内 政", new Rectangle(startX + btnW + spacing, cy, btnW, btnH));
        _interiorBtn.NormalColor = new Color(55, 95, 120);    // 深蓝 - 内政主题
        _interiorBtn.HoverColor = new Color(75, 125, 155);
        _interiorBtn.BorderColor = new Color(115, 145, 175);

        _talentBtn = new Button("人 才", new Rectangle(startX + (btnW + spacing) * 2, cy, btnW, btnH));
        _talentBtn.NormalColor = new Color(75, 115, 55);      // 深绿 - 人才主题
        _talentBtn.HoverColor = new Color(95, 145, 75);
        _talentBtn.BorderColor = new Color(135, 165, 95);

        _cancelBtn = new Button("取 消", new Rectangle(cx - 60, cy + 68, 120, 40));
        _cancelBtn.NormalColor = new Color(60, 55, 48);
        _cancelBtn.HoverColor = new Color(80, 75, 68);
        _cancelBtn.BorderColor = new Color(115, 105, 85);

        _backBtn = new Button("返 回", new Rectangle(20, 20, 100, 40));
        _backBtn.NormalColor = new Color(50, 50, 50);

        _confirmBtn = new Button("确认出击", new Rectangle(GameSettings.ScreenWidth - 140, GameSettings.ScreenHeight - 60, 120, 40));
        _confirmBtn.NormalColor = new Color(100, 50, 30);
        _confirmBtn.HoverColor = new Color(140, 70, 40);

        // 军事子菜单
        _deployBtn = new Button("编队出征", new Rectangle(cx - 90, cy - 40, 180, 50));
        _deployBtn.NormalColor = new Color(110, 55, 35);      // 深红 - 出征
        _deployBtn.HoverColor = new Color(140, 75, 50);

        _generalRosterBtn = new Button("武将培养", new Rectangle(cx - 90, cy + 20, 180, 50));
        _generalRosterBtn.NormalColor = new Color(85, 65, 110); // 紫色 - 培养
        _generalRosterBtn.HoverColor = new Color(110, 85, 140);

        // 内政子菜单 - 统一使用冷色调
        _economyBtn = new Button("经济开发", new Rectangle(cx - 90, cy - 40, 180, 50));
        _economyBtn.NormalColor = new Color(45, 100, 75);     // 翠绿 - 经济
        _economyBtn.HoverColor = new Color(65, 130, 100);

        _defenseBtn = new Button("城池防御", new Rectangle(cx - 90, cy + 20, 180, 50));
        _defenseBtn.NormalColor = new Color(65, 75, 110);     // 钢蓝 - 防御
        _defenseBtn.HoverColor = new Color(85, 100, 140);

        _buildingBtn = new Button("城池建筑", new Rectangle(cx - 90, cy + 80, 180, 50));
        _buildingBtn.NormalColor = new Color(105, 80, 45);    // 古铜 - 建筑
        _buildingBtn.HoverColor = new Color(135, 110, 65);

        // 编队出征页面按钮
        _selectGeneralBtn = new Button("选择武将", new Rectangle(GameSettings.ScreenWidth / 2 - 120, GameSettings.ScreenHeight - 70, 120, 45));
        _selectGeneralBtn.NormalColor = new Color(60, 50, 80);
        _selectGeneralBtn.HoverColor = new Color(90, 75, 120);

        _confirmDeployBtn = new Button("确认出击", new Rectangle(GameSettings.ScreenWidth / 2 + 10, GameSettings.ScreenHeight - 70, 120, 45));
        _confirmDeployBtn.NormalColor = new Color(100, 50, 30);
        _confirmDeployBtn.HoverColor = new Color(140, 70, 40);
    }

    public void Open(CityData city, List<GeneralData> allGenerals, Action? onClose = null, Action<List<string>, List<GeneralDeployEntry>, CityData>? onLaunchArmy = null)
    {
        SourceCity = city;
        SelectedGeneralId = null;
        TargetCity = null;
        MovePath = null;
        Phase = CityActionPhase.Main;
        _allGeneralsRef = allGenerals;
        _availableGenerals = GameState.Instance.GetAvailableGeneralsForCity(city);
        _cityGenerals = GameState.Instance.GetCityGenerals(city.Id);
        _onClose = onClose;
        _onLaunchArmy = onLaunchArmy;
        _deployCards.Clear();
        _currentTalentTab = TalentSubTab.Discover;
    }

    public void Update(InputManager input, List<CityNode> allCities)
    {
        switch (Phase)
        {
            case CityActionPhase.Main:
                UpdateMainPhase(input);
                break;
            case CityActionPhase.MilitaryMain:
                UpdateMilitaryMainPhase(input);
                break;
            case CityActionPhase.MilitaryDeploy:
                UpdateMilitaryDeployPhase(input);
                break;
            case CityActionPhase.SelectGeneral:
                UpdateSelectGeneralPhase(input);
                break;
            case CityActionPhase.MilitarySelectTarget:
                UpdateMilitarySelectTargetPhase(input, allCities);
                break;
            case CityActionPhase.MilitaryConfirm:
                UpdateMilitaryConfirmPhase(input);
                break;
            case CityActionPhase.InteriorMain:
                UpdateInteriorMainPhase(input);
                break;
            case CityActionPhase.InteriorEconomy:
                UpdateInteriorEconomyPhase(input);
                break;
            case CityActionPhase.InteriorDefense:
                UpdateInteriorDefensePhase(input);
                break;
            case CityActionPhase.InteriorBuilding:
                UpdateInteriorBuildingPhase(input);
                break;
            case CityActionPhase.TalentManage:
                UpdateTalentManagePhase(input);
                break;
        }
    }

    // ==================== 主菜单 ====================
    private void UpdateMainPhase(InputManager input)
    {
        _militaryBtn.Update(input);
        _interiorBtn.Update(input);
        _talentBtn.Update(input);
        _cancelBtn.Update(input);

        if (_cancelBtn.IsHovered && input.IsMouseClicked()) { Close(); return; }
        if (_militaryBtn.IsHovered && input.IsMouseClicked())
        {
            Phase = CityActionPhase.MilitaryMain;
            return;
        }
        if (_interiorBtn.IsHovered && input.IsMouseClicked())
        {
            Phase = CityActionPhase.InteriorMain;
            return;
        }
        if (_talentBtn.IsHovered && input.IsMouseClicked())
        {
            Phase = CityActionPhase.TalentManage;
            return;
        }
    }

    // ==================== 军事主菜单 ====================
    private void UpdateMilitaryMainPhase(InputManager input)
    {
        _deployBtn.Update(input);
        _generalRosterBtn.Update(input);
        _backBtn.Update(input);

        if (_backBtn.IsHovered && input.IsMouseClicked()) { Phase = CityActionPhase.Main; return; }
        if (_deployBtn.IsHovered && input.IsMouseClicked())
        {
            Phase = CityActionPhase.MilitaryDeploy;
            return;
        }
        if (_generalRosterBtn.IsHovered && input.IsMouseClicked())
        {
            _onOpenGeneralRoster?.Invoke();
            return;
        }

        // 显示当前编队信息
        int squadCount = GameState.Instance.CurrentSquad.Count;
        // （显示逻辑在 Draw 中处理）
    }

    // ==================== 编队出征 ====================
    private void UpdateMilitaryDeployPhase(InputManager input)
    {
        _backBtn.Update(input);
        _selectGeneralBtn.Update(input);
        _confirmDeployBtn.Update(input);

        if (_backBtn.IsHovered && input.IsMouseClicked()) { Phase = CityActionPhase.MilitaryMain; return; }

        // 选择武将按钮
        if (_selectGeneralBtn.IsHovered && input.IsMouseClicked())
        {
            Phase = CityActionPhase.SelectGeneral;
            _deployCards.Clear();
            return;
        }

        // 确认出击按钮
        if (_confirmDeployBtn.IsHovered && input.IsMouseClicked())
        {
            var squad = GameState.Instance.CurrentSquad;
            if (squad.Count == 0) return; // 编队为空，不能出击
            Phase = CityActionPhase.MilitarySelectTarget;
            return;
        }

        // 更新编队武将移除按钮
        _squadRemoveButtons.Clear();
        int squadCount = GameState.Instance.CurrentSquad.Count;
        int startX = 200;
        int startY = 150;
        int btnW = 200;
        int btnH = 55;
        int spacing = 10;

        for (int i = 0; i < squadCount; i++)
        {
            string genId = GameState.Instance.CurrentSquad[i];
            string name = _getGeneralName?.Invoke(genId) ?? genId;
            var btn = new Button($"X {name}", new Rectangle(startX + i * (btnW + spacing), startY, btnW, btnH));
            btn.NormalColor = new Color(80, 40, 40);
            btn.HoverColor = new Color(120, 60, 60);
            _squadRemoveButtons.Add(btn);
        }

        // 点击移除按钮
        for (int i = 0; i < _squadRemoveButtons.Count; i++)
        {
            _squadRemoveButtons[i].Update(input);
            if (_squadRemoveButtons[i].IsHovered && input.IsMouseClicked())
            {
                var squad = GameState.Instance.CurrentSquad.ToList();
                squad.RemoveAt(i);
                GameState.Instance.SetCurrentSquad(squad);
                return;
            }
        }
    }

    // ==================== 武将选择模式（简洁布局） ====================
    private int _clickCooldownFrames = 0;

    private void UpdateSelectGeneralPhase(InputManager input)
    {
        _backBtn.Update(input);

        if (_clickCooldownFrames > 0) _clickCooldownFrames--;
        bool canClick = _clickCooldownFrames == 0;

        if (_backBtn.IsHovered && input.IsMouseClicked() && canClick)
        {
            _clickCooldownFrames = 5;
            Phase = CityActionPhase.MilitaryDeploy;
            _deployCards.Clear();
            return;
        }

        // 确认选择按钮
        var confirmBtn = new Button("确认选择", new Rectangle(GameSettings.ScreenWidth / 2 - 60, GameSettings.ScreenHeight - 60, 120, 40));
        confirmBtn.NormalColor = new Color(60, 80, 40);
        confirmBtn.HoverColor = new Color(90, 120, 60);
        confirmBtn.Update(input);

        if (confirmBtn.IsHovered && input.IsMouseClicked() && canClick)
        {
            _clickCooldownFrames = 5;
            var selectedCards = _deployCards.Where(c => c.IsSelected).Take(3).ToList();
            if (selectedCards.Count > 0)
            {
                var squadIds = new List<string>();
                var deployEntries = new List<GeneralDeployEntry>();

                foreach (var card in selectedCards)
                {
                    squadIds.Add(card.Data.Id);
                    var unitType = GetUnitTypeFromIndex(card.UnitIndex);
                    var formation = GetFormationFromIndex(card.FormationIndex);
                    deployEntries.Add(new GeneralDeployEntry
                    {
                        GeneralId = card.Data.Id,
                        UnitType = unitType,
                        BattleFormation = formation,
                        SoldierCount = card.SoldierCount
                    });
                }

                GameState.Instance.SetCurrentSquad(squadIds);
                GameState.Instance.CurrentDeployConfigs = deployEntries;
            }
            Phase = CityActionPhase.MilitaryDeploy;
            _deployCards.Clear();
            return;
        }

        // 初始化卡片列表
        if (_deployCards.Count == 0)
        {
            var squadSet = GameState.Instance.CurrentSquad.ToHashSet();
            foreach (var genData in _allGeneralsRef)
            {
                var progress = GameState.Instance.GetGeneralProgress(genData.Id);
                if (progress == null || !progress.IsUnlocked) continue;
                if (progress.Status != GeneralStatus.Recruited) continue;
                if (squadSet.Contains(genData.Id)) continue;

                _deployCards.Add(new GeneralDeployCard
                {
                    Data = genData,
                    IsSelected = false,
                    SelectedUnit = "步兵",
                    SelectedFormation = "先锋",
                    SoldierCount = 30
                });
            }
        }

        var unitNames = GetUnitNames();
        var formationNames = GetFormationNames();

        // 更新卡片交互
        int cardStartY = 110;
        int cardH = 130;  // 增加高度以容纳两行配置
        int spacing = 10;
        int currentY = cardStartY;

        foreach (var card in _deployCards)
        {
            var cardRect = new Rectangle(20, currentY, GameSettings.ScreenWidth - 40, cardH);
            var mousePos = input.MousePosition.ToPoint();

            // 第一行配置区 Y
            int row1Y = cardRect.Y + 55;
            // 第二行配置区 Y
            int row2Y = cardRect.Y + 88;

            // 兵种点击区域
            var unitRect = new Rectangle(cardRect.X + 195, row1Y, 110, 25);
            // 阵型点击区域
            var formRect = new Rectangle(cardRect.X + 410, row1Y, 110, 25);
            // 士兵数 +/- 按钮
            var minusRect = new Rectangle(cardRect.X + 640, row1Y, 28, 25);
            var plusRect = new Rectangle(cardRect.X + 675, row1Y, 28, 25);

            bool clickedOnConfig = unitRect.Contains(mousePos) || formRect.Contains(mousePos) || minusRect.Contains(mousePos) || plusRect.Contains(mousePos);

            // 选择按钮
            var toggleRect = new Rectangle(cardRect.X + 8, cardRect.Y + 8, 65, 35);
            if (toggleRect.Contains(mousePos) && input.IsMouseClicked() && !clickedOnConfig && canClick)
            {
                _clickCooldownFrames = 5;
                if (GetSelectedCount() < 3) card.IsSelected = !card.IsSelected;
                else if (card.IsSelected) card.IsSelected = false;
            }

            // 兵种选择
            if (unitRect.Contains(mousePos) && input.IsMouseClicked() && canClick)
            {
                _clickCooldownFrames = 5;
                int currentIdx = card.UnitIndex;
                card.SelectedUnit = unitNames[(currentIdx + 1) % unitNames.Length];
            }

            // 阵型选择
            if (formRect.Contains(mousePos) && input.IsMouseClicked() && canClick)
            {
                _clickCooldownFrames = 5;
                int currentIdx = card.FormationIndex;
                card.SelectedFormation = formationNames[(currentIdx + 1) % formationNames.Length];
            }

            // 士兵数 -
            if (minusRect.Contains(mousePos) && input.IsMouseClicked() && canClick)
            {
                _clickCooldownFrames = 5;
                card.SoldierCount = Math.Max(10, card.SoldierCount - 5);
            }

            // 士兵数 +
            if (plusRect.Contains(mousePos) && input.IsMouseClicked() && canClick)
            {
                _clickCooldownFrames = 5;
                card.SoldierCount = Math.Min(100, card.SoldierCount + 5);
            }

            currentY += cardH + spacing;
        }
    }

    private string[] GetUnitNames() => new[] { "步兵", "枪兵", "盾兵", "骑兵", "重骑", "轻骑", "弓兵", "强弩", "攻城", "法师" };
    private string[] GetFormationNames() => new[] { "先锋", "鱼鳞", "方阵", "锥形", "长蛇", "鹤翼", "八卦", "偃月", "环形" };
    private UnitType GetUnitTypeFromIndex(int idx)
    {
        var types = new[] { UnitType.Infantry, UnitType.Spearman, UnitType.ShieldInfantry, UnitType.Cavalry, UnitType.HeavyCavalry, UnitType.LightCavalry, UnitType.Archer, UnitType.Crossbowman, UnitType.Siege, UnitType.Mage };
        return idx >= 0 && idx < types.Length ? types[idx] : UnitType.Infantry;
    }
    private BattleFormation GetFormationFromIndex(int idx)
    {
        var formations = new[] { BattleFormation.Vanguard, BattleFormation.FishScale, BattleFormation.Square, BattleFormation.Wedge, BattleFormation.LongSnake, BattleFormation.CraneWing, BattleFormation.EightTrigrams, BattleFormation.CrescentMoon, BattleFormation.Circle };
        return idx >= 0 && idx < formations.Length ? formations[idx] : BattleFormation.Vanguard;
    }

    private int GetSelectedCount() => _deployCards.Count(c => c.IsSelected);

    // ==================== 目标选择 ====================
    private void UpdateMilitarySelectTargetPhase(InputManager input, List<CityNode> allCities)
    {
        _backBtn.Update(input);

        if (_backBtn.IsHovered && input.IsMouseClicked())
        {
            Phase = CityActionPhase.MilitaryDeploy;
            TargetCity = null;
            MovePath = null;
            return;
        }

        if (input.IsMouseClicked())
        {
            foreach (var city in allCities)
            {
                if (city.Bounds.Contains(WorldMousePos.ToPoint()) && city.Data.Id != SourceCity?.Id)
                {
                    if (SourceCity != null)
                    {
                        var path = MapPathfinder.FindPath(SourceCity.Id, city.Data.Id, allCities, "player");
                        if (path.Count >= 2)
                        {
                            TargetCity = city.Data;
                            MovePath = path;
                            Phase = CityActionPhase.MilitaryConfirm;
                        }
                    }
                    break;
                }
            }
        }
    }

    // ==================== 确认行军 ====================
    private void UpdateMilitaryConfirmPhase(InputManager input)
    {
        _backBtn.Update(input);
        _confirmBtn.Update(input);

        if (_backBtn.IsHovered && input.IsMouseClicked())
        {
            Phase = CityActionPhase.MilitarySelectTarget;
            TargetCity = null;
            MovePath = null;
            return;
        }

        if (_confirmBtn.IsHovered && input.IsMouseClicked())
        {
            LaunchArmy();
        }
    }

    private void LaunchArmy()
    {
        if (SourceCity == null || TargetCity == null) return;

        var generalIds = GameState.Instance.CurrentSquad.ToList();
        if (generalIds.Count == 0) return;

        // 获取出征配置
        var deployConfigs = GameState.Instance.CurrentDeployConfigs.ToList();

        _onLaunchArmy?.Invoke(generalIds, deployConfigs, TargetCity);
        Close();
    }

    // ==================== 内政主菜单 ====================
    private void UpdateInteriorMainPhase(InputManager input)
    {
        _economyBtn.Update(input);
        _defenseBtn.Update(input);
        _buildingBtn.Update(input);
        _backBtn.Update(input);

        if (_backBtn.IsHovered && input.IsMouseClicked()) { Phase = CityActionPhase.Main; return; }
        if (_economyBtn.IsHovered && input.IsMouseClicked()) { Phase = CityActionPhase.InteriorEconomy; return; }
        if (_defenseBtn.IsHovered && input.IsMouseClicked()) { Phase = CityActionPhase.InteriorDefense; return; }
        if (_buildingBtn.IsHovered && input.IsMouseClicked()) { Phase = CityActionPhase.InteriorBuilding; return; }
    }

    private void UpdateInteriorEconomyPhase(InputManager input)
    {
        _backBtn.Update(input);
        if (_backBtn.IsHovered && input.IsMouseClicked()) { Phase = CityActionPhase.InteriorMain; return; }
    }

    private void UpdateInteriorDefensePhase(InputManager input)
    {
        _backBtn.Update(input);
        if (_backBtn.IsHovered && input.IsMouseClicked()) { Phase = CityActionPhase.InteriorMain; return; }
    }

    // ==================== 人才管理 ====================
    private void UpdateTalentManagePhase(InputManager input)
    {
        _backBtn.Update(input);
        if (_backBtn.IsHovered && input.IsMouseClicked()) { Phase = CityActionPhase.Main; return; }

        // Tab 按钮
        _talentTabButtons.Clear();
        int tabY = 80;
        int tabW = 120;
        int tabH = 35;
        int tabStartX = GameSettings.ScreenWidth / 2 - (tabW * 3 + 20) / 2;
        int tabSpacing = 10;

        string[] tabLabels = { "发现人才", "说服在野", "招降俘虏" };
        TalentSubTab[] tabValues = { TalentSubTab.Discover, TalentSubTab.Persuade, TalentSubTab.Recruit };
        for (int i = 0; i < 3; i++)
        {
            var btn = new Button(tabLabels[i], new Rectangle(tabStartX + i * (tabW + tabSpacing), tabY, tabW, tabH));
            btn.NormalColor = _currentTalentTab == tabValues[i] ? new Color(80, 100, 60) : new Color(50, 50, 50);
            btn.HoverColor = _currentTalentTab == tabValues[i] ? new Color(100, 130, 80) : new Color(70, 70, 70);
            _talentTabButtons.Add(btn);
            btn.Update(input);
            if (btn.IsHovered && input.IsMouseClicked())
            {
                _currentTalentTab = tabValues[i];
            }
        }

        // 根据当前 Tab 更新列表
        switch (_currentTalentTab)
        {
            case TalentSubTab.Discover:
                UpdateTalentDiscoverButtons(input);
                break;
            case TalentSubTab.Persuade:
                UpdateTalentPersuadeButtons(input);
                break;
            case TalentSubTab.Recruit:
                UpdateTalentRecruitButtons(input);
                break;
        }
    }

    private void UpdateTalentDiscoverButtons(InputManager input)
    {
        _talentDiscoverButtons.Clear();

        // 发现人才按钮
        var discoverBtn = new Button("发现人才 (消耗100战功)", new Rectangle(GameSettings.ScreenWidth / 2 - 100, 140, 200, 45));
        discoverBtn.NormalColor = new Color(80, 60, 40);
        discoverBtn.HoverColor = new Color(120, 90, 60);
        discoverBtn.Update(input);

        if (discoverBtn.IsHovered && input.IsMouseClicked())
        {
            if (GameState.Instance.DiscoverTalent(_allGeneralsRef, out string discoveredId, out string errorMsg))
            {
                // 发现成功，可在 UI 中显示提示
            }
        }

        // 显示已发现但未招募的武将
        var talents = GameState.Instance.GetAvailableTalents();
        int startX = 200;
        int startY = 200;
        int btnW = 180;
        int btnH = 45;
        int spacing = 10;
        int col = 0;
        int row = 0;

        foreach (var talent in talents)
        {
            string name = talent.Data.Name;
            var btn = new Button(name, new Rectangle(startX + col * (btnW + spacing), startY + row * (btnH + spacing), btnW, btnH));
            btn.NormalColor = new Color(50, 60, 40);
            btn.HoverColor = new Color(70, 90, 60);
            _talentDiscoverButtons.Add(btn);
            btn.Update(input);

            col++;
            if (col >= 3) { col = 0; row++; }
        }
    }

    private void UpdateTalentPersuadeButtons(InputManager input)
    {
        _talentPersuadeButtons.Clear();

        var talents = GameState.Instance.GetAvailableTalents();
        if (SourceCity == null) return;

        int startX = 200;
        int startY = 140;
        int btnW = 200;
        int btnH = 50;
        int spacing = 10;
        int col = 0;
        int row = 0;

        foreach (var talent in talents)
        {
            string name = talent.Data.Name;
            var btn = new Button($"说服 {name} (200金)", new Rectangle(startX + col * (btnW + spacing), startY + row * (btnH + spacing), btnW, btnH));
            btn.NormalColor = new Color(60, 50, 80);
            btn.HoverColor = new Color(90, 75, 120);
            _talentPersuadeButtons.Add(btn);
            btn.Update(input);

            if (btn.IsHovered && input.IsMouseClicked())
            {
                if (GameState.Instance.PersuadeTalent(talent.Data.Id, SourceCity.Id, out string errorMsg))
                {
                    // 说服成功
                }
            }

            col++;
            if (col >= 2) { col = 0; row++; }
        }
    }

    private void UpdateTalentRecruitButtons(InputManager input)
    {
        _talentRecruitButtons.Clear();

        var captives = GameState.Instance.GetCaptives();
        int startX = 200;
        int startY = 140;
        int btnW = 200;
        int btnH = 50;
        int spacing = 10;
        int col = 0;
        int row = 0;

        foreach (var captive in captives)
        {
            string name = captive.Data.Name;
            var btn = new Button($"招降 {name} (150战功)", new Rectangle(startX + col * (btnW + spacing), startY + row * (btnH + spacing), btnW, btnH));
            btn.NormalColor = new Color(80, 40, 40);
            btn.HoverColor = new Color(120, 60, 60);
            _talentRecruitButtons.Add(btn);
            btn.Update(input);

            if (btn.IsHovered && input.IsMouseClicked())
            {
                if (GameState.Instance.RecruitCaptive(captive.Data.Id, out string errorMsg))
                {
                    // 招降成功
                }
            }

            col++;
            if (col >= 2) { col = 0; row++; }
        }
    }

    // ==================== 绘制 ====================
    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont, InputManager input)
    {
        if (!IsActive) return;

        int cx = GameSettings.ScreenWidth / 2;
        int cy = GameSettings.ScreenHeight / 2;

        switch (Phase)
        {
            case CityActionPhase.Main:
                DrawMainDialog(sb, pixel, font, titleFont, cx, cy);
                break;
            case CityActionPhase.MilitaryMain:
                DrawMilitaryMainDialog(sb, pixel, font, titleFont, cx, cy);
                break;
            case CityActionPhase.MilitaryDeploy:
                DrawMilitaryDeployDialog(sb, pixel, font, titleFont);
                break;
            case CityActionPhase.SelectGeneral:
                DrawSelectGeneralDialog(sb, pixel, font, titleFont, input);
                break;
            case CityActionPhase.MilitarySelectTarget:
                DrawTargetSelectHint(sb, pixel, font);
                break;
            case CityActionPhase.MilitaryConfirm:
                DrawMilitaryConfirmDialog(sb, pixel, font, titleFont);
                break;
            case CityActionPhase.InteriorMain:
                DrawInteriorMainDialog(sb, pixel, font, titleFont, cx, cy);
                break;
            case CityActionPhase.InteriorEconomy:
                DrawInteriorEconomyDialog(sb, pixel, font, titleFont);
                break;
            case CityActionPhase.InteriorDefense:
                DrawInteriorDefenseDialog(sb, pixel, font, titleFont);
                break;
            case CityActionPhase.InteriorBuilding:
                DrawInteriorBuildingDialog(sb, pixel, font, titleFont);
                break;
            case CityActionPhase.TalentManage:
                DrawTalentManageDialog(sb, pixel, font, titleFont);
                break;
        }
    }

    private void DrawDialogBg(SpriteBatch sb, Texture2D pixel)
    {
        sb.Draw(pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight), Color.Black * 0.5f);
    }

    private void DrawMainDialog(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont, int cx, int cy)
    {
        DrawDialogBg(sb, pixel);

        int dlgW = 400;
        int dlgH = 260;
        Rectangle dlg = new Rectangle(cx - dlgW / 2, cy - dlgH / 2, dlgW, dlgH);

        // 对话框背景 - 使用渐变色效果
        sb.Draw(pixel, dlg, new Color(45, 40, 32));
        
        // 顶部装饰条 - 更粗更醒目
        sb.Draw(pixel, new Rectangle(dlg.X + 2, dlg.Y + 2, dlg.Width - 4, 4), new Color(200, 170, 100));
        
        // 内边框 - 增加层次感
        sb.Draw(pixel, new Rectangle(dlg.X + 8, dlg.Y + 8, dlg.Width - 16, dlg.Height - 16), Color.Transparent);
        DrawBorder(sb, pixel, new Rectangle(dlg.X + 8, dlg.Y + 8, dlg.Width - 16, dlg.Height - 16), new Color(80, 70, 55), 1);
        
        // 外边框 - 双层边框设计
        DrawBorder(sb, pixel, dlg, new Color(160, 135, 85), 3);

        // 标题 - 增大字号，居中显示
        string title = SourceCity?.Name ?? "城池操作";
        Vector2 titleSize = titleFont.MeasureString(title);
        sb.DrawString(titleFont, title, new Vector2(cx - titleSize.X / 2, dlg.Y + 18), new Color(255, 220, 150));
        
        // 副标题 - 使用分隔线
        sb.Draw(pixel, new Rectangle(dlg.X + 30, dlg.Y + 50, dlg.Width - 60, 1), new Color(100, 85, 60));
        sb.DrawString(font, "选择操作类型", new Vector2(cx - 55, dlg.Y + 58), new Color(200, 180, 140));

        // 绘制按钮
        _militaryBtn.Draw(sb, font, pixel);
        _interiorBtn.Draw(sb, font, pixel);
        _talentBtn.Draw(sb, font, pixel);
        _cancelBtn.Draw(sb, font, pixel);
    }

    private void DrawMilitaryMainDialog(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont, int cx, int cy)
    {
        DrawDialogBg(sb, pixel);

        Rectangle topPanel = new Rectangle(0, 0, GameSettings.ScreenWidth, 80);
        sb.Draw(pixel, topPanel, new Color(30, 25, 20, 240));
        DrawBorder(sb, pixel, topPanel, new Color(100, 85, 60), 2);

        sb.DrawString(titleFont, "军事管理", new Vector2(20, 25), new Color(240, 200, 140));
        _backBtn.Draw(sb, font, pixel);

        // 军事子菜单
        _deployBtn.Draw(sb, font, pixel);
        _generalRosterBtn.Draw(sb, font, pixel);

        // 城池信息
        if (SourceCity != null)
        {
            var progress = GameState.Instance.GetCityProgress(SourceCity.Id);
            if (progress != null)
            {
                sb.DrawString(font, $"城池等级: {progress.Level}", new Vector2(20, 90), new Color(200, 180, 140));
                sb.DrawString(font, $"驻军武将: {_cityGenerals.Count}人", new Vector2(20, 115), new Color(200, 180, 140));
                sb.DrawString(font, $"当前编队: {GameState.Instance.CurrentSquad.Count}/3人", new Vector2(20, 140), new Color(200, 180, 140));
            }
        }
    }

    private void DrawMilitaryDeployDialog(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont)
    {
        DrawDialogBg(sb, pixel);

        Rectangle topPanel = new Rectangle(0, 0, GameSettings.ScreenWidth, 80);
        sb.Draw(pixel, topPanel, new Color(30, 25, 20, 240));
        sb.DrawString(titleFont, "编队出征", new Vector2(20, 25), new Color(240, 200, 140));
        _backBtn.Draw(sb, font, pixel);

        // 当前编队武将卡片（最多3人）
        var squad = GameState.Instance.CurrentSquad;
        int cardStartX = 200;
        int cardY = 100;
        int cardW = 180;
        int cardH = 70;
        int cardSpacing = 15;

        for (int i = 0; i < 3; i++)
        {
            int cardX = cardStartX + i * (cardW + cardSpacing);
            Rectangle cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            if (i < squad.Count)
            {
                string genId = squad[i];
                string name = _getGeneralName?.Invoke(genId) ?? genId;

                // 武将卡片背景
                sb.Draw(pixel, cardRect, new Color(50, 45, 35));
                DrawBorder(sb, pixel, cardRect, new Color(120, 100, 70), 2);

                // 武将名
                sb.DrawString(font, name, new Vector2(cardX + 10, cardY + 10), new Color(240, 200, 140));

                // 属性信息
                var progress = GameState.Instance.GetGeneralProgress(genId);
                if (progress != null)
                {
                    sb.DrawString(font, $"Lv.{progress.Level}  武:{progress.Data.Strength}  智:{progress.Data.Intelligence}",
                        new Vector2(cardX + 10, cardY + 32), new Color(180, 160, 130));
                }
            }
            else
            {
                // 空位
                sb.Draw(pixel, cardRect, new Color(30, 28, 25));
                DrawBorder(sb, pixel, cardRect, new Color(80, 70, 50), 2);
                Vector2 emptySize = font.MeasureString("空位");
                sb.DrawString(font, "空位", new Vector2(cardX + (cardW - emptySize.X) / 2, cardY + (cardH - 20) / 2), new Color(100, 90, 70));
            }
        }

        // 底部按钮
        _selectGeneralBtn.Draw(sb, font, pixel);

        // 确认出击按钮（编队为空时置灰）
        bool squadEmpty = squad.Count == 0;
        Color origConfirmColor = _confirmDeployBtn.NormalColor;
        Color origConfirmHover = _confirmDeployBtn.HoverColor;
        if (squadEmpty)
        {
            _confirmDeployBtn.NormalColor = new Color(50, 50, 50);
            _confirmDeployBtn.HoverColor = new Color(60, 60, 60);
        }
        _confirmDeployBtn.Draw(sb, font, pixel);
        _confirmDeployBtn.NormalColor = origConfirmColor;
        _confirmDeployBtn.HoverColor = origConfirmHover;
    }

    private void DrawSelectGeneralDialog(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont, InputManager input)
    {
        DrawDialogBg(sb, pixel);

        Rectangle topPanel = new Rectangle(0, 0, GameSettings.ScreenWidth, 80);
        sb.Draw(pixel, topPanel, new Color(30, 25, 20, 240));
        sb.DrawString(titleFont, "选择武将并配置兵种", new Vector2(20, 25), new Color(240, 200, 140));
        _backBtn.Draw(sb, font, pixel);

        int squadCount = GameState.Instance.CurrentSquad.Count;
        sb.DrawString(font, $"当前编队: {squadCount}/3人  |  已选中: {GetSelectedCount()}人",
            new Vector2(20, 90), new Color(200, 180, 140));

        int cardStartY = 110;
        int cardH = 130;
        int spacing = 10;
        int currentY = cardStartY;

        foreach (var card in _deployCards)
        {
            var cardRect = new Rectangle(20, currentY, GameSettings.ScreenWidth - 40, cardH);
            var mousePos = input.MousePosition.ToPoint();

            // 卡片背景 - 增强对比度
            Color bgColor = card.IsSelected ? new Color(70, 95, 50) : new Color(50, 45, 38);
            sb.Draw(pixel, cardRect, bgColor);
            DrawBorder(sb, pixel, cardRect, card.IsSelected ? new Color(140, 180, 80) : new Color(110, 95, 70), 2);

            // 选择按钮 - 更明显的状态区分
            var toggleRect = new Rectangle(cardRect.X + 8, cardRect.Y + 8, 65, 35);
            sb.Draw(pixel, toggleRect, card.IsSelected ? new Color(100, 145, 65) : new Color(60, 55, 48));
            DrawBorder(sb, pixel, toggleRect, card.IsSelected ? new Color(140, 180, 80) : new Color(110, 95, 70), 2);
            sb.DrawString(font, card.IsSelected ? "已选" : "选择", new Vector2(toggleRect.X + 13, toggleRect.Y + 7), new Color(255, 235, 180));

            // 头像占位
            int avatarX = cardRect.X + 80;
            int avatarY = cardRect.Y + 8;
            int avatarW = 60;
            int avatarH = 70;
            var avatarRect = new Rectangle(avatarX, avatarY, avatarW, avatarH);
            sb.Draw(pixel, avatarRect, new Color(75, 65, 55));
            DrawBorder(sb, pixel, avatarRect, new Color(110, 100, 80), 2);

            // 武将名
            sb.DrawString(font, card.Data.Name, new Vector2(avatarX + avatarW + 10, cardRect.Y + 8), new Color(255, 230, 160));

            // 属性信息
            sb.DrawString(font, $"武力:{card.Data.Strength}  智力:{card.Data.Intelligence}  统帅:{card.Data.Leadership}  速度:{card.Data.Speed}",
                new Vector2(avatarX + avatarW + 10, cardRect.Y + 30), new Color(190, 170, 140));

            // 配置区第一行：兵种、阵型、士兵数
            int row1Y = cardRect.Y + 55;

            // 兵种（点击切换）- 增强边框对比度
            bool unitHover = new Rectangle(cardRect.X + 195, row1Y, 110, 25).Contains(mousePos);
            sb.DrawString(font, "兵种:", new Vector2(cardRect.X + 150, row1Y + 3), new Color(190, 170, 130));
            sb.Draw(pixel, new Rectangle(cardRect.X + 195, row1Y, 110, 25), unitHover ? new Color(80, 75, 60) : new Color(55, 50, 42));
            DrawBorder(sb, pixel, new Rectangle(cardRect.X + 195, row1Y, 110, 25), new Color(120, 105, 75), 2);
            sb.DrawString(font, card.SelectedUnit, new Vector2(cardRect.X + 215, row1Y + 3), new Color(255, 240, 180));

            // 阵型（点击切换）
            bool formHover = new Rectangle(cardRect.X + 410, row1Y, 110, 25).Contains(mousePos);
            sb.DrawString(font, "阵型:", new Vector2(cardRect.X + 365, row1Y + 3), new Color(190, 170, 130));
            sb.Draw(pixel, new Rectangle(cardRect.X + 410, row1Y, 110, 25), formHover ? new Color(80, 75, 60) : new Color(55, 50, 42));
            DrawBorder(sb, pixel, new Rectangle(cardRect.X + 410, row1Y, 110, 25), new Color(120, 105, 75), 2);
            sb.DrawString(font, card.SelectedFormation, new Vector2(cardRect.X + 430, row1Y + 3), new Color(255, 240, 180));

            // 士兵数
            sb.DrawString(font, "士兵:", new Vector2(cardRect.X + 560, row1Y + 3), new Color(170, 150, 120));
            sb.DrawString(font, card.SoldierCount.ToString(), new Vector2(cardRect.X + 600, row1Y + 3), new Color(230, 210, 170));

            // - 按钮
            bool minusHover = new Rectangle(cardRect.X + 640, row1Y, 28, 25).Contains(mousePos);
            sb.Draw(pixel, new Rectangle(cardRect.X + 640, row1Y, 28, 25), minusHover ? new Color(110, 70, 70) : new Color(90, 55, 55));
            DrawBorder(sb, pixel, new Rectangle(cardRect.X + 640, row1Y, 28, 25), new Color(130, 75, 75), 1);
            sb.DrawString(font, "-", new Vector2(cardRect.X + 650, row1Y + 3), new Color(250, 210, 150));

            // + 按钮
            bool plusHover = new Rectangle(cardRect.X + 675, row1Y, 28, 25).Contains(mousePos);
            sb.Draw(pixel, new Rectangle(cardRect.X + 675, row1Y, 28, 25), plusHover ? new Color(70, 110, 70) : new Color(55, 90, 55));
            DrawBorder(sb, pixel, new Rectangle(cardRect.X + 675, row1Y, 28, 25), new Color(75, 130, 75), 1);
            sb.DrawString(font, "+", new Vector2(cardRect.X + 685, row1Y + 3), new Color(250, 210, 150));

            // 配置区第二行：提示文字
            int row2Y = cardRect.Y + 88;
            sb.DrawString(font, "点击兵种/阵型文字切换选项", new Vector2(cardRect.X + 150, row2Y), new Color(140, 130, 110));

            currentY += cardH + spacing;
        }

        // 确认选择按钮
        var confirmBtn = new Button("确认选择", new Rectangle(GameSettings.ScreenWidth / 2 - 60, GameSettings.ScreenHeight - 60, 120, 40));
        confirmBtn.NormalColor = new Color(60, 80, 40);
        confirmBtn.HoverColor = new Color(90, 120, 60);
        confirmBtn.Draw(sb, font, pixel);
    }

    private void DrawTargetSelectHint(SpriteBatch sb, Texture2D pixel, SpriteFontBase font)
    {
        Rectangle hintBar = new Rectangle(0, 0, GameSettings.ScreenWidth, 50);
        sb.Draw(pixel, hintBar, new Color(30, 25, 20, 230));

        // 显示编队所有武将
        var squad = GameState.Instance.CurrentSquad;
        var squadNames = squad.Select(id => _getGeneralName?.Invoke(id) ?? id);
        string squadStr = string.Join(", ", squadNames);
        sb.DrawString(font, $"已选择编队: {squadStr} | 点击地图选择目标城池 | 返回取消", new Vector2(20, 15), new Color(200, 180, 140));
        _backBtn.Draw(sb, font, pixel);
    }

    private void DrawMilitaryConfirmDialog(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont)
    {
        DrawDialogBg(sb, pixel);

        Rectangle bottomPanel = new Rectangle(0, GameSettings.ScreenHeight - 120, GameSettings.ScreenWidth, 120);
        sb.Draw(pixel, bottomPanel, new Color(30, 25, 20, 240));
        DrawBorder(sb, pixel, bottomPanel, new Color(100, 85, 60), 2);

        if (SourceCity != null && TargetCity != null)
        {
            // 显示编队所有武将
            var squad = GameState.Instance.CurrentSquad;
            var squadNames = squad.Select(id => _getGeneralName?.Invoke(id) ?? id);
            string generalsStr = string.Join(", ", squadNames);

            sb.DrawString(font, $"出征武将: {generalsStr}", new Vector2(20, GameSettings.ScreenHeight - 110), new Color(200, 180, 140));
            sb.DrawString(font, $"从 {SourceCity.Name} → {TargetCity.Name}", new Vector2(20, GameSettings.ScreenHeight - 85), new Color(200, 180, 140));
            if (MovePath != null)
                sb.DrawString(font, $"途经 {MovePath.Count - 2} 城", new Vector2(400, GameSettings.ScreenHeight - 110), new Color(160, 140, 110));
            sb.DrawString(font, $"编队人数: {squad.Count}人", new Vector2(400, GameSettings.ScreenHeight - 85), new Color(160, 140, 110));
        }

        _backBtn.Draw(sb, font, pixel);
        _confirmBtn.Draw(sb, font, pixel);
    }

    private void DrawInteriorMainDialog(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont, int cx, int cy)
    {
        DrawDialogBg(sb, pixel);

        Rectangle topPanel = new Rectangle(0, 0, GameSettings.ScreenWidth, 80);
        sb.Draw(pixel, topPanel, new Color(30, 25, 20, 240));
        sb.DrawString(titleFont, "内政管理", new Vector2(20, 25), new Color(240, 200, 140));
        _backBtn.Draw(sb, font, pixel);

        _economyBtn.Draw(sb, font, pixel);
        _defenseBtn.Draw(sb, font, pixel);
        _buildingBtn.Draw(sb, font, pixel);

        // 城池信息
        if (SourceCity != null)
        {
            var progress = GameState.Instance.GetCityProgress(SourceCity.Id);
            if (progress != null)
            {
                sb.DrawString(font, $"城池等级: {progress.Level}", new Vector2(20, 90), new Color(200, 180, 140));
                sb.DrawString(font, $"人口: {progress.Population}", new Vector2(20, 115), new Color(200, 180, 140));
                sb.DrawString(font, $"粮草: {progress.Grain}", new Vector2(20, 140), new Color(200, 180, 140));
                sb.DrawString(font, $"兵力: {progress.CurrentTroops}/{SourceCity.MaxTroops}", new Vector2(20, 165), new Color(200, 180, 140));
            }
        }
    }

    private void DrawInteriorEconomyDialog(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont)
    {
        DrawDialogBg(sb, pixel);

        Rectangle topPanel = new Rectangle(0, 0, GameSettings.ScreenWidth, 80);
        sb.Draw(pixel, topPanel, new Color(30, 25, 20, 240));
        sb.DrawString(titleFont, "经济开发", new Vector2(20, 25), new Color(240, 200, 140));
        _backBtn.Draw(sb, font, pixel);

        if (SourceCity != null)
        {
            var progress = GameState.Instance.GetCityProgress(SourceCity.Id);
            if (progress != null)
            {
                sb.DrawString(font, "当前城池经济发展状况", new Vector2(200, 100), new Color(220, 200, 160));
                sb.DrawString(font, $"人口: {progress.Population}", new Vector2(200, 140), new Color(200, 180, 140));
                sb.DrawString(font, $"粮草储备: {progress.Grain}", new Vector2(200, 170), new Color(200, 180, 140));
                sb.DrawString(font, $"城池等级: {progress.Level}", new Vector2(200, 200), new Color(200, 180, 140));
                sb.DrawString(font, "(更多经济功能开发中...)", new Vector2(200, 240), new Color(120, 120, 100));
            }
        }
    }

    private void DrawInteriorDefenseDialog(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont)
    {
        DrawDialogBg(sb, pixel);

        Rectangle topPanel = new Rectangle(0, 0, GameSettings.ScreenWidth, 80);
        sb.Draw(pixel, topPanel, new Color(30, 25, 20, 240));
        sb.DrawString(titleFont, "城池防御", new Vector2(20, 25), new Color(240, 200, 140));
        _backBtn.Draw(sb, font, pixel);

        if (SourceCity != null)
        {
            sb.DrawString(font, "当前城池防御状况", new Vector2(200, 100), new Color(220, 200, 160));
            sb.DrawString(font, $"城墙等级: {SourceCity.WallLevel}", new Vector2(200, 140), new Color(200, 180, 140));
            sb.DrawString(font, $"防御等级: {SourceCity.DefenseLevel}", new Vector2(200, 170), new Color(200, 180, 140));
            sb.DrawString(font, $"驻军加成: {(int)(SourceCity.GarrisonDefenseBonus * 100)}%", new Vector2(200, 200), new Color(200, 180, 140));
            sb.DrawString(font, $"(更多防御功能开发中...)", new Vector2(200, 240), new Color(120, 120, 100));
        }
    }

    // ==================== 建筑管理 ====================
    private string _selectedBuildingId = "";
    private int _hoveredBuildingIdx = -1;

    private void UpdateInteriorBuildingPhase(InputManager input)
    {
        _backBtn.Update(input);
        if (_backBtn.IsHovered && input.IsMouseClicked()) { Phase = CityActionPhase.InteriorMain; return; }

        if (SourceCity == null) return;

        var progress = GameState.Instance.GetCityProgress(SourceCity.Id);
        if (progress == null) return;

        // 生成建筑按钮
        _buildingButtons.Clear();
        var buildings = progress.Buildings.Values.ToList();

        int startX = 20;
        int startY = 100;
        int btnW = 380;
        int btnH = 80;
        int spacing = 10;

        for (int i = 0; i < buildings.Count; i++)
        {
            var building = buildings[i];
            var config = InteriorConfig.GetBuildingConfig(building.Id);
            if (config == null) continue;

            int col = i % 2;
            int row = i / 2;
            int x = startX + col * (btnW + spacing);
            int y = startY + row * (btnH + spacing);

            var btn = new Button("", new Rectangle(x, y, btnW, btnH));
            btn.NormalColor = _selectedBuildingId == building.Id
                ? new Color(100, 80, 60)
                : new Color(50, 45, 40);
            btn.HoverColor = new Color(70, 65, 55);
            _buildingButtons.Add(btn);

            // 检测点击
            btn.Update(input);
            if (btn.IsHovered && input.IsMouseClicked())
            {
                _selectedBuildingId = building.Id;
            }

            // 点击升级按钮
            if (_selectedBuildingId == building.Id)
            {
                int upgradeBtnX = x + btnW - 100;
                int upgradeBtnY = y + btnH - 45;
                var upgradeBtn = new Button("升级", new Rectangle(upgradeBtnX, upgradeBtnY, 80, 35));
                upgradeBtn.NormalColor = new Color(80, 120, 60);
                upgradeBtn.HoverColor = new Color(100, 150, 80);
                upgradeBtn.Update(input);

                if (upgradeBtn.IsHovered && input.IsMouseClicked())
                {
                    if (progress.UpgradeBuilding(building.Id, out string errorMsg))
                    {
                        // 升级成功
                    }
                }
            }
        }
    }

    private void DrawInteriorBuildingDialog(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont)
    {
        DrawDialogBg(sb, pixel);

        Rectangle topPanel = new Rectangle(0, 0, GameSettings.ScreenWidth, 80);
        sb.Draw(pixel, topPanel, new Color(30, 25, 20, 240));
        sb.DrawString(titleFont, "城池建筑", new Vector2(20, 25), new Color(240, 200, 140));
        _backBtn.Draw(sb, font, pixel);

        if (SourceCity == null) return;

        var progress = GameState.Instance.GetCityProgress(SourceCity.Id);
        if (progress == null) return;

        // 绘制资源条
        sb.DrawString(font, $"金币: {progress.Gold}/{progress.GoldCap}", new Vector2(20, 90), new Color(255, 220, 100));
        sb.DrawString(font, $"粮草: {progress.Food}/{progress.FoodCap}", new Vector2(200, 90), new Color(100, 220, 100));
        sb.DrawString(font, $"木材: {progress.Wood}/{progress.WoodCap}", new Vector2(380, 90), new Color(180, 140, 100));
        sb.DrawString(font, $"铁矿: {progress.Iron}/{progress.IronCap}", new Vector2(560, 90), new Color(150, 150, 180));

        // 绘制建筑列表
        var buildings = progress.Buildings.Values.ToList();
        for (int i = 0; i < _buildingButtons.Count && i < buildings.Count; i++)
        {
            var building = buildings[i];
            var config = InteriorConfig.GetBuildingConfig(building.Id);
            if (config == null) continue;

            _buildingButtons[i].Draw(sb, font, pixel);

            // 建筑名称和等级
            var btnRect = _buildingButtons[i].Bounds;
            sb.DrawString(font, $"{building.Name}", new Vector2(btnRect.X + 15, btnRect.Y + 10),
                new Color(240, 220, 180));
            sb.DrawString(font, $"Lv.{building.Level}", new Vector2(btnRect.X + 15, btnRect.Y + 35),
                new Color(180, 180, 160));

            // 建筑类型图标
            string typeIcon = config.Type switch
            {
                BuildingType.Resource => "[资源]",
                BuildingType.Military => "[军事]",
                BuildingType.Functional => "[功能]",
                BuildingType.Tech => "[科技]",
                _ => ""
            };
            sb.DrawString(font, typeIcon, new Vector2(btnRect.X + 150, btnRect.Y + 10), new Color(160, 160, 140));

            // 产量信息
            if (config.ProducesResource != ResourceType.Population)
            {
                int production = InteriorConfig.CalculateProduction(config, building.Level, SourceCity.CityScale);
                string prodStr = config.ProducesResource switch
                {
                    ResourceType.Gold => $"+{production}/h 金币",
                    ResourceType.Food => $"+{production}/h 粮草",
                    ResourceType.Wood => $"+{production}/h 木材",
                    ResourceType.Iron => $"+{production}/h 铁矿",
                    _ => ""
                };
                sb.DrawString(font, prodStr, new Vector2(btnRect.X + 150, btnRect.Y + 35), new Color(100, 200, 100));
            }

            // 绘制升级按钮（选中时显示）
            if (_selectedBuildingId == building.Id)
            {
                var upgradeBtn = new Button("升级", new Rectangle(btnRect.X + btnRect.Width - 100, btnRect.Y + btnRect.Height - 45, 80, 35));
                upgradeBtn.NormalColor = new Color(80, 120, 60);
                upgradeBtn.HoverColor = new Color(100, 150, 80);

                // 检查是否可升级
                int goldCost = InteriorConfig.CalculateUpgradeCost(config.GoldUpgradeCost, building.Level + 1);
                int foodCost = InteriorConfig.CalculateUpgradeCost(config.FoodUpgradeCost, building.Level + 1);
                int woodCost = InteriorConfig.CalculateUpgradeCost(config.WoodUpgradeCost, building.Level + 1);
                int ironCost = InteriorConfig.CalculateUpgradeCost(config.IronUpgradeCost, building.Level + 1);
                bool canUpgrade = building.Level < building.MaxLevel
                    && progress.Gold >= goldCost && progress.Food >= foodCost
                    && progress.Wood >= woodCost && progress.Iron >= ironCost;

                if (!canUpgrade)
                {
                    upgradeBtn.NormalColor = new Color(60, 60, 60);
                }
                if (building.Level >= building.MaxLevel)
                {
                    upgradeBtn.NormalColor = new Color(80, 80, 50);
                    upgradeBtn.Text = "满级";
                }

                upgradeBtn.Draw(sb, font, pixel);
            }
        }

        // 绘制选中建筑详情
        if (!string.IsNullOrEmpty(_selectedBuildingId))
        {
            var selectedBuilding = progress.GetBuilding(_selectedBuildingId);
            var selectedConfig = InteriorConfig.GetBuildingConfig(_selectedBuildingId);
            if (selectedBuilding != null && selectedConfig != null)
            {
                Rectangle detailPanel = new Rectangle(20, GameSettings.ScreenHeight - 120,
                    GameSettings.ScreenWidth - 40, 100);
                sb.Draw(pixel, detailPanel, new Color(25, 22, 18, 240));
                DrawBorder(sb, pixel, detailPanel, new Color(100, 85, 60), 2);

                sb.DrawString(font, $"选中: {selectedBuilding.Name}", new Vector2(30, GameSettings.ScreenHeight - 110),
                    new Color(240, 200, 140));

                // 升级费用
                int goldCost = InteriorConfig.CalculateUpgradeCost(selectedConfig.GoldUpgradeCost, selectedBuilding.Level + 1);
                int foodCost = InteriorConfig.CalculateUpgradeCost(selectedConfig.FoodUpgradeCost, selectedBuilding.Level + 1);
                int woodCost = InteriorConfig.CalculateUpgradeCost(selectedConfig.WoodUpgradeCost, selectedBuilding.Level + 1);
                int ironCost = InteriorConfig.CalculateUpgradeCost(selectedConfig.IronUpgradeCost, selectedBuilding.Level + 1);

                sb.DrawString(font, $"升级费用: 金{goldCost} 粮{foodCost} 木{woodCost} 铁{ironCost}",
                    new Vector2(30, GameSettings.ScreenHeight - 80), new Color(200, 180, 140));

                bool canUpgrade = selectedBuilding.Level < selectedBuilding.MaxLevel
                    && progress.Gold >= goldCost && progress.Food >= foodCost
                    && progress.Wood >= woodCost && progress.Iron >= ironCost;

                if (selectedBuilding.Level >= selectedBuilding.MaxLevel)
                {
                    sb.DrawString(font, "已达满级", new Vector2(30, GameSettings.ScreenHeight - 50),
                        new Color(180, 180, 100));
                }
                else if (!canUpgrade)
                {
                    sb.DrawString(font, "资源不足", new Vector2(30, GameSettings.ScreenHeight - 50),
                        new Color(200, 100, 100));
                }
            }
        }
    }

    // ==================== 人才管理绘制 ====================
    private void DrawTalentManageDialog(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont)
    {
        DrawDialogBg(sb, pixel);

        Rectangle topPanel = new Rectangle(0, 0, GameSettings.ScreenWidth, 80);
        sb.Draw(pixel, topPanel, new Color(30, 25, 20, 240));
        sb.DrawString(titleFont, "人才管理", new Vector2(20, 25), new Color(240, 200, 140));
        _backBtn.Draw(sb, font, pixel);

        // Tab 按钮
        foreach (var btn in _talentTabButtons)
        {
            btn.Draw(sb, font, pixel);
        }

        // 根据当前 Tab 绘制内容
        switch (_currentTalentTab)
        {
            case TalentSubTab.Discover:
                DrawTalentDiscoverContent(sb, pixel, font);
                break;
            case TalentSubTab.Persuade:
                DrawTalentPersuadeContent(sb, pixel, font);
                break;
            case TalentSubTab.Recruit:
                DrawTalentRecruitContent(sb, pixel, font);
                break;
        }
    }

    private void DrawTalentDiscoverContent(SpriteBatch sb, Texture2D pixel, SpriteFontBase font)
    {
        // 发现人才按钮
        var discoverBtn = new Button("发现人才 (消耗100战功)", new Rectangle(GameSettings.ScreenWidth / 2 - 100, 140, 200, 45));
        discoverBtn.NormalColor = new Color(80, 60, 40);
        discoverBtn.HoverColor = new Color(120, 90, 60);
        discoverBtn.Draw(sb, font, pixel);

        // 显示战功
        sb.DrawString(font, $"当前战功: {GameState.Instance.BattleMerit}", new Vector2(20, 100), new Color(200, 180, 140));

        // 已发现但未招募的武将
        var talents = GameState.Instance.GetAvailableTalents();
        sb.DrawString(font, $"已发现未招募: {talents.Count}人", new Vector2(20, 200), new Color(180, 160, 130));

        foreach (var btn in _talentDiscoverButtons)
        {
            btn.Draw(sb, font, pixel);
        }
    }

    private void DrawTalentPersuadeContent(SpriteBatch sb, Texture2D pixel, SpriteFontBase font)
    {
        sb.DrawString(font, "说服在野武将加入麾下", new Vector2(20, 120), new Color(180, 160, 130));

        foreach (var btn in _talentPersuadeButtons)
        {
            btn.Draw(sb, font, pixel);
        }
    }

    private void DrawTalentRecruitContent(SpriteBatch sb, Texture2D pixel, SpriteFontBase font)
    {
        sb.DrawString(font, "招降俘虏武将", new Vector2(20, 120), new Color(180, 160, 130));

        foreach (var btn in _talentRecruitButtons)
        {
            btn.Draw(sb, font, pixel);
        }
    }

    private void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    public void Close()
    {
        SourceCity = null;
        SelectedGeneralId = null;
        TargetCity = null;
        MovePath = null;
        Phase = CityActionPhase.Main;
        _deployCards.Clear();
        _onClose?.Invoke();
    }
}
