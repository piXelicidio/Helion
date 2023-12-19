using System.Numerics;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Segments;

public interface ISeg2<TVec, F> where TVec : IVector2<F> where F : INumber<F>
{
    TVec Start { get; }
    TVec End { get; }
}
