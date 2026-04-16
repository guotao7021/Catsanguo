using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CatSanguo.Data.Schemas;

namespace CatSanguo.WorldMap;

/// <summary>
/// 势力领地渲染器 - Voronoi 领地划分 + RenderTarget2D 缓存
/// 只在城池归属变化时重新计算，每帧仅绘制一张缓存纹理
/// </summary>
public class ProvinceRenderer
{
    private const int WorldW = 2000;
    private const int WorldH = 1400;
    private const int GridStep = 5;

    private byte[]? _territoryGrid;
    private RenderTarget2D? _cachedTexture;
    private bool _dirty = true;
    private int _lastCityHash = 0;
    private Color[]? _cityFillColors;
    private Color[]? _cityBorderColors;

    public void Invalidate() => _dirty = true;

    /// <summary>
    /// 在 SpriteBatch.Begin() 之前调用，确保缓存纹理已生成
    /// </summary>
    public void EnsureCache(GraphicsDevice gd, SpriteBatch sb, Texture2D pixel, List<CityNode> cities)
    {
        if (cities.Count == 0) return;

        int cityHash = ComputeCityHash(cities);
        if (cityHash != _lastCityHash)
        {
            _dirty = true;
            _lastCityHash = cityHash;
        }

        if (!_dirty && _cachedTexture != null) return;

        // 创建/重用 RenderTarget
        if (_cachedTexture == null || _cachedTexture.Width != WorldW || _cachedTexture.Height != WorldH)
        {
            _cachedTexture?.Dispose();
            _cachedTexture = new RenderTarget2D(gd, WorldW, WorldH);
        }

        // 预计算颜色查找表
        BuildColorLookup(cities);

        // 计算领地网格
        int gridW = (WorldW + GridStep - 1) / GridStep;
        int gridH = (WorldH + GridStep - 1) / GridStep;
        ComputeTerritoryGrid(cities, gridW, gridH);

        // 渲染到缓存纹理
        gd.SetRenderTarget(_cachedTexture);
        gd.Clear(Color.Transparent);
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        DrawTerritoryFill(sb, pixel, gridW, gridH);
        DrawBorders(sb, pixel, gridW, gridH);
        DrawMapBorder(sb, pixel);

        sb.End();
        gd.SetRenderTarget(null);
        _dirty = false;
    }

    /// <summary>
    /// 在 SpriteBatch.Begin() 之后调用，绘制缓存纹理
    /// </summary>
    public void Draw(SpriteBatch sb)
    {
        if (_cachedTexture != null)
            sb.Draw(_cachedTexture, Vector2.Zero, Color.White);
    }

    private void BuildColorLookup(List<CityNode> cities)
    {
        _cityFillColors = new Color[cities.Count];
        _cityBorderColors = new Color[cities.Count];
        for (int i = 0; i < cities.Count; i++)
        {
            string owner = cities[i].Data.Owner;
            _cityFillColors[i] = GetFactionColor(owner);
            _cityBorderColors[i] = GetBorderColor(owner);
        }
    }

    private void DrawTerritoryFill(SpriteBatch sb, Texture2D pixel, int gridW, int gridH)
    {
        if (_territoryGrid == null || _cityFillColors == null) return;

        for (int gy = 0; gy < gridH; gy++)
        {
            int gx = 0;
            while (gx < gridW)
            {
                byte cityIdx = _territoryGrid[gy * gridW + gx];
                Color fillColor = cityIdx < _cityFillColors.Length
                    ? _cityFillColors[cityIdx] : new Color(160, 168, 190, 80);

                int runEnd = gx + 1;
                while (runEnd < gridW && _territoryGrid[gy * gridW + runEnd] == cityIdx)
                    runEnd++;

                int rectX = gx * GridStep;
                int rectW = (runEnd - gx) * GridStep;
                int rectY = gy * GridStep;
                if (rectX + rectW > WorldW) rectW = WorldW - rectX;
                int rectH = GridStep;
                if (rectY + rectH > WorldH) rectH = WorldH - rectY;

                if (rectW > 0 && rectH > 0)
                    sb.Draw(pixel, new Rectangle(rectX, rectY, rectW, rectH), fillColor);

                gx = runEnd;
            }
        }
    }

    private void DrawBorders(SpriteBatch sb, Texture2D pixel, int gridW, int gridH)
    {
        if (_territoryGrid == null || _cityBorderColors == null) return;
        byte[] grid = _territoryGrid;

        for (int gy = 0; gy < gridH; gy++)
        {
            for (int gx = 0; gx < gridW; gx++)
            {
                int idx = gy * gridW + gx;
                byte current = grid[idx];

                bool hasRight = gx < gridW - 1 && grid[idx + 1] != current;
                bool hasBottom = gy < gridH - 1 && grid[idx + gridW] != current;

                if (hasRight || hasBottom)
                {
                    int px = gx * GridStep;
                    int py = gy * GridStep;
                    Color color = current < _cityBorderColors.Length
                        ? _cityBorderColors[current] : new Color(60, 55, 45, 230);

                    if (hasRight)
                        sb.Draw(pixel, new Rectangle(px + GridStep - 1, py, 2, GridStep), color);
                    if (hasBottom)
                        sb.Draw(pixel, new Rectangle(px, py + GridStep - 1, GridStep, 2), color);
                }
            }
        }
    }

