using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;

namespace CatSanguo.UI.Battle;

public static class UIHelper
{
    // ===== 配色常量 =====
    public static readonly Color PanelBg = new Color(40, 35, 28);
    public static readonly Color PanelBorder = new Color(80, 65, 45);
    public static readonly Color TitleText = new Color(240, 200, 140);
    public static readonly Color BodyText = new Color(220, 190, 130);
    public static readonly Color SubText = new Color(160, 150, 120);
    public static readonly Color PlayerColor = new Color(60, 100, 180);
    public static readonly Color EnemyColor = new Color(180, 60, 60);
    public static readonly Color HighlightColor = new Color(220, 180, 80);
    public static readonly Color BuffColor = new Color(60, 130, 220);
    public static readonly Color DebuffColor = new Color(220, 60, 60);
    public static readonly Color ControlColor = new Color(180, 80, 200);

    public static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness = 2)
    {
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    public static void DrawPanel(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color bgColor, Color borderColor, int borderThickness = 2)
    {
        sb.Draw(pixel, rect, bgColor);
        DrawBorder(sb, pixel, rect, borderColor, borderThickness);
    }

    public static void DrawBar(SpriteBatch sb, Texture2D pixel, Rectangle rect, float ratio, Color fillColor, Color bgColor)
    {
        ratio = MathHelper.Clamp(ratio, 0f, 1f);
        sb.Draw(pixel, rect, bgColor);
        int fillW = (int)(rect.Width * ratio);
        if (fillW > 0)
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, fillW, rect.Height), fillColor);
    }

    public static void DrawBarWithHighlight(SpriteBatch sb, Texture2D pixel, Rectangle rect, float ratio, Color fillColor, Color bgColor)
    {
        ratio = MathHelper.Clamp(ratio, 0f, 1f);
        sb.Draw(pixel, rect, bgColor);
        int fillW = (int)(rect.Width * ratio);
        if (fillW > 0)
        {
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, fillW, rect.Height), fillColor);
            // 顶部高光
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, fillW, Math.Max(1, rect.Height / 4)),
                fillColor * 1.3f);
        }
    }

    public static Color GetHPColor(float ratio)
    {
        if (ratio > 0.6f) return new Color(50, 170, 50);
        if (ratio > 0.3f) return new Color(200, 170, 30);
        return new Color(190, 40, 40);
    }

    public static Color GetMoraleColor(float ratio)
    {
        if (ratio > 0.7f) return new Color(60, 110, 200);
        if (ratio > 0.4f) return new Color(200, 150, 40);
        return new Color(170, 50, 50);
    }

    public static Vector2 CenterText(SpriteFontBase font, string text, Rectangle bounds)
    {
        var size = font.MeasureString(text);
        return new Vector2(
            bounds.X + (bounds.Width - size.X) / 2,
            bounds.Y + (bounds.Height - size.Y) / 2
        );
    }

    public static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, Color color, int thickness = 1)
    {
        var diff = end - start;
        float angle = (float)Math.Atan2(diff.Y, diff.X);
        float length = diff.Length();
        sb.Draw(pixel, start, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0);
    }
}
