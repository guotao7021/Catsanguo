using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Core.Animation;

namespace CatSanguo.Battle.Sango;

/// <summary>
/// 武将单挑系统 - 三国群英传2风格
/// 两武将在画面中央一对一回合制对决
/// </summary>
public class DuelSystem
{
    public enum DuelState { Intro, Fighting, Clash, Result, Finished }

    // 参与者
    public GeneralUnit? Challenger { get; private set; }
    public GeneralUnit? Defender { get; private set; }
    public DuelState State { get; private set; } = DuelState.Finished;
    public bool IsActive => State != DuelState.Finished;

    // 单挑双方HP
    private float _challengerHP;
    private float _challengerMaxHP;
    private float _defenderHP;
    private float _defenderMaxHP;

    // 回合系统
    private bool _isAttackerTurn; // true=挑战者攻击, false=防守者攻击
    private float _turnTimer;
    private float _introTimer;
    private float _resultTimer;

    // 战斗动画
    private float _clashTimer;
    private string _lastActionText = "";
    private float _actionTextTimer;
    private float _damageFlashTimer;
    private bool _damageFlashIsChallenger;

    // 回合间隔
    private const float TurnInterval = 1.5f;
    private const float IntroDuration = 2.0f;
    private const float ClashDuration = 0.6f;
    private const float ResultDuration = 3.0f;

    // 结果
    public GeneralUnit? Winner { get; private set; }
    public GeneralUnit? Loser { get; private set; }

    // 回调
    public Action<GeneralUnit, GeneralUnit>? OnDuelComplete; // winner, loser

    // 位置
    private Vector2 _challengerPos;
    private Vector2 _defenderPos;
    private Vector2 _centerPos;

    public void StartDuel(GeneralUnit challenger, GeneralUnit defender)
    {
        Challenger = challenger;
        Defender = defender;
        State = DuelState.Intro;

        // 武将单挑HP = 武力*10 + 统率*5 + 500
        _challengerMaxHP = challenger.GeneralMaxHP;
        _challengerHP = _challengerMaxHP;
        _defenderMaxHP = defender.GeneralMaxHP;
        _defenderHP = _defenderMaxHP;

        _isAttackerTurn = true;
        _turnTimer = 0;
        _introTimer = IntroDuration;
        _resultTimer = ResultDuration;

        // 位置设置
        _centerPos = new Vector2(GameSettings.ScreenWidth / 2f, GameSettings.ScreenHeight / 2f - 30);
        _challengerPos = _centerPos + new Vector2(-150, 0);
        _defenderPos = _centerPos + new Vector2(150, 0);

        Winner = null;
        Loser = null;
        _lastActionText = "";
    }

    public void Update(float dt)
    {
        if (!IsActive) return;

        switch (State)
        {
            case DuelState.Intro:
                _introTimer -= dt;
                if (_introTimer <= 0)
                {
                    State = DuelState.Fighting;
                    _turnTimer = TurnInterval * 0.5f; // 稍短的首次等待
                }
                break;

            case DuelState.Fighting:
                UpdateFighting(dt);
                break;

            case DuelState.Clash:
                _clashTimer -= dt;
                if (_clashTimer <= 0)
                    State = DuelState.Fighting;
                break;

            case DuelState.Result:
                _resultTimer -= dt;
                if (_resultTimer <= 0)
                {
                    ApplyDuelResult();
                    State = DuelState.Finished;
                }
                break;
        }

        _actionTextTimer -= dt;
        _damageFlashTimer -= dt;

        // 更新动画
        Challenger?.GeneralAnimator?.Update(dt);
        Defender?.GeneralAnimator?.Update(dt);
    }

    private void UpdateFighting(float dt)
    {
        _turnTimer += dt;
        if (_turnTimer >= TurnInterval)
        {
            _turnTimer -= TurnInterval;
            PerformTurn();
        }
    }

