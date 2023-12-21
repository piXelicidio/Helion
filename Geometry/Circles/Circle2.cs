using System.Numerics;
using System.Runtime.CompilerServices;
using Helion.Geometry.New.Boxes;
using Helion.Geometry.New.Interfaces;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Circles;

public readonly record struct Circle2(Vec2 Center, float Radius) :
    IAdditionOperators<Circle2, Vec2, Circle2>,
    ISubtractionOperators<Circle2, Vec2, Circle2>,
    ICircle<float, Vec2>,
    IContains<Vec2>,
    IIntersects<Box2>
{
    public static int Dimension => 2;
    
    public float Area => float.Pi * LengthSquared();
    public Box2 Box => new(Center - (Radius, Radius), Center + (Radius, Radius));

    public static Circle2 operator +(Circle2 left, Vec2 right) => left with { Center = left.Center + right };
    public static Circle2 operator -(Circle2 left, Vec2 right) => left with { Center = left.Center - right };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => MathF.Sqrt(LengthSquared());
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSquared() => Radius * Radius;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(in Vec2 point) => (point - Center).LengthSquared() < LengthSquared();

    // Modified from: https://gamedev.stackexchange.com/a/178154/86759
    // TODO: Is the last comparison supposed to be `> r*r` or even `< r*r`?
    public bool Intersects(in Box2 box)
    {
        Vec2 distance = box.Center - Center;
        Vec2 clampedDistance = distance.Clamp(box.Min, box.Max);
        Vec2 closestPoint = Center + clampedDistance;
        return (closestPoint - Center).LengthSquared() > Radius;
    }
}