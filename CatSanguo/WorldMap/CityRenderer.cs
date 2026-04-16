using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Data.Schemas;
using CatSanguo.Data;

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
        // 玩家势力 - 蓝
        if (o == "player") return new Color(60, 120, 220);
        // 兼容 cities.json 旧值
        if (o == "enemy_wei") return new Color(220, 50, 50);
        if (o == "enemy_wu") return new Color(50, 190, 80);
        if (o == "neutral") return new Color(130, 120, 100);
        // 精确匹配 factionId 前缀（格式: {leader}_{scenario}）
        if (o.StartsWith("caocao")) return new Color(220, 50, 50);       // 曹操 - 红
        if (o.StartsWith("yuanshao")) return new Color(160, 50, 200);    // 袁绍 - 紫
        if (o.StartsWith("yuan_shu")) return new Color(200, 80, 160);    // 袁术 - 粉紫
        if (o.StartsWith("dongzhuo")) return new Color(230, 120, 40);    // 董卓 - 橙
        if (o.StartsWith("lvbu")) return new Color(255, 80, 0);          // 吕布 - 亮橙
        if (o.StartsWith("sun") || o.StartsWith("sun_liu")) return new Color(50, 190, 80); // 孙家 - 绿
        if (o.StartsWith("liubei") || o.StartsWith("liubei_")) return new Color(240, 200, 50); // 刘备 - 金黄
        if (o.StartsWith("liubiao")) return new Color(170, 150, 70);     // 刘表 - 暗黄
        if (o.StartsWith("liuzhang") || o.StartsWith("liuyan")) return new Color(120, 180, 60); // 刘璋/刘焉 - 黄绿
        if (o.StartsWith("gongsun")) return new Color(50, 200, 220);     // 公孙瓒 - 青
        if (o.StartsWith("machao") || o.StartsWith("ma_teng")) return new Color(60, 200, 160); // 马家 - 蓝绿
        if (o.StartsWith("zhanglu")) return new Color(180, 200, 40);     // 张鲁 - 黄绿
        if (o.StartsWith("menghuo")) return new Color(180, 100, 40);     // 孟获 - 棕
        // 中立
        return new Color(130, 120, 100);
    }

    private static Color GetFactionAccent(string owner)
    {
        string o = owner.ToLower();
        if (o == "player") return new Color(80, 150, 255);
        if (o == "enemy_wei") return new Color(255, 80, 80);
        if (o == "enemy_wu") return new Color(80, 220, 110);
        if (o == "neutral") return new Color(150, 140, 120);
        if (o.StartsWith("caocao")) return new Color(255, 80, 80);
        if (o.StartsWith("yuanshao")) return new Color(190, 80, 230);
        if (o.StartsWith("yuan_shu")) return new Color(230, 110, 190);
        if (o.StartsWith("dongzhuo")) return new Color(255, 150, 60);
        if (o.StartsWith("lvbu")) return new Color(255, 110, 30);
        if (o.StartsWith("sun") || o.StartsWith("sun_liu")) return new Color(80, 220, 110);
        if (o.StartsWith("liubei") || o.StartsWith("liubei_")) return new Color(255, 230, 80);
        if (o.StartsWith("liubiao")) return new Color(200, 180, 100);
        if (o.StartsWith("liuzhang") || o.StartsWith("liuyan")) return new Color(150, 210, 90);
        if (o.StartsWith("gongsun")) return new Color(80, 230, 250);
        if (o.StartsWith("machao") || o.StartsWith("ma_teng")) return new Color(90, 230, 190);
        if (o.StartsWith("zhanglu")) return new Color(210, 230, 70);
        if (o.StartsWith("menghuo")) return new Color(210, 130, 60);
        return new Color(150, 140, 120);
    }

    private static string GetFactionTag(string owner, string cityType)
    {
        string typeTag = cityType switch { "pass" => "关", "port" => "港", _ => "城" };
        string o = owner.ToLower();
        string factionTag;
        if (o == "player") factionTag = "我";
        else if (o.StartsWith("caocao") || o == "enemy_wei") factionTag = "魏";
        else if (o.StartsWith("sun") || o == "enemy_wu") factionTag = "吴";
        else if (o.StartsWith("liubei")) factionTag = "蜀";
        else if (o.StartsWith("yuanshao")) factionTag = "袁";
        else if (o.StartsWith("yuan_shu")) factionTag = "术";
        else if (o.StartsWith("dongzhuo")) factionTag = "董";
        else if (o.StartsWith("lvbu")) factionTag = "吕";
        else if (o.StartsWith("gongsun")) factionTag = "公";
        else if (o.StartsWith("liubiao")) factionTag = "表";
        else if (o.StartsWith("liuzhang") || o.StartsWith("liuyan")) factionTag = "蜀";
        else if (o.StartsWith("machao") || o.StartsWith("ma_teng")) factionTag = "马";
        else if (o.StartsWith("zhanglu")) factionTag = "张";
        else if (o.StartsWith("menghuo")) factionTag = "蛮";
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

        int size = (int)(24 * scale);
        int halfSize = size / 2;
        int baseY = (int)center.Y;

        // 势力颜色光晕（大范围半透明）
        int glowSize = size + 16;
        int glowHalf = glowSize / 2;
        sb.Draw(pixel, new Rectangle((int)center.X - glowHalf, baseY - glowHalf, glowSize, glowSize), factionColor * 0.25f);

        // 势力颜色填充（整个方块）
        sb.Draw(pixel, new Rectangle((int)center.X - halfSize, baseY - halfSize, size, size), factionColor * 0.7f);

        // 内部亮色核心
        int innerSize = size - 4;
        int innerHalf = innerSize / 2;
        Color innerColor = city.Data.CityScale switch
        {
            "large" or "huge" => Color.Lerp(factionColor, Color.White, 0.3f),
            _ => Color.Lerp(factionColor, Color.White, 0.15f)
        };
        sb.Draw(pixel, new Rectangle((int)center.X - innerHalf, baseY - innerHalf, innerSize, innerSize), innerColor * 0.6f);
        
        // 势力颜色边框（加粗）
        DrawBorder(sb, pixel, new Rectangle((int)center.X - halfSize, baseY - halfSize, size, size), factionAccent, 2);

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

        int size = (int)(22 * scale);
        int halfSize = size / 2;
        int baseY = (int)center.Y;

        // 势力颜色光晕
        int glowSize = size + 14;
        int glowHalf = glowSize / 2;
        sb.Draw(pixel, new Rectangle((int)center.X - glowHalf, baseY - glowHalf, glowSize, glowSize), factionColor * 0.2f);

        // 绘制菱形（势力颜色填充）
        for (int row = 0; row < size; row++)
        {
            int halfW = row < size / 2 ? row + 1 : size - row;
            int py = baseY - halfSize + row;
            sb.Draw(pixel, new Rectangle((int)center.X - halfW, py, halfW * 2, 1), factionColor * 0.7f);
        }
        
        // 势力颜色边框
        DrawBorder(sb, pixel, new Rectangle((int)center.X - halfSize, baseY - halfSize, size, size), factionAccent, 2);

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

        int size = (int)(22 * scale);
        int halfSize = size / 2;
        int baseY = (int)center.Y;
        int radius = halfSize;

        // 势力颜色光晕
        int glowSize = size + 14;
        int glowHalf = glowSize / 2;
        sb.Draw(pixel, new Rectangle((int)center.X - glowHalf, baseY - glowHalf, glowSize, glowSize), factionColor * 0.2f);

        // 绘制圆形（势力颜色填充）
        for (int row = -radius; row <= radius; row++)
        {
            int w = (int)MathF.Sqrt(radius * radius - row * row);
            if (w > 0)
            {
                sb.Draw(pixel, new Rectangle((int)center.X - w, baseY + row, w * 2, 1), factionColor * 0.7f);
            }
        }
        
        // 势力颜色圆形边框
        for (int row = -radius; row <= radius; row++)
        {
            int w = (int)MathF.Sqrt(radius * radius - row * row);
            if (w > 0)
            {
                sb.Draw(pixel, new Rectangle((int)center.X - w, baseY + row, 2, 1), factionAccent);
                sb.Draw(pixel, new Rectangle((int)center.X + w - 2, baseY + row, 2, 1), factionAccent);
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
