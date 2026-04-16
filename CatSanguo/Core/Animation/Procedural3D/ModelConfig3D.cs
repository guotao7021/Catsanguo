using System.Collections.Generic;
using Microsoft.Xna.Framework;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Core.Animation.Procedural3D;

/// <summary>
/// 10兵种+武将的3D模型零件树构建器
/// 复用 UnitVisualConfig/SangoPalette 的颜色和装备标志
/// </summary>
public static class ModelConfig3D
{
    // 士兵基准3D尺寸(世界单位，渲染时相机调整使其适合64x64帧)
    private const float SBodyW = 0.30f;
    private const float SBodyH = 0.40f;
    private const float SBodyD = 0.20f;
    private const float SHeadR = 0.13f;
    private const float SArmR = 0.05f;
    private const float SArmH = 0.30f;
    private const float SLegR = 0.06f;
    private const float SLegH = 0.30f;
    private const float SBootH = 0.08f;

    // 武将尺寸(比士兵大~1.3倍)
    private const float GBodyW = 0.38f;
    private const float GBodyH = 0.50f;
    private const float GBodyD = 0.25f;
    private const float GHeadR = 0.16f;
    private const float GArmR = 0.06f;
    private const float GArmH = 0.35f;
    private const float GLegR = 0.07f;
    private const float GLegH = 0.35f;

