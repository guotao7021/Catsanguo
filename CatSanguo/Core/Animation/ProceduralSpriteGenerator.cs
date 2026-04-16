using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Core.Animation;

/// <summary>
/// 程序化精灵图生成器 - 在启动时生成三国群英传2风格像素角色
/// 生成的Texture2D注入SpriteSheetManager，运行时通过Animator渲染
/// 当Content/Sprites/中放入同名PNG时，自动被覆盖（猫图替换路径）
/// </summary>
public static class ProceduralSpriteGenerator
{
    private const int SoldierFrameSize = 32;
    private const int GeneralFrameSize = 48;
    private const int GridCols = 4;
    private const int GridRows = 4;

    // ==================== 公共API ====================

    public static Texture2D GenerateSoldierSheet(GraphicsDevice gd, UnitType unitType)
    {
        int w = SoldierFrameSize * GridCols;  // 128
        int h = SoldierFrameSize * GridRows;  // 128
        var pixels = new Color[w * h];
        var cfg = UnitVisualConfig.GetSoldierConfig(unitType);

        // 4行动画 x 4帧
        for (int row = 0; row < GridRows; row++)
        {
            for (int col = 0; col < GridCols; col++)
            {
                int fx = col * SoldierFrameSize;
                int fy = row * SoldierFrameSize;
                DrawSoldierFrame(pixels, w, h, fx, fy, SoldierFrameSize, cfg, row, col);
            }
        }

        var tex = new Texture2D(gd, w, h);
        tex.SetData(pixels);
        return tex;
    }

    public static Texture2D GenerateGeneralSheet(GraphicsDevice gd)
    {
        int w = GeneralFrameSize * GridCols;  // 192
        int h = GeneralFrameSize * GridRows;  // 192
        var pixels = new Color[w * h];
        var cfg = UnitVisualConfig.GetGeneralConfig();

        for (int row = 0; row < GridRows; row++)
        {
            for (int col = 0; col < GridCols; col++)
            {
                int fx = col * GeneralFrameSize;
                int fy = row * GeneralFrameSize;
                DrawGeneralFrame(pixels, w, h, fx, fy, GeneralFrameSize, cfg, row, col);
            }
        }

        var tex = new Texture2D(gd, w, h);
        tex.SetData(pixels);
        return tex;
    }

    public static Texture2D GenerateShadowTexture(GraphicsDevice gd)
    {
        int w = 16, h = 8;
        var pixels = new Color[w * h];
        // 柔和椭圆阴影
        for (int dy = 0; dy < h; dy++)
        {
            for (int dx = 0; dx < w; dx++)
            {
                float ex = (dx - w / 2f) / (w / 2f);
                float ey = (dy - h / 2f) / (h / 2f);
                float dist = ex * ex + ey * ey;
                if (dist <= 1f)
                {
                    float alpha = (1f - dist) * 0.4f;
                    pixels[dy * w + dx] = new Color((byte)0, (byte)0, (byte)0, (byte)(alpha * 255));
                }
            }
        }
        var tex = new Texture2D(gd, w, h);
        tex.SetData(pixels);
        return tex;
    }

    public static Texture2D GenerateArrowTexture(GraphicsDevice gd)
    {
        int w = 12, h = 4;
        var pixels = new Color[w * h];
        var P = PixelArtBuilder.SetPixel;
        var F = PixelArtBuilder.FillRect;
        // 箭杆
        F(pixels, w, h, 0, 1, 8, 2, SangoPalette.WeaponWood);
        // 箭头
        P(pixels, w, h, 8, 0, SangoPalette.WeaponMetal);
        P(pixels, w, h, 8, 3, SangoPalette.WeaponMetal);
        P(pixels, w, h, 9, 1, SangoPalette.WeaponMetal);
        P(pixels, w, h, 9, 2, SangoPalette.WeaponMetal);
        P(pixels, w, h, 10, 1, SangoPalette.WeaponMetal);
        P(pixels, w, h, 10, 2, SangoPalette.WeaponMetal);
        P(pixels, w, h, 11, 1, SangoPalette.WeaponMetalDark);
        P(pixels, w, h, 11, 2, SangoPalette.WeaponMetalDark);
        // 尾羽
        P(pixels, w, h, 0, 0, SangoPalette.PlumeTip);
        P(pixels, w, h, 0, 3, SangoPalette.PlumeTip);

        var tex = new Texture2D(gd, w, h);
        tex.SetData(pixels);
        return tex;
    }

    // ==================== 士兵帧绘制 ====================

