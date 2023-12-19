using System.Numerics;

namespace Helion.Geometry.New.Interfaces;

public interface IPerpDot<TVec, F> where F : INumber<F>
{
    F PerpDot(in TVec point);
}