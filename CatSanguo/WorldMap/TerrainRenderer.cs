using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Data.Schemas;

namespace CatSanguo.WorldMap;

public class TerrainRenderer
{
    /// <summary>
    /// Draw interactive terrain features (resources only).
    /// Mountains, rivers, and forests are now part of the background image.
    /// </summary>
    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font,
        List<TerrainFeatureData> features,
        float mapLeft, float mapTop, float mapRight, float mapBottom)
    {
        float mapW = mapRight - mapLeft;
        float mapH = mapBottom - mapTop;

        foreach (var feature in features)
        {
            if (!feature.IsResource) continue;

            float screenX = mapLeft + (feature.GridX / 15f) * mapW;
            float screenY = mapTop + (feature.GridY / 9f) * mapH;

            switch (feature.Type)
            {
                case "mine":
                    DrawResource(sb, pixel, font, screenX, screenY, new Color(200, 160, 50), feature);
                    break;
                case "farm":
                    DrawResource(sb, pixel, font, screenX, screenY, new Color(80, 160, 50), feature);
                    break;
            }
        }
    }

    private void DrawResource(SpriteBatch sb, Texture2D pixel, SpriteFontBase font,
        float x, float y, Color baseColor, TerrainFeatureData feature)
    {
        int size = 16;
        int cx = (int)x;
        int cy = (int)y;

        // Larger diamond shape
        for (int row = 0; row < size; row++)
        {
            int halfW = row < size / 2 ? row + 1 : size - row;
            int py = cy - size / 2 + row;
            sb.Draw(pixel, new Rectangle(cx - halfW, py, halfW * 2, 1), baseColor * 0.7f);
        }

        // Border glow by owner
        Color borderColor = feature.Owner switch
        {
            "player" => new Color(80, 140, 220),
            "enemy" => new Color(220, 80, 80),
            _ => new Color(130, 120, 100)
        };
        sb.Draw(pixel, new Rectangle(cx - size / 2 - 1, cy - 1, size + 2, 2), borderColor * 0.5f);
        sb.Draw(pixel, new Rectangle(cx - 1, cy - size / 2 - 1, 2, size + 2), borderColor * 0.5f);

        // Inner icon
        string label = feature.ResourceType switch
        {
            "grain" => "粮",
            "troops" => "兵",
            "merit" => "功",
            _ => "?"
        };
        var labelSize = font.MeasureString(label);
        sb.DrawString(font, label, new Vector2(cx - labelSize.X / 2, cy + size / 2 + 3), baseColor * 0.9f);
    }
}
