using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CatSanguo.Core.Animation;

public class Animator
{
    public SpriteSheet SpriteSheet { get; }
    public AnimationClip? CurrentClip { get; private set; }
    public int CurrentFrame { get; private set; }
    public float FrameTimer { get; private set; }
    public bool IsFinished { get; private set; }

    public bool HasTexture => SpriteSheet.Texture != null;

    private string _currentClipName = "";

    public Animator(SpriteSheet spriteSheet)
    {
        SpriteSheet = spriteSheet;
    }

    public void Play(string clipName)
    {
        if (clipName == _currentClipName) return;

        var clip = SpriteSheet.GetClip(clipName);
        if (clip == null) return;

        _currentClipName = clipName;
        CurrentClip = clip;
        CurrentFrame = 0;
        FrameTimer = 0f;
        IsFinished = false;
    }

    public void Update(float deltaTime)
    {
        if (CurrentClip == null || IsFinished) return;

        FrameTimer += deltaTime;
        float frameDuration = 1f / CurrentClip.FrameRate;

        while (FrameTimer >= frameDuration)
        {
            FrameTimer -= frameDuration;
            CurrentFrame++;

            if (CurrentFrame >= CurrentClip.FrameCount)
            {
                if (CurrentClip.Loop)
                {
                    CurrentFrame = 0;
                }
                else
                {
                    CurrentFrame = CurrentClip.FrameCount - 1;
                    IsFinished = true;
                    return;
                }
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 position, Color tint, SpriteEffects effects = SpriteEffects.None, float scale = 1f)
    {
        if (CurrentClip == null || SpriteSheet.Texture == null) return;

        Rectangle sourceRect = CurrentClip.GetSourceRect(CurrentFrame, SpriteSheet.FrameWidth, SpriteSheet.FrameHeight);
        Vector2 origin = new Vector2(SpriteSheet.FrameWidth / 2f, SpriteSheet.FrameHeight / 2f);

        spriteBatch.Draw(
            SpriteSheet.Texture,
            position,
            sourceRect,
            tint,
            0f,
            origin,
            scale,
            effects,
            0f
        );
    }

    public void Reset()
    {
        CurrentFrame = 0;
        FrameTimer = 0f;
        IsFinished = false;
    }
}