    /// <summary>
    /// 绘制地图外边框，框住整个地图
    /// </summary>
    private void DrawMapBorder(SpriteBatch sb, Texture2D pixel)
    {
        int t = 3;
        Color c = new Color(50, 40, 30, 240);
        sb.Draw(pixel, new Rectangle(0, 0, WorldW, t), c);
        sb.Draw(pixel, new Rectangle(0, WorldH - t, WorldW, t), c);
        sb.Draw(pixel, new Rectangle(0, 0, t, WorldH), c);
        sb.Draw(pixel, new Rectangle(WorldW - t, 0, t, WorldH), c);
    }

    private void ComputeTerritoryGrid(List<CityNode> cities, int gridW, int gridH)
    {
        _territoryGrid = new byte[gridW * gridH];
        int cityCount = cities.Count;

        // 预取城市坐标到数组，避免每次访问 List + property
        float[] cx = new float[cityCount];
        float[] cy = new float[cityCount];
        for (int i = 0; i < cityCount; i++)
        {
            cx[i] = cities[i].Center.X;
            cy[i] = cities[i].Center.Y;
        }

        // 纯最近距离 Voronoi（所有城池 influence 相同，无需除法）
        for (int gy = 0; gy < gridH; gy++)
        {
            float wy = gy * GridStep + GridStep * 0.5f;
            int rowOff = gy * gridW;

            for (int gx = 0; gx < gridW; gx++)
            {
                float wx = gx * GridStep + GridStep * 0.5f;
                int bestIdx = 0;
                float bestDist = float.MaxValue;

                for (int i = 0; i < cityCount; i++)
                {
                    float dx = wx - cx[i];
                    float dy = wy - cy[i];
                    float dist = dx * dx + dy * dy;
                    if (dist < bestDist) { bestDist = dist; bestIdx = i; }
                }

                _territoryGrid[rowOff + gx] = (byte)bestIdx;
            }
        }
    }

    private static int ComputeCityHash(List<CityNode> cities)
    {
        int hash = cities.Count * 397;
        for (int i = 0; i < cities.Count; i++)
            hash = hash * 31 + (cities[i].Data.Owner?.GetHashCode() ?? 0);
        return hash;
    }

    public static Color GetFactionColor(string owner)
    {
        string o = owner.ToLower();
        // 中立区域：冷蓝灰色半透明叠加，能看到地形
        if (o == "neutral") return new Color(160, 168, 190, 80);
        if (o == "player") return new Color(50, 100, 210, 105);
        if (o == "enemy_wei") return new Color(210, 40, 40, 105);
        if (o == "enemy_wu") return new Color(40, 180, 70, 105);
        if (o.StartsWith("caocao")) return new Color(210, 40, 40, 105);
        if (o.StartsWith("yuanshao")) return new Color(150, 50, 200, 105);
        if (o.StartsWith("yuan_shu")) return new Color(190, 80, 160, 105);
        if (o.StartsWith("dongzhuo")) return new Color(220, 120, 40, 105);
        if (o.StartsWith("lvbu")) return new Color(240, 80, 0, 105);
        if (o.StartsWith("sun")) return new Color(40, 170, 70, 105);
        if (o.StartsWith("liubei")) return new Color(230, 190, 50, 105);
        if (o.StartsWith("liubiao")) return new Color(160, 140, 70, 95);
        if (o.StartsWith("liuzhang") || o.StartsWith("liuyan")) return new Color(120, 170, 60, 95);
        if (o.StartsWith("gongsun")) return new Color(50, 190, 210, 105);
        if (o.StartsWith("machao") || o.StartsWith("ma_teng")) return new Color(60, 190, 150, 95);
        if (o.StartsWith("zhanglu")) return new Color(170, 190, 40, 95);
        if (o.StartsWith("menghuo")) return new Color(170, 100, 40, 95);
        return new Color(160, 168, 190, 80);
    }

    /// <summary>
    /// 返回不透明版本的势力颜色，用于图例色块显示
    /// </summary>
    public static Color GetFactionSolidColor(string owner)
    {
        Color c = GetFactionColor(owner);
        return new Color(c.R, c.G, c.B, (byte)255);
    }

    private static Color GetBorderColor(string owner)
    {
        string o = owner.ToLower();
        // 所有边界统一用深色线条，确保清晰可见
        if (o == "neutral") return new Color(60, 55, 45, 230);
        if (o == "player") return new Color(30, 60, 140, 235);
        if (o == "enemy_wei") return new Color(140, 25, 25, 235);
        if (o == "enemy_wu") return new Color(25, 110, 45, 235);
        if (o.StartsWith("caocao")) return new Color(140, 25, 25, 235);
        if (o.StartsWith("yuanshao")) return new Color(90, 30, 130, 235);
        if (o.StartsWith("yuan_shu")) return new Color(120, 50, 100, 235);
        if (o.StartsWith("dongzhuo")) return new Color(140, 70, 25, 235);
        if (o.StartsWith("lvbu")) return new Color(150, 50, 0, 235);
        if (o.StartsWith("sun")) return new Color(25, 100, 45, 235);
        if (o.StartsWith("liubei")) return new Color(150, 120, 30, 235);
        if (o.StartsWith("liubiao")) return new Color(100, 85, 45, 220);
        if (o.StartsWith("liuzhang") || o.StartsWith("liuyan")) return new Color(70, 105, 40, 220);
        if (o.StartsWith("gongsun")) return new Color(30, 120, 135, 235);
        if (o.StartsWith("machao") || o.StartsWith("ma_teng")) return new Color(40, 120, 90, 220);
        if (o.StartsWith("zhanglu")) return new Color(105, 120, 25, 220);
        if (o.StartsWith("menghuo")) return new Color(105, 60, 25, 220);
        return new Color(60, 55, 45, 230);
    }
}
