using Microsoft.Xna.Framework;
using CatSanguo.Data.Schemas;

namespace CatSanguo.WorldMap;

public class CityNode
{
    public CityData Data { get; }
    public Vector2 Center { get; }
    public Rectangle Bounds { get; }
    public bool IsSelected { get; set; }
    public bool IsReachable { get; set; }

    public float IconScale => CityScaleConfig.GetIconScale(Data.CityScale) * (0.9f + Data.WallLevel * 0.05f);

    public CityNode(CityData data, Vector2 center)
    {
        Data = data;
        Center = center;
        var (w, h) = GetHitBoxSize(data.CityScale);
        Bounds = new Rectangle(
            (int)center.X - w / 2,
            (int)center.Y - h / 2,
            w, h);
    }

    private static (int w, int h) GetHitBoxSize(string scale) => scale switch
    {
        "small" => (50, 45),
        "medium" => (65, 58),
        "large" => (78, 70),
        "huge" => (90, 82),
        _ => (65, 58)
    };

    public bool IsAdjacentTo(CityNode other)
    {
        int dx = System.Math.Abs(Data.GridX - other.Data.GridX);
        int dy = System.Math.Abs(Data.GridY - other.Data.GridY);
        return (dx + dy) == 1;
    }

    public bool IsConnectedTo(CityNode other)
    {
        if (Data.ConnectedCityIds != null && Data.ConnectedCityIds.Count > 0)
            return Data.ConnectedCityIds.Contains(other.Data.Id);
        return IsAdjacentTo(other);
    }
}
