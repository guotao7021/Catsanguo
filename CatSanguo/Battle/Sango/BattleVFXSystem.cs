using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;

namespace CatSanguo.Battle.Sango;

/// <summary>
/// 战场视觉效果系统 - 飘字/粒子/全屏闪光
/// </summary>
public class BattleVFXSystem
{
    // 飘字
    private readonly List<FloatingText> _floatingTexts = new();

    // 粒子
    private readonly List<Particle> _particles = new();

    // 全屏闪光
    private float _screenFlashTimer;
    private Color _screenFlashColor = Color.White;
    private float _screenFlashDuration;

    public void Update(float dt)
    {
        // 飘字
        for (int i = _floatingTexts.Count - 1; i >= 0; i--)
        {
            _floatingTexts[i].Timer -= dt;
            _floatingTexts[i].Position += _floatingTexts[i].Velocity * dt;
            _floatingTexts[i].Velocity *= 0.95f; // 减速
            if (_floatingTexts[i].Timer <= 0)
                _floatingTexts.RemoveAt(i);
        }

        // 粒子
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Timer -= dt;
            p.Position += p.Velocity * dt;
            p.Velocity += p.Gravity * dt;
            p.Alpha = Math.Max(0, p.Timer / p.MaxTimer);
            p.Size = p.StartSize * p.Alpha;
            if (p.Timer <= 0)
                _particles.RemoveAt(i);
        }