    private void PerformTurn()
    {
        if (Challenger == null || Defender == null) return;

        GeneralUnit attacker = _isAttackerTurn ? Challenger : Defender;
        GeneralUnit defender = _isAttackerTurn ? Defender : Challenger;

        // 基础伤害 = 武力差 * 随机系数
        float atkStr = attacker.General.Strength;
        float defStr = defender.General.Strength;

        // 伤害 = (攻方武力 * 2 - 防方武力) * 0.8~1.2 随机 + 固定值
        float baseDmg = Math.Max(5, (atkStr * 2 - defStr) * (0.8f + (float)Random.Shared.NextDouble() * 0.4f));
        baseDmg += 10 + (float)Random.Shared.NextDouble() * 20; // 保底伤害

        // 暴击 (武力高者概率更大)
        bool isCrit = Random.Shared.NextDouble() < (atkStr / (atkStr + defStr + 50)) * 0.3;
        if (isCrit)
        {
            baseDmg *= 1.8f;
            _lastActionText = $"{attacker.General.Name} 暴击! -{(int)baseDmg}";
        }
        else
        {
            _lastActionText = $"{attacker.General.Name} 攻击 -{(int)baseDmg}";
        }
        _actionTextTimer = 1.2f;

        // 应用伤害
        if (_isAttackerTurn)
        {
            _defenderHP -= baseDmg;
            _damageFlashIsChallenger = false;
        }
        else
        {
            _challengerHP -= baseDmg;
            _damageFlashIsChallenger = true;
        }
        _damageFlashTimer = 0.2f;

        // 触发碰撞动画
        State = DuelState.Clash;
        _clashTimer = ClashDuration;

        // 检查胜负
        if (_challengerHP <= 0)
        {
            _challengerHP = 0;
            Winner = Defender;
            Loser = Challenger;
            _lastActionText = $"{Defender!.General.Name} 单挑获胜!";
            _actionTextTimer = ResultDuration;
            State = DuelState.Result;
        }
        else if (_defenderHP <= 0)
        {
            _defenderHP = 0;
            Winner = Challenger;
            Loser = Defender;
            _lastActionText = $"{Challenger!.General.Name} 单挑获胜!";
            _actionTextTimer = ResultDuration;
            State = DuelState.Result;
        }

        _isAttackerTurn = !_isAttackerTurn;
    }

    private void ApplyDuelResult()
    {
        if (Winner == null || Loser == null) return;

        // 胜者士气大幅提升
        Winner.Morale = Math.Min(100, Winner.Morale + 30);

        // 败者部队溃散 - 士气清零，进入撤退状态
        Loser.Morale = 0;
        Loser.ForceRetreat();

        OnDuelComplete?.Invoke(Winner, Loser);
    }

