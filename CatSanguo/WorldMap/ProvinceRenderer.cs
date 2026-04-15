using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CatSanguo.WorldMap;

public class ProvinceRenderer
{
    public void Draw(SpriteBatch sb, Texture2D pixel, List<CityNode> cities)
    {
        // Draw faction ownership halos around each city
        foreach (var city in cities)
        {
            Color factionColor = GetProvinceColor(city.Data.Owner);
            DrawOwnershipHalo(sb, pixel, city.Center, factionColor, 120, 90, 8);
        }

        // Draw ownership bands along roads between same-faction cities
        var cityLookup = cities.ToDictionary(c => c.Data.Id, c => c);
        var drawnPairs = new HashSet<string>();

        foreach (var city in cities)
        {
            if (city.Data.ConnectedCityIds == null) continue;
            string owner = city.Data.Owner.ToLower();
            if (owner == "neutral") continue;

            foreach (var connId in city.Data.ConnectedCityIds)
            {
                if (!cityLookup.TryGetValue(connId, out var other)) continue;
                // Same faction check
                if (other.Data.Owner.ToLower() != owner) continue;

                string pairKey = string.Compare(city.Data.Id, connId) < 0
                    ? $"{city.Data.Id}:{connId}" : $"{connId}:{city.Data.Id}";
                if (drawnPairs.Contains(pairKey)) continue;
                drawnPairs.Add(pairKey);

                Color bandColor = GetProvinceBandColor(owner);
                DrawBand(sb, pixel, city.Center, other.Center, bandColor, 30);
            }
        }
    }

    private static Color GetProvinceColor(string owner)
    {
        string o = owner.ToLower();
        if (o == "player") return new Color(40, 80, 180);
        if (o.StartsWith("enemy_wu")) return new Color(40, 140, 60);
        if (o.StartsWith("enemy")) return new Color(180, 40, 40);
        return new Color(100, 90, 70);
    }

    private static Color GetProvinceBandColor(string owner)
    {
        if (owner == "player") return new Color(40, 80, 180, 12);
        if (owner.StartsWith("enemy_wu")) return new Color(40, 140, 60, 12);
        if (owner.StartsWith("enemy")) return new Color(180, 40, 40, 12);
        return new Color(100, 90, 70, 8);
    }

    private static void DrawOwnershipHalo(SpriteBatch sb, Texture2D pixel, Vector2 center, Color baseColor, int rx, int ry, int layers)
    {
        for (int i = layers; i >= 1; i--)
        {
            float scale = (float)i / layers;
            int curRx = (int)(rx * scale);
            int curRy = (int)(ry * scale);
            int alpha = (int)(6 + (1f - scale) * 14);
            Color c = new Color(baseColor.R, baseColor.G, baseColor.B, alpha);
            sb.Draw(pixel,
                new Rectangle((int)center.X - curRx, (int)center.Y - curRy, curRx * 2, curRy * 2),
                c);
        }
    }

    private static void DrawBand(SpriteBatch sb, Texture2D pixel, Vector2 from, Vector2 to, Color color, int width)
    {
        Vector2 diff = to - from;
        float length = diff.Length();
        if (length < 1) return;
        float angle = System.MathF.Atan2(diff.Y, diff.X);
        sb.Draw(pixel, from, null, color, angle, new Vector2(0, width / 2f),
            new Vector2(length, width), SpriteEffects.None, 0);
    }
}
