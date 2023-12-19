using System.Numerics;

namespace Helion.Geometry.New.Interfaces;

public interface IToTime<TElement, F> where F : INumber<F>
{
    F ToTime(in TElement point);
}