using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.UI;

namespace CatSanguo.Battle.Sango;

/// <summary>
/// 战场指令栏 - 技能/单挑/军师技/阵型按钮
/// 位于底部栏中间区域
/// </summary>
public class SangoCommandBar
{
    private readonly Texture2D _pixel;
    private readonly SpriteFontBase _font;
    private readonly SpriteFontBase _smallFont;

    // 按钮
    private readonly List<CommandSlot> _slots = new();
    private bool _isVisible;

    // 回调
    public Action<int>? OnSkillUsed;
    public Action? OnDuelChallenge;
    public Action? OnAdvisorSkill;

    public bool IsVisible => _isVisible;

    /// <summary>是否处于指令阶段 (控制按钮可用性)</summary>
    public bool IsCommandPhase { get; set; }

    public SangoCommandBar(Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont)
    {
        _pixel = pixel;
        _font = font;
        _smallFont = smallFont;
        BuildDefaultSlots();
    }

    private void BuildDefaultSlots()
    {
        _slots.Clear();

        // 技能1-3 (根据选中武将动态更新)
        _slots.Add(new CommandSlot("技能1", CommandType.Skill, 0) { IsLocked = true, Tooltip = "选中武将后可用" });
        _slots.Add(new CommandSlot("技能2", CommandType.Skill, 1) { IsLocked = true, Tooltip = "选中武将后可用" });
        _slots.Add(new CommandSlot("技能3", CommandType.Skill, 2) { IsLocked = true, Tooltip = "选中武将后可用" });

        // 单挑
        _slots.Add(new CommandSlot("单挑", CommandType.Duel, -1) { IsLocked = true, Tooltip = "武将间单挑" });

        // 军师技
        _slots.Add(new CommandSlot("军师技", CommandType.Advisor, -1) { IsLocked = true, Tooltip = "全屏范围技" });
    }

    /// <summary>更新指令栏（根据选中武将刷新技能）</summary>
    public void UpdateForGeneral(GeneralUnit? unit)
    {
        if (unit == null || unit.IsDefeated)
        {
            foreach (var s in _slots)
            {
                s.IsLocked = true;
                if (s.Type == CommandType.Skill)
                    s.Label = $"技能{s.SlotIndex + 1}";
            }
            return;
        }

        // 从 ResolvedSkills 读取技能信息
        for (int i = 0; i < 3; i++)
        {
            var slot = _slots[i];
            if (i < unit.ResolvedSkills.Count)
            {
                var rs = unit.ResolvedSkills[i];
                slot.Label = rs.Data.Name;
                slot.CooldownRemaining = rs.CooldownRoundsLeft;
                slot.CooldownTotal = rs.CooldownRoundsTotal;
                slot.IsLocked = !IsCommandPhase || rs.CooldownRoundsLeft > 0;
            }
            else
            {
                slot.Label = "---";
                slot.IsLocked = true;
            }
        }

        // 单挑: 指令阶段可用
        _slots[3].IsLocked = !IsCommandPhase;

        // 军师技: 智力>=70 且指令阶段可用
        _slots[4].IsLocked = !IsCommandPhase || unit.General.EffectiveIntelligence < 70;
    }

    public void Show() => _isVisible = true;
    public void Hide() => _isVisible = false;

    public void Update(InputManager input)
    {
        if (!_isVisible) return;

        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot.IsLocked) continue;

            if (input.IsMouseClicked() && input.IsMouseInRect(slot.Bounds))
            {
                switch (slot.Type)
                {
                    case CommandType.Skill:
                        OnSkillUsed?.Invoke(slot.SlotIndex);
                        break;
                    case CommandType.Duel:
                        OnDuelChallenge?.Invoke();
                        break;
                    case CommandType.Advisor:
                        OnAdvisorSkill?.Invoke();
                        break;
                }
            }
        }
    }

    public void Draw(SpriteBatch sb)
    {
        if (!_isVisible) return;

        int barY = GameSettings.ScreenHeight - (int)GameSettings.SangoBottomBarHeight;
        int slotSize = 50;
        int gap = 8;
        int totalW = _slots.Count * slotSize + (_slots.Count - 1) * gap;
        int startX = GameSettings.ScreenWidth / 2 - totalW / 2;
        int startY = barY + 15;

        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            int x = startX + i * (slotSize + gap);
            slot.Bounds = new Rectangle(x, startY, slotSize, slotSize);

            // 背景
            Color bgColor = slot.IsLocked ? new Color(35, 30, 25) :
                           slot.IsHovered ? new Color(60, 55, 45) :
                           new Color(45, 40, 32);
            sb.Draw(_pixel, slot.Bounds, bgColor);

            // 边框
            Color borderColor = slot.IsLocked ? new Color(60, 50, 40) :
                               slot.Type == CommandType.Duel ? new Color(200, 160, 80) :
                               slot.Type == CommandType.Advisor ? new Color(120, 100, 200) :
                               new Color(100, 90, 70);
            DrawBorder(sb, slot.Bounds, borderColor, 1);

            // 标签文字
            Color textColor = slot.IsLocked ? new Color(80, 70, 60) : Color.White;
            Vector2 textSize = _smallFont.MeasureString(slot.Label);
            // 居中显示，超长截断
            string displayText = slot.Label.Length > 3 ? slot.Label[..3] : slot.Label;
            Vector2 dSize = _smallFont.MeasureString(displayText);
            sb.DrawString(_smallFont, displayText,
                new Vector2(x + slotSize / 2 - dSize.X / 2, startY + slotSize / 2 - dSize.Y / 2),
                textColor);

            // 冷却遮罩 (回合制: 显示剩余回合数)
            if (slot.CooldownRemaining > 0)
            {
                sb.Draw(_pixel, new Rectangle(x, startY, slotSize, slotSize),
                    new Color(0, 0, 0) * 0.6f);
                string cdText = ((int)slot.CooldownRemaining).ToString();
                Vector2 cdSize = _font.MeasureString(cdText);
                sb.DrawString(_font, cdText,
                    new Vector2(x + slotSize / 2 - cdSize.X / 2, startY + slotSize / 2 - cdSize.Y / 2),
                    new Color(255, 200, 100));
            }

            // 快捷键提示
            string hotkey = (i + 1).ToString();
            sb.DrawString(_smallFont, hotkey, new Vector2(x + 2, startY + 2), new Color(150, 140, 120));
        }

        // 底部完整标签行（hover时显示）
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            int x = startX + i * (slotSize + gap);
            string fullLabel = slot.Label;
            Vector2 lSize = _smallFont.MeasureString(fullLabel);
            sb.DrawString(_smallFont, fullLabel,
                new Vector2(x + slotSize / 2 - lSize.X / 2, startY + slotSize + 4),
                slot.IsLocked ? new Color(70, 60, 50) : new Color(180, 170, 150));
        }
    }

    public void UpdateHover(InputManager input)
    {
        foreach (var slot in _slots)
        {
            slot.IsHovered = !slot.IsLocked && input.IsMouseInRect(slot.Bounds);
        }
    }

    private void DrawBorder(SpriteBatch sb, Rectangle rect, Color color, int thickness)
    {
        sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}

public enum CommandType { Skill, Duel, Advisor }

public class CommandSlot
{
    public string Label;
    public CommandType Type;
    public int SlotIndex;
    public bool IsLocked;
    public bool IsHovered;
    public string Tooltip = "";
    public float CooldownRemaining;
    public float CooldownTotal = 1f;
    public Rectangle Bounds;

    public CommandSlot(string label, CommandType type, int slotIndex)
    {
        Label = label;
        Type = type;
        SlotIndex = slotIndex;
    }
}
