using System.Numerics;

namespace Helion.Geometry.New.Interfaces;

public interface IFromTime<TVec, F> where F : INumber<F>
{
    TVec FromTime(in F t);
}