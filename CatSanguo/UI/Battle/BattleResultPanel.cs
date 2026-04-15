using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;

namespace CatSanguo.UI.Battle;

public class BattleResultData
{
    public bool IsVictory { get; set; }
    public string PerformanceRating { get; set; } = "C";
    public float BattleTime { get; set; }
    public int PlayerLost { get; set; }
    public int EnemyLost { get; set; }
    public int TotalXp { get; set; }
    public int GoldReward { get; set; }
    public int FoodReward { get; set; }
    public int WoodReward { get; set; }
    public int IronReward { get; set; }
    public int MeritReward { get; set; }
    public List<string> KeyEvents { get; set; } = new();
}

public class BattleResultPanel
{
    private Texture2D _pixel = null!;
    private SpriteFontBase _font = null!;
    private SpriteFontBase _titleFont = null!;
    private SpriteFontBase _smallFont = null!;

    public bool IsActive { get; private set; }
    public BattleResultData? Data { get; private set; }

    private Button _continueButton = null!;
    private float _animTimer;
    private float _fadeAlpha;
    public Action? OnContinue;

    public void Initialize(Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont, SpriteFontBase smallFont)
    {
        _pixel = pixel;
        _font = font;
        _titleFont = titleFont;
        _smallFont = smallFont;

        int sw = GameSettings.ScreenWidth;
        int sh = GameSettings.ScreenHeight;

        _continueButton = new Button("继  续", new Rectangle(sw / 2 - 60, sh / 2 + 170, 120, 40))
        {
            NormalColor = new Color(60, 50, 35),
            HoverColor = new Color(90, 70, 45),
            BorderColor = new Color(150, 120, 80),
            OnClick = () => OnContinue?.Invoke()
        };
    }

    public void Show(BattleResultData data)
    {
        Data = data;
        IsActive = true;
        _animTimer = 0;
        _fadeAlpha = 0;
    }

    public void Hide()
    {
        IsActive = false;
        Data = null;
    }

    public void Update(float deltaTime, InputManager input)
    {
        if (!IsActive || Data == null) return;

        _animTimer += deltaTime;
        _fadeAlpha = MathHelper.Clamp(_animTimer / 0.5f, 0, 1);

        if (_animTimer > 0.5f)
            _continueButton.Update(input);
    }

