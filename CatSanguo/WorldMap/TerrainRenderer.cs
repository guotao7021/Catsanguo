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
    /// Draw terrain features using proportional city layout coordinates.
    /// mapLeft/mapTop/mapRight/mapBottom define the full map drawing area.
    /// </summary>
    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font,
        List<TerrainFeatureData> features,
        float mapLeft, float mapTop, float mapRight, float mapBottom)
    {
        float mapW = mapRight - mapLeft;
        float mapH = mapBottom - mapTop;

        foreach (var feature in features)
        {
            float screenX = mapLeft + (feature.GridX / 15f) * mapW;
            float screenY = mapTop + (feature.GridY / 9f) * mapH;

            switch (feature.Type)
            {
                case "mountain":
                    DrawMountain(sb, pixel, screenX, screenY);
                    break;
                case "river":
                    DrawRiver(sb, pixel, screenX, screenY);
                    break;
                case "forest":
                    DrawForest(sb, pixel, screenX, screenY);
                    break;
                case "mine":
                    DrawResource(sb, pixel, font, screenX, screenY, new Color(200, 160, 50), feature);
                    break;
                case "farm":
                    DrawResource(sb, pixel, font, screenX, screenY, new Color(80, 160, 50), feature);
                    break;
            }
        }
    }

    private void DrawMountain(SpriteBatch sb, Texture2D pixel, float x, float y)
    {
        // 参考图风格 - 多层次山脉纹理
        // 绘制多座山峰形成山脉群
        DrawMountainRange(sb, pixel, x - 30, y + 10, 0.8f);
        DrawMountainRange(sb, pixel, x + 25, y + 15, 0.7f);
        DrawMountainRange(sb, pixel, x, y, 1.0f);  // 主峰
        
        // 山脚阴影
        Color shadowColor = new Color(30, 60, 30, 80);
        sb.Draw(pixel, new Rectangle((int)x - 50, (int)y + 30, 100, 12), shadowColor);
    }

    private void DrawMountainRange(SpriteBatch sb, Texture2D pixel, float cx, float cy, float scale)
    {
        // 山脉颜色层次
        Color mountainBase = new Color(50, 100, 50, 180);     // 基底绿色
        Color mountainMid = new Color(70, 130, 60, 170);      // 中间层
        Color mountainHigh = new Color(90, 150, 70, 160);     // 高层
        Color mountainPeak = new Color(110, 170, 80, 150);    // 山顶
        Color mountainSnow = new Color(200, 210, 190, 120);   // 山顶积雪/高光

        int rows = (int)(18 * scale);
        int baseWidth = (int)(50 * scale);

        for (int i = 0; i < rows; i++)
        {
            float t = (float)i / rows;
            // 使用更自然的曲线
            float widthCurve = 1f - MathF.Pow(t, 1.3f) * 0.85f;
            int w = (int)(baseWidth * widthCurve);
            int px = (int)cx - w / 2;
            int py = (int)cy + (int)((rows - 1 - i) * 3f * scale) - (int)(rows * 3f * scale);

            Color rowColor;
            if (t < 0.25f) rowColor = mountainBase;
            else if (t < 0.5f) rowColor = mountainMid;
            else if (t < 0.75f) rowColor = mountainHigh;
            else if (t < 0.9f) rowColor = mountainPeak;
            else rowColor = mountainSnow;

            sb.Draw(pixel, new Rectangle(px, py, w, (int)(4f * scale)), rowColor);
        }

        // 添加山体纹理 - 横向条纹模拟岩层
        Color textureColor = new Color(40, 80, 40, 60);
        for (int i = 0; i < rows; i += 3)
        {
            float t = (float)i / rows;
            if (t > 0.3f && t < 0.8f)
            {
                float widthCurve = 1f - MathF.Pow(t, 1.3f) * 0.85f;
                int w = (int)(baseWidth * widthCurve * 0.6f);
                int px = (int)cx - w / 2;
                int py = (int)cy + (int)((rows - 1 - i) * 3f * scale) - (int)(rows * 3f * scale);
                sb.Draw(pixel, new Rectangle(px, py, w, 1), textureColor);
            }
        }

        // 添加山坡阴影（左侧更暗）
        Color shadowColor = new Color(30, 60, 30, 50);
        for (int i = 0; i < rows; i += 2)
        {
            float t = (float)i / rows;
            float widthCurve = 1f - MathF.Pow(t, 1.3f) * 0.85f;
            int w = (int)(baseWidth * widthCurve * 0.3f);
            int px = (int)cx - (int)(baseWidth * widthCurve / 2);
            int py = (int)cy + (int)((rows - 1 - i) * 3f * scale) - (int)(rows * 3f * scale);
            sb.Draw(pixel, new Rectangle(px, py, w, (int)(3f * scale)), shadowColor);
        }
    }

    private void DrawRiver(SpriteBatch sb, Texture2D pixel, float x, float y)
    {
        // 参考图风格 - 宽阔蜿蜒的蓝色河流
        Color riverMain = new Color(40, 140, 200, 180);     // 主河流颜色
        Color riverLight = new Color(80, 180, 230, 140);    // 高光
        Color riverDark = new Color(30, 100, 160, 160);     // 深色边缘
        Color riverShallow = new Color(60, 160, 210, 100);  // 浅水区

        // 更长的河流，更自然的弯曲
        int segments = 12;
        float segLen = 35f;
        float waveAmp = 15f;

        // 绘制多层河流 - 模拟深度
        // 1. 河床底色（最宽）
        for (int i = 0; i < segments; i++)
        {
            float sx = x - segments * segLen / 2f + i * segLen;
            float sy = y + MathF.Sin(i * 0.6f) * waveAmp;
            float ex = sx + segLen + 6;
            float ey = y + MathF.Sin((i + 1) * 0.6f) * waveAmp;

            DrawLine(sb, pixel, new Vector2(sx, sy), new Vector2(ex, ey), riverDark, 14);
        }

        // 2. 主河道
        for (int i = 0; i < segments; i++)
        {
            float sx = x - segments * segLen / 2f + i * segLen;
            float sy = y + MathF.Sin(i * 0.6f) * waveAmp;
            float ex = sx + segLen + 6;
            float ey = y + MathF.Sin((i + 1) * 0.6f) * waveAmp;

            DrawLine(sb, pixel, new Vector2(sx, sy), new Vector2(ex, ey), riverMain, 10);
        }

        // 3. 高光水流
        for (int i = 0; i < segments; i++)
        {
            float sx = x - segments * segLen / 2f + i * segLen;
            float sy = y + MathF.Sin(i * 0.6f) * waveAmp - 2;
            float ex = sx + segLen + 4;
            float ey = y + MathF.Sin((i + 1) * 0.6f) * waveAmp - 2;

            DrawLine(sb, pixel, new Vector2(sx, sy), new Vector2(ex, ey), riverLight, 4);
        }

        // 4. 浅水区域点缀
        Color shallowDot = new Color(100, 190, 240, 80);
        for (int i = 0; i < segments; i += 3)
        {
            float sx = x - segments * segLen / 2f + i * segLen;
            float sy = y + MathF.Sin(i * 0.6f) * waveAmp;
            
            // 小水波纹
            sb.Draw(pixel, new Rectangle((int)sx - 2, (int)sy - 2, 5, 3), shallowDot);
        }

        // 5. 河流分支效果（模拟支流）
        DrawRiverBranch(sb, pixel, x, y, segments, segLen, waveAmp, -1);  // 上分支
        DrawRiverBranch(sb, pixel, x, y, segments, segLen, waveAmp, 1);   // 下分支
    }

    private void DrawRiverBranch(SpriteBatch sb, Texture2D pixel, float startX, float startY, 
        int segments, float segLen, float waveAmp, int direction)
    {
        Color branchColor = new Color(50, 150, 210, 120);
        int branchSegments = segments / 3;
        float branchLen = segLen * 0.8f;
        
        for (int i = 0; i < branchSegments; i++)
        {
            float progress = (float)i / branchSegments;
            float sx = startX + (segments * segLen / 2f) * progress * 0.5f;
            float sy = startY + MathF.Sin(i * 0.8f) * waveAmp * 0.5f + direction * i * 8;
            float ex = sx + branchLen;
            float ey = sy + direction * 5;

            DrawLine(sb, pixel, new Vector2(sx, sy), new Vector2(ex, ey), branchColor, 4);
        }
    }

    private void DrawForest(SpriteBatch sb, Texture2D pixel, float x, float y)
    {
        Color[] greens = {
            new Color(35, 80, 35, 130),
            new Color(45, 95, 40, 130),
            new Color(30, 70, 30, 130)
        };
        Color trunk = new Color(80, 55, 30, 100);

        // Cluster of 8 trees
        int[] offX = { -22, -14, -6, 2, 10, 18, -10, 6 };
        int[] offY = { 0, -4, 2, -2, 0, 3, 6, 7 };

        for (int i = 0; i < offX.Length; i++)
        {
            int tx = (int)x + offX[i];
            int ty = (int)y + offY[i];
            Color canopyColor = greens[i % greens.Length];

            // Canopy (layered)
            sb.Draw(pixel, new Rectangle(tx - 4, ty - 6, 9, 8), canopyColor);
            sb.Draw(pixel, new Rectangle(tx - 3, ty - 8, 7, 4), canopyColor * 0.8f);
            // Trunk
            sb.Draw(pixel, new Rectangle(tx - 1, ty + 2, 3, 5), trunk);
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

    private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, Color color, int thickness)
    {
        Vector2 diff = end - start;
        float length = diff.Length();
        if (length < 1) return;
        float angle = MathF.Atan2(diff.Y, diff.X);
        sb.Draw(pixel, start, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0);
    }
}
