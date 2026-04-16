using System;
using Microsoft.Xna.Framework;

namespace CatSanguo.Core.Animation;

/// <summary>
/// 像素级绘制工具 - 在Color[]数组上绘制基础图形
/// 用于程序化生成精灵图
/// </summary>
public static class PixelArtBuilder
{
    public static void SetPixel(Color[] pixels, int sheetW, int sheetH, int x, int y, Color color)
    {
        if (x < 0 || x >= sheetW || y < 0 || y >= sheetH) return;
        int idx = y * sheetW + x;
        if (idx >= 0 && idx < pixels.Length)
            pixels[idx] = color;
    }

    public static void SetPixelBlend(Color[] pixels, int sheetW, int sheetH, int x, int y, Color color)
    {
        if (x < 0 || x >= sheetW || y < 0 || y >= sheetH) return;
        int idx = y * sheetW + x;
        if (idx < 0 || idx >= pixels.Length) return;

        Color existing = pixels[idx];
        float a = color.A / 255f;
        float inv = 1f - a;
        pixels[idx] = new Color(
            (byte)(color.R * a + existing.R * inv),
            (byte)(color.G * a + existing.G * inv),
            (byte)(color.B * a + existing.B * inv),
            (byte)Math.Min(255, color.A + existing.A));
    }

    public static void FillRect(Color[] pixels, int sheetW, int sheetH, int x, int y, int w, int h, Color color)
    {
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                SetPixel(pixels, sheetW, sheetH, x + dx, y + dy, color);
    }

    public static void DrawRect(Color[] pixels, int sheetW, int sheetH, int x, int y, int w, int h, Color color)
    {
        for (int dx = 0; dx < w; dx++)
        {
            SetPixel(pixels, sheetW, sheetH, x + dx, y, color);
            SetPixel(pixels, sheetW, sheetH, x + dx, y + h - 1, color);
        }
        for (int dy = 0; dy < h; dy++)
        {
            SetPixel(pixels, sheetW, sheetH, x, y + dy, color);
            SetPixel(pixels, sheetW, sheetH, x + w - 1, y + dy, color);
        }
    }

    public static void FillEllipse(Color[] pixels, int sheetW, int sheetH, int cx, int cy, int rx, int ry, Color color)
    {
        for (int dy = -ry; dy <= ry; dy++)
        {
            for (int dx = -rx; dx <= rx; dx++)
            {
                float ex = (float)dx / rx;
                float ey = (float)dy / ry;
                if (ex * ex + ey * ey <= 1f)
                    SetPixel(pixels, sheetW, sheetH, cx + dx, cy + dy, color);
            }
        }
    }

    public static void FillEllipseBlend(Color[] pixels, int sheetW, int sheetH, int cx, int cy, int rx, int ry, Color color)
    {
        for (int dy = -ry; dy <= ry; dy++)
        {
            for (int dx = -rx; dx <= rx; dx++)
            {
                float ex = (float)dx / rx;
                float ey = (float)dy / ry;
                if (ex * ex + ey * ey <= 1f)
                    SetPixelBlend(pixels, sheetW, sheetH, cx + dx, cy + dy, color);
            }
        }
    }

    public static void DrawLine(Color[] pixels, int sheetW, int sheetH, int x0, int y0, int x1, int y1, Color color)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            SetPixel(pixels, sheetW, sheetH, x0, y0, color);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    public static void DrawLineThick(Color[] pixels, int sheetW, int sheetH, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        int half = thickness / 2;
        for (int t = -half; t <= half; t++)
        {
            bool isVertical = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);
            if (isVertical)
                DrawLine(pixels, sheetW, sheetH, x0 + t, y0, x1 + t, y1, color);
            else
                DrawLine(pixels, sheetW, sheetH, x0, y0 + t, x1, y1 + t, color);
        }
    }

    /// <summary>颜色加深</summary>
    public static Color Darken(Color c, float factor)
    {
        return new Color(
            (byte)(c.R * factor),
            (byte)(c.G * factor),
            (byte)(c.B * factor),
            c.A);
    }

    /// <summary>颜色加亮</summary>
    public static Color Lighten(Color c, float factor)
    {
        return new Color(
            (byte)Math.Min(255, c.R * factor),
            (byte)Math.Min(255, c.G * factor),
            (byte)Math.Min(255, c.B * factor),
            c.A);
    }
}
