using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Segments;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Seg2
{
    public Vec2 Start;
    public Vec2 End;

    public Vec2 Middle => (Start + End) * 0.5f;
    
    public Seg2(in Vec2 start, in Vec2 end)
    {
        Start = start;
        End = end;
    }
    
    public Seg2(float startX, float startY, float endX, float endY)
    {
        Start = new(startX, startY);
        End = new(endX, endY);
    }
    
    public static implicit operator Seg2(in ValueTuple<Vec2, Vec2> tuple) => new(tuple.Item1, tuple.Item2);
    public static implicit operator Seg2(in ValueTuple<float, float, float, float> tuple) => new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);

    public static Seg2 operator +(Seg2 left, Vec2 right) => new(left.Start + right, left.End + right);
    public static Seg2 operator -(Seg2 left, Vec2 right) => new(left.Start - right, left.End - right);
    public static Seg2 operator *(Seg2 left, Vec2 right) => new(left.Start * right, left.End * right);
    public static Seg2 operator *(Seg2 left, float value) => new(left.Start * value, left.End * value);
    public static Seg2 operator /(Seg2 left, Vec2 right) => new(left.Start / right, left.End / right);
    public static Seg2 operator /(Seg2 left, float value) => new(left.Start / value, left.End / value);
    public static bool operator ==(Seg2 left, Seg2 right) => left.Start == right.Start && left.End == right.End;
    public static bool operator !=(Seg2 left, Seg2 right) => !(left == right);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Seg2 Create(in Vec2 first, in Vec2 second) => new(first, second);
    
    public override bool Equals(object? obj) => obj is Seg2 other && Start == other.Start && End == other.End;
    public override string ToString() => $"({Start}), ({End})";
    public override int GetHashCode() => HashCode.Combine(Start, End);
}