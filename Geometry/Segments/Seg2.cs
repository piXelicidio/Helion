using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Helion.Geometry.New.Boxes;
using Helion.Geometry.New.Interfaces;
using Helion.Geometry.New.Vectors;
using Helion.Util.Extensions;

namespace Helion.Geometry.New.Segments;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Seg2(Vec2 Start, Vec2 End) :
    IAdditionOperators<Seg2, Vec2, Seg2>,
    ISubtractionOperators<Seg2, Vec2, Seg2>,
    IMultiplyOperators<Seg2, Vec2, Seg2>,
    IMultiplyOperators<Seg2, float, Seg2>,
    IDivisionOperators<Seg2, Vec2, Seg2>,
    IDivisionOperators<Seg2, float, Seg2>,
    IEqualityOperators<Seg2, Seg2, bool>,
    ILength<float>,
    IPerpDot<Vec2, float>,
    ILerp<Seg2, float, Seg2>
{
    public Vec2 Delta => End - Start;
    public Vec2 Middle => (Start + End) * 0.5f;
    public Box2 Box => (Start.Min(End), Start.Max(End));
    
    public Seg2(float startX, float startY, float endX, float endY) : this((startX, startY), (endX, endY))
    {
    }
    
    public static implicit operator Seg2(in ValueTuple<Vec2, Vec2> tuple) => new(tuple.Item1, tuple.Item2);
    public static implicit operator Seg2(in ValueTuple<float, float, float, float> tuple) => new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);

    public static Seg2 operator +(Seg2 left, Vec2 right) => new(left.Start + right, left.End + right);
    public static Seg2 operator -(Seg2 left, Vec2 right) => new(left.Start - right, left.End - right);
    public static Seg2 operator *(Seg2 left, Vec2 right) => new(left.Start * right, left.End * right);
    public static Seg2 operator *(Seg2 left, float value) => new(left.Start * value, left.End * value);
    public static Seg2 operator /(Seg2 left, Vec2 right) => new(left.Start / right, left.End / right);
    public static Seg2 operator /(Seg2 left, float value) => new(left.Start / value, left.End / value);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Seg2 Create(in Vec2 first, in Vec2 second) => new(first, second);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => Delta.Length();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSquared() => Delta.LengthSquared();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float PerpDot(in Vec2 point) => Delta.Dot((point.Y - Start.Y, Start.X - point.X));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Seg2 Lerp(in Seg2 other, in float amount) => (Start.Lerp(other.Start, amount), End.Lerp(other.End, amount));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OnRight(in Vec2 point) => PerpDot(point) <= 0.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OnRight(in Seg2 seg) => OnRight(seg.Start) && OnRight(seg.End);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec2 FromTime(in float t) => Start + (Delta * t);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ToTime(in Vec2 point) => Start.X.ApproxEquals(End.X) ? (point.Y - Start.Y) / (End.Y - Start.Y) : (point.X - Start.X) / (End.X - Start.X);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec2 ClosestPoint(in Vec2 point)
    {
        Vec2 pointToStartDelta = Start - point;
        Vec2 delta = Delta;
        float t = pointToStartDelta.Dot(-delta) / delta.Dot(delta);
        return FromTime(t.Clamp(0.0f, 1.0f));
    }

    public override string ToString() => $"({Start}), ({End})";
    public override int GetHashCode() => HashCode.Combine(Start, End);
}