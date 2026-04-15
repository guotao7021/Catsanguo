using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Battle;
using CatSanguo.Data.Schemas;
using CatSanguo.Skills;

namespace CatSanguo.UI.Battle;

public class SkillPanel
{
    private Texture2D _pixel = null!;
    private SpriteFontBase _font = null!;
    private SpriteFontBase _smallFont = null!;

    // 选中的武将索引
    public int SelectedIndex { get; set; } = -1;

    // 回调
    public Action<int>? OnSquadSelected;
    public Action<Squad>? OnSkillActivated;

    public void Initialize(Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont)
    {
        _pixel = pixel;
        _font = font;
        _smallFont = smallFont;
    }

    public void Update(InputManager input, List<Squad> playerSquads)
    {
        if (playerSquads.Count == 0) return;

        // 点击武将卡牌切换选中
        Vector2 mp = input.MousePosition;
        if (input.IsMouseClicked())
        {
            for (int i = 0; i < playerSquads.Count; i++)
            {
                Rectangle card = GetCardRect(i, playerSquads.Count);
                if (card.Contains(mp.ToPoint()))
                {
                    SelectedIndex = i;
                    OnSquadSelected?.Invoke(i);
                    return;
                }
            }

            // 技能按钮点击
            if (SelectedIndex >= 0 && SelectedIndex < playerSquads.Count)
            {
                var squad = playerSquads[SelectedIndex];
                if (squad.ActiveSkill != null && squad.IsActive)
                {
                    Rectangle skillBtn = GetSkillButtonRect(playerSquads.Count);
                    if (skillBtn.Contains(mp.ToPoint()) && squad.ActiveSkill.IsReady)
                    {
                        OnSkillActivated?.Invoke(squad);
                    }
                }
            }
        }
    }

    public void Draw(SpriteBatch sb, List<Squad> playerSquads, List<Squad> enemySquads, float battleTime)
    {
        if (playerSquads.Count == 0) return;

        int sw = GameSettings.ScreenWidth;
        int sh = GameSettings.ScreenHeight;

        // 底部栏背景
        sb.Draw(_pixel, new Rectangle(0, sh - 95, sw, 95), new Color(30, 25, 18, 220));
        sb.Draw(_pixel, new Rectangle(0, sh - 95, sw, 1), new Color(80, 65, 45));

        // 绘制玩家武将卡牌
        for (int i = 0; i < playerSquads.Count; i++)
        {
            DrawGeneralCard(sb, playerSquads[i], i, playerSquads.Count, i == SelectedIndex);
        }

        // 绘制敌方武将简略卡牌（右侧）
        for (int i = 0; i < enemySquads.Count; i++)
        {
            DrawEnemyCard(sb, enemySquads[i], i);
        }

        // 绘制选中武将的技能和属性信息
        if (SelectedIndex >= 0 && SelectedIndex < playerSquads.Count)
        {
            DrawSkillInfo(sb, playerSquads[SelectedIndex], playerSquads.Count);
        }

        // 操作提示
        sb.DrawString(_smallFont, "点击己方武将选中 | 按1释放技能",
            new Vector2(380, sh - 18), new Color(90, 80, 65));
    }

    private Rectangle GetCardRect(int index, int count)
    {
        int px = 15 + index * 135;
        int py = GameSettings.ScreenHeight - 90;
        return new Rectangle(px, py, 125, 82);
    }

    private Rectangle GetSkillButtonRect(int squadCount)
    {
        int x = 15 + squadCount * 135 + 10;
        int y = GameSettings.ScreenHeight - 85;
        return new Rectangle(x, y, 60, 60);
    }

    private void DrawGeneralCard(SpriteBatch sb, Squad squad, int index, int total, bool isSelected)
    {
        Rectangle rect = GetCardRect(index, total);

        Color bg = squad.IsDead ? new Color(40, 25, 25) :
                   (isSelected ? new Color(70, 55, 40) : new Color(42, 36, 28));
        sb.Draw(_pixel, rect, bg);

        Color borderColor = isSelected ? new Color(220, 180, 80) : new Color(70, 60, 45);
        UIHelper.DrawBorder(sb, _pixel, rect, borderColor, 2);

        // 选中标记
        if (isSelected)
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Color(220, 180, 80));

        string name = squad.General?.Name ?? "?";
        int barX = rect.X + 8, barY = rect.Y + 4;
        int barW = 108, barH = 8;

        // HP条
        float hpR = squad.MaxHP > 0 ? Math.Clamp(squad.HP / squad.MaxHP, 0, 1) : 0;
        sb.Draw(_pixel, new Rectangle(barX - 1, barY - 1, barW + 2, barH + 2), new Color(50, 40, 30));
        UIHelper.DrawBarWithHighlight(sb, _pixel, new Rectangle(barX, barY, barW, barH),
            hpR, UIHelper.GetHPColor(hpR), new Color(20, 15, 10));

