using System.Numerics;
using Helion.Geometry.New.Segments;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Graphs;

public interface IGeometricGraph2<F, TVertex, TEdge> : IGraph<TVertex, TEdge>
    where F : INumber<F>
    where TVertex : IVector2<F>
    where TEdge : ISeg2<TVertex, F>
{
}