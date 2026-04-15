using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;

namespace CatSanguo.UI;

public class Button
{
    public Rectangle Bounds { get; set; }
    public string Text { get; set; }
    public Color NormalColor { get; set; } = new Color(60, 40, 30);
    public Color HoverColor { get; set; } = new Color(90, 60, 40);
    public Color TextColor { get; set; } = new Color(240, 220, 180);
    public Color BorderColor { get; set; } = new Color(150, 120, 80);
    public bool IsHovered { get; private set; }
    public bool Enabled { get; set; } = true;
    public Action? OnClick { get; set; }

    public Button(string text, Rectangle bounds)
    {
        Text = text;
        Bounds = bounds;
    }

    public void Update(InputManager input)
    {
        if (!Enabled) { IsHovered = false; return; }
        IsHovered = input.IsMouseInRect(Bounds);
        if (IsHovered && input.IsMouseClicked())
        {
            OnClick?.Invoke();
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFontBase font, Texture2D pixel)
    {
        Color bgColor = !Enabled ? new Color(30, 30, 30) : (IsHovered ? HoverColor : NormalColor);
        Color textColor = !Enabled ? Color.Gray * 0.6f : TextColor;
        Color borderColor = !Enabled ? new Color(50, 50, 50) : BorderColor;
        spriteBatch.Draw(pixel, Bounds, bgColor);
        DrawBorder(spriteBatch, pixel, Bounds, borderColor, 2);

        Vector2 textSize = font.MeasureString(Text);
        Vector2 textPos = new Vector2(
            Bounds.X + (Bounds.Width - textSize.X) / 2,
            Bounds.Y + (Bounds.Height - textSize.Y) / 2
        );
        spriteBatch.DrawString(font, Text, textPos, textColor);
    }

    private void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}

public class SkillButton
{
    public Rectangle Bounds { get; set; }
    public string SkillName { get; set; }
    public float CooldownRatio { get; set; }
    public bool IsReady => CooldownRatio <= 0;
    public bool IsHovered { get; private set; }
    public Action? OnClick { get; set; }

    public SkillButton(string name, Rectangle bounds)
    {
        SkillName = name;
        Bounds = bounds;
    }

    public void Update(InputManager input)
    {
        IsHovered = input.IsMouseInRect(Bounds);
        if (IsHovered && input.IsMouseClicked() && IsReady)
        {
            OnClick?.Invoke();
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFontBase font, Texture2D pixel)
    {
        Color bgColor = IsReady
            ? (IsHovered ? new Color(80, 60, 40) : new Color(50, 40, 30))
            : new Color(30, 30, 30);

        spriteBatch.Draw(pixel, Bounds, bgColor);

        if (!IsReady)
        {
            int cdHeight = (int)(Bounds.Height * CooldownRatio);
            spriteBatch.Draw(pixel, new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, cdHeight),
                new Color(0, 0, 0, 128));
        }

        Color borderColor = IsReady ? new Color(200, 180, 100) : new Color(80, 80, 80);
        DrawBorder(spriteBatch, pixel, Bounds, borderColor, 2);

        string display = SkillName.Length > 2 ? SkillName[..2] : SkillName;
        Vector2 ts = font.MeasureString(display);
        Vector2 pos = new Vector2(Bounds.X + (Bounds.Width - ts.X) / 2, Bounds.Y + (Bounds.Height - ts.Y) / 2);
        spriteBatch.DrawString(font, display, pos, IsReady ? new Color(255, 230, 150) : Color.Gray);
    }

    private void DrawBorder(SpriteBatch sb, Texture2D px, Rectangle r, Color c, int t)
    {
        sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, t), c);
        sb.Draw(px, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        sb.Draw(px, new Rectangle(r.X, r.Y, t, r.Height), c);
        sb.Draw(px, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }
}
