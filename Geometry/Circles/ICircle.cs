using System.Numerics;
using Helion.Geometry.New.Interfaces;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Circles;

public interface ICircle<F, TVec> : 
    IDimensional,
    ILength<F>
    where F : INumber<F> 
    where TVec : IVector2<F>
{
    F Area { get; }
}