    private static void DrawSoldierFrame(Color[] px, int sw, int sh,
        int fx, int fy, int fs, SoldierVisual cfg, int animRow, int frame)
    {
        // 帧中心坐标
        int cx = fx + fs / 2;

        // 动画偏移
        int bobY = GetBobOffset(animRow, frame);
        int legOffset = GetLegOffset(animRow, frame);
        float weaponPhase = GetWeaponPhase(animRow, frame);

        // 基准Y坐标 (角色底部=帧底部-4px, 留空间给阴影)
        int baseY = fy + fs - 5;

        // 死亡动画: 渐渐倒下
        if (animRow == 3) // Death
        {
            DrawSoldierDeath(px, sw, sh, cx, baseY, cfg, frame);
            return;
        }

        // === 绘制顺序: 从后到前 ===

        // 1. 马匹 (骑兵)
        if (cfg.HasHorse)
        {
            DrawHorse(px, sw, sh, cx, baseY, animRow, frame);
            baseY -= 8; // 骑手在马背上
        }

        // 2. 攻城器械
        if (cfg.HasSiegeRam)
        {
            DrawSiegeRam(px, sw, sh, cx, baseY, animRow, frame);
        }

        // 3. 箭壶 (弓兵背后)
        if (cfg.HasQuiver)
        {
            PixelArtBuilder.FillRect(px, sw, sh, cx - 4, baseY - 14 + bobY, 2, 5, SangoPalette.LeatherDark);
        }

        // 4. 披风
        if (cfg.HasCape)
        {
            int capeX = cx - 3;
            int capeY = baseY - 12 + bobY;
            int sway = (frame % 2 == 0) ? 0 : -1;
            PixelArtBuilder.FillRect(px, sw, sh, capeX + sway, capeY, 3, 6, SangoPalette.CapeBase);
        }

        // 5. 腿部
        int leftLegX = cx - 2;
        int rightLegX = cx + 1;
        int leftLegOff = cfg.HasHorse ? 0 : legOffset;
        int rightLegOff = cfg.HasHorse ? 0 : -legOffset;

        if (!cfg.IsRobed && !cfg.HasHorse)
        {
            // 左腿
            PixelArtBuilder.FillRect(px, sw, sh, leftLegX, baseY - 5 + bobY + leftLegOff, 2, 5, SangoPalette.ArmorDark);
            // 右腿
            PixelArtBuilder.FillRect(px, sw, sh, rightLegX, baseY - 5 + bobY + rightLegOff, 2, 5, SangoPalette.ArmorDark);
            // 靴子
            PixelArtBuilder.FillRect(px, sw, sh, leftLegX - 1, baseY + bobY + leftLegOff, 3, 2, new Color(58, 46, 36));
            PixelArtBuilder.FillRect(px, sw, sh, rightLegX - 1, baseY + bobY + rightLegOff, 3, 2, new Color(58, 46, 36));
        }
        else if (cfg.IsRobed)
        {
            // 法袍下摆
            int robeBot = baseY + bobY;
            PixelArtBuilder.FillRect(px, sw, sh, cx - cfg.BodyW / 2 - 1, robeBot - 5, cfg.BodyW + 2, 5,
                PixelArtBuilder.Darken(cfg.ArmorColor, 0.8f));
        }

        // 6. 躯干
        int torsoX = cx - cfg.BodyW / 2;
        int torsoY = baseY - 5 - cfg.BodyH + bobY;
        PixelArtBuilder.FillRect(px, sw, sh, torsoX, torsoY, cfg.BodyW, cfg.BodyH, cfg.ArmorColor);
        // 躯干暗面 (右侧1列)
        PixelArtBuilder.FillRect(px, sw, sh, torsoX + cfg.BodyW - 1, torsoY, 1, cfg.BodyH, cfg.ArmorDarkColor);
        // 腰带
        PixelArtBuilder.FillRect(px, sw, sh, torsoX, torsoY + cfg.BodyH - 2, cfg.BodyW, 1, SangoPalette.LeatherDark);

        // 7. 盾牌
        if (cfg.HasShield)
        {
            int shX = cx - cfg.BodyW / 2 - 4;
            int shY = torsoY + 1;
            PixelArtBuilder.FillRect(px, sw, sh, shX, shY, 4, 6, SangoPalette.ShieldFace);
            PixelArtBuilder.DrawRect(px, sw, sh, shX, shY, 4, 6, SangoPalette.ShieldRim);
            // 盾面纹饰 (十字)
            PixelArtBuilder.SetPixel(px, sw, sh, shX + 2, shY + 2, SangoPalette.ShieldRim);
            PixelArtBuilder.SetPixel(px, sw, sh, shX + 1, shY + 3, SangoPalette.ShieldRim);
            PixelArtBuilder.SetPixel(px, sw, sh, shX + 2, shY + 3, SangoPalette.ShieldRim);
            PixelArtBuilder.SetPixel(px, sw, sh, shX + 3, shY + 3, SangoPalette.ShieldRim);
        }

        // 8. 手臂 + 武器
        DrawSoldierArms(px, sw, sh, cx, torsoY, cfg, weaponPhase, bobY);

        // 9. 头部
        int headY = torsoY - 4;
        PixelArtBuilder.FillEllipse(px, sw, sh, cx, headY, 2, 2, SangoPalette.Skin);
        // 头发
        PixelArtBuilder.SetPixel(px, sw, sh, cx - 1, headY - 2, SangoPalette.Hair);
        PixelArtBuilder.SetPixel(px, sw, sh, cx, headY - 2, SangoPalette.Hair);
        PixelArtBuilder.SetPixel(px, sw, sh, cx + 1, headY - 2, SangoPalette.Hair);

        // 10. 头盔
        DrawHelmet(px, sw, sh, cx, headY, cfg.Helmet);
    }

