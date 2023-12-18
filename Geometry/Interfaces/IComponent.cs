using System.Numerics;

namespace Helion.Geometry.New.Interfaces;

public interface IComponent<TOther, F> where F : INumber<F>
{
    F Component(in TOther other);
}