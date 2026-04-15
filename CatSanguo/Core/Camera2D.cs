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
        float x = MathHelper.Clamp(Position.X, WorldBounds.Left + halfW, WorldBounds.Right - halfW);
        float y = MathHelper.Clamp(Position.Y, WorldBounds.Top + halfH, WorldBounds.Bottom - halfH);
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
}