    public void Draw(SpriteBatch sb)
    {
        if (!IsActive || Data == null) return;

        int sw = GameSettings.ScreenWidth;
        int sh = GameSettings.ScreenHeight;

        // 半透明覆盖
        sb.Draw(_pixel, new Rectangle(0, 0, sw, sh), new Color(0, 0, 0, (int)(180 * _fadeAlpha)));

        if (_fadeAlpha < 0.3f) return;

        float panelAlpha = MathHelper.Clamp((_fadeAlpha - 0.3f) / 0.7f, 0, 1);

        // 面板
        int panelW = 560, panelH = 420;
        int px = sw / 2 - panelW / 2;
        int py = sh / 2 - panelH / 2;
        Rectangle panelRect = new Rectangle(px, py, panelW, panelH);

        Color borderColor = Data.IsVictory ? new Color(200, 170, 80) : new Color(180, 80, 80);
        UIHelper.DrawPanel(sb, _pixel, panelRect,
            new Color(40, 35, 28) * panelAlpha, borderColor * panelAlpha, 3);

        // 标题
        string title = Data.IsVictory ? "战 斗 胜 利" : "战 斗 失 败";
        Color titleColor = Data.IsVictory ? new Color(255, 220, 100) : new Color(220, 100, 100);
        var titleSize = _titleFont.MeasureString(title);
        sb.DrawString(_titleFont, title,
            new Vector2(sw / 2 - titleSize.X / 2, py + 20), titleColor * panelAlpha);

        // 评级
        string rating = Data.PerformanceRating;
        Color ratingColor = rating switch
        {
            "S" => new Color(255, 220, 50),
            "A" => new Color(220, 200, 120),
            "B" => new Color(120, 200, 120),
            _ => new Color(160, 160, 160)
        };

        float ratingScale = _animTimer < 1f ? 1f + (1f - MathHelper.Clamp(_animTimer, 0, 1)) * 0.5f : 1f;
        var ratingSize = _titleFont.MeasureString(rating);
        sb.DrawString(_titleFont, rating,
            new Vector2(sw / 2 + titleSize.X / 2 + 15, py + 20), ratingColor * panelAlpha);

        // 分割线
        sb.Draw(_pixel, new Rectangle(px + 20, py + 75, panelW - 40, 1), new Color(80, 65, 45) * panelAlpha);

        int col1X = px + 30;
        int col2X = px + panelW / 2 + 20;
        int rowY = py + 90;

        // 左列: 战斗统计
        sb.DrawString(_font, "【战斗统计】", new Vector2(col1X, rowY), UIHelper.TitleText * panelAlpha);
        rowY += 30;

        float displayTime = _animTimer > 1f ? Data.BattleTime : Data.BattleTime * MathHelper.Clamp(_animTimer, 0, 1);
        sb.DrawString(_smallFont, $"战斗时间: {(int)displayTime}秒", new Vector2(col1X, rowY), UIHelper.BodyText * panelAlpha);
        rowY += 22;
        sb.DrawString(_smallFont, $"我军损失: {Data.PlayerLost}队", new Vector2(col1X, rowY), UIHelper.BodyText * panelAlpha);
        rowY += 22;
        sb.DrawString(_smallFont, $"敌军歼灭: {Data.EnemyLost}队", new Vector2(col1X, rowY), UIHelper.BodyText * panelAlpha);
        rowY += 22;
        sb.DrawString(_smallFont, $"获得经验: +{Data.TotalXp}", new Vector2(col1X, rowY), new Color(100, 200, 255) * panelAlpha);

        // 右列: 奖励
        rowY = py + 90;
        sb.DrawString(_font, "【战斗奖励】", new Vector2(col2X, rowY), UIHelper.TitleText * panelAlpha);
        rowY += 30;
        sb.DrawString(_smallFont, $"金币: +{Data.GoldReward}", new Vector2(col2X, rowY), new Color(255, 220, 100) * panelAlpha);
        rowY += 22;
        sb.DrawString(_smallFont, $"粮草: +{Data.FoodReward}", new Vector2(col2X, rowY), new Color(100, 220, 100) * panelAlpha);
        rowY += 22;
        sb.DrawString(_smallFont, $"木材: +{Data.WoodReward}", new Vector2(col2X, rowY), new Color(180, 140, 100) * panelAlpha);
        rowY += 22;
        sb.DrawString(_smallFont, $"铁矿: +{Data.IronReward}", new Vector2(col2X, rowY), new Color(150, 150, 180) * panelAlpha);
        rowY += 22;
        sb.DrawString(_smallFont, $"战功: +{Data.MeritReward}", new Vector2(col2X, rowY), new Color(255, 200, 80) * panelAlpha);

        // 分割线
        int evtY = py + 260;
        sb.Draw(_pixel, new Rectangle(px + 20, evtY, panelW - 40, 1), new Color(80, 65, 45) * panelAlpha);
        evtY += 10;

        // 关键事件
        sb.DrawString(_smallFont, "关键事件:", new Vector2(col1X, evtY), UIHelper.SubText * panelAlpha);
        evtY += 20;
        int maxEvents = Math.Min(Data.KeyEvents.Count, 3);
        for (int i = 0; i < maxEvents; i++)
        {
            sb.DrawString(_smallFont, $"· {Data.KeyEvents[i]}",
                new Vector2(col1X, evtY + i * 20), UIHelper.BodyText * panelAlpha);
        }

        // 继续按钮
        if (_animTimer > 0.5f)
            _continueButton.Draw(sb, _font, _pixel);
    }
}