    private static void DrawSoldierArms(Color[] px, int sw, int sh,
        int cx, int torsoY, SoldierVisual cfg, float weaponPhase, int bobY)
    {
        // 武器侧手臂 (右侧)
        int armY = torsoY + 2;

        switch (cfg.Weapon)
        {
            case WeaponShape.Sword:
            {
                int wpX = cx + cfg.BodyW / 2;
                int wpY = armY;
                // 手臂
                PixelArtBuilder.FillRect(px, sw, sh, wpX, armY, 2, 3, SangoPalette.Skin);
                // 剑 (随攻击相位旋转)
                int swordLen = cfg.WeaponLength;
                if (weaponPhase < 0.3f) // 待机/蓄力
                {
                    PixelArtBuilder.FillRect(px, sw, sh, wpX + 1, wpY - swordLen + 3, 1, swordLen, SangoPalette.WeaponMetal);
                    PixelArtBuilder.SetPixel(px, sw, sh, wpX + 1, wpY - swordLen + 3, SangoPalette.WeaponMetalDark);
                }
                else if (weaponPhase < 0.7f) // 挥砍 (水平)
                {
                    PixelArtBuilder.FillRect(px, sw, sh, wpX + 1, wpY, swordLen, 1, SangoPalette.WeaponMetal);
                }
                else // 回收
                {
                    PixelArtBuilder.FillRect(px, sw, sh, wpX + 1, wpY - swordLen / 2, 1, swordLen / 2, SangoPalette.WeaponMetal);
                }
                break;
            }
            case WeaponShape.Spear:
            {
                int wpX = cx + cfg.BodyW / 2;
                // 手臂
                PixelArtBuilder.FillRect(px, sw, sh, wpX, armY, 2, 3, SangoPalette.Skin);
                int spearLen = cfg.WeaponLength;
                if (weaponPhase < 0.5f) // 持枪
                {
                    int spY = armY - spearLen + 2;
                    PixelArtBuilder.DrawLine(px, sw, sh, wpX + 1, spY, wpX + 1, armY, SangoPalette.WeaponWood);
                    // 枪尖
                    PixelArtBuilder.SetPixel(px, sw, sh, wpX, spY, SangoPalette.WeaponMetal);
                    PixelArtBuilder.SetPixel(px, sw, sh, wpX + 1, spY - 1, SangoPalette.WeaponMetal);
                    PixelArtBuilder.SetPixel(px, sw, sh, wpX + 2, spY, SangoPalette.WeaponMetal);
                }
                else // 刺出
                {
                    int spearEndX = wpX + spearLen;
                    PixelArtBuilder.DrawLine(px, sw, sh, wpX + 1, armY + 1, spearEndX, armY + 1, SangoPalette.WeaponWood);
                    PixelArtBuilder.SetPixel(px, sw, sh, spearEndX, armY, SangoPalette.WeaponMetal);
                    PixelArtBuilder.SetPixel(px, sw, sh, spearEndX + 1, armY + 1, SangoPalette.WeaponMetal);
                    PixelArtBuilder.SetPixel(px, sw, sh, spearEndX, armY + 2, SangoPalette.WeaponMetal);
                }
                break;
            }
            case WeaponShape.Mace:
            {
                int wpX = cx + cfg.BodyW / 2;
                PixelArtBuilder.FillRect(px, sw, sh, wpX, armY, 2, 3, SangoPalette.Skin);
                if (weaponPhase < 0.5f)
                {
                    PixelArtBuilder.FillRect(px, sw, sh, wpX + 1, armY - 4, 1, 5, SangoPalette.WeaponWood);
                    PixelArtBuilder.FillRect(px, sw, sh, wpX, armY - 5, 3, 2, SangoPalette.WeaponMetal);
                }
                else
                {
                    PixelArtBuilder.FillRect(px, sw, sh, wpX + 1, armY, 5, 1, SangoPalette.WeaponWood);
                    PixelArtBuilder.FillRect(px, sw, sh, wpX + 5, armY - 1, 2, 3, SangoPalette.WeaponMetal);
                }
                break;
            }
            case WeaponShape.Lance:
            {
                int wpX = cx + cfg.BodyW / 2;
                PixelArtBuilder.FillRect(px, sw, sh, wpX, armY, 2, 3, SangoPalette.Skin);
                int lanceLen = cfg.WeaponLength;
                if (weaponPhase < 0.5f) // 架枪
                {
                    PixelArtBuilder.DrawLine(px, sw, sh, wpX + 1, armY - 2, wpX + lanceLen, armY - 5, SangoPalette.WeaponWood);
                    PixelArtBuilder.SetPixel(px, sw, sh, wpX + lanceLen, armY - 6, SangoPalette.WeaponMetal);
                    PixelArtBuilder.SetPixel(px, sw, sh, wpX + lanceLen + 1, armY - 5, SangoPalette.WeaponMetal);
                }
                else // 冲刺
                {
                    PixelArtBuilder.DrawLine(px, sw, sh, wpX + 1, armY + 1, wpX + lanceLen, armY + 1, SangoPalette.WeaponWood);
                    PixelArtBuilder.SetPixel(px, sw, sh, wpX + lanceLen, armY, SangoPalette.WeaponMetal);
                    PixelArtBuilder.SetPixel(px, sw, sh, wpX + lanceLen + 1, armY + 1, SangoPalette.WeaponMetal);
                }
                break;
            }
            case WeaponShape.Saber:
            {
                int wpX = cx + cfg.BodyW / 2;
                PixelArtBuilder.FillRect(px, sw, sh, wpX, armY, 2, 3, SangoPalette.Skin);
                if (weaponPhase < 0.5f)
                {
                    // 弯刀朝上 (轻微弧度用2段线近似)
                    PixelArtBuilder.DrawLine(px, sw, sh, wpX + 1, armY + 2, wpX + 3, armY - 3, SangoPalette.WeaponMetal);
                    PixelArtBuilder.DrawLine(px, sw, sh, wpX + 3, armY - 3, wpX + 4, armY - 5, SangoPalette.WeaponMetalDark);
                }
                else
                {
                    PixelArtBuilder.DrawLine(px, sw, sh, wpX + 1, armY + 1, wpX + 6, armY - 1, SangoPalette.WeaponMetal);
                }
                break;
            }
            case WeaponShape.Bow:
            {
                // 弓在左手
                int bowX = cx - cfg.BodyW / 2 - 3;
                int bowY = armY - 1;
                PixelArtBuilder.FillRect(px, sw, sh, cx - cfg.BodyW / 2 - 1, armY, 2, 3, SangoPalette.Skin);
                // 弓身 (弧形用多段线近似)
                PixelArtBuilder.DrawLine(px, sw, sh, bowX, bowY, bowX - 1, bowY - 3, SangoPalette.WeaponWood);
                PixelArtBuilder.DrawLine(px, sw, sh, bowX, bowY, bowX - 1, bowY + 4, SangoPalette.WeaponWood);
                // 弓弦
                PixelArtBuilder.DrawLine(px, sw, sh, bowX - 1, bowY - 3, bowX - 1, bowY + 4, new Color(200, 180, 150));
                // 右手 (拉弦/搭箭)
                PixelArtBuilder.FillRect(px, sw, sh, cx + cfg.BodyW / 2, armY + 1, 2, 2, SangoPalette.Skin);
                if (weaponPhase >= 0.3f && weaponPhase < 0.8f) // 射击时
                {
                    // 箭
                    PixelArtBuilder.DrawLine(px, sw, sh, bowX - 1, bowY + 1, bowX - 6, bowY + 1, SangoPalette.WeaponWood);
                    PixelArtBuilder.SetPixel(px, sw, sh, bowX - 6, bowY, SangoPalette.WeaponMetal);
                    PixelArtBuilder.SetPixel(px, sw, sh, bowX - 6, bowY + 2, SangoPalette.WeaponMetal);
                }
                break;
            }
            case WeaponShape.Crossbow:
            {
                int wpX = cx + cfg.BodyW / 2;
                PixelArtBuilder.FillRect(px, sw, sh, wpX, armY, 2, 3, SangoPalette.Skin);
                // 弩身
                int cX = wpX + 2;
                int cY = armY + 1;
                PixelArtBuilder.FillRect(px, sw, sh, cX, cY, 5, 2, SangoPalette.WeaponWood);
                // 弩臂
                PixelArtBuilder.DrawLine(px, sw, sh, cX + 4, cY - 2, cX + 4, cY + 3, SangoPalette.WeaponWood);
                // 弦
                PixelArtBuilder.DrawLine(px, sw, sh, cX + 4, cY - 2, cX + 2, cY, new Color(200, 180, 150));
                PixelArtBuilder.DrawLine(px, sw, sh, cX + 4, cY + 3, cX + 2, cY + 1, new Color(200, 180, 150));
                if (weaponPhase >= 0.3f && weaponPhase < 0.8f) // 射击
                {
                    PixelArtBuilder.SetPixel(px, sw, sh, cX + 5, cY, SangoPalette.WeaponMetal);
                    PixelArtBuilder.SetPixel(px, sw, sh, cX + 5, cY + 1, SangoPalette.WeaponMetal);
                }
                break;
            }
            case WeaponShape.Staff:
            {
                int wpX = cx + cfg.BodyW / 2;
                PixelArtBuilder.FillRect(px, sw, sh, wpX, armY, 2, 3, SangoPalette.Skin);
                int staffTop = armY - cfg.WeaponLength + 2;
                PixelArtBuilder.DrawLine(px, sw, sh, wpX + 1, staffTop, wpX + 1, armY + 2, SangoPalette.WeaponWood);
                // 法球
                PixelArtBuilder.FillEllipse(px, sw, sh, wpX + 1, staffTop - 1, 2, 2, SangoPalette.MageOrbGlow);
                if (weaponPhase >= 0.3f && weaponPhase < 0.8f) // 施法
                {
                    // 发光效果
                    PixelArtBuilder.SetPixelBlend(px, sw, sh, wpX - 1, staffTop - 2,
                        new Color(106, 197, 255, 100));
                    PixelArtBuilder.SetPixelBlend(px, sw, sh, wpX + 3, staffTop - 2,
                        new Color(106, 197, 255, 100));
                }
                break;
            }
            case WeaponShape.None:
            {
                // 攻城兵 - 双手推
                PixelArtBuilder.FillRect(px, sw, sh, cx + cfg.BodyW / 2, armY + 1, 3, 2, SangoPalette.Skin);
                PixelArtBuilder.FillRect(px, sw, sh, cx - cfg.BodyW / 2 - 2, armY + 1, 3, 2, SangoPalette.Skin);
                break;
            }
        }
    }

