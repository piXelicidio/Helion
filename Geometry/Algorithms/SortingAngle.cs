using System.Numerics;
using Helion.Geometry.New.Interfaces;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Algorithms;

public static class SortingAngle
{
    public static F CalculateCosTheta<F, TVertex, TVector>(in TVertex first, in TVertex second, in TVertex third)
        where F : IFloatingPoint<F>, IRootFunctions<F>
        where TVertex : IVector2<F>, ISubtractionOperators<TVertex, TVertex, TVector>
        where TVector : IVector2<F>, ICreatable2<F, F, TVector>, IDot2<TVector, F>, ILength<F>
    {
        TVector a = second - first;
        TVector b = third - first;
        
        // The perpendicular dot product tells us the side `third` is on, <= is on/right.
        TVector cross = TVector.Create(third.Y - first.Y, first.X - third.X);
        bool onRight = a.Dot(cross) <= F.Zero;
        
        // Find `cos(angle)` from `a.b / |a||b|`
        F result = a.Dot(b) / F.Sqrt(a.LengthSquared() * b.LengthSquared());
        
        // This maps it onto the quadrant. This treats (first, second) as if everything was rotated
        // to where that pair is (1, 0). This number should result in a value from [0, 3), whereby
        // a series of vectors turned into this value from [0, 3) can be sorted and have the min
        // value used as the most clockwise value.
        // The JIT optimizer should fold `1 + 1 - result` into `2 - result`.
        return onRight ? result + F.One : F.One + F.One - result;
    }
}