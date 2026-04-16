using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Core.Animation.Procedural3D;

/// <summary>
/// 3D精灵表渲染器 - 用RenderTarget2D将3D模型渲染为2D精灵表纹理
/// 仅在启动时调用，生成后释放所有3D资源
/// </summary>
public static class SpriteSheetRenderer3D
{
    private const int SoldierFrameSize = 64;
    private const int GeneralFrameSize = 96;
    private const int GridCols = 4;
    private const int GridRows = 4;

    /// <summary>渲染士兵精灵表 256x256 (4x4 grid, 64x64帧)</summary>
    public static Texture2D RenderSoldierSheet(GraphicsDevice gd, UnitType unitType)
    {
        var model = ModelConfig3D.BuildSoldierModel(unitType);
        return RenderSheet(gd, model, SoldierFrameSize, unitType, false);
    }

    /// <summary>渲染武将精灵表 384x384 (4x4 grid, 96x96帧)</summary>
    public static Texture2D RenderGeneralSheet(GraphicsDevice gd)
    {
        var model = ModelConfig3D.BuildGeneralModel();
        return RenderSheet(gd, model, GeneralFrameSize, UnitType.Infantry, true);
    }

    private static Texture2D RenderSheet(GraphicsDevice gd, CharacterPart rootModel,
        int frameSize, UnitType unitType, bool isGeneral)
    {
        int sheetW = frameSize * GridCols;
        int sheetH = frameSize * GridRows;
        var finalPixels = new Color[sheetW * sheetH];

        // 保存GPU状态
        var savedViewport = gd.Viewport;
        var savedTargets = gd.GetRenderTargets();
        var savedDepth = gd.DepthStencilState;
        var savedRaster = gd.RasterizerState;
        var savedBlend = gd.BlendState;

        // 创建临时RenderTarget(逐帧渲染，更稳健)
        var rt = new RenderTarget2D(gd, frameSize, frameSize, false,
            SurfaceFormat.Color, DepthFormat.Depth24);

        // 创建BasicEffect
        var effect = new BasicEffect(gd)
        {
            TextureEnabled = false,
            VertexColorEnabled = false,
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            AmbientLightColor = new Vector3(0.35f, 0.30f, 0.25f),
        };

        // 主光源 - 左上方暖色光
        effect.DirectionalLight0.Enabled = true;
        effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.5f, -1f, -0.8f));
        effect.DirectionalLight0.DiffuseColor = new Vector3(0.9f, 0.85f, 0.75f);
        effect.DirectionalLight0.SpecularColor = new Vector3(0.2f, 0.2f, 0.2f);

        // 补光 - 右侧冷色光
        effect.DirectionalLight1.Enabled = true;
        effect.DirectionalLight1.Direction = Vector3.Normalize(new Vector3(0.8f, -0.3f, 0.5f));
        effect.DirectionalLight1.DiffuseColor = new Vector3(0.15f, 0.18f, 0.25f);

        // 摄像机设置
        float camDist = isGeneral ? 3.2f : 2.8f;
        float camY = isGeneral ? 0.55f : 0.45f;
        float lookY = isGeneral ? 0.20f : 0.15f;

        effect.View = Matrix.CreateLookAt(
            new Vector3(0, camY, camDist),
            new Vector3(0, lookY, 0),
            Vector3.Up);

        effect.Projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(28f), 1.0f, 0.1f, 20f);

        // 3D渲染状态
        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState = RasterizerState.CullCounterClockwise;
        gd.BlendState = BlendState.AlphaBlend;

        // 展平零件树
        var flatParts = new List<(CharacterPart part, Matrix world)>();
        CharacterPart.Flatten(rootModel, Matrix.Identity, flatParts);

        try
        {
            // 渲染16帧
            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridCols; col++)
                {
                    // 获取动画姿态
                    var pose = KeyframeAnimator3D.GetPose(row, col, unitType, isGeneral);

                    // 重新展平（应用动画变换）
                    var animatedParts = new List<(CharacterPart part, Matrix world)>();
                    FlattenWithAnimation(rootModel, Matrix.Identity, pose, animatedParts);

                    // 渲染到RT
                    gd.SetRenderTarget(rt);
                    gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Transparent, 1f, 0);

                    foreach (var (part, world) in animatedParts)
                    {
                        if (part.Size == Vector3.Zero) continue; // 跳过空的root节点
                        if (part.DiffuseColor == Color.Transparent) continue;

                        var mesh = part.GetMesh();
                        effect.World = world;
                        effect.DiffuseColor = part.DiffuseColor.ToVector3();

                        foreach (var pass in effect.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            gd.DrawUserIndexedPrimitives(
                                PrimitiveType2D.TriangleList,
                                mesh.Vertices, 0, mesh.Vertices.Length,
                                mesh.Indices, 0, mesh.Indices.Length / 3);
                        }
                    }

                    // 读取像素并拷贝到精灵表
                    var framePixels = new Color[frameSize * frameSize];
                    gd.SetRenderTarget(null);
                    rt.GetData(framePixels);

                    // premultiply alpha + 拷贝到最终数组
                    int destX = col * frameSize;
                    int destY = row * frameSize;
                    for (int y = 0; y < frameSize; y++)
                    {
                        for (int x = 0; x < frameSize; x++)
                        {
                            int srcIdx = y * frameSize + x;
                            var c = framePixels[srcIdx];
                            if (c.A > 0)
                            {
                                // premultiply
                                c = new Color(
                                    (byte)(c.R * c.A / 255),
                                    (byte)(c.G * c.A / 255),
                                    (byte)(c.B * c.A / 255),
                                    c.A);
                            }
                            int destIdx = (destY + y) * sheetW + (destX + x);
                            finalPixels[destIdx] = c;
                        }
                    }
                }
            }
        }
        finally
        {
            // 恢复GPU状态
            if (savedTargets.Length > 0)
                gd.SetRenderTargets(savedTargets);
            else
                gd.SetRenderTarget(null);

            gd.Viewport = savedViewport;
            gd.DepthStencilState = savedDepth;
            gd.RasterizerState = savedRaster;
            gd.BlendState = savedBlend;

            // 释放3D资源
            rt.Dispose();
            effect.Dispose();
        }

        // 创建最终纹理
        var texture = new Texture2D(gd, sheetW, sheetH);
        texture.SetData(finalPixels);
        return texture;
    }

    /// <summary>带动画变换的零件树展平</summary>
    private static void FlattenWithAnimation(CharacterPart part, Matrix parentWorld,
        Dictionary<string, Matrix> pose, List<(CharacterPart part, Matrix world)> output)
    {
        // 先应用局部变换
        var localMatrix = part.LocalTransform;

        // 如果该零件有动画变换，叠加
        if (!string.IsNullOrEmpty(part.Tag) && pose.TryGetValue(part.Tag, out var animTransform))
        {
            localMatrix = animTransform * localMatrix;
        }

        var world = localMatrix * parentWorld;
        output.Add((part, world));

        foreach (var child in part.Children)
            FlattenWithAnimation(child, world, pose, output);
    }

    // MonoGame中 PrimitiveType 枚举别名
    private static class PrimitiveType2D
    {
        public const Microsoft.Xna.Framework.Graphics.PrimitiveType TriangleList =
            Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList;
    }
}
