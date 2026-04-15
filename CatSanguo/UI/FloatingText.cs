using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;

namespace CatSanguo.UI;

public class FloatingText
{
    public string Text { get; set; }
    public Vector2 Position { get; set; }
    public Color TextColor { get; set; }
    public float Life { get; set; }
    public float MaxLife { get; set; }
    public float Scale { get; set; } = 1f;

    public FloatingText(string text, Vector2 position, Color color, float duration = 0.8f)
    {
        Text = text;
        Position = position;
        TextColor = color;
        Life = duration;
        MaxLife = duration;
    }

    public bool IsExpired => Life <= 0;

    public void Update(float deltaTime)
    {
        Life -= deltaTime;
        Position -= new Vector2(0, 40 * deltaTime);
    }

    public float Alpha => Math.Clamp(Life / MaxLife, 0, 1);
}

public class FloatingTextManager
{
    private readonly List<FloatingText> _texts = new();

    public void AddText(string text, Vector2 position, Color color)
    {
        _texts.Add(new FloatingText(text, position + new Vector2(Random.Shared.Next(-10, 10), Random.Shared.Next(-10, 10)), color));
    }

    public void Update(float deltaTime)
    {
        for (int i = _texts.Count - 1; i >= 0; i--)
        {
            _texts[i].Update(deltaTime);
            if (_texts[i].IsExpired)
                _texts.RemoveAt(i);
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFontBase font)
    {
        foreach (var text in _texts)
        {
            Color c = text.TextColor * text.Alpha;
            spriteBatch.DrawString(font, text.Text, text.Position, c, 0f, Vector2.Zero, new Vector2(text.Scale), 0f);
        }
    }
}
