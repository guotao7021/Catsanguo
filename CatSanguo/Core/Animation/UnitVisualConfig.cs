using Microsoft.Xna.Framework;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Core.Animation;

// ==================== 颜色调色板 ====================

public static class SangoPalette
{
    // 皮肤
    public static readonly Color Skin = new(212, 165, 116);
    public static readonly Color SkinDark = new(180, 130, 90);

    // 盔甲 (中性暖色 - 便于阵营色调叠加)
    public static readonly Color ArmorLight = new(180, 165, 140);
    public static readonly Color ArmorBase = new(140, 125, 105);
    public static readonly Color ArmorDark = new(100, 85, 70);

    // 皮革
    public static readonly Color Leather = new(150, 110, 70);
    public static readonly Color LeatherDark = new(110, 80, 50);

    // 布料
    public static readonly Color ClothLight = new(170, 155, 130);
    public static readonly Color ClothBase = new(130, 115, 95);

    // 马匹
    public static readonly Color Horse = new(139, 105, 20);
    public static readonly Color HorseDark = new(100, 72, 14);
    public static readonly Color HorseLight = new(170, 135, 50);

    // 武器金属
    public static readonly Color WeaponMetal = new(200, 200, 210);
    public static readonly Color WeaponMetalDark = new(140, 140, 155);
    public static readonly Color WeaponWood = new(120, 85, 50);

    // 头发
    public static readonly Color Hair = new(50, 40, 30);

    // 将军专用
    public static readonly Color CapeBase = new(160, 140, 110);
    public static readonly Color PlumeTip = new(220, 60, 60);
    public static readonly Color GoldTrim = new(220, 190, 80);

    // 法师专用
    public static readonly Color MageRobe = new(90, 75, 120);
    public static readonly Color MageOrbGlow = new(106, 197, 255);

    // 盾牌
    public static readonly Color ShieldFace = new(170, 155, 130);
    public static readonly Color ShieldRim = new(120, 105, 85);
}

// ==================== 士兵视觉配置 ====================

public enum HelmetStyle { Round, Pointed, Flat, Hood, WizardHat, None }
public enum WeaponShape { Sword, Spear, Mace, Saber, Bow, Crossbow, Staff, Lance, None }

public struct SoldierVisual
{
    public int BodyW;
    public int BodyH;
    public HelmetStyle Helmet;
    public WeaponShape Weapon;
    public bool HasShield;
    public bool HasHorse;
    public bool HasQuiver;
    public bool HasCape;
    public bool IsRobed;       // 法师宽袍
    public bool HasSiegeRam;
    public Color ArmorColor;
    public Color ArmorDarkColor;
    public int WeaponLength;
}

public struct GeneralVisual
{
    public int BodyW;
    public int BodyH;
    public int PlumeHeight;
    public int CapeW;
    public int CapeH;
    public int WeaponLength;
}

// ==================== 配置表 ====================

public static class UnitVisualConfig
{
    public static SoldierVisual GetSoldierConfig(UnitType type)
    {
        return type switch
        {
            UnitType.Infantry => new SoldierVisual
            {
                BodyW = 6, BodyH = 7, Helmet = HelmetStyle.Round,
                Weapon = WeaponShape.Sword, WeaponLength = 8,
                ArmorColor = SangoPalette.ArmorBase, ArmorDarkColor = SangoPalette.ArmorDark
            },
            UnitType.Spearman => new SoldierVisual
            {
                BodyW = 6, BodyH = 7, Helmet = HelmetStyle.Pointed,
                Weapon = WeaponShape.Spear, WeaponLength = 14,
                ArmorColor = SangoPalette.ArmorBase, ArmorDarkColor = SangoPalette.ArmorDark
            },
            UnitType.ShieldInfantry => new SoldierVisual
            {
                BodyW = 6, BodyH = 7, Helmet = HelmetStyle.Round,
                Weapon = WeaponShape.Mace, WeaponLength = 5,
                HasShield = true,
                ArmorColor = SangoPalette.ArmorLight, ArmorDarkColor = SangoPalette.ArmorBase
            },
            UnitType.Cavalry => new SoldierVisual
            {
                BodyW = 6, BodyH = 6, Helmet = HelmetStyle.Round,
                Weapon = WeaponShape.Sword, WeaponLength = 7,
                HasHorse = true,
                ArmorColor = SangoPalette.ArmorBase, ArmorDarkColor = SangoPalette.ArmorDark
            },
            UnitType.HeavyCavalry => new SoldierVisual
            {
                BodyW = 7, BodyH = 6, Helmet = HelmetStyle.Flat,
                Weapon = WeaponShape.Lance, WeaponLength = 12,
                HasHorse = true,
                ArmorColor = SangoPalette.ArmorLight, ArmorDarkColor = SangoPalette.ArmorBase
            },
            UnitType.LightCavalry => new SoldierVisual
            {
                BodyW = 5, BodyH = 6, Helmet = HelmetStyle.None,
                Weapon = WeaponShape.Saber, WeaponLength = 6,
                HasHorse = true,
                ArmorColor = SangoPalette.Leather, ArmorDarkColor = SangoPalette.LeatherDark
            },
            UnitType.Archer => new SoldierVisual
            {
                BodyW = 5, BodyH = 7, Helmet = HelmetStyle.None,
                Weapon = WeaponShape.Bow, WeaponLength = 8,
                HasQuiver = true,
                ArmorColor = SangoPalette.Leather, ArmorDarkColor = SangoPalette.LeatherDark
            },
            UnitType.Crossbowman => new SoldierVisual
            {
                BodyW = 6, BodyH = 7, Helmet = HelmetStyle.Round,
                Weapon = WeaponShape.Crossbow, WeaponLength = 5,
                ArmorColor = SangoPalette.ArmorBase, ArmorDarkColor = SangoPalette.ArmorDark
            },
            UnitType.Siege => new SoldierVisual
            {
                BodyW = 6, BodyH = 7, Helmet = HelmetStyle.None,
                Weapon = WeaponShape.None, WeaponLength = 0,
                HasSiegeRam = true,
                ArmorColor = SangoPalette.ClothBase, ArmorDarkColor = SangoPalette.ClothLight
            },
            UnitType.Mage => new SoldierVisual
            {
                BodyW = 7, BodyH = 8, Helmet = HelmetStyle.WizardHat,
                Weapon = WeaponShape.Staff, WeaponLength = 12,
                IsRobed = true,
                ArmorColor = SangoPalette.MageRobe, ArmorDarkColor = PixelArtBuilder.Darken(SangoPalette.MageRobe, 0.7f)
            },
            _ => new SoldierVisual
            {
                BodyW = 6, BodyH = 7, Helmet = HelmetStyle.Round,
                Weapon = WeaponShape.Sword, WeaponLength = 8,
                ArmorColor = SangoPalette.ArmorBase, ArmorDarkColor = SangoPalette.ArmorDark
            }
        };
    }

    public static GeneralVisual GetGeneralConfig()
    {
        return new GeneralVisual
        {
            BodyW = 10, BodyH = 12,
            PlumeHeight = 4,
            CapeW = 8, CapeH = 8,
            WeaponLength = 14
        };
    }
}