        // HP百分比
        string hpPct = squad.IsDead ? "0%" : $"{(int)(hpR * 100)}%";
        var hpPctSize = _smallFont.MeasureString(hpPct);
        sb.DrawString(_smallFont, hpPct,
            new Vector2(barX + barW - hpPctSize.X, barY + barH + 1),
            squad.IsDead ? new Color(120, 60, 60) : new Color(200, 190, 160));

        // 名字
        sb.DrawString(_font, name, new Vector2(rect.X + 8, rect.Y + 18),
            squad.IsDead ? Color.Gray : new Color(220, 190, 130));

        // 军种标记
        string unitIcon = GetUnitIcon(squad);
        Color unitColor = GetUnitColor(squad);
        sb.DrawString(_smallFont, unitIcon, new Vector2(rect.X + 110, rect.Y + 20), unitColor);

        // 士气条
        float mR = Math.Clamp(squad.Morale / 100f, 0, 1);
        int mBarY = rect.Y + 44;
        UIHelper.DrawBar(sb, _pixel, new Rectangle(barX, mBarY, barW, 4),
            mR, UIHelper.GetMoraleColor(mR), new Color(15, 12, 8));

        // 士气/兵力
        sb.DrawString(_smallFont, "士气", new Vector2(barX, mBarY + 6), new Color(120, 110, 90));
        sb.DrawString(_smallFont, $"兵{squad.SoldierCount}",
            new Vector2(barX + 40, mBarY + 6), new Color(140, 130, 110));

        // 技能CD指示(小圆点)
        if (squad.ActiveSkill != null && !squad.IsDead)
        {
            Color cdDot = squad.ActiveSkill.IsReady ? new Color(60, 200, 200) : new Color(80, 80, 80);
            sb.Draw(_pixel, new Rectangle(rect.X + barW + 6, rect.Y + 62, 6, 6), cdDot);
        }

