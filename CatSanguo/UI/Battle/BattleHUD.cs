using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Battle;

namespace CatSanguo.UI.Battle;

public class BattleHUD
{
    private Texture2D _pixel = null!;
    private SpriteFontBase _font = null!;
    private SpriteFontBase _smallFont = null!;

    // 状态
    public string StageName { get; set; } = "";
    public float BattleTime { get; set; }
    public float SpeedMultiplier { get; set; } = 2f;
    public bool IsPaused { get; set; }
    public bool IsAutoBattle { get; set; } = true;

    // 聚合HP
    public float PlayerHPRatio { get; set; }
    public float EnemyHPRatio { get; set; }
    public int PlayerAlive { get; set; }
    public int PlayerTotal { get; set; }
    public int EnemyAlive { get; set; }
    public int EnemyTotal { get; set; }

    // 按钮
    private Button _speedButton = null!;
    private Button _pauseButton = null!;
    private Button? _autoButton;
    private Button? _skipButton;

    // 配置
    public bool ShowAutoButton { get; set; }
    public bool ShowSkipButton { get; set; }
    public float[] SpeedOptions { get; set; } = { 2f, 4f, 8f };

    // 回调
    public Action? OnSpeedToggled;
    public Action? OnPauseToggled;
    public Action? OnAutoToggled;
    public Action? OnSkipClicked;

    public void Initialize(Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont)
    {
        _pixel = pixel;
        _font = font;
        _smallFont = smallFont;

        int sw = GameSettings.ScreenWidth;

        _speedButton = new Button($"{SpeedMultiplier:0}x", new Rectangle(sw / 2 + 50, 12, 55, 28))
        {
            NormalColor = new Color(50, 40, 30),
            HoverColor = new Color(80, 60, 40),
            BorderColor = new Color(120, 100, 70),
            OnClick = () => OnSpeedToggled?.Invoke()
        };

        _pauseButton = new Button("||", new Rectangle(sw / 2 + 112, 12, 40, 28))
        {
            NormalColor = new Color(50, 40, 30),
            HoverColor = new Color(80, 60, 40),
            BorderColor = new Color(120, 100, 70),
            OnClick = () => OnPauseToggled?.Invoke()
        };

        if (ShowAutoButton)
        {
            _autoButton = new Button("AI", new Rectangle(sw / 2 + 160, 12, 40, 28))
            {
                NormalColor = new Color(50, 40, 30),
                HoverColor = new Color(80, 60, 40),
                BorderColor = new Color(120, 100, 70),
                OnClick = () => OnAutoToggled?.Invoke()
            };
        }

        if (ShowSkipButton)
        {
            _skipButton = new Button("跳过", new Rectangle(sw - 90, 12, 70, 28))
            {
                NormalColor = new Color(60, 35, 30),
                HoverColor = new Color(100, 50, 40),
                BorderColor = new Color(150, 100, 60),
                OnClick = () => OnSkipClicked?.Invoke()
            };
        }
    }

    public void UpdateData(List<Squad> playerSquads, List<Squad> enemySquads, float battleTime, float speed, bool paused)
    {
        BattleTime = battleTime;
        SpeedMultiplier = speed;
        IsPaused = paused;

        float pHP = playerSquads.Sum(s => Math.Max(0, s.HP));
        float pMax = playerSquads.Sum(s => s.MaxHP);
        float eHP = enemySquads.Sum(s => Math.Max(0, s.HP));
        float eMax = enemySquads.Sum(s => s.MaxHP);

        PlayerHPRatio = pMax > 0 ? pHP / pMax : 0;
        EnemyHPRatio = eMax > 0 ? eHP / eMax : 0;
        PlayerAlive = playerSquads.Count(s => s.IsActive);
        PlayerTotal = playerSquads.Count;
        EnemyAlive = enemySquads.Count(s => s.IsActive);
        EnemyTotal = enemySquads.Count;

        _speedButton.Text = $"{SpeedMultiplier:0}x";
        _pauseButton.Text = IsPaused ? ">" : "||";
    }

    public void Update(InputManager input)
    {
        _speedButton.Update(input);
        _pauseButton.Update(input);
        _autoButton?.Update(input);
        _skipButton?.Update(input);
    }

    public void Draw(SpriteBatch sb)
    {
        int sw = GameSettings.ScreenWidth;

        // 顶部背景栏
        sb.Draw(_pixel, new Rectangle(0, 0, sw, 50), new Color(25, 20, 14, 230));
        sb.Draw(_pixel, new Rectangle(0, 50, sw, 1), new Color(80, 65, 45));

        // 城池/关卡名
        sb.DrawString(_font, StageName, new Vector2(15, 12), UIHelper.TitleText);

        // 战斗时间
        int mins = (int)(BattleTime / 60);
        int secs = (int)(BattleTime % 60);
        sb.DrawString(_font, $"{mins:00}:{secs:00}", new Vector2(sw / 2 - 25, 12), UIHelper.BodyText);

        // 玩家HP条 (左侧)
        int hpBarW = 180, hpBarH = 10;
        int pBarX = 15, pBarY = 36;
        UIHelper.DrawBarWithHighlight(sb, _pixel, new Rectangle(pBarX, pBarY, hpBarW, hpBarH),
            PlayerHPRatio, UIHelper.PlayerColor, new Color(20, 15, 10));
        sb.DrawString(_smallFont, $"我军 {PlayerAlive}/{PlayerTotal} {(int)(PlayerHPRatio * 100)}%",
            new Vector2(pBarX + hpBarW + 5, pBarY - 2), new Color(100, 160, 230));

        // 敌军HP条 (右侧)
        int eBarX = sw - 15 - hpBarW;
        UIHelper.DrawBarWithHighlight(sb, _pixel, new Rectangle(eBarX, pBarY, hpBarW, hpBarH),
            EnemyHPRatio, UIHelper.EnemyColor, new Color(20, 15, 10));
        sb.DrawString(_smallFont, $"敌军 {EnemyAlive}/{EnemyTotal} {(int)(EnemyHPRatio * 100)}%",
            new Vector2(eBarX - 140, pBarY - 2), new Color(230, 100, 100));

        // 按钮
        _speedButton.Draw(sb, _smallFont, _pixel);
        _pauseButton.Draw(sb, _smallFont, _pixel);
        _autoButton?.Draw(sb, _smallFont, _pixel);
        _skipButton?.Draw(sb, _smallFont, _pixel);

        // 暂停指示
        if (IsPaused)
        {
            var pauseText = "已暂停";
            var size = _font.MeasureString(pauseText);
            sb.DrawString(_font, pauseText, new Vector2(sw / 2 - size.X / 2, 55), new Color(255, 200, 100));
        }
    }
}
