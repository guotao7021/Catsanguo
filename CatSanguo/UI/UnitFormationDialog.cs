using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data.Schemas;

namespace CatSanguo.UI;

/// <summary>
/// 军种和阵型选择对话框
/// </summary>
public class UnitFormationDialog
{
    // 状态
    public bool IsActive { get; private set; }
    public bool IsVisible { get; set; } = true;

    // 选择结果
    public UnitType SelectedUnitType { get; private set; } = UnitType.Infantry;
    public BattleFormation SelectedFormation { get; private set; } = BattleFormation.Vanguard;

    // 回调
    private Action<UnitType, BattleFormation>? _onConfirm;
    private Action? _onCancel;

    // UI组件
    private Button _confirmBtn = null!;
    private Button _cancelBtn = null!;
    private Button _closeBtn = null!;

    // 选项列表
    private List<Button> _unitButtons = new();
    private List<Button> _formationButtons = new();
    private Dictionary<Button, int> _unitButtonIndex = new();
    private Dictionary<Button, int> _formationButtonIndex = new();
    private Dictionary<Button, object> _unitButtonData = new();
    private Dictionary<Button, object> _formationButtonData = new();

    // 选中状态
    private int _selectedUnitIdx = 0;
    private int _selectedFormationIdx = 0;

    // 资源
    private SpriteFontBase? _font;
    private SpriteFontBase? _titleFont;

    public void Initialize(SpriteFontBase font, SpriteFontBase titleFont)
    {
        _font = font;
        _titleFont = titleFont;

        int screenW = GameSettings.ScreenWidth;
        int screenH = GameSettings.ScreenHeight;

        // 确认按钮
        _confirmBtn = new Button("确 定", new Rectangle(screenW - 230, screenH - 70, 100, 45));
        _confirmBtn.NormalColor = new Color(60, 100, 60);
        _confirmBtn.HoverColor = new Color(80, 130, 80);

        // 取消按钮
        _cancelBtn = new Button("取 消", new Rectangle(screenW - 120, screenH - 70, 100, 45));
        _cancelBtn.NormalColor = new Color(80, 50, 50);
        _cancelBtn.HoverColor = new Color(100, 60, 60);

        // 初始化选项
        InitUnitButtons();
        InitFormationButtons();
    }

    private void InitUnitButtons()
    {
        _unitButtons.Clear();
        var unitTypes = new[]
        {
            (UnitType.Infantry, "步兵", "平衡型前排单位"),
            (UnitType.Spearman, "枪兵", "反骑兵，专克骑兵"),
            (UnitType.ShieldInfantry, "盾兵", "高防御，抗远程"),
            (UnitType.Cavalry, "骑兵", "机动突击，可穿透"),
            (UnitType.HeavyCavalry, "重骑", "冲锋爆发，高伤害"),
            (UnitType.LightCavalry, "轻骑", "极速机动，收割残血"),
            (UnitType.Archer, "弓兵", "远程输出，可溅射"),
            (UnitType.Crossbowman, "强弩", "高单体伤害"),
            (UnitType.Mage, "术士", "AOE法术伤害"),
        };

        int startX = 30;
        int startY = 100;
        int btnW = 200;
        int btnH = 70;
        int spacing = 15;

        for (int i = 0; i < unitTypes.Length; i++)
        {
            var (type, name, desc) = unitTypes[i];
            int col = i % 3;
            int row = i / 3;
            int x = startX + col * (btnW + spacing);
            int y = startY + row * (btnH + spacing);

            var btn = new Button($"{name}\n{desc}", new Rectangle(x, y, btnW, btnH));
            btn.NormalColor = new Color(50, 45, 40);
            btn.HoverColor = new Color(70, 65, 55);
            _unitButtons.Add(btn);
            _unitButtonIndex[btn] = i;
            _unitButtonData[btn] = type;
        }
    }

