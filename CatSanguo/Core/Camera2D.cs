using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CatSanguo.Core;

public class Camera2D
{
    public Vector2 Position { get; set; }
    public float Zoom { get; set; } = 1f;
    public float Rotation { get; set; }
    public float MinZoom { get; set; } = 0.5f;
    public float MaxZoom { get; set; } = 1.5f;
    public Rectangle WorldBounds { get; set; }

    private readonly GraphicsDevice _graphicsDevice;

    public Camera2D(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public void SetZoom(float zoom)
    {
        Zoom = MathHelper.Clamp(zoom, MinZoom, MaxZoom);
    }

    public void ClampPosition()
    {
        if (WorldBounds.Width == 0 || WorldBounds.Height == 0) return;
        var vp = _graphicsDevice.Viewport;
        float halfW = vp.Width / (2f * Zoom);
        float halfH = vp.Height / (2f * Zoom);

        // 顶部 HUD 占 60px 屏幕空间，转换为世界空间偏移量
        float hudWorldOffset = 60f / Zoom;

        float minX = WorldBounds.Left + halfW;
        float maxX = WorldBounds.Right - halfW;
        // 允许向上多滚动，使 HUD 下方能看到顶部城池
        float minY = WorldBounds.Top + halfH - hudWorldOffset;
        float maxY = WorldBounds.Bottom - halfH;

        float x, y;
        // 可视区域超过世界范围时居中，否则正常 clamp
        if (minX >= maxX)
            x = (WorldBounds.Left + WorldBounds.Right) / 2f;
        else
            x = MathHelper.Clamp(Position.X, minX, maxX);

        if (minY >= maxY)
            y = (WorldBounds.Top + WorldBounds.Bottom) / 2f;
        else
            y = MathHelper.Clamp(Position.Y, minY, maxY);

        Position = new Vector2(x, y);
    }

    public Rectangle VisibleWorldRect
    {
        get
        {
            var vp = _graphicsDevice.Viewport;
            float halfW = vp.Width / (2f * Zoom);
            float halfH = vp.Height / (2f * Zoom);
            return new Rectangle(
                (int)(Position.X - halfW), (int)(Position.Y - halfH),
                (int)(halfW * 2), (int)(halfH * 2));
        }
    }

    public Matrix GetTransformMatrix()
    {
        var viewport = _graphicsDevice.Viewport;
        return Matrix.CreateTranslation(new Vector3(-Position, 0)) *
               Matrix.CreateRotationZ(Rotation) *
               Matrix.CreateScale(Zoom) *
               Matrix.CreateTranslation(new Vector3(viewport.Width / 2f, viewport.Height / 2f, 0));
    }

    public Vector2 ScreenToWorld(Vector2 screenPos)
    {
        return Vector2.Transform(screenPos, Matrix.Invert(GetTransformMatrix()));
    }

    public Vector2 WorldToScreen(Vector2 worldPos)
    {
        return Vector2.Transform(worldPos, GetTransformMatrix());
    }
}
