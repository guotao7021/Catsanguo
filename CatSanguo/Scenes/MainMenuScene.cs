using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.UI;

namespace CatSanguo.Scenes;

public class MainMenuScene : Scene
{
    private Button _startButton;
    private Button _exitButton;
    private Texture2D _pixel;
    private SpriteFontBase _font;
    private SpriteFontBase _titleFont;
    private List<InkParticle> _particles = new();
    private float _titlePulse;

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.TitleFont;

        int btnW = 240, btnH = 55;
        int centerX = GameSettings.ScreenWidth / 2 - btnW / 2;

        _startButton = new Button("开 始 游 戏", new Rectangle(centerX, 310, btnW, btnH));
        _startButton.OnClick = () => Game.SceneManager.ChangeScene(new WorldMapScene());

        _exitButton = new Button("退       出", new Rectangle(centerX, 385, btnW, btnH));
        _exitButton.OnClick = () => Game.Exit();

        // Initialize ink particles
        for (int i = 0; i < 25; i++)
        {
            _particles.Add(new InkParticle());
        }
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _titlePulse += dt;

        _startButton.Update(Input);
        _exitButton.Update(Input);

        foreach (var p in _particles) p.Update(dt);
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(35, 28, 22));

        SpriteBatch.Begin();

        // Draw ink wash background gradient
        DrawBackground();

        // Draw floating ink particles
        foreach (var p in _particles)
        {
            float alpha = p.Alpha * 0.4f;
            SpriteBatch.Draw(_pixel, new Rectangle((int)p.Position.X, (int)p.Position.Y, (int)p.Size, (int)p.Size),
                new Color(80, 65, 50) * alpha);
        }

        // Title
        string title = "猫 三 国";
        Vector2 titleSize = _titleFont.MeasureString(title);
        float scale = 1.0f + MathF.Sin(_titlePulse * 1.5f) * 0.02f;
        Vector2 titlePos = new Vector2(GameSettings.ScreenWidth / 2 - titleSize.X * scale / 2, 160);
        // Shadow
        SpriteBatch.DrawString(_titleFont, title, titlePos + new Vector2(3, 3), new Color(20, 15, 10) * 0.6f, 0f, Vector2.Zero, new Vector2(scale), 0f);
        SpriteBatch.DrawString(_titleFont, title, titlePos, new Color(220, 190, 130), 0f, Vector2.Zero, new Vector2(scale), 0f);

        // Subtitle
        string subtitle = "策略 · 战斗 · 猫咪三国";
        Vector2 subSize = _font.MeasureString(subtitle);
        SpriteBatch.DrawString(_font, subtitle,
            new Vector2(GameSettings.ScreenWidth / 2 - subSize.X / 2, 280),
            new Color(160, 140, 100));

        // Buttons
        _startButton.Draw(SpriteBatch, _font, _pixel);
        _exitButton.Draw(SpriteBatch, _font, _pixel);

        // Version
        SpriteBatch.DrawString(_font, "v0.1",
            new Vector2(GameSettings.ScreenWidth - 80, GameSettings.ScreenHeight - 30),
            new Color(80, 70, 50));

        SpriteBatch.End();
    }

    private void DrawBackground()
    {
        // Gradient layers to simulate ink wash
        for (int y = 0; y < GameSettings.ScreenHeight; y += 4)
        {
            float t = (float)y / GameSettings.ScreenHeight;
            byte r = (byte)MathHelper.Lerp(45, 30, t);
            byte g = (byte)MathHelper.Lerp(38, 24, t);
            byte b = (byte)MathHelper.Lerp(30, 18, t);
            SpriteBatch.Draw(_pixel, new Rectangle(0, y, GameSettings.ScreenWidth, 4), new Color(r, g, b));
        }

        // Horizontal decorative lines
        int lineY = 320;
        SpriteBatch.Draw(_pixel, new Rectangle(200, lineY, GameSettings.ScreenWidth - 400, 1), new Color(100, 85, 60) * 0.5f);
        SpriteBatch.Draw(_pixel, new Rectangle(200, lineY + 3, GameSettings.ScreenWidth - 400, 1), new Color(100, 85, 60) * 0.3f);
    }

    private class InkParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public float Alpha;
        private static readonly Random _rng = new();

        public InkParticle()
        {
            Reset();
            Position.Y = _rng.Next(0, GameSettings.ScreenHeight);
        }

        public void Reset()
        {
            Position = new Vector2(_rng.Next(0, GameSettings.ScreenWidth), -10);
            Velocity = new Vector2(_rng.Next(-20, 20) * 0.1f, _rng.Next(8, 25));
            Size = _rng.Next(2, 6);
            Alpha = (float)_rng.NextDouble() * 0.5f + 0.3f;
        }

        public void Update(float dt)
        {
            Position += Velocity * dt;
            if (Position.Y > GameSettings.ScreenHeight + 10) Reset();
        }
    }
}