    // ==================== 装饰元素 ====================

    private static void DrawHelmet(Color[] px, int sw, int sh, int cx, int headY, HelmetStyle style)
    {
        switch (style)
        {
            case HelmetStyle.Round:
                PixelArtBuilder.FillRect(px, sw, sh, cx - 2, headY - 3, 5, 2, SangoPalette.ArmorBase);
                PixelArtBuilder.SetPixel(px, sw, sh, cx, headY - 4, SangoPalette.ArmorDark);
                break;
            case HelmetStyle.Pointed:
                PixelArtBuilder.FillRect(px, sw, sh, cx - 2, headY - 3, 5, 2, SangoPalette.ArmorBase);
                PixelArtBuilder.SetPixel(px, sw, sh, cx, headY - 4, SangoPalette.ArmorDark);
                PixelArtBuilder.SetPixel(px, sw, sh, cx, headY - 5, SangoPalette.WeaponMetal);
                break;
            case HelmetStyle.Flat:
                PixelArtBuilder.FillRect(px, sw, sh, cx - 3, headY - 3, 7, 1, SangoPalette.ArmorLight);
                PixelArtBuilder.FillRect(px, sw, sh, cx - 2, headY - 4, 5, 1, SangoPalette.ArmorBase);
                break;
            case HelmetStyle.WizardHat:
                PixelArtBuilder.FillRect(px, sw, sh, cx - 3, headY - 3, 7, 1, SangoPalette.MageRobe);
                PixelArtBuilder.FillRect(px, sw, sh, cx - 2, headY - 4, 5, 1, SangoPalette.MageRobe);
                PixelArtBuilder.FillRect(px, sw, sh, cx - 1, headY - 5, 3, 1, SangoPalette.MageRobe);
                PixelArtBuilder.SetPixel(px, sw, sh, cx, headY - 6, SangoPalette.MageRobe);
                PixelArtBuilder.SetPixel(px, sw, sh, cx, headY - 7, SangoPalette.MageOrbGlow);
                break;
            case HelmetStyle.Hood:
                PixelArtBuilder.FillRect(px, sw, sh, cx - 2, headY - 3, 5, 2, SangoPalette.Leather);
                break;
            case HelmetStyle.None:
                // 只有头发
                break;
        }
    }