        // 阵亡覆盖
        if (squad.IsDead)
        {
            sb.Draw(_pixel, rect, new Color(0, 0, 0, 120));
            string deathText = "阵 亡";
            var dtSize = _font.MeasureString(deathText);
            sb.DrawString(_font, deathText,
                new Vector2(rect.X + (rect.Width - dtSize.X) / 2, rect.Y + (rect.Height - dtSize.Y) / 2),
                new Color(200, 60, 60));
        }
    }

    private void DrawEnemyCard(SpriteBatch sb, Squad squad, int index)
    {
        int ex = GameSettings.ScreenWidth - 15 - (index + 1) * 120;
        int ey = GameSettings.ScreenHeight - 90;
        Rectangle rect = new Rectangle(ex, ey, 110, 82);

        Color bg = squad.IsDead ? new Color(40, 25, 25) : new Color(42, 28, 28);
        sb.Draw(_pixel, rect, bg);
        UIHelper.DrawBorder(sb, _pixel, rect, new Color(100, 50, 45), 2);

        // 红色标记
        sb.Draw(_pixel, new Rectangle(ex + rect.Width - 3, ey, 3, rect.Height), new Color(180, 60, 60));

        string name = squad.General?.Name ?? "?";
        int barX = ex + 6, barY = ey + 4;
        int barW = 98, barH = 8;

        // HP条
        float hpR = squad.MaxHP > 0 ? Math.Clamp(squad.HP / squad.MaxHP, 0, 1) : 0;
        sb.Draw(_pixel, new Rectangle(barX - 1, barY - 1, barW + 2, barH + 2), new Color(50, 40, 30));
        UIHelper.DrawBarWithHighlight(sb, _pixel, new Rectangle(barX, barY, barW, barH),
            hpR, UIHelper.GetHPColor(hpR), new Color(20, 15, 10));

        // HP百分比
        string hpPct = squad.IsDead ? "0%" : $"{(int)(hpR * 100)}%";
        var hpPctSize = _smallFont.MeasureString(hpPct);
        sb.DrawString(_smallFont, hpPct,
            new Vector2(barX + barW - hpPctSize.X, barY + barH + 1),
            squad.IsDead ? new Color(120, 60, 60) : new Color(200, 190, 160));

        // 名字
        sb.DrawString(_font, name, new Vector2(ex + 6, ey + 18),
            squad.IsDead ? Color.Gray : new Color(220, 160, 140));

        // 军种标记
        string unitIcon = GetUnitIcon(squad);
        Color unitColor = GetUnitColor(squad);
        sb.DrawString(_smallFont, unitIcon, new Vector2(ex + 95, ey + 20), unitColor);

        // 士气条
        float mR = Math.Clamp(squad.Morale / 100f, 0, 1);
        int mBarY = ey + 44;
        UIHelper.DrawBar(sb, _pixel, new Rectangle(barX, mBarY, barW, 4),
            mR, UIHelper.GetMoraleColor(mR), new Color(15, 12, 8));

        sb.DrawString(_smallFont, "士气", new Vector2(barX, mBarY + 6), new Color(120, 110, 90));
        sb.DrawString(_smallFont, $"兵{squad.SoldierCount}",
            new Vector2(barX + 40, mBarY + 6), new Color(140, 130, 110));

        // 阵亡覆盖
        if (squad.IsDead)
        {
            sb.Draw(_pixel, rect, new Color(0, 0, 0, 120));
            string deathText = "阵 亡";
            var dtSize = _font.MeasureString(deathText);
            sb.DrawString(_font, deathText,
                new Vector2(ex + (rect.Width - dtSize.X) / 2, ey + (rect.Height - dtSize.Y) / 2),
                new Color(200, 60, 60));
        }
    }

    private void DrawSkillInfo(SpriteBatch sb, Squad squad, int squadCount)
    {
        int sh = GameSettings.ScreenHeight;

        // 技能按钮
        if (squad.ActiveSkill != null && squad.IsActive)
        {
            Rectangle skillBtn = GetSkillButtonRect(squadCount);
            float cdRatio = Math.Clamp(squad.ActiveSkill.CurrentCooldown / Math.Max(0.1f, squad.ActiveSkill.Cooldown), 0, 1);

            // 按钮背景
            Color btnBg = squad.ActiveSkill.IsReady ? new Color(50, 40, 30) : new Color(30, 30, 30);
            sb.Draw(_pixel, skillBtn, btnBg);

            // CD覆盖
            if (!squad.ActiveSkill.IsReady)
            {
                int cdH = (int)(skillBtn.Height * cdRatio);
                sb.Draw(_pixel, new Rectangle(skillBtn.X, skillBtn.Y, skillBtn.Width, cdH),
                    new Color(0, 0, 0, 128));
            }

            // 边框
            Color skillBorder = squad.ActiveSkill.IsReady ? new Color(200, 180, 100) : new Color(80, 80, 80);
            UIHelper.DrawBorder(sb, _pixel, skillBtn, skillBorder, 2);

            // 技能名(前2字)
            string display = squad.ActiveSkill.Name.Length > 2 ? squad.ActiveSkill.Name[..2] : squad.ActiveSkill.Name;
            var ts = _font.MeasureString(display);
            sb.DrawString(_font, display,
                new Vector2(skillBtn.X + (skillBtn.Width - ts.X) / 2, skillBtn.Y + (skillBtn.Height - ts.Y) / 2),
                squad.ActiveSkill.IsReady ? new Color(255, 230, 150) : Color.Gray);

            // 技能提示文字
            int infoX = skillBtn.Right + 15;
            string tip = squad.ActiveSkill.IsReady
                ? $"[1] {squad.ActiveSkill.Name} - 就绪!"
                : $"[1] {squad.ActiveSkill.Name} - CD: {squad.ActiveSkill.CurrentCooldown:F1}s";
            sb.DrawString(_font, tip, new Vector2(infoX, sh - 85),
                squad.ActiveSkill.IsReady ? new Color(255, 230, 100) : new Color(120, 110, 90));

            // 技能类型描述
            string desc = squad.ActiveSkill.EffectType switch
            {
                "damage" => "伤害技能",
                "buff" => "增益技能",
                "morale" => "士气技能",
                _ => ""
            };
            string targetDesc = squad.ActiveSkill.TargetMode switch
            {
                SkillTargetMode.SingleTarget => "单体",
                SkillTargetMode.AOE_Circle => "范围",
                SkillTargetMode.AOE_Line => "直线",
                SkillTargetMode.Self => "自身",
                _ => ""
            };
            if (desc.Length > 0)
            {
                sb.DrawString(_smallFont, $"{desc} | {targetDesc}",
                    new Vector2(infoX, sh - 62), new Color(140, 130, 110));
            }
        }

        // 选中武将属性
        if (squad.IsActive)
        {
            int statsX = squad.ActiveSkill != null ? GetSkillButtonRect(squadCount).Right + 15 : 15 + squadCount * 135 + 10;
            string stats = $"攻:{(int)squad.EffectiveAttack} 防:{(int)squad.EffectiveDefense} 速:{(int)squad.EffectiveSpeed}";
            sb.DrawString(_smallFont, stats, new Vector2(statsX, sh - 42), new Color(160, 150, 120));
        }
    }

    private static string GetUnitIcon(Squad squad)
    {
        return squad.UnitType switch
        {
            UnitType.Infantry => "步",
            UnitType.Spearman => "枪",
            UnitType.ShieldInfantry => "盾",
            UnitType.Cavalry => "骑",
            UnitType.HeavyCavalry => "重",
            UnitType.LightCavalry => "轻",
            UnitType.Archer => "弓",
            UnitType.Crossbowman => "弩",
            UnitType.Siege => "攻",
            UnitType.Mage => "术",
            _ => "兵"
        };
    }

    private static Color GetUnitColor(Squad squad)
    {
        return squad.UnitType switch
        {
            UnitType.Infantry or UnitType.Spearman or UnitType.ShieldInfantry => new Color(100, 160, 100),
            UnitType.Cavalry or UnitType.HeavyCavalry or UnitType.LightCavalry => new Color(130, 100, 170),
            UnitType.Archer or UnitType.Crossbowman => new Color(160, 130, 80),
            UnitType.Mage => new Color(100, 130, 200),
            UnitType.Siege => new Color(160, 140, 100),
            _ => UIHelper.SubText
        };
    }
}
