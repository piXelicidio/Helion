using System.Numerics;

namespace Helion.Geometry.New.Interfaces;

public interface ILength<F> where F : INumber<F>
{
    F Length();
    F LengthSquared();
}