    private static void DrawHorse(Color[] px, int sw, int sh, int cx, int baseY, int animRow, int frame)
    {
        int horseY = baseY - 4;
        int legOff = (animRow == 1) ? GetLegOffset(1, frame) : 0;

        // 马身
        PixelArtBuilder.FillEllipse(px, sw, sh, cx, horseY, 7, 4, SangoPalette.Horse);
        // 马身暗面
        PixelArtBuilder.FillRect(px, sw, sh, cx - 5, horseY + 1, 10, 2, SangoPalette.HorseDark);
        // 马头
        PixelArtBuilder.FillRect(px, sw, sh, cx + 6, horseY - 3, 3, 4, SangoPalette.Horse);
        PixelArtBuilder.SetPixel(px, sw, sh, cx + 8, horseY - 4, SangoPalette.HorseLight); // 耳朵
        // 马眼
        PixelArtBuilder.SetPixel(px, sw, sh, cx + 8, horseY - 2, Color.Black);
        // 马鬃
        PixelArtBuilder.FillRect(px, sw, sh, cx + 2, horseY - 4, 4, 1, SangoPalette.HorseDark);
        // 马腿 (4条)
        int legW = 1, legH = 4;
        PixelArtBuilder.FillRect(px, sw, sh, cx - 5, baseY - 2 + legOff, legW + 1, legH, SangoPalette.HorseDark);
        PixelArtBuilder.FillRect(px, sw, sh, cx - 2, baseY - 2 - legOff, legW + 1, legH, SangoPalette.HorseDark);
        PixelArtBuilder.FillRect(px, sw, sh, cx + 2, baseY - 2 + legOff, legW + 1, legH, SangoPalette.HorseDark);
        PixelArtBuilder.FillRect(px, sw, sh, cx + 5, baseY - 2 - legOff, legW + 1, legH, SangoPalette.HorseDark);
        // 马尾
        PixelArtBuilder.DrawLine(px, sw, sh, cx - 7, horseY - 1, cx - 9, horseY + 2, SangoPalette.HorseDark);
    }

