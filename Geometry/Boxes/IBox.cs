using System.Numerics;
using Helion.Geometry.New.Interfaces;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Boxes;

public interface IBox<TVec> : IDimensional
{
    TVec Min { get; }
    TVec Max { get; }
}

public interface IBox2<TVec, F> : IBox<TVec> where TVec : IVector2<F> where F : INumber<F>
{
    F Area { get; }
}

public interface IBox3<TVec, F> : IBox<TVec> where TVec : IVector3<F> where F : INumber<F>
{
    F Volume { get; }
}