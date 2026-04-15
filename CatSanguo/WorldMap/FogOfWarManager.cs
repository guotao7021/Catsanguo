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

        // Step 1: Demote Visible -> Explored
        for (int x = 0; x < _gridWidth; x++)
            for (int y = 0; y < _gridHeight; y++)
                if (_fogGrid[x, y] == FogState.Visible)
                    _fogGrid[x, y] = FogState.Explored;

        var cityLookup = allCities.ToDictionary(c => c.Data.Id, c => c);

        // Step 2: Reveal player cities + neighbors
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

        // Step 3: Reveal army positions + neighbors
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

        // Check if state changed
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
    /// Draw smooth distance-based fog overlay in world space.
    /// </summary>
    public void Draw(SpriteBatch sb, Texture2D pixel, List<CityNode> cities)
    {
        // World-space fog covering the entire 2000x1400 virtual map
        int worldW = 2000;
        int worldH = 1400;

        var visiblePositions = new List<Vector2>();
        var exploredPositions = new List<Vector2>();

        foreach (var city in cities)
        {
            var state = GetFogState(city.Data.GridX, city.Data.GridY);
            if (state == FogState.Visible)
                visiblePositions.Add(city.Center);
            else if (state == FogState.Explored)
                exploredPositions.Add(city.Center);
        }

        int step = 24; // Slightly coarser for larger map (performance)
        float visibleRadius = 180f;
        float exploredRadius = 180f;

        for (int py = 0; py < worldH; py += step)
        {
            for (int px = 0; px < worldW; px += step)
            {
                Vector2 pos = new Vector2(px + step / 2f, py + step / 2f);

                float minVisibleDist = float.MaxValue;
                foreach (var vp in visiblePositions)
                {
                    float d = Vector2.Distance(pos, vp);
                    if (d < minVisibleDist) minVisibleDist = d;
                }

                float minExploredDist = float.MaxValue;
                foreach (var ep in exploredPositions)
                {
                    float d = Vector2.Distance(pos, ep);
                    if (d < minExploredDist) minExploredDist = d;
                }

                float alpha;
                if (minVisibleDist < visibleRadius)
                {
                    float t = minVisibleDist / visibleRadius;
                    alpha = t * t * 0.3f;
                }
                else if (minExploredDist < exploredRadius)
                {
                    float t = minExploredDist / exploredRadius;
                    alpha = 0.25f + t * 0.25f;
                }
                else
                {
                    float minDist = MathF.Min(minVisibleDist, minExploredDist);
                    float maxRadius = MathF.Max(visibleRadius, exploredRadius);
                    float t = MathHelper.Clamp((minDist - maxRadius) / 200f, 0f, 1f);
                    alpha = 0.45f + t * 0.35f;
                }

                if (alpha > 0.02f)
                {
                    sb.Draw(pixel, new Rectangle(px, py, step, step), Color.Black * alpha);
                }
            }
        }
    }
}
