using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using CatSanguo.Data.Schemas;
using CatSanguo.Core.Animation.Procedural3D;

namespace CatSanguo.Core.Animation;

public class SpriteSheetManager
{
    private readonly Dictionary<string, SpriteSheet> _sheets = new();
    public bool IsLoaded { get; private set; }

    // 阴影和箭矢纹理 (启动时生成一次)
    public Texture2D? ShadowTexture { get; private set; }
    public Texture2D? ArrowTexture { get; private set; }

    // 扩展后的11种单位类型键
    private static readonly string[] KnownUnitTypes =
    {
        "soldier_infantry",
        "soldier_spearman",
        "soldier_shield",
        "soldier_cavalry",
        "soldier_heavy_cavalry",
        "soldier_light_cavalry",
        "soldier_archer",
        "soldier_crossbow",
        "soldier_siege",
        "soldier_mage",
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

            if (File.Exists(filePath))
            {
                // 优先从文件加载 (支持未来猫图替换)
                try
                {
                    using var stream = File.OpenRead(filePath);
                    var texture = Texture2D.FromStream(graphicsDevice, stream);
                    int frameWidth = texture.Width / GridColumns;
                    int frameHeight = texture.Height / GridRows;

                    var sheet = new SpriteSheet(texture, frameWidth, frameHeight, new Dictionary<string, AnimationClip>(defaultClips));
                    _sheets[unitType] = sheet;
                    Debug.WriteLine($"[SpriteSheetManager] Loaded from file: {unitType} ({texture.Width}x{texture.Height}, frame: {frameWidth}x{frameHeight})");
                    continue;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SpriteSheetManager] Failed to load {filePath}: {ex.Message}");
                }
            }

            // 文件不存在或加载失败 → 3D程序化生成（回退到2D像素画）
            try
            {
                Texture2D texture;
                int frameSize;

                if (unitType.StartsWith("general_"))
                {
                    texture = SpriteSheetRenderer3D.RenderGeneralSheet(graphicsDevice);
                    frameSize = 96;
                }
                else
                {
                    var ut = MapKeyToUnitType(unitType);
                    texture = SpriteSheetRenderer3D.RenderSoldierSheet(graphicsDevice, ut);
                    frameSize = 64;
                }

                var sheet = new SpriteSheet(texture, frameSize, frameSize, new Dictionary<string, AnimationClip>(defaultClips));
                _sheets[unitType] = sheet;
                Debug.WriteLine($"[SpriteSheetManager] Generated 3D: {unitType} (frame: {frameSize}x{frameSize})");
            }
            catch (Exception ex3d)
            {
                Debug.WriteLine($"[SpriteSheetManager] 3D failed for {unitType}: {ex3d.Message}, falling back to 2D");
                try
                {
                    Texture2D texture;
                    int frameSize;
                    if (unitType.StartsWith("general_"))
                    {
                        texture = ProceduralSpriteGenerator.GenerateGeneralSheet(graphicsDevice);
                        frameSize = 48;
                    }
                    else
                    {
                        var ut = MapKeyToUnitType(unitType);
                        texture = ProceduralSpriteGenerator.GenerateSoldierSheet(graphicsDevice, ut);
                        frameSize = 32;
                    }
                    var sheet = new SpriteSheet(texture, frameSize, frameSize, new Dictionary<string, AnimationClip>(defaultClips));
                    _sheets[unitType] = sheet;
                    Debug.WriteLine($"[SpriteSheetManager] Fallback 2D: {unitType} (frame: {frameSize}x{frameSize})");
                }
                catch (Exception ex2d)
                {
                    Debug.WriteLine($"[SpriteSheetManager] All generation failed for {unitType}: {ex2d.Message}");
                }
            }
        }

        // 生成工具纹理
        ShadowTexture = ProceduralSpriteGenerator.GenerateShadowTexture(graphicsDevice);
        ArrowTexture = ProceduralSpriteGenerator.GenerateArrowTexture(graphicsDevice);

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

    private static UnitType MapKeyToUnitType(string key)
    {
        return key switch
        {
            "soldier_infantry" => UnitType.Infantry,
            "soldier_spearman" => UnitType.Spearman,
            "soldier_shield" => UnitType.ShieldInfantry,
            "soldier_cavalry" => UnitType.Cavalry,
            "soldier_heavy_cavalry" => UnitType.HeavyCavalry,
            "soldier_light_cavalry" => UnitType.LightCavalry,
            "soldier_archer" => UnitType.Archer,
            "soldier_crossbow" => UnitType.Crossbowman,
            "soldier_siege" => UnitType.Siege,
            "soldier_mage" => UnitType.Mage,
            _ => UnitType.Infantry
        };
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
