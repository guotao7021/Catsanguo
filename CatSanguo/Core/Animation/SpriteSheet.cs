using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CatSanguo.Core.Animation;

public class SpriteSheet
{
    public Texture2D Texture { get; }
    public int FrameWidth { get; }
    public int FrameHeight { get; }
    public Dictionary<string, AnimationClip> Clips { get; }

    public SpriteSheet(Texture2D texture, int frameWidth, int frameHeight, Dictionary<string, AnimationClip> clips)
    {
        Texture = texture;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        Clips = clips;
    }

    public AnimationClip? GetClip(string name)
    {
        return Clips.TryGetValue(name, out var clip) ? clip : null;
    }

    public Rectangle GetFrameRect(string clipName, int frameIndex)
    {
        var clip = GetClip(clipName);
        if (clip == null) return new Rectangle(0, 0, FrameWidth, FrameHeight);
        return clip.GetSourceRect(frameIndex, FrameWidth, FrameHeight);
    }
}
