using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace CatSanguo.Core.Animation.Procedural3D;

public enum PrimitiveType { Box, Cylinder, Sphere, Cone }

/// <summary>
/// 角色3D零件 - 组成零件树，每个零件有图元类型/尺寸/颜色/局部变换
/// </summary>
public class CharacterPart
{
    public PrimitiveType Type { get; set; }
    public Vector3 Size { get; set; }
    public Matrix LocalTransform { get; set; } = Matrix.Identity;
    public Color DiffuseColor { get; set; } = Color.Gray;
    public string Tag { get; set; } = "";
    public List<CharacterPart> Children { get; set; } = new();

    /// <summary>获取此图元的网格数据</summary>
    public PrimitiveFactory.MeshData GetMesh()
    {
        return Type switch
        {
            PrimitiveType.Box => PrimitiveFactory.CreateBox(Size.X, Size.Y, Size.Z),
            PrimitiveType.Cylinder => PrimitiveFactory.CreateCylinder(Size.X, Size.Y, (int)Size.Z > 0 ? (int)Size.Z : 10),
            PrimitiveType.Sphere => PrimitiveFactory.CreateSphere(Size.X),
            PrimitiveType.Cone => PrimitiveFactory.CreateCone(Size.X, Size.Y, (int)Size.Z > 0 ? (int)Size.Z : 8),
            _ => PrimitiveFactory.CreateBox(Size.X, Size.Y, Size.Z)
        };
    }

    /// <summary>递归收集所有零件到平面列表，并计算其世界变换</summary>
    public static void Flatten(CharacterPart part, Matrix parentWorld,
        List<(CharacterPart part, Matrix world)> output)
    {
        var world = part.LocalTransform * parentWorld;
        output.Add((part, world));
        foreach (var child in part.Children)
            Flatten(child, world, output);
    }
}
