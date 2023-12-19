using System.Numerics;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Triangles;

public interface ITriangle<TVec>
{
    TVec First { get; }
    TVec Second { get; }
    TVec Third { get; }
}

public interface ITriangle2<F, TVec> : ITriangle<TVec> where TVec : IVector2<F> where F : INumber<F>;