    private static void DrawSiegeRam(Color[] px, int sw, int sh, int cx, int baseY, int animRow, int frame)
    {
        // 攻城车框架
        int ramX = cx + 4;
        int ramY = baseY - 8;
        PixelArtBuilder.FillRect(px, sw, sh, ramX, ramY, 10, 6, SangoPalette.WeaponWood);
        PixelArtBuilder.DrawRect(px, sw, sh, ramX, ramY, 10, 6, PixelArtBuilder.Darken(SangoPalette.WeaponWood, 0.7f));
        // 撞锤
        PixelArtBuilder.FillRect(px, sw, sh, ramX + 9, ramY + 1, 3, 4, SangoPalette.WeaponMetal);
        // 轮子
        PixelArtBuilder.FillEllipse(px, sw, sh, ramX + 2, baseY - 1, 2, 2, SangoPalette.WeaponWood);
        PixelArtBuilder.FillEllipse(px, sw, sh, ramX + 7, baseY - 1, 2, 2, SangoPalette.WeaponWood);
    }

    private static void DrawSoldierDeath(Color[] px, int sw, int sh, int cx, int baseY, SoldierVisual cfg, int frame)
    {
        // 4帧死亡: 0=站立歪斜, 1=半倒, 2=倒地, 3=完全倒地
        int tiltY = frame * 2;
        int tiltX = frame * 1;
        float alpha = 1f - frame * 0.15f;

        int bodyY = baseY - 8 + tiltY;
        Color ac = new Color(
            (byte)(cfg.ArmorColor.R * alpha),
            (byte)(cfg.ArmorColor.G * alpha),
            (byte)(cfg.ArmorColor.B * alpha));

        if (frame < 2)
        {
            // 还在往下倒
            PixelArtBuilder.FillEllipse(px, sw, sh, cx + tiltX, bodyY - 5, 2, 2, SangoPalette.Skin);
            PixelArtBuilder.FillRect(px, sw, sh, cx - cfg.BodyW / 2 + tiltX, bodyY - 3, cfg.BodyW, cfg.BodyH - 2, ac);
            PixelArtBuilder.FillRect(px, sw, sh, cx - 2 + tiltX, bodyY + cfg.BodyH - 5, 4, 3, SangoPalette.ArmorDark);
        }
        else
        {
            // 倒在地上 (水平绘制)
            PixelArtBuilder.FillEllipse(px, sw, sh, cx - 4, baseY - 1, 2, 1, SangoPalette.Skin);
            PixelArtBuilder.FillRect(px, sw, sh, cx - 2, baseY - 2, cfg.BodyH, 3, ac);
            PixelArtBuilder.FillRect(px, sw, sh, cx + cfg.BodyH - 2, baseY - 1, 3, 2, SangoPalette.ArmorDark);
        }
    }

    // ==================== 武将帧绘制 ====================