        // 全屏闪光
        if (_screenFlashTimer > 0)
            _screenFlashTimer -= dt;
    }

    // ==================== 飘字 ====================

    public void AddDamageText(Vector2 worldPos, int damage, bool isCrit = false)
    {
        _floatingTexts.Add(new FloatingText
        {
            Text = $"-{damage}",
            Position = worldPos + new Vector2(-10 + (float)Random.Shared.NextDouble() * 20, 0),
            Velocity = new Vector2(0, -40),
            Timer = isCrit ? 1.5f : 1.0f,
            MaxTimer = isCrit ? 1.5f : 1.0f,
            Color = isCrit ? new Color(255, 80, 50) : new Color(255, 220, 100),
            Scale = isCrit ? 1.3f : 1.0f
        });
    }

    public void AddHealText(Vector2 worldPos, int amount)
    {
        _floatingTexts.Add(new FloatingText
        {
            Text = $"+{amount}",
            Position = worldPos,
            Velocity = new Vector2(0, -35),
            Timer = 1.0f,
            MaxTimer = 1.0f,
            Color = new Color(100, 255, 100),
            Scale = 1.0f
        });
    }

    public void AddStatusText(Vector2 worldPos, string text, Color color)
    {
        _floatingTexts.Add(new FloatingText
        {
            Text = text,
            Position = worldPos,
            Velocity = new Vector2(0, -30),
            Timer = 1.5f,
            MaxTimer = 1.5f,
            Color = color,
            Scale = 1.0f
        });
    }

    // ==================== 粒子 ====================

    public void SpawnExplosion(Vector2 worldPos, Color color, int count = 15)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = 30 + (float)Random.Shared.NextDouble() * 80;
            _particles.Add(new Particle
            {
                Position = worldPos,
                Velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed),
                Gravity = new Vector2(0, 40),
                Color = color,
                Timer = 0.5f + (float)Random.Shared.NextDouble() * 0.5f,
                MaxTimer = 1f,
                StartSize = 3 + (float)Random.Shared.NextDouble() * 4,
                Size = 4,
                Alpha = 1f
            });
        }
    }

    public void SpawnFireEffect(Vector2 worldPos, int count = 20)
    {
        for (int i = 0; i < count; i++)
        {
            float offsetX = -30 + (float)Random.Shared.NextDouble() * 60;
            float offsetY = -10 + (float)Random.Shared.NextDouble() * 20;
            Color fireColor = Random.Shared.NextDouble() > 0.5
                ? new Color(255, 130, 30)
                : new Color(255, 200, 50);
            _particles.Add(new Particle
            {
                Position = worldPos + new Vector2(offsetX, offsetY),
                Velocity = new Vector2(-5 + (float)Random.Shared.NextDouble() * 10, -40 - (float)Random.Shared.NextDouble() * 30),
                Gravity = new Vector2(0, -10), // 火焰向上
                Color = fireColor,
                Timer = 0.8f + (float)Random.Shared.NextDouble() * 0.6f,
                MaxTimer = 1.4f,
                StartSize = 4 + (float)Random.Shared.NextDouble() * 5,
                Size = 5,
                Alpha = 1f
            });
        }
    }

    public void SpawnLightningEffect(Vector2 worldPos)
    {
        // 闪电：竖向粒子 + 闪光
        for (int i = 0; i < 12; i++)
        {
            float offsetX = -5 + (float)Random.Shared.NextDouble() * 10;
            float offsetY = -100 + i * 15;
            _particles.Add(new Particle
            {
                Position = worldPos + new Vector2(offsetX, offsetY),
                Velocity = new Vector2(offsetX * 2, 20),
                Gravity = Vector2.Zero,
                Color = new Color(200, 220, 255),
                Timer = 0.3f + (float)Random.Shared.NextDouble() * 0.2f,
                MaxTimer = 0.5f,
                StartSize = 3 + (float)Random.Shared.NextDouble() * 3,
                Size = 4,
                Alpha = 1f
            });
        }
        ScreenFlash(new Color(200, 220, 255), 0.15f);
    }

    public void SpawnIceEffect(Vector2 worldPos, int count = 15)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = 20 + (float)Random.Shared.NextDouble() * 40;
            _particles.Add(new Particle
            {
                Position = worldPos + new Vector2(
                    -20 + (float)Random.Shared.NextDouble() * 40,
                    -10 + (float)Random.Shared.NextDouble() * 20),
                Velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - 20),
                Gravity = new Vector2(0, 30),
                Color = new Color(150, 200, 255),
                Timer = 0.6f + (float)Random.Shared.NextDouble() * 0.6f,
                MaxTimer = 1.2f,
                StartSize = 2 + (float)Random.Shared.NextDouble() * 3,
                Size = 3,
                Alpha = 1f
            });
        }
    }

    public void SpawnHitSparks(Vector2 worldPos, int count = 4)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = 30 + (float)Random.Shared.NextDouble() * 30;
            Color sparkColor = Random.Shared.NextDouble() > 0.5
                ? new Color(255, 255, 220)
                : new Color(255, 230, 140);
            _particles.Add(new Particle
            {
                Position = worldPos,
                Velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed),
                Gravity = new Vector2(0, 60),
                Color = sparkColor,
                Timer = 0.15f + (float)Random.Shared.NextDouble() * 0.1f,
                MaxTimer = 0.25f,
                StartSize = 2,
                Size = 2,
                Alpha = 1f
            });
        }
    }

    public void ScreenFlash(Color color, float duration)
    {
        _screenFlashColor = color;
        _screenFlashDuration = duration;
        _screenFlashTimer = duration;
    }

    // ==================== Draw ====================

    /// <summary>绘制世界空间粒子 (在camera transform下)</summary>
    public void DrawWorld(SpriteBatch sb, Texture2D pixel)
    {
        foreach (var p in _particles)
        {
            if (p.Timer <= 0) continue;
            int s = Math.Max(1, (int)p.Size);
            sb.Draw(pixel, new Rectangle(
                (int)p.Position.X - s / 2, (int)p.Position.Y - s / 2, s, s),
                p.Color * p.Alpha);
        }
    }

    /// <summary>绘制屏幕空间飘字 + 全屏闪光 (无camera transform)</summary>
    public void DrawScreen(SpriteBatch sb, Texture2D pixel, SpriteFontBase font,
                           Func<Vector2, Vector2> worldToScreen)
    {
        // 飘字 (世界坐标转屏幕坐标)
        foreach (var ft in _floatingTexts)
        {
            if (ft.Timer <= 0) continue;
            float alpha = Math.Min(1f, ft.Timer / (ft.MaxTimer * 0.3f));
            Vector2 screenPos = worldToScreen(ft.Position);
            Vector2 textSize = font.MeasureString(ft.Text);
            // 描边
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    sb.DrawString(font, ft.Text, screenPos + new Vector2(dx, dy) - textSize / 2,
                        new Color(0, 0, 0) * alpha * 0.7f);
            sb.DrawString(font, ft.Text, screenPos - textSize / 2, ft.Color * alpha);
        }

        // 全屏闪光
        if (_screenFlashTimer > 0)
        {
            float alpha = _screenFlashTimer / _screenFlashDuration;
            sb.Draw(pixel, new Rectangle(0, 0,
                Core.GameSettings.ScreenWidth, Core.GameSettings.ScreenHeight),
                _screenFlashColor * alpha * 0.4f);
        }
    }
}

public class FloatingText
{
    public string Text = "";
    public Vector2 Position;
    public Vector2 Velocity;
    public float Timer;
    public float MaxTimer;
    public Color Color = Color.White;
    public float Scale = 1f;
}

public class Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 Gravity;
    public Color Color = Color.White;
    public float Timer;
    public float MaxTimer;
    public float Size;
    public float StartSize;
    public float Alpha = 1f;
}
