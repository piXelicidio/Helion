using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Helion.Geometry.New.Interfaces;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Boxes;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Box2(Vec2 Min, Vec2 Max) :
    IBox2<Vec2, float>,
    ICreatable2<Vec2, Vec2, Box2>,
    IAdditionOperators<Box2, Vec2, Box2>,
    ISubtractionOperators<Box2, Vec2, Box2>,
    IMultiplyOperators<Box2, Vec2, Box2>,
    IMultiplyOperators<Box2, float, Box2>,
    IDivisionOperators<Box2, Vec2, Box2>,
    IDivisionOperators<Box2, float, Box2>,
    IEqualityOperators<Box2, Box2, bool>,
    IBound<Vec2, Box2>,
    IBound<Box2, Box2>,
    IContains<Vec2>,
    IContains<Box2>,
    IOverlaps<Box2>,
    ILerp<Vec2, float, Box2>,
    ILerp<Box2, float, Box2>
{
    public static int Dimension => 2;
    public static Box2 Unit => ((0.0f, 0.0f), (1.0f, 1.0f));
    
    public float Area => Rect.Area;
    public Vec2 Center => (Min + Max) * 0.5f;
    public Vec2 Sides => Max - Min;
    public Vec2 TopLeft => (Min.X, Max.Y);
    public Vec2 BottomLeft => Min;
    public Vec2 BottomRight => (Max.X, Min.Y);
    public Vec2 TopRight => Max;
    public float Top => Max.Y;
    public float Bottom => Min.Y;
    public float Left => Min.X;
    public float Right => Max.X;
    public float Width => Max.X - Min.X;
    public float Height => Max.Y - Min.Y;
    public RectF Rect => new(Min, Sides);
    
    public Box2(float minX, float minY, float maxX, float maxY) : this((minX, minY), (maxX, maxY))
    {
    }
    
    public Box2(in RectF rect) : this(rect.Origin, rect.Origin + rect.Sides)
    {
    }
    
    public static implicit operator Box2(in ValueTuple<Vec2, Vec2> tuple) => new(tuple.Item1, tuple.Item2);
    public static implicit operator Box2(in ValueTuple<float, float, float, float> tuple) => new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);

    public static Box2 operator +(Box2 left, Vec2 right) => new(left.Min + right, left.Max + right);
    public static Box2 operator -(Box2 left, Vec2 right) => new(left.Min - right, left.Max - right);
    public static Box2 operator *(Box2 left, Vec2 right) => new(left.Min * right, left.Max * right);
    public static Box2 operator *(Box2 left, float value) => new(left.Min * value, left.Max * value);
    public static Box2 operator /(Box2 left, Vec2 right) => new(left.Min / right, left.Max / right);
    public static Box2 operator /(Box2 left, float value) => new(left.Min / value, left.Max / value);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Box2 Create(in Vec2 first, in Vec2 second) => new(first, second);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(in Vec2 point) => point.X > Min.X && point.X < Max.X && point.Y > Min.Y && point.Y < Max.Y;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(in Box2 box) => box.Min.X > Min.X && box.Max.X < Max.X && box.Min.Y > Min.Y && box.Max.Y < Max.Y;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Overlaps(in Box2 box) => Min.X < box.Max.X && Max.X > box.Min.X && Min.Y < box.Max.Y && Max.Y > box.Min.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Box2 Bound(in Vec2 point) => new(Min.Min(point), Max.Max(point));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Box2 Bound(in Box2 box) => new(Min.Min(box.Min), Max.Max(box.Max));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Box2 Lerp(in Vec2 centerTarget, in float amount) => this + Center.Lerp(centerTarget, amount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Box2 Lerp(in Box2 other, in float amount) => (Min.Lerp(other.Min, amount), Max.Lerp(other.Max, amount));

    public override string ToString() => $"({Min}), ({Max})";
}