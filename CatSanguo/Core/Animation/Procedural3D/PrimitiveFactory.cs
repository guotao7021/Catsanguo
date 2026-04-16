using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CatSanguo.Core.Animation.Procedural3D;

/// <summary>
/// 3D基础图元顶点生成器 - 生成纯数据数组，不创建GPU资源
/// </summary>
public static class PrimitiveFactory
{
    public struct MeshData
    {
        public VertexPositionNormalTexture[] Vertices;
        public short[] Indices;
    }

    /// <summary>创建Box - 24顶点36索引，以原点为中心</summary>
    public static MeshData CreateBox(float width, float height, float depth)
    {
        float hw = width / 2f, hh = height / 2f, hd = depth / 2f;
        var verts = new VertexPositionNormalTexture[24];
        var inds = new short[36];

        // 6个面，每面4顶点
        int v = 0, i = 0;

        // Front (Z+)
        AddQuad(verts, inds, ref v, ref i,
            new Vector3(-hw, -hh, hd), new Vector3(hw, -hh, hd),
            new Vector3(hw, hh, hd), new Vector3(-hw, hh, hd), Vector3.UnitZ);
        // Back (Z-)
        AddQuad(verts, inds, ref v, ref i,
            new Vector3(hw, -hh, -hd), new Vector3(-hw, -hh, -hd),
            new Vector3(-hw, hh, -hd), new Vector3(hw, hh, -hd), -Vector3.UnitZ);
        // Top (Y+)
        AddQuad(verts, inds, ref v, ref i,
            new Vector3(-hw, hh, hd), new Vector3(hw, hh, hd),
            new Vector3(hw, hh, -hd), new Vector3(-hw, hh, -hd), Vector3.UnitY);
        // Bottom (Y-)
        AddQuad(verts, inds, ref v, ref i,
            new Vector3(-hw, -hh, -hd), new Vector3(hw, -hh, -hd),
            new Vector3(hw, -hh, hd), new Vector3(-hw, -hh, hd), -Vector3.UnitY);
        // Right (X+)
        AddQuad(verts, inds, ref v, ref i,
            new Vector3(hw, -hh, hd), new Vector3(hw, -hh, -hd),
            new Vector3(hw, hh, -hd), new Vector3(hw, hh, hd), Vector3.UnitX);
        // Left (X-)
        AddQuad(verts, inds, ref v, ref i,
            new Vector3(-hw, -hh, -hd), new Vector3(-hw, -hh, hd),
            new Vector3(-hw, hh, hd), new Vector3(-hw, hh, -hd), -Vector3.UnitX);

        return new MeshData { Vertices = verts, Indices = inds };
    }

    /// <summary>创建圆柱 - 侧面+上下盖，以原点为中心</summary>
    public static MeshData CreateCylinder(float radius, float height, int segments = 12)
    {
        float hh = height / 2f;
        var verts = new List<VertexPositionNormalTexture>();
        var inds = new List<short>();

        // 侧面
        for (int s = 0; s <= segments; s++)
        {
            float angle = MathHelper.TwoPi * s / segments;
            float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
            var normal = new Vector3(cos, 0, sin);
            verts.Add(new VertexPositionNormalTexture(
                new Vector3(cos * radius, -hh, sin * radius), normal, new Vector2((float)s / segments, 1)));
            verts.Add(new VertexPositionNormalTexture(
                new Vector3(cos * radius, hh, sin * radius), normal, new Vector2((float)s / segments, 0)));
        }
        for (int s = 0; s < segments; s++)
        {
            short bl = (short)(s * 2), tl = (short)(s * 2 + 1);
            short br = (short)((s + 1) * 2), tr = (short)((s + 1) * 2 + 1);
            inds.Add(bl); inds.Add(tl); inds.Add(tr);
            inds.Add(bl); inds.Add(tr); inds.Add(br);
        }

        // 上盖
        short topCenter = (short)verts.Count;
        verts.Add(new VertexPositionNormalTexture(new Vector3(0, hh, 0), Vector3.UnitY, new Vector2(0.5f, 0.5f)));
        for (int s = 0; s <= segments; s++)
        {
            float angle = MathHelper.TwoPi * s / segments;
            verts.Add(new VertexPositionNormalTexture(
                new Vector3(MathF.Cos(angle) * radius, hh, MathF.Sin(angle) * radius),
                Vector3.UnitY, new Vector2(0.5f, 0.5f)));
        }
        for (int s = 0; s < segments; s++)
        {
            inds.Add(topCenter);
            inds.Add((short)(topCenter + 1 + s));
            inds.Add((short)(topCenter + 2 + s));
        }

        // 下盖
        short botCenter = (short)verts.Count;
        verts.Add(new VertexPositionNormalTexture(new Vector3(0, -hh, 0), -Vector3.UnitY, new Vector2(0.5f, 0.5f)));
        for (int s = 0; s <= segments; s++)
        {
            float angle = MathHelper.TwoPi * s / segments;
            verts.Add(new VertexPositionNormalTexture(
                new Vector3(MathF.Cos(angle) * radius, -hh, MathF.Sin(angle) * radius),
                -Vector3.UnitY, new Vector2(0.5f, 0.5f)));
        }
        for (int s = 0; s < segments; s++)
        {
            inds.Add(botCenter);
            inds.Add((short)(botCenter + 2 + s));
            inds.Add((short)(botCenter + 1 + s));
        }

        return new MeshData { Vertices = verts.ToArray(), Indices = inds.ToArray() };
    }

