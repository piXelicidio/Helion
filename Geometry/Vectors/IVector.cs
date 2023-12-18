using System.Numerics;
using Helion.Geometry.New.Interfaces;

namespace Helion.Geometry.New.Vectors;

public interface IVector2<F> : IDimensional where F : INumber<F>
{
    F X { get; }
    F Y { get; }
}

public interface IVector3<F> : IDimensional where F : INumber<F>
{
    F X { get; }
    F Y { get; }
    F Z { get; }
}

public interface IVector4<F> : IDimensional where F : INumber<F>
{
    F X { get; }
    F Y { get; }
    F Z { get; }
    F W { get; }
}