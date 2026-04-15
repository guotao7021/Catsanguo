using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Battle;
using CatSanguo.Data.Schemas;

namespace CatSanguo.UI.Battle;

public class UnitOverheadUI
{
    private Texture2D _pixel = null!;
    private SpriteFontBase _font = null!;
    private SpriteFontBase _smallFont = null!;

    public bool DetailMode { get; set; } = true;

    // 性能优化：文本缓存
    private float _textUpdateTimer;
    private const float TextUpdateInterval = 0.2f;

    public void Initialize(Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont)
    {
        _pixel = pixel;
        _font = font;
        _smallFont = smallFont;
    }

    public void Update(float deltaTime)
    {
        _textUpdateTimer += deltaTime;
        if (_textUpdateTimer >= TextUpdateInterval)
            _textUpdateTimer = 0;
    }

    public void DrawAll(SpriteBatch sb, List<Squad> squads, BuffSystem? buffSystem, Vector2 screenOffset)
    {
        foreach (var squad in squads)
        {
            if (squad.IsDead) continue;
            DrawSingleUnit(sb, squad, buffSystem, screenOffset);
        }
    }

    private void DrawSingleUnit(SpriteBatch sb, Squad squad, BuffSystem? buffSystem, Vector2 offset)
    {
        Vector2 pos = squad.Position + offset;
        string name = squad.General?.Name ?? "???";

        int barW = DetailMode ? 60 : 50;
        int barX = (int)pos.X - barW / 2;
        int baseY = (int)pos.Y - (DetailMode ? 75 : 55);

        if (DetailMode)
        {
            // ===== 详细模式 =====

            // 1. 名字（带半透明背景）
            var nameSize = _smallFont.MeasureString(name);
            int nameX = (int)(pos.X - nameSize.X / 2);
            sb.Draw(_pixel, new Rectangle(nameX - 3, baseY - 2, (int)nameSize.X + 6, (int)nameSize.Y + 2),
                new Color(0, 0, 0, 120));
            sb.DrawString(_smallFont, name, new Vector2(nameX, baseY - 1), UIHelper.TitleText);
            baseY += (int)nameSize.Y + 3;

            // 2. Buff图标行
            if (buffSystem != null)
            {
                DrawBuffIcons(sb, squad, buffSystem, barX, ref baseY);
            }

            // 3. 状态指示器
            DrawStatusIndicators(sb, squad, barX, barW, ref baseY);

            // 4. HP条
            float hpRatio = squad.MaxHP > 0 ? squad.HP / squad.MaxHP : 0;
            Color hpColor = UIHelper.GetHPColor(hpRatio);
            UIHelper.DrawBarWithHighlight(sb, _pixel,
                new Rectangle(barX, baseY, barW, 7), hpRatio, hpColor, new Color(20, 15, 10));
            baseY += 9;

            // 5. 技能CD条
            if (squad.ActiveSkill != null)
            {
                float cdRatio = squad.ActiveSkill.IsReady ? 1f :
                    1f - (squad.ActiveSkill.CurrentCooldown / Math.Max(0.1f, squad.ActiveSkill.Cooldown));
                Color cdColor = squad.ActiveSkill.IsReady ? new Color(60, 200, 200) : new Color(80, 80, 80);
                UIHelper.DrawBar(sb, _pixel,
                    new Rectangle(barX, baseY, barW, 4), cdRatio, cdColor, new Color(15, 15, 15));
                baseY += 5;
            }

            // 6. 士气条
            float moraleRatio = squad.Morale / 100f;
            Color moraleColor = UIHelper.GetMoraleColor(moraleRatio);
            UIHelper.DrawBar(sb, _pixel,
                new Rectangle(barX, baseY, barW, 3), moraleRatio, moraleColor, new Color(15, 15, 15));
            baseY += 5;

            // 7. 军种标记
            string unitIcon = GetUnitIcon(squad);
            Color unitColor = GetUnitColor(squad);
            var iconSize = _smallFont.MeasureString(unitIcon);
            sb.DrawString(_smallFont, unitIcon, new Vector2(pos.X - iconSize.X / 2, baseY), unitColor);
        }
        else
        {
            // ===== 简化模式（自动战斗） =====

            // 名字
            var nameSize = _smallFont.MeasureString(name);
            int nameX = (int)(pos.X - nameSize.X / 2);
            sb.DrawString(_smallFont, name, new Vector2(nameX, baseY), UIHelper.TitleText);
            baseY += (int)nameSize.Y + 2;

            // HP条
            float hpRatio = squad.MaxHP > 0 ? squad.HP / squad.MaxHP : 0;
            Color hpColor = UIHelper.GetHPColor(hpRatio);
            UIHelper.DrawBar(sb, _pixel,
                new Rectangle(barX, baseY, barW, 5), hpRatio, hpColor, new Color(20, 15, 10));
        }
    }

    private void DrawBuffIcons(SpriteBatch sb, Squad squad, BuffSystem buffSystem, int barX, ref int baseY)
    {
        var buffs = buffSystem.GetBuffsOn(squad);
        if (buffs.Count == 0) return;

        int iconSize = 12;
        int spacing = 2;
        int maxIcons = 4;
        int count = Math.Min(buffs.Count, maxIcons);
        int totalW = count * (iconSize + spacing) - spacing;
        int startX = barX;

        for (int i = 0; i < count; i++)
        {
            var buff = buffs[i];
            Color bgColor = buff.Config.IsDebuff ? UIHelper.DebuffColor : UIHelper.BuffColor;

            // 判断是否为控制效果
            if (buff.Config.Effects.Any(e => e.Type == "Stun" || e.Type == "Silence"))
                bgColor = UIHelper.ControlColor;

            int ix = startX + i * (iconSize + spacing);
            sb.Draw(_pixel, new Rectangle(ix, baseY, iconSize, iconSize), bgColor * 0.8f);

            // 首字
            string firstChar = buff.Config.Name.Length > 0 ? buff.Config.Name[..1] : "?";
            var charSize = _smallFont.MeasureString(firstChar);
            sb.DrawString(_smallFont, firstChar,
                new Vector2(ix + (iconSize - charSize.X) / 2, baseY + (iconSize - charSize.Y) / 2),
                Color.White * 0.9f);
        }

        if (buffs.Count > maxIcons)
        {
            sb.DrawString(_smallFont, "..", new Vector2(startX + totalW + 2, baseY), UIHelper.SubText);
        }

        baseY += iconSize + 2;
    }

    private void DrawStatusIndicators(SpriteBatch sb, Squad squad, int barX, int barW, ref int baseY)
    {
        // 眩晕
        if (squad.IsStunned)
        {
            sb.DrawString(_smallFont, "晕", new Vector2(barX + barW + 3, baseY - 2), new Color(255, 220, 50));
        }

        // 沉默
        if (squad.IsSilenced)
        {
            sb.DrawString(_smallFont, "禁", new Vector2(barX + barW + 3, baseY - 2), UIHelper.ControlColor);
        }

        // 施法中
        if (squad.State == SquadState.UsingSkill && squad.ActiveSkill != null)
        {
            string skillName = squad.ActiveSkill.Name;
            var size = _smallFont.MeasureString(skillName);
            sb.DrawString(_smallFont, skillName,
                new Vector2(barX + barW / 2 - size.X / 2, baseY - 14), new Color(255, 230, 100));
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
