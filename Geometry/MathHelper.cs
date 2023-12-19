using System.Numerics;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New;

public static class MathHelper
{
    public static bool ApproxZero<F>(this F f) where F : IFloatingPointIeee754<F>
    {
        return F.Abs(f) < F.Epsilon;
    }
    
    public static bool ApproxEqual<F>(this F a, in F b) where F : IFloatingPointIeee754<F>
    {
        return F.Abs(a - b) < F.Epsilon;
    }

    public static bool DifferentSign<F>(this F a, in F b) where F : IFloatingPoint<F>
    {
        return a * b < F.Zero;
    }

    public static bool InNormalRangeInclusive<F>(this F f) where F : INumber<F>
    {
        return f >= F.Zero && f <= F.One;   
    }
    
    public static bool InNormalRangeExclusive<F>(this F f) where F : INumber<F>
    {
        return f > F.Zero && f < F.One;   
    }
    
    public static F DoubleTriArea<F>(F aX, F aY, F bX, F bY, F cX, F cY) where F : INumber<F>
    {
        return ((aX - cX) * (bY - cY)) - ((aY - cY) * (bX - cX));
    }
    
    public static F DoubleTriArea<TVec, F>(in TVec a, in TVec b, in TVec c) where TVec : IVector2<F> where F : INumber<F>
    {
        return ((a.X - c.X) * (b.Y - c.Y)) - ((a.Y - c.Y) * (b.X - c.X));
    }
    
    public static bool CollinearHelper<F>(F aX, F aY, F bX, F bY, F cX, F cY) where F : IFloatingPointIeee754<F>
    {
        return ApproxZero((aX * (bY - cY)) + (bX * (cY - aY)) + (cX * (aY - bY)));
    }
}