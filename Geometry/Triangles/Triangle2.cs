using System.Numerics;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Triangles;

public abstract class Triangle2<F, TVec>(TVec first, TVec second, TVec third) : 
    ITriangle2<F, TVec> 
    where TVec : IVector2<F> 
    where F : INumber<F>
{
    public TVec First => first;
    public TVec Second => second;
    public TVec Third => third;
}