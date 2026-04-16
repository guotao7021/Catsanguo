using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CatSanguo.Core;
using CatSanguo.Data.Schemas;

namespace CatSanguo.WorldMap;

public enum FogState
{
    Hidden,
    Explored,
    Visible
}

public class FogOfWarManager
{
    private FogState[,] _fogGrid;
    private readonly int _gridWidth;
    private readonly int _gridHeight;
    private bool _dirty = true;
    private RenderTarget2D? _cachedTexture;

    public FogOfWarManager(int gridWidth, int gridHeight)
    {
        _gridWidth = gridWidth;
        _gridHeight = gridHeight;
        _fogGrid = new FogState[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                _fogGrid[x, y] = FogState.Hidden;
    }

    public FogState GetFogState(int gridX, int gridY)
    {
        if (gridX < 0 || gridX >= _gridWidth || gridY < 0 || gridY >= _gridHeight)
            return FogState.Hidden;
        return _fogGrid[gridX, gridY];
    }

    public void Update(List<CityNode> allCities, List<string> playerCityIds, List<ArmyToken>? playerArmies = null)
    {
        var oldGrid = (FogState[,])_fogGrid.Clone();

        for (int x = 0; x < _gridWidth; x++)
            for (int y = 0; y < _gridHeight; y++)
                if (_fogGrid[x, y] == FogState.Visible)
                    _fogGrid[x, y] = FogState.Explored;

        var cityLookup = allCities.ToDictionary(c => c.Data.Id, c => c);

        foreach (var cityId in playerCityIds)
        {
            if (!cityLookup.TryGetValue(cityId, out var city)) continue;
            RevealCell(city.Data.GridX, city.Data.GridY);

            if (city.Data.ConnectedCityIds == null) continue;
            foreach (var connId in city.Data.ConnectedCityIds)
            {
                if (cityLookup.TryGetValue(connId, out var connCity))
                    RevealCell(connCity.Data.GridX, connCity.Data.GridY);
            }
        }

        if (playerArmies != null)
        {
            foreach (var army in playerArmies.Where(a => a.Team == "player"))
            {
                var cityId = army.CurrentCityId ?? army.GetNearestCityId();
                if (cityId != null && cityLookup.TryGetValue(cityId, out var armyCity))
                {
                    RevealCell(armyCity.Data.GridX, armyCity.Data.GridY);

                    if (armyCity.Data.ConnectedCityIds != null)
                    {
                        foreach (var connId in armyCity.Data.ConnectedCityIds)
                        {
                            if (cityLookup.TryGetValue(connId, out var connCity))
                                RevealCell(connCity.Data.GridX, connCity.Data.GridY);
                        }
                    }
                }
            }
        }

        // 检查是否有变化
        for (int x = 0; x < _gridWidth; x++)
            for (int y = 0; y < _gridHeight; y++)
                if (_fogGrid[x, y] != oldGrid[x, y])
                {
                    _dirty = true;
                    return;
                }
    }

    private void RevealCell(int gridX, int gridY)
    {
        if (gridX >= 0 && gridX < _gridWidth && gridY >= 0 && gridY < _gridHeight)
            _fogGrid[gridX, gridY] = FogState.Visible;
    }

    /// <summary>
    /// 在 SpriteBatch.Begin() 之前调用，确保缓存生成
    /// </summary>
    public void EnsureCache(GraphicsDevice gd, SpriteBatch sb, Texture2D pixel, List<CityNode> cities)
    {
        if (!_dirty && _cachedTexture != null) return;

        int worldW = 2000;
        int worldH = 1400;

        if (_cachedTexture == null || _cachedTexture.Width != worldW || _cachedTexture.Height != worldH)
        {
            _cachedTexture?.Dispose();
            _cachedTexture = new RenderTarget2D(gd, worldW, worldH);
        }

        // 预计算可见位置列表
        var revealedPositions = new List<Vector2>();
        foreach (var city in cities)
        {
            var state = GetFogState(city.Data.GridX, city.Data.GridY);
            if (state == FogState.Visible || state == FogState.Explored)
                revealedPositions.Add(city.Center);
        }

        gd.SetRenderTarget(_cachedTexture);
        gd.Clear(Color.Transparent);
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        int step = 20; // 更大步进减少 draw call
        float revealRadius = 220f;

        for (int py = 0; py < worldH; py += step)
        {
            for (int px = 0; px < worldW; px += step)
            {
                Vector2 pos = new Vector2(px + step / 2f, py + step / 2f);

                float minDist = float.MaxValue;
                foreach (var rp in revealedPositions)
                {
                    float dx = pos.X - rp.X;
                    float dy = pos.Y - rp.Y;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d < minDist) minDist = d;
                }

                float alpha;
                if (minDist < revealRadius)
                {
                    float t = minDist / revealRadius;
                    alpha = t * t * 0.15f;
                }
                else
                {
                    float t = MathHelper.Clamp((minDist - revealRadius) / 300f, 0f, 1f);
                    alpha = 0.15f + t * 0.35f;
                }

                if (alpha > 0.01f)
                    sb.Draw(pixel, new Rectangle(px, py, step, step), Color.Black * alpha);
            }
        }

        sb.End();
        gd.SetRenderTarget(null);
        _dirty = false;
    }

    /// <summary>
    /// 在 SpriteBatch.Begin() 之后调用，绘制缓存的迷雾纹理
    /// </summary>
    public void Draw(SpriteBatch sb)
    {
        if (_cachedTexture != null)
            sb.Draw(_cachedTexture, Vector2.Zero, Color.White);
    }
}
