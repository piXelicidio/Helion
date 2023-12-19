using System.Runtime.InteropServices;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Boxes;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RectF(Vec2 Origin, Vec2 Sides)
{
    public RectF(float minX, float minY, float maxX, float maxY) : this((minX, minY), (maxX, maxY))
    {
    }
    
    public float Area => Sides.X * Sides.Y;
    
    public static implicit operator RectF(in ValueTuple<Vec2, Vec2> tuple) => new(tuple.Item1, tuple.Item2);
    public static implicit operator RectF(in ValueTuple<float, float, float, float> tuple) => new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
    
    public static RectF operator +(RectF left, Vec2 right) => left with { Origin = left.Origin + right };
    public static RectF operator -(RectF left, Vec2 right) => left with { Origin = left.Origin - right };
    public static RectF operator *(RectF left, Vec2 right) => left with { Origin = left.Origin * right };
    public static RectF operator *(RectF left, float value) => left with { Origin = left.Origin * value };
    public static RectF operator /(RectF left, Vec2 right) => left with { Origin = left.Origin / right };
    public static RectF operator /(RectF left, float value) => left with { Origin = left.Origin / value };
}