using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Helion.Geometry.New.Interfaces;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Triangles;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Triangle2f(Vec2 first, Vec2 second, Vec2 third) :
    ITriangle2<float, Vec2>,
    ICreatable3<Vec2, Vec2, Vec2, Triangle2f>
{
    private Vec2 m_first = first;
    private Vec2 m_second = second;
    private Vec2 m_third = third;
    
    public readonly Vec2 First => m_first;
    public readonly Vec2 Second => m_second;
    public readonly Vec2 Third => m_third;

    public Vec2 this[int index] => Unsafe.Add(ref Unsafe.AsRef(ref m_first), index);

    public static Triangle2f Create(in Vec2 first, in Vec2 second, in Vec2 third)
    {
        return new(first, second, third);
    }

    // Modified from: https://stackoverflow.com/a/20861130/3453041
    // This may not be correct for certain edge cases. See link comments for more info.
    public readonly bool Contains(in Vec2 point)
    {
        float s = (m_first.X - m_third.X) * (point.Y - m_third.Y) - (m_first.Y - m_third.Y) * (point.X - m_third.X);
        float t = (m_second.X - m_first.X) * (point.Y - m_first.Y) - (m_second.Y - m_first.Y) * (point.X - m_first.X);
        if (s.DifferentSign(t) && s.ApproxZero() && !t.ApproxZero())
            return false;

        float d = (m_third.X - m_second.X) * (point.Y - m_second.Y) - (m_third.Y - m_second.Y) * (point.X - m_second.X);
        return d.ApproxZero() || (d < 0.0f) == (s + t <= 0.0f);
    }
}