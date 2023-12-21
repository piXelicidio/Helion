using System.Numerics;
using Helion.Geometry.New.Segments;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Graphs.Algorithms;

public static class GeometricGraphWalker
{
    public static void WalkWindingOrder<F, TVertex, TEdge>(this IGeometricGraph2<F, TVertex, TEdge> graph, TVertex start,
            Func<TEdge, bool> shouldWalk)
        where F : INumber<F>
        where TVertex : IVector2<F>, IEqualityOperators<TVertex, TVertex, bool>
        where TEdge : ISeg2<TVertex, F>
    {
        // TODO
    }
}