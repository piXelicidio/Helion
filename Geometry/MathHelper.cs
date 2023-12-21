using System.Numerics;
using System.Runtime.CompilerServices;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New;

public static class MathHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ApproxZero<F>(this F f) where F : IFloatingPointIeee754<F>
    {
        return F.Abs(f) < F.Epsilon;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ApproxEqual<F>(this F a, in F b) where F : IFloatingPointIeee754<F>
    {
        return F.Abs(a - b) < F.Epsilon;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool DifferentSign<F>(this F a, in F b) where F : IFloatingPoint<F>
    {
        return a * b < F.Zero;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F Clamp<F>(this F value, in F min, in F max) where F : IComparisonOperators<F, F, bool>
    {
        return value < min ? min : (value > max ? max : value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InNormalRangeInclusive<F>(this F f) where F : INumber<F>
    {
        return f >= F.Zero && f <= F.One;   
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InNormalRangeExclusive<F>(this F f) where F : INumber<F>
    {
        return f > F.Zero && f < F.One;   
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F DoubleTriArea<F>(F aX, F aY, F bX, F bY, F cX, F cY) where F : INumber<F>
    {
        return ((aX - cX) * (bY - cY)) - ((aY - cY) * (bX - cX));
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F DoubleTriArea<TVec, F>(in TVec a, in TVec b, in TVec c) where TVec : IVector2<F> where F : INumber<F>
    {
        return ((a.X - c.X) * (b.Y - c.Y)) - ((a.Y - c.Y) * (b.X - c.X));
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CollinearHelper<F>(F aX, F aY, F bX, F bY, F cX, F cY) where F : IFloatingPointIeee754<F>
    {
        return ApproxZero((aX * (bY - cY)) + (bX * (cY - aY)) + (cX * (aY - bY)));
    }
}