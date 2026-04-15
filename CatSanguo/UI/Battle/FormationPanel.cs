using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Battle;
using CatSanguo.Data.Schemas;

namespace CatSanguo.UI.Battle;

public class FormationPanel
{
    private Texture2D _pixel = null!;
    private SpriteFontBase _font = null!;
    private SpriteFontBase _smallFont = null!;

    // 展开/收起
    public bool IsExpanded { get; private set; }

    // 切换冷却
    private float _switchCooldown;
    private const float SwitchCooldownTime = 10f;

    // 当前选中的阵型
    public BattleFormation? SelectedFormation { get; set; }

    // 按钮
    private Button _toggleButton = null!;

    // 回调
    public Action<BattleFormation>? OnFormationChanged;

    // 可用阵型列表
    private static readonly BattleFormation[] AvailableFormations = new[]
    {
        BattleFormation.Vanguard,
        BattleFormation.FishScale,
        BattleFormation.Square,
        BattleFormation.Wedge,
        BattleFormation.LongSnake,
        BattleFormation.CraneWing,
        BattleFormation.CrescentMoon,
        BattleFormation.EightTrigrams,
        BattleFormation.Circle,
    };

    public void Initialize(Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont)
    {
        _pixel = pixel;
        _font = font;
        _smallFont = smallFont;

        int sw = GameSettings.ScreenWidth;
        int sh = GameSettings.ScreenHeight;

        _toggleButton = new Button("阵型", new Rectangle(sw - 80, sh - 140, 65, 30))
        {
            NormalColor = new Color(50, 40, 30),
            HoverColor = new Color(80, 60, 40),
            BorderColor = new Color(120, 100, 70),
            OnClick = () => IsExpanded = !IsExpanded
        };
    }

    public void Update(float deltaTime, InputManager input)
    {
        if (_switchCooldown > 0)
            _switchCooldown -= deltaTime;

        _toggleButton.Update(input);

        if (!IsExpanded) return;

        // 检查阵型按钮点击
        Vector2 mp = input.MousePosition;
        if (input.IsMouseClicked())
        {
            Rectangle panelRect = GetPanelRect();
            if (!panelRect.Contains(mp.ToPoint()) && !_toggleButton.Bounds.Contains(mp.ToPoint()))
            {
                IsExpanded = false;
                return;
            }

            for (int i = 0; i < AvailableFormations.Length; i++)
            {
                Rectangle itemRect = GetFormationItemRect(i);
                if (itemRect.Contains(mp.ToPoint()) && _switchCooldown <= 0)
                {
                    var formation = AvailableFormations[i];
                    if (SelectedFormation != formation)
                    {
                        SelectedFormation = formation;
                        _switchCooldown = SwitchCooldownTime;
                        OnFormationChanged?.Invoke(formation);
                        IsExpanded = false;
                    }
                    return;
                }
            }
        }
    }

    public void Draw(SpriteBatch sb)
    {
        // 切换按钮
        _toggleButton.Draw(sb, _smallFont, _pixel);

        // CD指示器
        if (_switchCooldown > 0)
        {
            string cdText = $"CD:{_switchCooldown:F0}s";
            sb.DrawString(_smallFont, cdText,
                new Vector2(_toggleButton.Bounds.X, _toggleButton.Bounds.Y - 16),
                new Color(200, 100, 100));
        }

        if (!IsExpanded) return;

        // 面板背景
        Rectangle panelRect = GetPanelRect();
        UIHelper.DrawPanel(sb, _pixel, panelRect, new Color(35, 30, 24, 240), new Color(80, 65, 45), 2);

        // 标题
        sb.DrawString(_font, "选择阵型", new Vector2(panelRect.X + 10, panelRect.Y + 6), UIHelper.TitleText);
        sb.Draw(_pixel, new Rectangle(panelRect.X + 8, panelRect.Y + 28, panelRect.Width - 16, 1),
            new Color(80, 65, 45));

        // 阵型列表
        for (int i = 0; i < AvailableFormations.Length; i++)
        {
            var formation = AvailableFormations[i];
            var config = FormationConfigTable.GetConfig(formation);
            Rectangle itemRect = GetFormationItemRect(i);

            bool isSelected = SelectedFormation == formation;
            bool canSwitch = _switchCooldown <= 0;

            // 背景
            Color itemBg = isSelected ? new Color(60, 50, 35) :
                          (canSwitch ? new Color(45, 38, 28) : new Color(35, 30, 25));
            sb.Draw(_pixel, itemRect, itemBg);

            if (isSelected)
                sb.Draw(_pixel, new Rectangle(itemRect.X, itemRect.Y, 3, itemRect.Height), UIHelper.HighlightColor);

            // 阵型名称
            string name = config?.Name ?? formation.ToString();
            Color nameColor = isSelected ? UIHelper.HighlightColor :
                             (canSwitch ? UIHelper.BodyText : UIHelper.SubText);
            sb.DrawString(_smallFont, name, new Vector2(itemRect.X + 8, itemRect.Y + 2), nameColor);

            // 分类标签
            if (config != null)
            {
                string category = config.Category switch
                {
                    "defense" => "防",
                    "attack" => "攻",
                    "tactical" => "术",
                    _ => "?"
                };
                Color catColor = config.Category switch
                {
                    "defense" => new Color(60, 130, 200),
                    "attack" => new Color(200, 80, 60),
                    "tactical" => new Color(130, 100, 200),
                    _ => UIHelper.SubText
                };
                sb.DrawString(_smallFont, category,
                    new Vector2(itemRect.Right - 18, itemRect.Y + 2), catColor);
            }

            // 简短描述（第二行）
            if (config != null)
            {
                // 取描述前15个字
                string desc = config.Description.Length > 15 ? config.Description[..15] + ".." : config.Description;
                sb.DrawString(_smallFont, desc,
                    new Vector2(itemRect.X + 8, itemRect.Y + 18), UIHelper.SubText * 0.8f);
            }
        }
    }

    private Rectangle GetPanelRect()
    {
        int sw = GameSettings.ScreenWidth;
        int sh = GameSettings.ScreenHeight;
        int panelW = 220;
        int panelH = 40 + AvailableFormations.Length * 36;
        return new Rectangle(sw - panelW - 10, sh - 150 - panelH, panelW, panelH);
    }

    private Rectangle GetFormationItemRect(int index)
    {
        Rectangle panel = GetPanelRect();
        return new Rectangle(panel.X + 5, panel.Y + 34 + index * 36, panel.Width - 10, 34);
    }
}
