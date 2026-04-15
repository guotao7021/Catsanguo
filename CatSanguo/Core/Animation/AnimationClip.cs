using Microsoft.Xna.Framework;

namespace CatSanguo.Core.Animation;

public class AnimationClip
{
    public string Name { get; }
    public int Row { get; }
    public int FrameCount { get; }
    public float FrameRate { get; }
    public bool Loop { get; }

    public AnimationClip(string name, int row, int frameCount, float frameRate, bool loop)
    {
        Name = name;
        Row = row;
        FrameCount = frameCount;
        FrameRate = frameRate;
        Loop = loop;
    }

    public Rectangle GetSourceRect(int frameIndex, int frameWidth, int frameHeight)
    {
        int clampedFrame = System.Math.Clamp(frameIndex, 0, FrameCount - 1);
        return new Rectangle(clampedFrame * frameWidth, Row * frameHeight, frameWidth, frameHeight);
    }
}
