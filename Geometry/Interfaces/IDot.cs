using System.Numerics;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Interfaces;

public interface IDot2<TSelf, F> where F : INumber<F> where TSelf : IVector2<F>
{
    F Dot(in TSelf vec);
    F Dot<TOtherVec>(in TOtherVec vec) where TOtherVec : IVector2<F>;
}