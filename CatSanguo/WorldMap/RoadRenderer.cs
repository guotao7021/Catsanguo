using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CatSanguo.WorldMap;

public class RoadRenderer
{
    public void Draw(SpriteBatch sb, Texture2D pixel, List<CityNode> cityNodes, FogOfWarManager? fog = null)
    {
        var cityLookup = new Dictionary<string, CityNode>();
        foreach (var node in cityNodes)
            cityLookup[node.Data.Id] = node;

        var drawnPairs = new HashSet<string>();

        foreach (var node in cityNodes)
        {
            if (node.Data.ConnectedCityIds == null) continue;

            foreach (var connectedId in node.Data.ConnectedCityIds)
            {
                string pairKey = string.Compare(node.Data.Id, connectedId, StringComparison.Ordinal) < 0
                    ? $"{node.Data.Id}:{connectedId}"
                    : $"{connectedId}:{node.Data.Id}";

                if (drawnPairs.Contains(pairKey)) continue;
                drawnPairs.Add(pairKey);

                if (!cityLookup.TryGetValue(connectedId, out var otherNode)) continue;

                if (fog != null)
                {
                    var fogA = fog.GetFogState(node.Data.GridX, node.Data.GridY);
                    var fogB = fog.GetFogState(otherNode.Data.GridX, otherNode.Data.GridY);
                    if (fogA == FogState.Hidden && fogB == FogState.Hidden)
                        continue;
                }

                bool isWaterRoute = node.Data.CityType == "port" && otherNode.Data.CityType == "port";

                if (isWaterRoute)
                    DrawWaterRoute(sb, pixel, node.Center, otherNode.Center);
                else
                    DrawStyledRoad(sb, pixel, node.Center, otherNode.Center);
            }
        }
    }

    private void DrawStyledRoad(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end)
    {
        // 陆路 - 土黄色，适当粗细和透明度
        Color roadColor = new Color(90, 75, 50) * 0.45f;
        DrawLine(sb, pixel, start, end, roadColor, 3);
    }

    private void DrawWaterRoute(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end)
    {
        // 水路 - 蓝色虚线
        Color waterColor = new Color(70, 130, 190) * 0.35f;

        Vector2 diff = end - start;
        float totalLen = diff.Length();
        if (totalLen < 10) return;

        Vector2 dir = diff / totalLen;
        float dashLen = 12f;
        float gapLen = 8f;

        for (float d = 0; d < totalLen; d += dashLen + gapLen)
        {
            float segEnd = MathF.Min(d + dashLen, totalLen);
            Vector2 segStart = start + dir * d;
            Vector2 segEndPos = start + dir * segEnd;
            DrawLine(sb, pixel, segStart, segEndPos, waterColor, 2);
        }
    }

    private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, Color color, int thickness)
    {
        Vector2 diff = end - start;
        float length = diff.Length();
        if (length < 1) return;
        float angle = MathF.Atan2(diff.Y, diff.X);
        sb.Draw(pixel, start, null, color, angle, new Vector2(0, thickness / 2f),
            new Vector2(length, thickness), SpriteEffects.None, 0);
    }
}