    // ==================== Draw ====================

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont, SpriteFontBase smallFont)
    {
        if (!IsActive) return;

        // 全屏半透明遮罩
        sb.Draw(pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight),
            new Color(0, 0, 0) * 0.7f);

        // 单挑标题
        if (State == DuelState.Intro)
        {
            DrawIntro(sb, pixel, titleFont, font);
            return;
        }

        // 决斗区域背景
        int arenaW = 500, arenaH = 300;
        int arenaX = GameSettings.ScreenWidth / 2 - arenaW / 2;
        int arenaY = GameSettings.ScreenHeight / 2 - arenaH / 2 - 20;
        sb.Draw(pixel, new Rectangle(arenaX, arenaY, arenaW, arenaH), new Color(30, 25, 20) * 0.8f);
        DrawBorder(sb, pixel, new Rectangle(arenaX, arenaY, arenaW, arenaH), new Color(180, 150, 80), 2);

        // "单挑" 标题
        string title = "— 武将单挑 —";
        Vector2 titleSize = font.MeasureString(title);
        sb.DrawString(font, title,
            new Vector2(GameSettings.ScreenWidth / 2 - titleSize.X / 2, arenaY + 10),
            new Color(255, 220, 120));

        // 绘制双方武将
        DrawDuelGeneral(sb, pixel, font, smallFont, Challenger!, _challengerPos, _challengerHP, _challengerMaxHP,
            true, _damageFlashTimer > 0 && _damageFlashIsChallenger);
        DrawDuelGeneral(sb, pixel, font, smallFont, Defender!, _defenderPos, _defenderHP, _defenderMaxHP,
            false, _damageFlashTimer > 0 && !_damageFlashIsChallenger);

        // VS
        string vs = "VS";
        Vector2 vsSize = titleFont.MeasureString(vs);
        sb.DrawString(titleFont, vs,
            new Vector2(GameSettings.ScreenWidth / 2 - vsSize.X / 2, _centerPos.Y - vsSize.Y / 2),
            new Color(255, 200, 80));

        // 碰撞特效
        if (State == DuelState.Clash)
        {
            float flash = _clashTimer / ClashDuration;
            int sparkSize = (int)(20 * flash);
            sb.Draw(pixel, new Rectangle(
                (int)_centerPos.X - sparkSize / 2, (int)_centerPos.Y - sparkSize / 2,
                sparkSize, sparkSize), new Color(255, 230, 100) * flash);
        }

        // 行动文字
        if (_actionTextTimer > 0 && !string.IsNullOrEmpty(_lastActionText))
        {
            float alpha = Math.Min(1f, _actionTextTimer);
            Vector2 actSize = font.MeasureString(_lastActionText);
            sb.DrawString(font, _lastActionText,
                new Vector2(GameSettings.ScreenWidth / 2 - actSize.X / 2, arenaY + arenaH - 40),
                new Color(255, 240, 180) * alpha);
        }

        // 结果
        if (State == DuelState.Result && Winner != null)
        {
            string resultText = $"{Winner.General.Name} 获胜!";
            Vector2 rSize = titleFont.MeasureString(resultText);
            sb.DrawString(titleFont, resultText,
                new Vector2(GameSettings.ScreenWidth / 2 - rSize.X / 2, arenaY + arenaH + 15),
                new Color(255, 220, 80));
        }
    }

    private void DrawIntro(SpriteBatch sb, Texture2D pixel, SpriteFontBase titleFont, SpriteFontBase font)
    {
        if (Challenger == null || Defender == null) return;

        float t = 1f - _introTimer / IntroDuration;

        // 大标题 "单挑"
        string title = "单  挑";
        Vector2 titleSize = titleFont.MeasureString(title);
        float scale = 0.5f + t * 0.5f; // 放大效果
        sb.DrawString(titleFont, title,
            new Vector2(GameSettings.ScreenWidth / 2 - titleSize.X / 2,
                        GameSettings.ScreenHeight / 2 - 80),
            new Color(255, 220, 80) * Math.Min(1f, t * 2));

        // 双方名字从两侧滑入
        float slideX = (1f - t) * 200;
        string cName = Challenger.General.Name;
        string dName = Defender.General.Name;
        Vector2 cSize = font.MeasureString(cName);
        Vector2 dSize = font.MeasureString(dName);

        sb.DrawString(font, cName,
            new Vector2(GameSettings.ScreenWidth / 2 - 150 - cSize.X / 2 - slideX,
                        GameSettings.ScreenHeight / 2 + 10),
            new Color(120, 180, 255));

        sb.DrawString(font, "VS", new Vector2(GameSettings.ScreenWidth / 2 - 12, GameSettings.ScreenHeight / 2 + 10),
            new Color(200, 180, 120));

        sb.DrawString(font, dName,
            new Vector2(GameSettings.ScreenWidth / 2 + 150 - dSize.X / 2 + slideX,
                        GameSettings.ScreenHeight / 2 + 10),
            new Color(255, 130, 130));
    }

    private void DrawDuelGeneral(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont,
        GeneralUnit unit, Vector2 pos, float hp, float maxHP, bool isLeft, bool isFlashing)
    {
        // 武将精灵
        var effects = isLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        Color tint = isFlashing ? Color.Red : Color.White;

        if (unit.GeneralAnimator != null && unit.GeneralAnimator.HasTexture)
        {
            unit.GeneralAnimator.Draw(sb, pos, tint, effects, scale: 1.5f);
        }
        else
        {
            Color color = isLeft ? new Color(40, 100, 200) : new Color(200, 40, 40);
            if (isFlashing) color = Color.Red;
            sb.Draw(pixel, new Rectangle((int)pos.X - 36, (int)pos.Y - 36, 72, 72), color);
        }

        // 名字
        string name = unit.General.Name;
        Vector2 nameSize = font.MeasureString(name);
        Color nameColor = isLeft ? new Color(120, 180, 255) : new Color(255, 130, 130);
        sb.DrawString(font, name, new Vector2(pos.X - nameSize.X / 2, pos.Y - 80), nameColor);

        // HP条
        int barW = 100, barH = 8;
        int barX = (int)pos.X - barW / 2;
        int barY = (int)pos.Y + 30;
        sb.Draw(pixel, new Rectangle(barX, barY, barW, barH), new Color(40, 35, 30));
        float hpRatio = maxHP > 0 ? hp / maxHP : 0;
        Color hpColor = hpRatio > 0.5f ? new Color(80, 200, 80)
                      : hpRatio > 0.25f ? new Color(220, 180, 50)
                      : new Color(220, 50, 50);
        sb.Draw(pixel, new Rectangle(barX, barY, (int)(barW * hpRatio), barH), hpColor);
        DrawBorder(sb, pixel, new Rectangle(barX, barY, barW, barH), new Color(120, 100, 70), 1);

        // HP数值
        string hpText = $"{(int)hp}/{(int)maxHP}";
        Vector2 hpSize = smallFont.MeasureString(hpText);
        sb.DrawString(smallFont, hpText, new Vector2(pos.X - hpSize.X / 2, barY + barH + 3), new Color(200, 190, 170));

        // 武力值
        string strText = $"武力:{(int)unit.General.Strength}";
        Vector2 strSize = smallFont.MeasureString(strText);
        sb.DrawString(smallFont, strText, new Vector2(pos.X - strSize.X / 2, barY + barH + 18), new Color(180, 160, 130));
    }

    private void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
