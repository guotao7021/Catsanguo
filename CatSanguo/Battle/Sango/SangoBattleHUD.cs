using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;

namespace CatSanguo.Battle.Sango;

/// <summary>
/// 战场HUD - 顶部信息栏 + 底部武将信息栏
/// </summary>
public class SangoBattleHUD
{
    private readonly Texture2D _pixel;
    private readonly SpriteFontBase _font;
    private readonly SpriteFontBase _smallFont;

    // 选中武将索引 (-1=未选中)
    public int SelectedGeneralIndex { get; set; } = -1;
    public Action<int>? OnGeneralSelected;

    // 回合信息 (round, isCommand, executionTimer, executionDuration)
    public (int round, bool isCommand, float timer, float duration) DrawRoundInfo { get; set; }

    public SangoBattleHUD(Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont)
    {
        _pixel = pixel;
        _font = font;
        _smallFont = smallFont;
    }

    // ==================== 顶部HUD ====================

    public void DrawTopHUD(SpriteBatch sb, ArmyGroup playerArmy, ArmyGroup enemyArmy,
                           float battleTimer, SangoBattlePhase phase)
    {
        int hudH = (int)GameSettings.SangoTopHUDHeight;

        // 背景
        sb.Draw(_pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, hudH), new Color(20, 15, 10) * 0.9f);

        // 玩家方
        int pAlive = playerArmy.GetTotalAlive();
        int pMax = playerArmy.GetTotalMax();
        DrawTeamInfo(sb, $"我方 兵:{pAlive}/{pMax}", 20, 8, new Color(120, 180, 255), true);

        // 敌方
        int eAlive = enemyArmy.GetTotalAlive();
        int eMax = enemyArmy.GetTotalMax();
        string eText = $"兵:{eAlive}/{eMax} 敌方";
        Vector2 eSize = _font.MeasureString(eText);
        DrawTeamInfo(sb, eText, GameSettings.ScreenWidth - (int)eSize.X - 20, 8, new Color(255, 130, 130), false);

        // 时间
        int mins = (int)battleTimer / 60;
        int secs = (int)battleTimer % 60;
        string timeText = $"{mins:D2}:{secs:D2}";
        Vector2 timeSize = _font.MeasureString(timeText);
        sb.DrawString(_font, timeText,
            new Vector2(GameSettings.ScreenWidth / 2 - timeSize.X / 2, 8),
            new Color(220, 200, 160));

        // 阶段标识
        string phaseText = GetPhaseText(phase);
        if (!string.IsNullOrEmpty(phaseText))
        {
            Vector2 phaseSize = _smallFont.MeasureString(phaseText);
            sb.DrawString(_smallFont, phaseText,
                new Vector2(GameSettings.ScreenWidth / 2 - phaseSize.X / 2, 28),
                new Color(180, 160, 120));
        }

        // 兵力对比条
        DrawTroopBar(sb, pAlive, pMax, eAlive, eMax, hudH - 10);

        // 执行阶段进度条
        if (phase == SangoBattlePhase.RoundExecution && DrawRoundInfo.duration > 0)
        {
            int timerBarW = 200;
            int timerBarH = 4;
            int timerBarX = GameSettings.ScreenWidth / 2 - timerBarW / 2;
            int timerBarY = hudH - 5;
            float progress = 1f - DrawRoundInfo.timer / DrawRoundInfo.duration;
            sb.Draw(_pixel, new Rectangle(timerBarX, timerBarY, timerBarW, timerBarH), new Color(30, 25, 20));
            sb.Draw(_pixel, new Rectangle(timerBarX, timerBarY, (int)(timerBarW * progress), timerBarH), new Color(220, 180, 80));
        }

