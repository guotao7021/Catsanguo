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

                // Only draw roads between cities that are at least explored
                if (fog != null)
                {
                    var fogA = fog.GetFogState(node.Data.GridX, node.Data.GridY);
                    var fogB = fog.GetFogState(otherNode.Data.GridX, otherNode.Data.GridY);
                    if (fogA == FogState.Hidden && fogB == FogState.Hidden)
                        continue;
                }

                // Determine road type and tint
                string ownerA = node.Data.Owner.ToLower();
                string ownerB = otherNode.Data.Owner.ToLower();
                bool playerRoad = ownerA == "player" && ownerB == "player";
                bool isWaterRoute = node.Data.CityType == "port" && otherNode.Data.CityType == "port";

                if (isWaterRoute)
                    DrawWaterRoute(sb, pixel, node.Center, otherNode.Center);
                else
                    DrawStyledRoad(sb, pixel, node.Center, otherNode.Center, playerRoad);
            }
        }
    }

    private void DrawStyledRoad(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, bool playerOwned)
    {
        // 古风道路 - 土黄色调
        Color borderColor = new Color(90, 75, 50, 200);     // 深棕色边框
        Color fillColor = playerOwned
            ? new Color(180, 165, 130, 220)    // 玩家道路：更亮的土黄色
            : new Color(170, 150, 110, 200);   // 普通道路：标准土黄色

        // 边框（更宽）
        DrawLine(sb, pixel, start, end, borderColor, 6);
        // 填充
        DrawLine(sb, pixel, start, end, fillColor, 4);
        // 中心细线
        DrawLine(sb, pixel, start, end, new Color(200, 185, 150, 180), 1);

        // 装饰点 - 更大的间距
        Vector2 diff = end - start;
        float totalLen = diff.Length();
        if (totalLen < 50) return;

        Vector2 dir = diff / totalLen;
        Color dotColor = new Color(140, 125, 95, 180);
        float spacing = 40f;

        for (float d = spacing; d < totalLen - spacing / 2; d += spacing)
        {
            Vector2 pos = start + dir * d;
            // 小方块装饰
            sb.Draw(pixel, new Rectangle((int)pos.X - 1, (int)pos.Y - 1, 3, 3), dotColor);
        }
    }

    private void DrawWaterRoute(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end)
    {
        // 古风水路 - 蓝绿色调
        Color waterColor = new Color(70, 130, 190, 180);     // 主水色
        Color waterLight = new Color(110, 170, 220, 140);    // 高光

        Vector2 diff = end - start;
        float totalLen = diff.Length();
        if (totalLen < 10) return;

        Vector2 dir = diff / totalLen;
        float dashLen = 15f;   // 更长的实线
        float gapLen = 10f;    // 更长的间隔

        for (float d = 0; d < totalLen; d += dashLen + gapLen)
        {
            float segEnd = MathF.Min(d + dashLen, totalLen);
            Vector2 segStart = start + dir * d;
            Vector2 segEndPos = start + dir * segEnd;
            // 主线
            DrawLine(sb, pixel, segStart, segEndPos, waterColor, 4);
            // 高光
            if (segEnd - d > 5)
            {
                Vector2 highlightStart = segStart + dir * 2;
                Vector2 highlightEnd = segEndPos - dir * 2;
                DrawLine(sb, pixel, highlightStart, highlightEnd, waterLight, 1);
            }
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
