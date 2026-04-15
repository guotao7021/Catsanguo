using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace CatSanguo.Core.Animation;

public class SpriteSheetManager
{
    private readonly Dictionary<string, SpriteSheet> _sheets = new();
    public bool IsLoaded { get; private set; }

    private static readonly string[] KnownUnitTypes =
    {
        "soldier_vanguard",
        "soldier_archer",
        "soldier_cavalry",
        "general_default"
    };

    private const int GridColumns = 4;
    private const int GridRows = 4;

    public void LoadAll(GraphicsDevice graphicsDevice, string spritesRootPath)
    {
        var defaultClips = CreateDefaultClips();

        foreach (string unitType in KnownUnitTypes)
        {
            string filePath = Path.Combine(spritesRootPath, $"{unitType}.png");
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"[SpriteSheetManager] Sprite not found: {filePath}");
                continue;
            }

            try
            {
                using var stream = File.OpenRead(filePath);
                var texture = Texture2D.FromStream(graphicsDevice, stream);

                // Auto-detect frame size from texture dimensions
                int frameWidth = texture.Width / GridColumns;
                int frameHeight = texture.Height / GridRows;

                var sheet = new SpriteSheet(texture, frameWidth, frameHeight, new Dictionary<string, AnimationClip>(defaultClips));
                _sheets[unitType] = sheet;
                Debug.WriteLine($"[SpriteSheetManager] Loaded: {unitType} ({texture.Width}x{texture.Height}, frame: {frameWidth}x{frameHeight})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpriteSheetManager] Failed to load {filePath}: {ex.Message}");
            }
        }

        IsLoaded = true;
    }

    public Animator? CreateAnimator(string unitType)
    {
        if (!_sheets.TryGetValue(unitType, out var sheet)) return null;
        var animator = new Animator(sheet);
        animator.Play("Idle");
        return animator;
    }

    public SpriteSheet? GetSheet(string unitType)
    {
        return _sheets.TryGetValue(unitType, out var sheet) ? sheet : null;
    }

    private static Dictionary<string, AnimationClip> CreateDefaultClips()
    {
        return new Dictionary<string, AnimationClip>
        {
            ["Idle"]   = new AnimationClip("Idle",   0, 4, 6f,  true),
            ["Walk"]   = new AnimationClip("Walk",   1, 4, 10f, true),
            ["Attack"] = new AnimationClip("Attack", 2, 4, 12f, false),
            ["Death"]  = new AnimationClip("Death",  3, 4, 8f,  false),
        };
    }
}
