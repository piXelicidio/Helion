using System.Numerics;

namespace Helion.Geometry.New.Interfaces;

public interface IDeterminant<TVec, F> where F : INumber<F>
{
    F Determinant(in TVec end, in TVec point);
}