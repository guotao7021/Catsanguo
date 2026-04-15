using Microsoft.Xna.Framework;
using FontStashSharp;

namespace CatSanguo.WorldMap;

public class MarchAnimator
{
    public Vector2 StartPosition { get; }
    public Vector2 EndPosition { get; }
    public float Progress { get; private set; }
    public float Duration { get; } = 2.5f;
    public string GeneralName { get; }
    public bool IsPlayer { get; }
    public bool IsComplete => Progress >= 1f;

    public Vector2 CurrentPosition => Vector2.Lerp(StartPosition, EndPosition, EaseInOutCubic(Progress));

    public MarchAnimator(Vector2 start, Vector2 end, string generalName, bool isPlayer)
    {
        StartPosition = start;
        EndPosition = end;
        GeneralName = generalName;
        IsPlayer = isPlayer;
    }

    public void Update(float deltaTime)
    {
        if (IsComplete) return;
        Progress += deltaTime / Duration;
        if (Progress > 1f) Progress = 1f;
    }

    public void Draw(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, Microsoft.Xna.Framework.Graphics.Texture2D pixel, FontStashSharp.SpriteFontBase font)
    {
        Vector2 pos = CurrentPosition;
        Color color = IsPlayer ? new Color(60, 120, 220) : new Color(220, 60, 60);

        // Draw marching icon (diamond shape)
        sb.Draw(pixel, new Rectangle((int)pos.X - 5, (int)pos.Y - 5, 10, 10), color);
        sb.Draw(pixel, new Rectangle((int)pos.X - 1, (int)pos.Y - 7, 2, 14), color * 1.2f);

        // Draw general name below
        var nameSize = font.MeasureString(GeneralName);
        sb.DrawString(font, GeneralName, new Vector2(pos.X - nameSize.X / 2, pos.Y + 12), Color.White);
    }

    private static float EaseInOutCubic(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - System.MathF.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
