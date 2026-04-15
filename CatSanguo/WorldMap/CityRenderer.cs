using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Data.Schemas;

namespace CatSanguo.WorldMap;

public class CityRenderer
{
    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont,
                     List<CityNode> cities, ArmyToken? selectedArmy, float time)
    {
        foreach (var city in cities)
        {
            switch (city.Data.CityType)
            {
                case "pass":
                    DrawPass(sb, pixel, font, smallFont, city, selectedArmy, time);
                    break;
                case "port":
                    DrawPort(sb, pixel, font, smallFont, city, selectedArmy, time);
                    break;
                default:
                    DrawFortress(sb, pixel, font, smallFont, city, selectedArmy, time);
                    break;
            }
        }
    }

    private static Color GetFactionColor(string owner)
    {
        string o = owner.ToLower();
        if (o == "player") return new Color(60, 120, 220);
        if (o.StartsWith("enemy_wu")) return new Color(60, 180, 100);
        if (o.StartsWith("enemy")) return new Color(220, 60, 60);
        return new Color(130, 120, 100);
    }

    private static Color GetFactionAccent(string owner)
    {
        string o = owner.ToLower();
        if (o == "player") return new Color(80, 150, 255);
        if (o.StartsWith("enemy_wu")) return new Color(80, 210, 130);
        if (o.StartsWith("enemy")) return new Color(255, 80, 80);
        return new Color(150, 140, 120);
    }

    private static string GetFactionTag(string owner, string cityType)
    {
        string typeTag = cityType switch { "pass" => "关", "port" => "港", _ => "城" };
        string o = owner.ToLower();
        string factionTag;
        if (o == "player") factionTag = "蜀";
        else if (o.StartsWith("enemy_wu")) factionTag = "吴";
        else if (o.StartsWith("enemy")) factionTag = "魏";
        else factionTag = "中";
        return $"[{typeTag}·{factionTag}]";
    }

    private void DrawFortress(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont,
                              CityNode city, ArmyToken? selectedArmy, float time)
    {
        Vector2 center = city.Center;
        float scale = CityScaleConfig.GetIconScale(city.Data.CityScale);
        string owner = city.Data.Owner;
        Color factionColor = GetFactionColor(owner);
        Color factionAccent = GetFactionAccent(owner);

        // 参考图风格 - 简洁小方块
        int size = (int)(18 * scale);
        int halfSize = size / 2;
        int baseY = (int)center.Y;

        // 根据城池规模调整颜色深度
        Color baseColor = city.Data.CityScale switch
        {
            "large" => new Color(240, 230, 200),     // 大城池 - 更亮
            "medium" => new Color(220, 210, 180),    // 中等城池
            _ => new Color(200, 190, 160)            // 小城池 - 稍暗
        };

        // 绘制方块
        sb.Draw(pixel, new Rectangle((int)center.X - halfSize, baseY - halfSize, size, size), baseColor);
        
        // 势力颜色边框
        DrawBorder(sb, pixel, new Rectangle((int)center.X - halfSize, baseY - halfSize, size, size), factionColor, 2);
        
        // 内部填充 - 势力颜色淡化
        Color innerColor = factionColor * 0.3f;
        sb.Draw(pixel, new Rectangle((int)center.X - halfSize + 2, baseY - halfSize + 2, size - 4, size - 4), innerColor);

        // 选择指示器
        DrawSelectionIndicator(sb, pixel, city, selectedArmy, time,
            new Rectangle((int)center.X - halfSize - 5, baseY - halfSize - 5, size + 10, size + 10));

        // 标签（在方块下方）
        DrawCityLabels(sb, font, smallFont, city, center, baseY + halfSize + 4, factionAccent);
    }

    private void DrawPass(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont,
                          CityNode city, ArmyToken? selectedArmy, float time)
    {
        Vector2 center = city.Center;
        float scale = CityScaleConfig.GetIconScale(city.Data.CityScale);
        Color factionColor = GetFactionColor(city.Data.Owner);
        Color factionAccent = GetFactionAccent(city.Data.Owner);

        // 关口 - 菱形标记
        int size = (int)(16 * scale);
        int halfSize = size / 2;
        int baseY = (int)center.Y;

        Color baseColor = new Color(210, 200, 170);
        
        // 绘制菱形
        for (int row = 0; row < size; row++)
        {
            int halfW = row < size / 2 ? row + 1 : size - row;
            int py = baseY - halfSize + row;
            sb.Draw(pixel, new Rectangle((int)center.X - halfW, py, halfW * 2, 1), baseColor);
        }
        
        // 势力颜色边框
        DrawBorder(sb, pixel, new Rectangle((int)center.X - halfSize, baseY - halfSize, size, size), factionColor, 2);

        // 选择指示器
        DrawSelectionIndicator(sb, pixel, city, selectedArmy, time,
            new Rectangle((int)center.X - halfSize - 5, baseY - halfSize - 5, size + 10, size + 10));

        // 标签
        DrawCityLabels(sb, font, smallFont, city, center, baseY + halfSize + 4, factionAccent);
    }

    private void DrawPort(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont,
                          CityNode city, ArmyToken? selectedArmy, float time)
    {
        Vector2 center = city.Center;
        float scale = CityScaleConfig.GetIconScale(city.Data.CityScale);
        Color factionColor = GetFactionColor(city.Data.Owner);
        Color factionAccent = GetFactionAccent(city.Data.Owner);

        // 港口 - 圆形标记
        int size = (int)(16 * scale);
        int halfSize = size / 2;
        int baseY = (int)center.Y;
        int radius = halfSize;

        Color baseColor = new Color(190, 210, 220);  // 偏蓝
        
        // 绘制圆形
        for (int row = -radius; row <= radius; row++)
        {
            int w = (int)MathF.Sqrt(radius * radius - row * row);
            if (w > 0)
            {
                sb.Draw(pixel, new Rectangle((int)center.X - w, baseY + row, w * 2, 1), baseColor);
            }
        }
        
        // 势力颜色边框
        Color borderColor = factionColor;
        for (int row = -radius; row <= radius; row += 2)
        {
            int w = (int)MathF.Sqrt(radius * radius - row * row);
            if (w > 0)
            {
                sb.Draw(pixel, new Rectangle((int)center.X - w, baseY + row, 2, 1), borderColor);
                sb.Draw(pixel, new Rectangle((int)center.X + w - 2, baseY + row, 2, 1), borderColor);
            }
        }

        // 选择指示器
        DrawSelectionIndicator(sb, pixel, city, selectedArmy, time,
            new Rectangle((int)center.X - halfSize - 5, baseY - halfSize - 5, size + 10, size + 10));

        // 标签
        DrawCityLabels(sb, font, smallFont, city, center, baseY + halfSize + 4, factionAccent);
    }
    private static void DrawSelectionIndicator(SpriteBatch sb, Texture2D pixel, CityNode city,
        ArmyToken? selectedArmy, float time, Rectangle bounds)
    {
        if (city.IsSelected || (selectedArmy != null && selectedArmy.TargetCityId == city.Data.Id))
        {
            float pulse = 0.6f + 0.4f * MathF.Sin(time * 5f);
            Color selectColor = new Color(255, 220, 100) * pulse;
            DrawBorder(sb, pixel, bounds, selectColor, 2);
        }
    }

    private void DrawCityLabels(SpriteBatch sb, SpriteFontBase font, SpriteFontBase smallFont,
        CityNode city, Vector2 center, int baseY, Color factionAccent)
    {
        // City name with shadow
        var nameSize = font.MeasureString(city.Data.Name);
        Vector2 namePos = new Vector2(center.X - nameSize.X / 2, baseY + 10);
        sb.DrawString(font, city.Data.Name, namePos + new Vector2(1, 1), new Color(20, 18, 12));
        sb.DrawString(font, city.Data.Name, namePos, new Color(240, 220, 170));

        // Faction + type tag
        string tag = GetFactionTag(city.Data.Owner, city.Data.CityType);
        var tagSize = smallFont.MeasureString(tag);
        sb.DrawString(smallFont, tag,
            new Vector2(center.X - tagSize.X / 2, baseY + 28), factionAccent);

        // Garrison indicator
        if (city.Data.Garrison.Count > 0)
        {
            string garr = $"驻{city.Data.Garrison.Count}";
            var garrSize = smallFont.MeasureString(garr);
            sb.DrawString(smallFont, garr,
                new Vector2(center.X - garrSize.X / 2, baseY + 42), new Color(200, 150, 100));
        }
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