    private void InitFormationButtons()
    {
        _formationButtons.Clear();
        var formations = new[]
        {
            (BattleFormation.FishScale, "鱼鳞阵", "防御", "前排减伤40%，伤害分摊"),
            (BattleFormation.Square, "方阵", "防御", "全体防御+20%"),
            (BattleFormation.Wedge, "锥形阵", "进攻", "前排伤害+50%，穿透"),
            (BattleFormation.LongSnake, "长蛇阵", "进攻", "移动速度+30%"),
            (BattleFormation.CraneWing, "鹤翼阵", "进攻", "侧翼伤害+30%"),
            (BattleFormation.EightTrigrams, "八卦阵", "战术", "状态切换：防/攻/控"),
            (BattleFormation.CrescentMoon, "偃月阵", "战术", "集中输出，叠加伤害"),
            (BattleFormation.Circle, "环形阵", "战术", "中心目标+40%伤害"),
            (BattleFormation.Vanguard, "先锋阵", "进攻", "标准进攻阵型"),
        };

        int startX = 680;
        int startY = 100;
        int btnW = 200;
        int btnH = 70;
        int spacing = 15;

        for (int i = 0; i < formations.Length; i++)
        {
            var (type, name, category, desc) = formations[i];
            int col = i % 3;
            int row = i / 3;
            int x = startX + col * (btnW + spacing);
            int y = startY + row * (btnH + spacing);

            var btn = new Button($"{name}\n{category}:{desc}", new Rectangle(x, y, btnW, btnH));
            btn.NormalColor = GetFormationColor(category);
            btn.HoverColor = new Color(80, 75, 65);
            _formationButtons.Add(btn);
            _formationButtonIndex[btn] = i;
            _formationButtonData[btn] = type;
        }
    }

    private Color GetFormationColor(string category) => category switch
    {
        "防御" => new Color(40, 50, 70),
        "进攻" => new Color(70, 40, 40),
        "战术" => new Color(50, 50, 60),
        _ => new Color(50, 45, 40)
    };

    public void Open(Action<UnitType, BattleFormation>? onConfirm = null, Action? onCancel = null)
    {
        IsActive = true;
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        // 重置选中状态
        _selectedUnitIdx = 0;
        _selectedFormationIdx = 8; // 默认先锋阵
        SelectedUnitType = UnitType.Infantry;
        SelectedFormation = BattleFormation.Vanguard;

        UpdateButtonSelection();
    }

    public void Close()
    {
        IsActive = false;
        _onConfirm = null;
        _onCancel = null;
    }

    private void UpdateButtonSelection()
    {
        // 更新军种按钮选中状态
        for (int i = 0; i < _unitButtons.Count; i++)
        {
            _unitButtons[i].NormalColor = i == _selectedUnitIdx
                ? new Color(80, 70, 60)
                : new Color(50, 45, 40);
        }

        // 更新阵型按钮选中状态
        for (int i = 0; i < _formationButtons.Count; i++)
        {
            var btn = _formationButtons[i];
            var type = (BattleFormation)_formationButtonData[btn]!;
            var config = FormationConfigTable.GetConfig(type);
            string category = config?.Category ?? "attack";
            btn.NormalColor = i == _selectedFormationIdx
                ? new Color(100, 90, 80)
                : GetFormationColor(category);
        }
    }

    public void Update(InputManager input)
    {
        if (!IsActive || !IsVisible) return;

        // 军种按钮
        foreach (var btn in _unitButtons)
        {
            btn.Update(input);
            if (btn.IsHovered && input.IsMouseClicked())
            {
                _selectedUnitIdx = _unitButtonIndex[btn];
                SelectedUnitType = (UnitType)_unitButtonData[btn]!;
                UpdateButtonSelection();
            }
        }

        // 阵型按钮
        foreach (var btn in _formationButtons)
        {
            btn.Update(input);
            if (btn.IsHovered && input.IsMouseClicked())
            {
                _selectedFormationIdx = _formationButtonIndex[btn];
                SelectedFormation = (BattleFormation)_formationButtonData[btn]!;
                UpdateButtonSelection();
            }
        }

        // 确认/取消按钮
        _confirmBtn.Update(input);
        _cancelBtn.Update(input);

        if (_confirmBtn.IsHovered && input.IsMouseClicked())
        {
            _onConfirm?.Invoke(SelectedUnitType, SelectedFormation);
            Close();
        }

        if (_cancelBtn.IsHovered && input.IsMouseClicked())
        {
            _onCancel?.Invoke();
            Close();
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!IsActive || !IsVisible || _font == null || _titleFont == null) return;

        // 半透明背景
        sb.Draw(pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight),
            Color.Black * 0.7f);

        // 主面板
        Rectangle panel = new Rectangle(20, 20, GameSettings.ScreenWidth - 40, GameSettings.ScreenHeight - 40);
        sb.Draw(pixel, panel, new Color(35, 30, 25));
        DrawBorder(sb, pixel, panel, new Color(100, 85, 60), 3);

        // 标题
        sb.DrawString(_titleFont, "选择军种与阵型", new Vector2(40, 35), new Color(240, 200, 140));

        // 军种区域标题
        sb.DrawString(_font, "【军种选择】", new Vector2(30, 80), new Color(200, 180, 140));

        // 绘制军种按钮
        foreach (var btn in _unitButtons)
            btn.Draw(sb, _font, pixel);

        // 阵型区域标题
        sb.DrawString(_font, "【阵型选择】", new Vector2(680, 80), new Color(200, 180, 140));