    public static CharacterPart BuildSoldierModel(UnitType type)
    {
        var cfg = UnitVisualConfig.GetSoldierConfig(type);
        float bodyW = SBodyW * (cfg.BodyW / 6f);
        float bodyH = SBodyH * (cfg.BodyH / 7f);

        // 根节点 = 躯干
        var torso = new CharacterPart
        {
            Type = PrimitiveType.Box, Tag = "torso",
            Size = new Vector3(bodyW, bodyH, SBodyD),
            DiffuseColor = cfg.ArmorColor,
            LocalTransform = Matrix.Identity
        };

        // 头部
        var head = new CharacterPart
        {
            Type = PrimitiveType.Sphere, Tag = "head",
            Size = new Vector3(SHeadR, 0, 0),
            DiffuseColor = SangoPalette.Skin,
            LocalTransform = Matrix.CreateTranslation(0, bodyH / 2 + SHeadR * 0.85f, 0)
        };
        torso.Children.Add(head);

        // 头盔
        AddHelmet(head, cfg.Helmet, SHeadR);

        // 右臂 + 武器
        var armR = new CharacterPart
        {
            Type = PrimitiveType.Cylinder, Tag = "arm_r",
            Size = new Vector3(SArmR, SArmH, 10),
            DiffuseColor = cfg.ArmorColor,
            LocalTransform = Matrix.CreateTranslation(bodyW / 2 + SArmR + 0.02f, bodyH / 4, 0)
        };
        torso.Children.Add(armR);
        AddWeapon(armR, cfg.Weapon, cfg.WeaponLength / 10f);

        // 左臂
        var armL = new CharacterPart
        {
            Type = PrimitiveType.Cylinder, Tag = "arm_l",
            Size = new Vector3(SArmR, SArmH, 10),
            DiffuseColor = cfg.ArmorColor,
            LocalTransform = Matrix.CreateTranslation(-(bodyW / 2 + SArmR + 0.02f), bodyH / 4, 0)
        };
        torso.Children.Add(armL);

        // 盾牌
        if (cfg.HasShield)
        {
            armL.Children.Add(new CharacterPart
            {
                Type = PrimitiveType.Box, Tag = "shield",
                Size = new Vector3(0.04f, 0.28f, 0.22f),
                DiffuseColor = SangoPalette.ShieldFace,
                LocalTransform = Matrix.CreateTranslation(-0.06f, -SArmH / 2 + 0.05f, 0)
            });
        }

        // 右腿
        var legR = new CharacterPart
        {
            Type = PrimitiveType.Cylinder, Tag = "leg_r",
            Size = new Vector3(SLegR, SLegH, 10),
            DiffuseColor = cfg.ArmorDarkColor,
            LocalTransform = Matrix.CreateTranslation(bodyW / 4, -bodyH / 2 - SLegH / 2, 0)
        };
        torso.Children.Add(legR);
        legR.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Box, Tag = "boot_r",
            Size = new Vector3(0.10f, SBootH, 0.14f),
            DiffuseColor = SangoPalette.LeatherDark,
            LocalTransform = Matrix.CreateTranslation(0, -SLegH / 2 - SBootH / 2, 0.02f)
        });

        // 左腿
        var legL = new CharacterPart
        {
            Type = PrimitiveType.Cylinder, Tag = "leg_l",
            Size = new Vector3(SLegR, SLegH, 10),
            DiffuseColor = cfg.ArmorDarkColor,
            LocalTransform = Matrix.CreateTranslation(-bodyW / 4, -bodyH / 2 - SLegH / 2, 0)
        };
        torso.Children.Add(legL);
        legL.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Box, Tag = "boot_l",
            Size = new Vector3(0.10f, SBootH, 0.14f),
            DiffuseColor = SangoPalette.LeatherDark,
            LocalTransform = Matrix.CreateTranslation(0, -SLegH / 2 - SBootH / 2, 0.02f)
        });

        // 法师宽袍
        if (cfg.IsRobed)
        {
            torso.Children.Add(new CharacterPart
            {
                Type = PrimitiveType.Box, Tag = "robe",
                Size = new Vector3(bodyW + 0.06f, bodyH * 0.6f, SBodyD + 0.04f),
                DiffuseColor = SangoPalette.MageRobe,
                LocalTransform = Matrix.CreateTranslation(0, -bodyH * 0.25f, 0)
            });
        }

        // 箭壶
        if (cfg.HasQuiver)
        {
            torso.Children.Add(new CharacterPart
            {
                Type = PrimitiveType.Cylinder, Tag = "quiver",
                Size = new Vector3(0.04f, 0.25f, 8),
                DiffuseColor = SangoPalette.Leather,
                LocalTransform = Matrix.CreateRotationZ(MathHelper.ToRadians(10))
                    * Matrix.CreateTranslation(-bodyW / 2 + 0.02f, bodyH / 4, -SBodyD / 2 - 0.02f)
            });
        }

        // 马匹
        if (cfg.HasHorse)
        {
            var horse = BuildHorse(type == UnitType.HeavyCavalry ? 1.1f : type == UnitType.LightCavalry ? 0.85f : 1f);
            // 把士兵整体上移到马背高度
            torso.LocalTransform = Matrix.CreateTranslation(0, 0.35f, 0);
            // 马作为根的同级(用wrapper)
            var root = new CharacterPart
            {
                Type = PrimitiveType.Box, Tag = "mount_root",
                Size = Vector3.Zero,
                DiffuseColor = Color.Transparent,
                LocalTransform = Matrix.Identity
            };
            root.Children.Add(horse);
            root.Children.Add(torso);
            return root;
        }

        // 攻城车
        if (cfg.HasSiegeRam)
        {
            var siege = BuildSiegeRam();
            torso.LocalTransform = Matrix.CreateTranslation(-0.15f, 0.1f, 0);
            var root = new CharacterPart
            {
                Type = PrimitiveType.Box, Tag = "siege_root",
                Size = Vector3.Zero,
                DiffuseColor = Color.Transparent,
                LocalTransform = Matrix.Identity
            };
            root.Children.Add(siege);
            root.Children.Add(torso);
            return root;
        }

        return torso;
    }

    public static CharacterPart BuildGeneralModel()
    {
        var torso = new CharacterPart
        {
            Type = PrimitiveType.Box, Tag = "torso",
            Size = new Vector3(GBodyW, GBodyH, GBodyD),
            DiffuseColor = SangoPalette.ArmorLight,
            LocalTransform = Matrix.Identity
        };

        // 头
        var head = new CharacterPart
        {
            Type = PrimitiveType.Sphere, Tag = "head",
            Size = new Vector3(GHeadR, 0, 0),
            DiffuseColor = SangoPalette.Skin,
            LocalTransform = Matrix.CreateTranslation(0, GBodyH / 2 + GHeadR * 0.85f, 0)
        };
        torso.Children.Add(head);

        // 武将头盔 - 尖盔+盔缨
        AddHelmet(head, HelmetStyle.Pointed, GHeadR);
        head.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Cone, Tag = "plume",
            Size = new Vector3(0.04f, 0.18f, 6),
            DiffuseColor = SangoPalette.PlumeTip,
            LocalTransform = Matrix.CreateTranslation(0, GHeadR + 0.18f, -0.02f)
        });

        // 肩甲
        torso.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Sphere, Tag = "shoulder_r",
            Size = new Vector3(0.08f, 0, 0),
            DiffuseColor = SangoPalette.GoldTrim,
            LocalTransform = Matrix.CreateTranslation(GBodyW / 2 + 0.04f, GBodyH / 2 - 0.04f, 0)
        });
        torso.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Sphere, Tag = "shoulder_l",
            Size = new Vector3(0.08f, 0, 0),
            DiffuseColor = SangoPalette.GoldTrim,
            LocalTransform = Matrix.CreateTranslation(-(GBodyW / 2 + 0.04f), GBodyH / 2 - 0.04f, 0)
        });

        // 腰带
        torso.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Box, Tag = "belt",
            Size = new Vector3(GBodyW + 0.02f, 0.05f, GBodyD + 0.02f),
            DiffuseColor = SangoPalette.GoldTrim,
            LocalTransform = Matrix.CreateTranslation(0, -GBodyH / 6, 0)
        });

        // 右臂 + 大刀
        var armR = new CharacterPart
        {
            Type = PrimitiveType.Cylinder, Tag = "arm_r",
            Size = new Vector3(GArmR, GArmH, 10),
            DiffuseColor = SangoPalette.ArmorBase,
            LocalTransform = Matrix.CreateTranslation(GBodyW / 2 + GArmR + 0.03f, GBodyH / 4, 0)
        };
        torso.Children.Add(armR);
        // 武将大刀
        armR.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Box, Tag = "weapon",
            Size = new Vector3(0.04f, 0.55f, 0.02f),
            DiffuseColor = SangoPalette.WeaponMetal,
            LocalTransform = Matrix.CreateTranslation(0, -GArmH / 2 - 0.20f, 0)
        });

        // 左臂
        torso.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Cylinder, Tag = "arm_l",
            Size = new Vector3(GArmR, GArmH, 10),
            DiffuseColor = SangoPalette.ArmorBase,
            LocalTransform = Matrix.CreateTranslation(-(GBodyW / 2 + GArmR + 0.03f), GBodyH / 4, 0)
        });

        // 腿
        torso.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Cylinder, Tag = "leg_r",
            Size = new Vector3(GLegR, GLegH, 10),
            DiffuseColor = SangoPalette.ArmorDark,
            LocalTransform = Matrix.CreateTranslation(GBodyW / 4, -GBodyH / 2 - GLegH / 2, 0)
        });
        torso.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Cylinder, Tag = "leg_l",
            Size = new Vector3(GLegR, GLegH, 10),
            DiffuseColor = SangoPalette.ArmorDark,
            LocalTransform = Matrix.CreateTranslation(-GBodyW / 4, -GBodyH / 2 - GLegH / 2, 0)
        });

        // 披风
        torso.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Box, Tag = "cape",
            Size = new Vector3(GBodyW * 0.9f, GBodyH * 0.8f, 0.03f),
            DiffuseColor = SangoPalette.CapeBase,
            LocalTransform = Matrix.CreateTranslation(0, -GBodyH * 0.1f, -GBodyD / 2 - 0.02f)
        });

        return torso;
    }

    private static void AddHelmet(CharacterPart head, HelmetStyle style, float headR)
    {
        switch (style)
        {
            case HelmetStyle.Round:
                head.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Sphere, Tag = "helmet",
                    Size = new Vector3(headR + 0.03f, 0, 0),
                    DiffuseColor = SangoPalette.ArmorLight,
                    LocalTransform = Matrix.CreateTranslation(0, headR * 0.15f, 0)
                });
                break;
            case HelmetStyle.Pointed:
                head.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Sphere, Tag = "helmet_base",
                    Size = new Vector3(headR + 0.02f, 0, 0),
                    DiffuseColor = SangoPalette.ArmorLight,
                    LocalTransform = Matrix.CreateTranslation(0, headR * 0.15f, 0)
                });
                head.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Cone, Tag = "helmet_spike",
                    Size = new Vector3(0.04f, 0.10f, 6),
                    DiffuseColor = SangoPalette.WeaponMetal,
                    LocalTransform = Matrix.CreateTranslation(0, headR + 0.05f, 0)
                });
                break;
            case HelmetStyle.Flat:
                head.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Box, Tag = "helmet",
                    Size = new Vector3(headR * 2.2f, headR * 0.7f, headR * 2.2f),
                    DiffuseColor = SangoPalette.ArmorLight,
                    LocalTransform = Matrix.CreateTranslation(0, headR * 0.5f, 0)
                });
                break;
            case HelmetStyle.WizardHat:
                head.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Cone, Tag = "helmet",
                    Size = new Vector3(headR + 0.02f, 0.20f, 8),
                    DiffuseColor = SangoPalette.MageRobe,
                    LocalTransform = Matrix.CreateTranslation(0, headR * 0.5f, 0)
                });
                break;
            case HelmetStyle.Hood:
                head.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Sphere, Tag = "helmet",
                    Size = new Vector3(headR + 0.04f, 0, 0),
                    DiffuseColor = SangoPalette.ClothBase,
                    LocalTransform = Matrix.CreateTranslation(0, headR * 0.1f, -0.02f)
                });
                break;
            case HelmetStyle.None:
                // 显示头发
                head.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Sphere, Tag = "hair",
                    Size = new Vector3(headR + 0.01f, 0, 0),
                    DiffuseColor = SangoPalette.Hair,
                    LocalTransform = Matrix.CreateTranslation(0, headR * 0.25f, -0.01f)
                });
                break;
        }
    }

    private static void AddWeapon(CharacterPart arm, WeaponShape shape, float length)
    {
        switch (shape)
        {
            case WeaponShape.Sword:
                arm.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Box, Tag = "weapon",
                    Size = new Vector3(0.03f, length * 0.4f, 0.01f),
                    DiffuseColor = SangoPalette.WeaponMetal,
                    LocalTransform = Matrix.CreateTranslation(0, -SArmH / 2 - length * 0.15f, 0)
                });
                break;
            case WeaponShape.Spear:
            case WeaponShape.Lance:
                arm.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Cylinder, Tag = "weapon_shaft",
                    Size = new Vector3(0.015f, length * 0.45f, 8),
                    DiffuseColor = SangoPalette.WeaponWood,
                    LocalTransform = Matrix.CreateTranslation(0, -SArmH / 2 - length * 0.15f, 0)
                });
                arm.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Cone, Tag = "weapon_tip",
                    Size = new Vector3(0.03f, 0.08f, 6),
                    DiffuseColor = SangoPalette.WeaponMetal,
                    LocalTransform = Matrix.CreateTranslation(0, -SArmH / 2 - length * 0.4f, 0)
                });
                break;
            case WeaponShape.Mace:
                arm.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Cylinder, Tag = "weapon_shaft",
                    Size = new Vector3(0.02f, length * 0.3f, 8),
                    DiffuseColor = SangoPalette.WeaponWood,
                    LocalTransform = Matrix.CreateTranslation(0, -SArmH / 2 - length * 0.1f, 0)
                });
                arm.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Sphere, Tag = "weapon_head",
                    Size = new Vector3(0.05f, 0, 0),
                    DiffuseColor = SangoPalette.WeaponMetal,
                    LocalTransform = Matrix.CreateTranslation(0, -SArmH / 2 - length * 0.3f, 0)
                });
                break;
            case WeaponShape.Saber:
                arm.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Box, Tag = "weapon",
                    Size = new Vector3(0.025f, length * 0.35f, 0.01f),
                    DiffuseColor = SangoPalette.WeaponMetal,
                    LocalTransform = Matrix.CreateRotationZ(MathHelper.ToRadians(8))
                        * Matrix.CreateTranslation(0.02f, -SArmH / 2 - length * 0.12f, 0)
                });
                break;
            case WeaponShape.Bow:
                // 弓体 - 用细圆柱弯曲表示
                arm.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Cylinder, Tag = "weapon_bow",
                    Size = new Vector3(0.012f, length * 0.4f, 8),
                    DiffuseColor = SangoPalette.WeaponWood,
                    LocalTransform = Matrix.CreateRotationZ(MathHelper.ToRadians(15))
                        * Matrix.CreateTranslation(0.04f, -SArmH / 4, 0)
                });
                break;
            case WeaponShape.Crossbow:
                arm.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Box, Tag = "weapon_stock",
                    Size = new Vector3(0.03f, 0.18f, 0.04f),
                    DiffuseColor = SangoPalette.WeaponWood,
                    LocalTransform = Matrix.CreateTranslation(0.02f, -SArmH / 3, 0.05f)
                });
                arm.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Box, Tag = "weapon_limb",
                    Size = new Vector3(0.25f, 0.015f, 0.02f),
                    DiffuseColor = SangoPalette.WeaponWood,
                    LocalTransform = Matrix.CreateTranslation(0.02f, -SArmH / 3 - 0.06f, 0.05f)
                });
                break;
            case WeaponShape.Staff:
                arm.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Cylinder, Tag = "weapon_shaft",
                    Size = new Vector3(0.015f, length * 0.5f, 8),
                    DiffuseColor = SangoPalette.WeaponWood,
                    LocalTransform = Matrix.CreateTranslation(0, -SArmH / 2 - length * 0.15f, 0)
                });
                arm.Children.Add(new CharacterPart
                {
                    Type = PrimitiveType.Sphere, Tag = "weapon_orb",
                    Size = new Vector3(0.04f, 0, 0),
                    DiffuseColor = SangoPalette.MageOrbGlow,
                    LocalTransform = Matrix.CreateTranslation(0, -SArmH / 2 - length * 0.42f, 0)
                });
                break;
        }
    }

    private static CharacterPart BuildHorse(float scale)
    {
        float s = scale;
        var horse = new CharacterPart
        {
            Type = PrimitiveType.Box, Tag = "horse_body",
            Size = new Vector3(0.55f * s, 0.25f * s, 0.25f * s),
            DiffuseColor = SangoPalette.Horse,
            LocalTransform = Matrix.CreateTranslation(0, 0.05f, 0)
        };

        // 马头
        horse.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Box, Tag = "horse_head",
            Size = new Vector3(0.12f * s, 0.18f * s, 0.14f * s),
            DiffuseColor = SangoPalette.HorseLight,
            LocalTransform = Matrix.CreateRotationZ(MathHelper.ToRadians(-20))
                * Matrix.CreateTranslation(0.32f * s, 0.15f * s, 0)
        });

        // 4条腿
        float legR = 0.035f * s, legH = 0.25f * s;
        float bodyHalfX = 0.20f * s, bodyHalfZ = 0.08f * s;
        string[] legTags = { "horse_leg_fr", "horse_leg_fl", "horse_leg_br", "horse_leg_bl" };
        float[] legX = { bodyHalfX, bodyHalfX, -bodyHalfX, -bodyHalfX };
        float[] legZ = { bodyHalfZ, -bodyHalfZ, bodyHalfZ, -bodyHalfZ };
        for (int i = 0; i < 4; i++)
        {
            horse.Children.Add(new CharacterPart
            {
                Type = PrimitiveType.Cylinder, Tag = legTags[i],
                Size = new Vector3(legR, legH, 8),
                DiffuseColor = SangoPalette.HorseDark,
                LocalTransform = Matrix.CreateTranslation(legX[i], -0.125f * s - legH / 2, legZ[i])
            });
        }

        // 马尾
        horse.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Cylinder, Tag = "horse_tail",
            Size = new Vector3(0.02f * s, 0.15f * s, 6),
            DiffuseColor = SangoPalette.Hair,
            LocalTransform = Matrix.CreateRotationZ(MathHelper.ToRadians(30))
                * Matrix.CreateTranslation(-0.30f * s, 0.08f * s, 0)
        });

        return horse;
    }

    private static CharacterPart BuildSiegeRam()
    {
        var frame = new CharacterPart
        {
            Type = PrimitiveType.Box, Tag = "siege_frame",
            Size = new Vector3(0.6f, 0.35f, 0.3f),
            DiffuseColor = SangoPalette.WeaponWood,
            LocalTransform = Matrix.CreateTranslation(0.15f, -0.15f, 0)
        };

        // 攻城槌
        frame.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Cylinder, Tag = "siege_ram",
            Size = new Vector3(0.05f, 0.45f, 8),
            DiffuseColor = SangoPalette.WeaponMetalDark,
            LocalTransform = Matrix.CreateRotationZ(MathHelper.ToRadians(90))
                * Matrix.CreateTranslation(0.15f, -0.05f, 0)
        });

        // 轮子
        frame.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Cylinder, Tag = "siege_wheel_r",
            Size = new Vector3(0.08f, 0.04f, 8),
            DiffuseColor = SangoPalette.LeatherDark,
            LocalTransform = Matrix.CreateRotationX(MathHelper.PiOver2)
                * Matrix.CreateTranslation(0.20f, -0.175f, 0.18f)
        });
        frame.Children.Add(new CharacterPart
        {
            Type = PrimitiveType.Cylinder, Tag = "siege_wheel_l",
            Size = new Vector3(0.08f, 0.04f, 8),
            DiffuseColor = SangoPalette.LeatherDark,
            LocalTransform = Matrix.CreateRotationX(MathHelper.PiOver2)
                * Matrix.CreateTranslation(0.20f, -0.175f, -0.18f)
        });

        return frame;
    }
}
