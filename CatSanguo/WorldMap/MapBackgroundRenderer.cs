using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CatSanguo.Core;

namespace CatSanguo.WorldMap;

public class MapBackgroundRenderer
{
    private RenderTarget2D? _cached;
    private bool _dirty = true;

    private const int WorldWidth = 2000;
    private const int WorldHeight = 1400;

    public void Invalidate() => _dirty = true;

    public void EnsureCache(GraphicsDevice gd, SpriteBatch sb, Texture2D pixel, List<CityNode> cities)
    {
        if (!_dirty && _cached != null) return;

        int w = WorldWidth;
        int h = WorldHeight;

        if (_cached == null || _cached.Width != w || _cached.Height != h)
        {
            _cached?.Dispose();
            _cached = new RenderTarget2D(gd, w, h);
        }

        gd.SetRenderTarget(_cached);
        gd.Clear(Color.Transparent);
        sb.Begin();

        // 1. 基础地形 - 多层噪声模拟自然地形
        DrawTerrainBase(sb, pixel, w, h);

        // 2. 山脉区域 - 深绿色高地区域
        DrawMountainRegions(sb, pixel, w, h);

        // 3. 平原区域 - 浅绿色低地区域
        DrawPlainRegions(sb, pixel, w, h);

        // 4. 城池周围高亮
        foreach (var city in cities)
        {
            DrawCityGlow(sb, pixel, city.Center, GetFactionGlowColor(city.Data.Owner));
        }

        // 5. 边界暗角
        DrawVignette(sb, pixel, w, h);

        sb.End();
        gd.SetRenderTarget(null);
        _dirty = false;
    }

    private void DrawTerrainBase(SpriteBatch sb, Texture2D pixel, int w, int h)
    {
        // 使用多层噪声生成自然地形色调
        Random rng = new Random(12345); // 固定种子
        
        // 基础绿色渐变
        for (int y = 0; y < h; y += 2)
        {
            for (int x = 0; x < w; x += 2)
            {
                // 基础噪声
                float noise1 = SimpleNoise(x * 0.005f, y * 0.005f, rng);
                float noise2 = SimpleNoise(x * 0.01f, y * 0.01f, rng) * 0.5f;
                float noise = (noise1 + noise2) * 0.5f + 0.5f;
                
                // 根据位置调整色调
                float xRatio = (float)x / w;
                float yRatio = (float)y / h;
                
                byte r, g, b;
                if (noise > 0.6f)
                {
                    // 高地 - 深绿色
                    r = (byte)(60 + noise * 40);
                    g = (byte)(120 + noise * 60);
                    b = (byte)(40 + noise * 20);
                }
                else if (noise > 0.4f)
                {
                    // 中海拔 - 中绿色
                    r = (byte)(80 + noise * 50);
                    g = (byte)(140 + noise * 50);
                    b = (byte)(60 + noise * 30);
                }
                else
                {
                    // 低地 - 浅绿色/黄绿色
                    r = (byte)(120 + noise * 60);
                    g = (byte)(160 + noise * 40);
                    b = (byte)(70 + noise * 30);
                }
                
                // 东西方向色调变化
                if (xRatio < 0.33f)
                {
                    // 西部更深绿
                    g = (byte)Math.Min(255, g + 10);
                }
                else if (xRatio > 0.66f)
                {
                    // 东部偏蓝绿
                    b = (byte)Math.Min(255, b + 15);
                }
                
                sb.Draw(pixel, new Rectangle(x, y, 2, 2), new Color(r, g, b));
            }
        }
    }

    private void DrawMountainRegions(SpriteBatch sb, Texture2D pixel, int w, int h)
    {
        // 在特定区域绘制山脉底色
        Color mountainColor = new Color(40, 90, 40, 60);
        
        // 西部山脉区域
        DrawSoftEllipse(sb, pixel, new Vector2(w * 0.2f, h * 0.3f), 300, 250, mountainColor, 10);
        DrawSoftEllipse(sb, pixel, new Vector2(w * 0.25f, h * 0.5f), 250, 200, mountainColor, 8);
        
        // 中部山脉
        DrawSoftEllipse(sb, pixel, new Vector2(w * 0.5f, h * 0.4f), 200, 180, mountainColor * 0.8f, 8);
        
        // 东部丘陵
        DrawSoftEllipse(sb, pixel, new Vector2(w * 0.75f, h * 0.35f), 280, 220, mountainColor * 0.7f, 8);
    }

    private void DrawPlainRegions(SpriteBatch sb, Texture2D pixel, int w, int h)
    {
        // 平原区域 - 浅黄色
        Color plainColor = new Color(160, 180, 100, 40);
        
        // 中原平原
        DrawSoftEllipse(sb, pixel, new Vector2(w * 0.45f, h * 0.5f), 350, 280, plainColor, 10);
        
        // 东部平原
        DrawSoftEllipse(sb, pixel, new Vector2(w * 0.7f, h * 0.6f), 250, 200, plainColor * 0.8f, 8);
    }

    private void DrawCityGlow(SpriteBatch sb, Texture2D pixel, Vector2 center, Color glowColor)
    {
        // 城池周围的柔和光晕
        DrawSoftEllipse(sb, pixel, center, 60, 50, glowColor, 6);
    }

    private static Color GetFactionGlowColor(string owner)
    {
        return owner.ToLower() switch
        {
            "player" => new Color(80, 140, 220, 30),
            "enemy_wu" => new Color(80, 200, 120, 30),
            "enemy" => new Color(220, 100, 80, 30),
            _ => new Color(200, 200, 180, 25)
        };
    }

    private void DrawVignette(SpriteBatch sb, Texture2D pixel, int w, int h)
    {
        int vignetteSize = 120;
        for (int i = 0; i < vignetteSize; i++)
        {
            float t = (float)i / vignetteSize;
            float alpha = (1f - t) * 0.5f;
            Color vc = new Color(20, 15, 10, alpha);
            sb.Draw(pixel, new Rectangle(i, 0, 1, h), vc);
            sb.Draw(pixel, new Rectangle(w - 1 - i, 0, 1, h), vc);
            sb.Draw(pixel, new Rectangle(0, i, w, 1), vc);
            sb.Draw(pixel, new Rectangle(0, h - 1 - i, w, 1), vc);
        }
    }

    // 简化的噪声函数
    private static float SimpleNoise(float x, float y, Random rng)
    {
        // 使用确定性噪声（基于坐标）
        int seed = (int)(x * 1000 + y * 3000);
        rng = new Random(seed);
        return (float)rng.NextDouble();
    }

    private static void DrawSoftEllipse(SpriteBatch sb, Texture2D pixel, Vector2 center, int rx, int ry, Color color, int layers)
    {
        for (int i = layers; i >= 1; i--)
        {
            float scale = (float)i / layers;
            int curRx = (int)(rx * scale);
            int curRy = (int)(ry * scale);
            float alpha = (1f - scale + 0.1f) * 1.2f;
            alpha = Math.Min(alpha, 1f);
            Color layerColor = color * alpha;
            sb.Draw(pixel,
                new Rectangle((int)center.X - curRx, (int)center.Y - curRy, curRx * 2, curRy * 2),
                layerColor);
        }
    }

    public void Draw(SpriteBatch sb)
    {
        if (_cached != null)
        {
            sb.Draw(_cached, Vector2.Zero, Color.White);
        }
    }
}