        // 绘制阵型按钮
        foreach (var btn in _formationButtons)
            btn.Draw(sb, _font, pixel);

        // 详情面板
        Rectangle detailPanel = new Rectangle(680, 420, 620, 180);
        sb.Draw(pixel, detailPanel, new Color(30, 25, 20));
        DrawBorder(sb, pixel, detailPanel, new Color(80, 70, 50), 2);

        // 绘制选中详情
        DrawUnitDetail(sb, pixel);
        DrawFormationDetail(sb, pixel);

        // 按钮
        _confirmBtn.Draw(sb, _font, pixel);
        _cancelBtn.Draw(sb, _font, pixel);
    }

    private void DrawUnitDetail(SpriteBatch sb, Texture2D pixel)
    {
        if (_font == null) return;

        var unitConfig = UnitConfigTable.GetConfig(SelectedUnitType);
        if (unitConfig == null) return;

        sb.DrawString(_font, $"选中军种: {unitConfig.Name}", new Vector2(700, 435), new Color(240, 200, 140));

        string tags = "";
        if (unitConfig.Tags.HasFlag(UnitTag.Melee)) tags += "近战 ";
        if (unitConfig.Tags.HasFlag(UnitTag.Ranged)) tags += "远程 ";
        if (unitConfig.Tags.HasFlag(UnitTag.Cavalry)) tags += "骑兵 ";
        if (unitConfig.Tags.HasFlag(UnitTag.Heavy)) tags += "重型 ";
        if (unitConfig.Tags.HasFlag(UnitTag.Light)) tags += "轻型 ";
        sb.DrawString(_font, $"类型: {tags}", new Vector2(700, 465), new Color(180, 180, 160));

        sb.DrawString(_font, $"攻击: {unitConfig.AttackMultiplier * 100:F0}%  防御: {unitConfig.DefenseMultiplier * 100:F0}%",
            new Vector2(700, 495), new Color(180, 180, 160));
        sb.DrawString(_font, $"速度: {unitConfig.SpeedMultiplier * 100:F0}%  生命: {unitConfig.HPMultiplier * 100:F0}%",
            new Vector2(700, 520), new Color(180, 180, 160));

        string abilities = "";
        if (unitConfig.CanPierce) abilities += "穿透 ";
        if (unitConfig.CanSplash) abilities += "溅射 ";
        if (unitConfig.HasCounter) abilities += "反击 ";
        if (!string.IsNullOrEmpty(abilities))
            sb.DrawString(_font, $"特性: {abilities}", new Vector2(700, 550), new Color(100, 200, 100));
    }

    private void DrawFormationDetail(SpriteBatch sb, Texture2D pixel)
    {
        if (_font == null) return;

        var formConfig = FormationConfigTable.GetConfig(SelectedFormation);
        if (formConfig == null) return;

        sb.DrawString(_font, $"选中阵型: {formConfig.Name}", new Vector2(1000, 435), new Color(240, 200, 140));
        sb.DrawString(_font, $"类型: {formConfig.Category}", new Vector2(1000, 465), new Color(180, 180, 160));

        string bonuses = "";
        if (formConfig.AttackBonus > 0) bonuses += $"攻击+{formConfig.AttackBonus * 100:F0}% ";
        if (formConfig.DefenseBonus > 0) bonuses += $"防御+{formConfig.DefenseBonus * 100:F0}% ";
        if (formConfig.SpeedBonus > 0) bonuses += $"速度+{formConfig.SpeedBonus * 100:F0}% ";
        if (formConfig.DamageReduction > 0) bonuses += $"减伤+{formConfig.DamageReduction * 100:F0}% ";
        if (!string.IsNullOrEmpty(bonuses))
            sb.DrawString(_font, $"效果: {bonuses}", new Vector2(1000, 495), new Color(100, 200, 100));

        // 特殊能力
        string specials = "";
        if (formConfig.HasDamageShare) specials += "伤害分摊 ";
        if (formConfig.HasPierce) specials += "穿透攻击 ";
        if (formConfig.HasSurroundBonus) specials += "包围加成 ";
        if (formConfig.HasChainAttack) specials += "连锁攻击 ";
        if (formConfig.HasPhaseSwitch) specials += "状态切换 ";
        if (formConfig.HasStackDamage) specials += "叠加伤害 ";
        if (!string.IsNullOrEmpty(specials))
            sb.DrawString(_font, $"阵法: {specials}", new Vector2(1000, 525), new Color(150, 150, 200));

        // 结构描述
        sb.DrawString(_font, formConfig.Description, new Vector2(1000, 555), new Color(160, 160, 140));
    }

    private void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
