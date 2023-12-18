using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Helion.Geometry.New.Interfaces;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Boxes;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Box2 : 
    IDimensional,
    ICreatable2<Vec2, Vec2, Box2>,
    IAdditionOperators<Box2, Vec2, Box2>,
    ISubtractionOperators<Box2, Vec2, Box2>,
    IMultiplyOperators<Box2, Vec2, Box2>,
    IMultiplyOperators<Box2, float, Box2>,
    IDivisionOperators<Box2, Vec2, Box2>,
    IDivisionOperators<Box2, float, Box2>,
    IEqualityOperators<Box2, Box2, bool>
{
    public static int Dimension => 2;
    public static Box2 Unit => new((0.0f, 0.0f), (1.0f, 1.0f));

    public Vec2 Min;
    public Vec2 Max;

    public Vec2 Center => (Min + Max) * 0.5f;
    public Vec2 Sides => Max - Min;
    
    public Box2(in Vec2 min, in Vec2 max)
    {
        Min = min;
        Max = max;
    }
    
    public Box2(float minX, float minY, float maxX, float maxY)
    {
        Min = new(minX, minY);
        Max = new(maxX, maxY);
    }
    
    public static implicit operator Box2(in ValueTuple<Vec2, Vec2> tuple) => new(tuple.Item1, tuple.Item2);
    public static implicit operator Box2(in ValueTuple<float, float, float, float> tuple) => new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);

    public static Box2 operator +(Box2 left, Vec2 right) => new(left.Min + right, left.Max + right);
    public static Box2 operator -(Box2 left, Vec2 right) => new(left.Min - right, left.Max - right);
    public static Box2 operator *(Box2 left, Vec2 right) => new(left.Min * right, left.Max * right);
    public static Box2 operator *(Box2 left, float value) => new(left.Min * value, left.Max * value);
    public static Box2 operator /(Box2 left, Vec2 right) => new(left.Min / right, left.Max / right);
    public static Box2 operator /(Box2 left, float value) => new(left.Min / value, left.Max / value);
    public static bool operator ==(Box2 left, Box2 right) => left.Min == right.Min && left.Max == right.Max;
    public static bool operator !=(Box2 left, Box2 right) => !(left == right);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Box2 Create(in Vec2 first, in Vec2 second) => new(first, second);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Contains(in Vec2 point) => point.X > Min.X && point.X < Max.X && point.Y > Min.Y && point.Y < Max.Y;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Contains(in Box2 box) => box.Min.X > Min.X && box.Max.X < Max.X && box.Min.Y > Min.Y && box.Max.Y < Max.Y;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Overlaps(in Box2 box) => Min.X < box.Max.X && Max.X > box.Min.X && Min.Y < box.Max.Y && Max.Y > box.Min.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Box2 Combine(in Box2 box) => new(Min.Min(box.Min), Max.Max(box.Max));

    public override bool Equals(object? obj) => obj is Box2 other && Min == other.Min && Max == other.Max;
    public override string ToString() => $"({Min}), ({Max})";
    public override int GetHashCode() => HashCode.Combine(Min, Max);
}