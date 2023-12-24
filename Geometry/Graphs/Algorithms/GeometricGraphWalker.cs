using System.Diagnostics;
using System.Numerics;
using Helion.Geometry.New.Graphs.Visitor;
using Helion.Geometry.New.Interfaces;

namespace Helion.Geometry.New.Graphs.Algorithms;

public static class GeometricGraphWalker
{
    // Will aggressively walk clockwise and return each edge it encounters.
    private static IEnumerable<TEdge> WalkClockwiseGreedy<F, TVertex, TEdge, TEdgeSource, TNodeVisitTracker>(this TEdgeSource graph,
            TEdge startingEdge, Func<TEdge, bool> shouldWalk, TNodeVisitTracker visitTracker)
        where F : struct, INumber<F>, IMinMaxValue<F>
        where TVertex : IEqualityOperators<TVertex, TVertex, bool>
        where TEdge : IEdge<TVertex>, IClockwiseSortAngle<TVertex, F>
        where TEdgeSource : IEdgeSource<TVertex, TEdge>
        where TNodeVisitTracker : INodeVisitTracker<TVertex>
    {
        Debug.Assert(!visitTracker.WasVisited(startingEdge.Start), "Already visited start when doing clockwise greedy walk");
        Debug.Assert(!visitTracker.WasVisited(startingEdge.End), "Already visited end when doing clockwise greedy walk");

        yield return startingEdge;

        bool firstWalk = true;
        TVertex start = startingEdge.Start;
        TVertex current = startingEdge.End;
        TEdge prevEdge = startingEdge;

        visitTracker.MarkVisited(current);

        while (current != start)
        {
            TEdge? edgeToWalk = default;
            F bestSortAngle = F.MaxValue;
            
            foreach (TEdge edge in graph.GetEdges(current))
            {
                // Due to the state we track, don't walk back to the start on our
                // first walk. It's probably the case that a bidirectional graph
                // has a two-way edge back to itself, so we will not use that edge.
                // We do this also because we have not marked `start` so we can
                // eventually return to it.
                if (firstWalk && edge.End == start)
                    continue;
                
                if (!shouldWalk(edge) || visitTracker.WasVisited(edge.End))
                    continue;

                // Remember if it was the best one seen so far out of all the edges,
                // since we want to walk as tightly clockwise as possible.
                F sortAngle = prevEdge.CalculateClockwiseSortAngle(edge.End);
                if (sortAngle < bestSortAngle)
                {
                    bestSortAngle = sortAngle;
                    edgeToWalk = edge;
                }
            }

            // If we walked down a terminal chain (because we are greedily going clockwise)
            // then we cannot proceed any further.
            if (edgeToWalk == null)
                break;
            
            current = edgeToWalk.End;
            firstWalk = false;
            visitTracker.MarkVisited(current);
            
            yield return edgeToWalk;
        }
    }
}