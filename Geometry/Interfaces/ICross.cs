using System.Numerics;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Interfaces;

public interface ICross2<TOther, F> where F : INumber<F> where TOther : IVector2<F>
{
    F Cross(in TOther other);
    F Cross<TOtherVec>(in TOtherVec vec) where TOtherVec : IVector2<F>;
}