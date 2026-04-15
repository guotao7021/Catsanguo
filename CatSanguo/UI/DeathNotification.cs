using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;

namespace CatSanguo.UI;

public class DeathNotification
{
    public string GeneralName { get; }
    public bool IsPlayer { get; }
    public float Life { get; private set; }
    public float MaxLife { get; }

    // Animation
    private float _slideProgress;
    private float _alpha;

    public bool IsExpired => Life <= 0;

    public DeathNotification(string generalName, bool isPlayer, float duration = 2.5f)
    {
        GeneralName = generalName;
        IsPlayer = isPlayer;
        Life = duration;
        MaxLife = duration;
    }

    public void Update(float dt)
    {
        Life -= dt;

        // Slide in (first 0.3s)
        float elapsed = MaxLife - Life;
        if (elapsed < 0.3f)
            _slideProgress = elapsed / 0.3f;
        else
            _slideProgress = 1f;

        // Fade out (last 0.5s)
        if (Life < 0.5f)
            _alpha = Math.Max(0, Life / 0.5f);
        else
            _alpha = Math.Min(1f, elapsed / 0.2f);
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFontBase notifyFont, SpriteFontBase smallFont, Texture2D pixel, float yOffset)
    {
        string text = $"{GeneralName} 阵亡!";
        string subText = IsPlayer ? "我军折损大将!" : "敌将已被斩杀!";

        Vector2 textSize = notifyFont.MeasureString(text);
        Vector2 subSize = smallFont.MeasureString(subText);

        float panelWidth = Math.Max(textSize.X, subSize.X) + 80;
        float panelHeight = 70;

        // Slide from right
        float targetX = GameSettings.ScreenWidth / 2 - panelWidth / 2;
        float startX = GameSettings.ScreenWidth + 20;
        float currentX = MathHelper.Lerp(startX, targetX, EaseOutBack(_slideProgress));
        float currentY = 80 + yOffset;

        Rectangle panelRect = new Rectangle(
            (int)currentX, (int)currentY,
            (int)panelWidth, (int)panelHeight);

        // Panel background
        Color bgColor = IsPlayer
            ? new Color(80, 30, 30) * _alpha
            : new Color(30, 60, 80) * _alpha;
        spriteBatch.Draw(pixel, panelRect, bgColor);

        // Border
        Color borderColor = IsPlayer
            ? new Color(200, 80, 80) * _alpha
            : new Color(80, 180, 220) * _alpha;
        DrawBorder(spriteBatch, pixel, panelRect, borderColor, 2);

        // Accent line on the left
        Color accentColor = IsPlayer
            ? new Color(220, 60, 60) * _alpha
            : new Color(60, 200, 255) * _alpha;
        spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, 4, panelRect.Height), accentColor);

        // Main text
        Color textColor = IsPlayer
            ? new Color(255, 120, 120) * _alpha
            : new Color(255, 220, 100) * _alpha;
        float textX = currentX + (panelWidth - textSize.X) / 2;
        spriteBatch.DrawString(notifyFont, text, new Vector2(textX, currentY + 5), textColor);

        // Sub text
        Color subColor = IsPlayer
            ? new Color(200, 150, 150) * _alpha
            : new Color(200, 200, 180) * _alpha;
        float subX = currentX + (panelWidth - subSize.X) / 2;
        spriteBatch.DrawString(smallFont, subText, new Vector2(subX, currentY + 42), subColor);
    }

    private static float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D px, Rectangle r, Color c, int t)
    {
        sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, t), c);
        sb.Draw(px, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        sb.Draw(px, new Rectangle(r.X, r.Y, t, r.Height), c);
        sb.Draw(px, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }
}

public class DeathNotificationManager
{
    private readonly List<DeathNotification> _notifications = new();
    private const int MaxVisible = 3;

    public void AddNotification(string generalName, bool isPlayer)
    {
        _notifications.Add(new DeathNotification(generalName, isPlayer));
        // Keep only a reasonable amount
        if (_notifications.Count > 10)
            _notifications.RemoveAt(0);
    }

    public void Update(float dt)
    {
        for (int i = _notifications.Count - 1; i >= 0; i--)
        {
            _notifications[i].Update(dt);
            if (_notifications[i].IsExpired)
                _notifications.RemoveAt(i);
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFontBase notifyFont, SpriteFontBase smallFont, Texture2D pixel)
    {
        int count = Math.Min(_notifications.Count, MaxVisible);
        for (int i = 0; i < count; i++)
        {
            _notifications[_notifications.Count - count + i].Draw(
                spriteBatch, notifyFont, smallFont, pixel, i * 80);
        }
    }
}