    private static void DrawGeneralFrame(Color[] px, int sw, int sh,
        int fx, int fy, int fs, GeneralVisual cfg, int animRow, int frame)
    {
        int cx = fx + fs / 2;
        int baseY = fy + fs - 6;

        int bobY = GetBobOffset(animRow, frame);
        int legOffset = GetLegOffset(animRow, frame);
        float weaponPhase = GetWeaponPhase(animRow, frame);

        if (animRow == 3) // Death
        {
            DrawGeneralDeath(px, sw, sh, cx, baseY, cfg, frame);
            return;
        }

        // 1. 披风 (在身后)
        int capeX = cx - cfg.CapeW / 2 - 1;
        int capeY = baseY - 14 + bobY;
        int capeSway = (frame % 2 == 0) ? 0 : -1;
        PixelArtBuilder.FillRect(px, sw, sh, capeX + capeSway, capeY, cfg.CapeW, cfg.CapeH, SangoPalette.CapeBase);
        PixelArtBuilder.FillRect(px, sw, sh, capeX + capeSway, capeY + cfg.CapeH - 1, cfg.CapeW, 1,
            PixelArtBuilder.Darken(SangoPalette.CapeBase, 0.7f));

        // 2. 腿部
        int leftLegX = cx - 3;
        int rightLegX = cx + 1;
        PixelArtBuilder.FillRect(px, sw, sh, leftLegX, baseY - 7 + bobY + legOffset, 3, 7, SangoPalette.ArmorDark);
        PixelArtBuilder.FillRect(px, sw, sh, rightLegX, baseY - 7 + bobY - legOffset, 3, 7, SangoPalette.ArmorDark);
        // 护胫
        PixelArtBuilder.FillRect(px, sw, sh, leftLegX, baseY - 2 + bobY + legOffset, 3, 1, SangoPalette.ArmorLight);
        PixelArtBuilder.FillRect(px, sw, sh, rightLegX, baseY - 2 + bobY - legOffset, 3, 1, SangoPalette.ArmorLight);
        // 靴子
        PixelArtBuilder.FillRect(px, sw, sh, leftLegX - 1, baseY + bobY + legOffset, 4, 2, new Color(50, 40, 30));
        PixelArtBuilder.FillRect(px, sw, sh, rightLegX - 1, baseY + bobY - legOffset, 4, 2, new Color(50, 40, 30));

        // 3. 躯干 (更宽更高)
        int torsoX = cx - cfg.BodyW / 2;
        int torsoY = baseY - 7 - cfg.BodyH + bobY;
        PixelArtBuilder.FillRect(px, sw, sh, torsoX, torsoY, cfg.BodyW, cfg.BodyH, SangoPalette.ArmorBase);
        // 2px轮廓线
        PixelArtBuilder.DrawRect(px, sw, sh, torsoX, torsoY, cfg.BodyW, cfg.BodyH, SangoPalette.ArmorDark);
        // 胸甲纹饰
        PixelArtBuilder.FillRect(px, sw, sh, cx - 2, torsoY + 2, 4, 1, SangoPalette.GoldTrim);
        PixelArtBuilder.FillRect(px, sw, sh, cx - 1, torsoY + 4, 2, 1, SangoPalette.GoldTrim);
        // 腰带
        PixelArtBuilder.FillRect(px, sw, sh, torsoX, torsoY + cfg.BodyH - 2, cfg.BodyW, 2, SangoPalette.LeatherDark);
        PixelArtBuilder.SetPixel(px, sw, sh, cx, torsoY + cfg.BodyH - 2, SangoPalette.GoldTrim); // 腰扣

        // 4. 肩甲
        PixelArtBuilder.FillRect(px, sw, sh, torsoX - 2, torsoY, 3, 3, SangoPalette.ArmorLight);
        PixelArtBuilder.FillRect(px, sw, sh, torsoX + cfg.BodyW - 1, torsoY, 3, 3, SangoPalette.ArmorLight);

        // 5. 手臂 + 武器 (将军大刀)
        int armY = torsoY + 3;
        int wpX = cx + cfg.BodyW / 2 + 1;
        PixelArtBuilder.FillRect(px, sw, sh, wpX, armY, 3, 4, SangoPalette.Skin);
        // 大刀
        int wLen = cfg.WeaponLength;
        if (weaponPhase < 0.3f)
        {
            PixelArtBuilder.DrawLineThick(px, sw, sh, wpX + 2, armY - wLen + 3, wpX + 2, armY, SangoPalette.WeaponMetal, 2);
            PixelArtBuilder.SetPixel(px, sw, sh, wpX + 1, armY - wLen + 2, SangoPalette.WeaponMetalDark);
            PixelArtBuilder.SetPixel(px, sw, sh, wpX + 3, armY - wLen + 2, SangoPalette.WeaponMetalDark);
        }
        else if (weaponPhase < 0.7f)
        {
            PixelArtBuilder.DrawLineThick(px, sw, sh, wpX + 2, armY, wpX + wLen, armY - 2, SangoPalette.WeaponMetal, 2);
        }
        else
        {
            PixelArtBuilder.DrawLineThick(px, sw, sh, wpX + 2, armY - wLen / 2, wpX + 2, armY + 2, SangoPalette.WeaponMetal, 2);
        }
        // 左手 (护体)
        PixelArtBuilder.FillRect(px, sw, sh, torsoX - 3, armY + 1, 3, 3, SangoPalette.Skin);

        // 6. 头部 (更大)
        int headY = torsoY - 5;
        PixelArtBuilder.FillEllipse(px, sw, sh, cx, headY, 3, 3, SangoPalette.Skin);
        // 眼睛
        PixelArtBuilder.SetPixel(px, sw, sh, cx - 1, headY, Color.Black);
        PixelArtBuilder.SetPixel(px, sw, sh, cx + 1, headY, Color.Black);
        // 头发/胡须
        PixelArtBuilder.FillRect(px, sw, sh, cx - 2, headY - 3, 5, 1, SangoPalette.Hair);
        PixelArtBuilder.SetPixel(px, sw, sh, cx - 3, headY - 2, SangoPalette.Hair);
        PixelArtBuilder.SetPixel(px, sw, sh, cx + 3, headY - 2, SangoPalette.Hair);

        // 7. 头盔 (华丽)
        PixelArtBuilder.FillRect(px, sw, sh, cx - 4, headY - 4, 9, 2, SangoPalette.ArmorLight);
        PixelArtBuilder.DrawRect(px, sw, sh, cx - 4, headY - 4, 9, 2, SangoPalette.GoldTrim);
        // 盔缨
        for (int p = 0; p < cfg.PlumeHeight; p++)
        {
            int plumeW = cfg.PlumeHeight - p;
            PixelArtBuilder.FillRect(px, sw, sh, cx - plumeW / 2, headY - 5 - p, plumeW, 1, SangoPalette.PlumeTip);
        }
    }