        // 底部分隔线
        sb.Draw(_pixel, new Rectangle(0, hudH - 1, GameSettings.ScreenWidth, 1), new Color(100, 80, 55));
    }

    private void DrawTeamInfo(SpriteBatch sb, string text, int x, int y, Color color, bool isPlayer)
    {
        sb.DrawString(_font, text, new Vector2(x, y), color);
    }

    private void DrawTroopBar(SpriteBatch sb, int pAlive, int pMax, int eAlive, int eMax, int barY)
    {
        int totalMax = pMax + eMax;
        if (totalMax <= 0) return;

        int barW = GameSettings.ScreenWidth - 200;
        int barH = 5;
        int barX = 100;

        // 背景条
        sb.Draw(_pixel, new Rectangle(barX, barY, barW, barH), new Color(40, 35, 30));

        // 玩家方从左边填充(蓝)
        int pFillW = (int)(barW * pAlive / (float)totalMax);
        sb.Draw(_pixel, new Rectangle(barX, barY, pFillW, barH), new Color(60, 140, 255));

        // 敌方从右边填充(红)
        int eFillW = (int)(barW * eAlive / (float)totalMax);
        sb.Draw(_pixel, new Rectangle(barX + barW - eFillW, barY, eFillW, barH), new Color(255, 70, 70));
    }

    private string GetPhaseText(SangoBattlePhase phase)
    {
        return phase switch
        {
            SangoBattlePhase.Deploy => "部署中",
            SangoBattlePhase.Countdown => "倒计时",
            SangoBattlePhase.Charge => "冲锋",
            SangoBattlePhase.Melee => "交战中",
            SangoBattlePhase.RoundCommand => $"第{DrawRoundInfo.round}回合 - 指令阶段",
            SangoBattlePhase.RoundExecution => $"第{DrawRoundInfo.round}回合 - 交战中",
            SangoBattlePhase.Duel => "单挑",
            SangoBattlePhase.Result => "战斗结束",
            _ => ""
        };
    }

    // ==================== 底部武将栏 ====================

    public void DrawBottomBar(SpriteBatch sb, ArmyGroup playerArmy, ArmyGroup enemyArmy,
                              SangoBattlePhase phase, InputManager input)
    {
        int barH = (int)GameSettings.SangoBottomBarHeight;
        int barY = GameSettings.ScreenHeight - barH;

        // 背景
        sb.Draw(_pixel, new Rectangle(0, barY, GameSettings.ScreenWidth, barH), new Color(20, 15, 10) * 0.9f);
        sb.Draw(_pixel, new Rectangle(0, barY, GameSettings.ScreenWidth, 1), new Color(100, 80, 55));

        // 左侧: 我方武将头像
        DrawGeneralPortraits(sb, playerArmy, 15, barY + 8, true, input);

        // 右侧: 敌方武将头像
        int enemyStartX = GameSettings.ScreenWidth - 15 - enemyArmy.Units.Count * 65;
        DrawGeneralPortraits(sb, enemyArmy, enemyStartX, barY + 8, false, input);

        // 中间: 战斗速度/倍速标识区域 (按钮由Scene管理)
    }

    private void DrawGeneralPortraits(SpriteBatch sb, ArmyGroup army, int startX, int startY,
                                       bool isPlayer, InputManager input)
    {
        int portraitSize = 55;
        int gap = 10;
        int x = startX;

        for (int i = 0; i < army.Units.Count; i++)
        {
            var unit = army.Units[i];
            var rect = new Rectangle(x, startY, portraitSize, portraitSize);

            // 头像背景
            Color bgColor = unit.IsDefeated ? new Color(40, 30, 25) : new Color(30, 25, 20);
            sb.Draw(_pixel, rect, bgColor);

            // 选中高亮 (仅玩家方)
            if (isPlayer && i == SelectedGeneralIndex)
            {
                DrawBorder(sb, rect, new Color(255, 220, 100), 2);
            }
            else
            {
                Color frameColor = unit.IsDefeated ? new Color(80, 60, 50) : isPlayer ? new Color(80, 100, 140) : new Color(140, 80, 70);
                DrawBorder(sb, rect, frameColor, 1);
            }

            // 败退标记
            if (unit.IsDefeated)
            {
                sb.Draw(_pixel, rect, new Color(0, 0, 0) * 0.5f);
                Vector2 defSize = _smallFont.MeasureString("败");
                sb.DrawString(_smallFont, "败",
                    new Vector2(x + portraitSize / 2 - defSize.X / 2, startY + portraitSize / 2 - defSize.Y / 2),
                    new Color(200, 80, 80));
            }
            else
            {
                // 武将名
                sb.DrawString(_smallFont, unit.General.Name,
                    new Vector2(x + 3, startY + 2), Color.White);

                // 兵种图标文字
                string unitTypeText = GetUnitTypeShort(unit.UnitType);
                Color utColor = GetUnitTypeColor(unit.UnitType);
                Vector2 utSize = _smallFont.MeasureString(unitTypeText);
                sb.DrawString(_smallFont, unitTypeText,
                    new Vector2(x + portraitSize - utSize.X - 2, startY + 2), utColor);
            }

            // 士兵HP条
            int hpBarY = startY + portraitSize + 3;
            sb.Draw(_pixel, new Rectangle(x, hpBarY, portraitSize, 4), new Color(40, 35, 30));
            float hpRatio = unit.AliveSoldierCount / (float)Math.Max(1, unit.InitialSoldierCount);
            Color hpColor = hpRatio > 0.5f ? new Color(80, 200, 80)
                          : hpRatio > 0.25f ? new Color(220, 180, 50)
                          : new Color(220, 50, 50);
            sb.Draw(_pixel, new Rectangle(x, hpBarY, (int)(portraitSize * hpRatio), 4), hpColor);

            // 兵力文字
            string countText = $"{unit.AliveSoldierCount}/{unit.InitialSoldierCount}";
            sb.DrawString(_smallFont, countText,
                new Vector2(x, hpBarY + 6), new Color(180, 170, 150));

            // 士气指示器
            DrawMoraleIndicator(sb, unit, x, hpBarY + 20, portraitSize);

            // 点击检测 (仅玩家方)
            if (isPlayer && !unit.IsDefeated && input.IsMouseClicked() && input.IsMouseInRect(rect))
            {
                SelectedGeneralIndex = i;
                OnGeneralSelected?.Invoke(i);
            }

            x += portraitSize + gap;
        }
    }

    private void DrawMoraleIndicator(SpriteBatch sb, GeneralUnit unit, int x, int y, int width)
    {
        float morale = unit.Morale;
        int barW = width;
        int barH = 3;

        sb.Draw(_pixel, new Rectangle(x, y, barW, barH), new Color(30, 25, 20));
        Color moraleColor = morale > 70 ? new Color(100, 180, 255)
                          : morale > 40 ? new Color(180, 180, 100)
                          : new Color(200, 100, 80);
        int fill = (int)(barW * morale / 100f);
        sb.Draw(_pixel, new Rectangle(x, y, fill, barH), moraleColor);
    }

    private string GetUnitTypeShort(Data.Schemas.UnitType unitType)
    {
        return unitType switch
        {
            Data.Schemas.UnitType.Infantry => "步",
            Data.Schemas.UnitType.Archer => "弓",
            Data.Schemas.UnitType.Cavalry => "骑",
            Data.Schemas.UnitType.Spearman => "枪",
            Data.Schemas.UnitType.Crossbowman => "弩",
            _ => "步"
        };
    }

    private Color GetUnitTypeColor(Data.Schemas.UnitType unitType)
    {
        return unitType switch
        {
            Data.Schemas.UnitType.Infantry => new Color(180, 180, 180),
            Data.Schemas.UnitType.Archer => new Color(120, 200, 120),
            Data.Schemas.UnitType.Cavalry => new Color(200, 160, 100),
            Data.Schemas.UnitType.Spearman => new Color(100, 160, 220),
            Data.Schemas.UnitType.Crossbowman => new Color(160, 120, 200),
            _ => Color.White
        };
    }

    private void DrawBorder(SpriteBatch sb, Rectangle rect, Color color, int thickness)
    {
        sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