    /// <summary>创建球体 - 经纬线细分，以原点为中心</summary>
    public static MeshData CreateSphere(float radius, int rings = 8, int segments = 12)
    {
        var verts = new List<VertexPositionNormalTexture>();
        var inds = new List<short>();

        for (int r = 0; r <= rings; r++)
        {
            float phi = MathHelper.Pi * r / rings;
            float sinPhi = MathF.Sin(phi), cosPhi = MathF.Cos(phi);

            for (int s = 0; s <= segments; s++)
            {
                float theta = MathHelper.TwoPi * s / segments;
                float sinT = MathF.Sin(theta), cosT = MathF.Cos(theta);

                var normal = new Vector3(sinPhi * cosT, cosPhi, sinPhi * sinT);
                var pos = normal * radius;
                var uv = new Vector2((float)s / segments, (float)r / rings);
                verts.Add(new VertexPositionNormalTexture(pos, normal, uv));
            }
        }

        int cols = segments + 1;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                short tl = (short)(r * cols + s);
                short tr = (short)(r * cols + s + 1);
                short bl = (short)((r + 1) * cols + s);
                short br = (short)((r + 1) * cols + s + 1);

                inds.Add(tl); inds.Add(bl); inds.Add(tr);
                inds.Add(tr); inds.Add(bl); inds.Add(br);
            }
        }

        return new MeshData { Vertices = verts.ToArray(), Indices = inds.ToArray() };
    }

    /// <summary>创建圆锥 - 底面圆+侧面三角，以底面中心为原点</summary>
    public static MeshData CreateCone(float radius, float height, int segments = 8)
    {
        var verts = new List<VertexPositionNormalTexture>();
        var inds = new List<short>();

        // 侧面
        float slopeLen = MathF.Sqrt(radius * radius + height * height);
        float ny = radius / slopeLen, nr = height / slopeLen;

        short tipIdx = (short)verts.Count;
        verts.Add(new VertexPositionNormalTexture(new Vector3(0, height, 0), Vector3.UnitY, new Vector2(0.5f, 0)));

        for (int s = 0; s <= segments; s++)
        {
            float angle = MathHelper.TwoPi * s / segments;
            float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
            var normal = new Vector3(cos * nr, ny, sin * nr);
            normal = Vector3.Normalize(normal);
            verts.Add(new VertexPositionNormalTexture(
                new Vector3(cos * radius, 0, sin * radius), normal, new Vector2((float)s / segments, 1)));
        }
        for (int s = 0; s < segments; s++)
        {
            inds.Add(tipIdx);
            inds.Add((short)(tipIdx + 1 + s + 1));
            inds.Add((short)(tipIdx + 1 + s));
        }

        // 底面
        short botCenter = (short)verts.Count;
        verts.Add(new VertexPositionNormalTexture(Vector3.Zero, -Vector3.UnitY, new Vector2(0.5f, 0.5f)));
        for (int s = 0; s <= segments; s++)
        {
            float angle = MathHelper.TwoPi * s / segments;
            verts.Add(new VertexPositionNormalTexture(
                new Vector3(MathF.Cos(angle) * radius, 0, MathF.Sin(angle) * radius),
                -Vector3.UnitY, new Vector2(0.5f, 0.5f)));
        }
        for (int s = 0; s < segments; s++)
        {
            inds.Add(botCenter);
            inds.Add((short)(botCenter + 1 + s));
            inds.Add((short)(botCenter + 2 + s));
        }

        return new MeshData { Vertices = verts.ToArray(), Indices = inds.ToArray() };
    }

    private static void AddQuad(VertexPositionNormalTexture[] verts, short[] inds,
        ref int v, ref int i, Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Vector3 normal)
    {
        short baseV = (short)v;
        verts[v++] = new VertexPositionNormalTexture(bl, normal, new Vector2(0, 1));
        verts[v++] = new VertexPositionNormalTexture(br, normal, new Vector2(1, 1));
        verts[v++] = new VertexPositionNormalTexture(tr, normal, new Vector2(1, 0));
        verts[v++] = new VertexPositionNormalTexture(tl, normal, new Vector2(0, 0));
        inds[i++] = baseV; inds[i++] = (short)(baseV + 1); inds[i++] = (short)(baseV + 2);
        inds[i++] = baseV; inds[i++] = (short)(baseV + 2); inds[i++] = (short)(baseV + 3);
    }
}