    private static void DrawGeneralDeath(Color[] px, int sw, int sh, int cx, int baseY, GeneralVisual cfg, int frame)
    {
        int tiltY = frame * 3;
        float alpha = 1f - frame * 0.15f;
        Color ac = new Color(
            (byte)(SangoPalette.ArmorBase.R * alpha),
            (byte)(SangoPalette.ArmorBase.G * alpha),
            (byte)(SangoPalette.ArmorBase.B * alpha));

        if (frame < 2)
        {
            int bY = baseY - 12 + tiltY;
            PixelArtBuilder.FillEllipse(px, sw, sh, cx + frame * 2, bY - 6, 3, 3, SangoPalette.Skin);
            PixelArtBuilder.FillRect(px, sw, sh, cx - cfg.BodyW / 2 + frame, bY - 3, cfg.BodyW, cfg.BodyH - 4, ac);
            PixelArtBuilder.FillRect(px, sw, sh, cx - 3 + frame, bY + cfg.BodyH - 7, 6, 4, SangoPalette.ArmorDark);
            // 披风散落
            PixelArtBuilder.FillRect(px, sw, sh, cx - cfg.CapeW / 2 - 1, bY - 2, cfg.CapeW, 3,
                PixelArtBuilder.Darken(SangoPalette.CapeBase, 0.8f));
        }
        else
        {
            // 倒地
            PixelArtBuilder.FillEllipse(px, sw, sh, cx - 6, baseY - 1, 3, 1, SangoPalette.Skin);
            PixelArtBuilder.FillRect(px, sw, sh, cx - 3, baseY - 3, cfg.BodyH, 4, ac);
            PixelArtBuilder.FillRect(px, sw, sh, cx + cfg.BodyH - 3, baseY - 2, 4, 3, SangoPalette.ArmorDark);
            // 披风展开
            PixelArtBuilder.FillRect(px, sw, sh, cx - 5, baseY + 1, cfg.CapeW + 2, 2,
                PixelArtBuilder.Darken(SangoPalette.CapeBase, 0.6f));
        }
    }

    // ==================== 动画辅助 ====================

    private static int GetBobOffset(int animRow, int frame)
    {
        return animRow switch
        {
            0 => (frame == 1 || frame == 3) ? -1 : 0, // Idle: 轻微呼吸
            1 => (frame % 2 == 0) ? -1 : 0,           // Walk: 跑步弹跳
            2 => frame switch { 0 => 0, 1 => -1, 2 => 1, _ => 0 }, // Attack: 蓄力-挥动-收招
            _ => 0
        };
    }

    private static int GetLegOffset(int animRow, int frame)
    {
        return animRow switch
        {
            1 => frame switch { 0 => -2, 1 => 0, 2 => 2, _ => 0 }, // Walk: 腿交替
            2 => frame switch { 0 => 0, 1 => -1, 2 => 1, _ => 0 }, // Attack: 微调
            _ => 0
        };
    }

    private static float GetWeaponPhase(int animRow, int frame)
    {
        if (animRow != 2) return 0f; // 只有Attack行有武器相位
        return frame switch
        {
            0 => 0f,    // 蓄力
            1 => 0.4f,  // 挥出
            2 => 0.7f,  // 接触
            _ => 0.9f   // 回收
        };
    }
}
