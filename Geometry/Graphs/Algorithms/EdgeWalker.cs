using System.Diagnostics;
using Helion.Geometry.New.Graphs.Visitor;

namespace Helion.Geometry.New.Graphs.Algorithms;

public static class EdgeWalker
{
    public static IEnumerable<(TVertex, TEdge)> BreadthFirst<TVertex, TEdge, TVisitor>(this IGraph<TVertex, TEdge> graph,
            TVertex start, TVisitor visitTracker)
        where TVisitor : INodeVisitTracker<TVertex>
        where TEdge : IEdge<TVertex>
    {
        LinkedList<TEdge> edgesToVisit = [];

        Debug.Assert(!visitTracker.WasVisited(start), "Trying to BFS when root already visited");
        visitTracker.MarkVisited(start);
        
        // Because of how the loop works, it's easier to do the first pass now.
        foreach (TEdge edge in graph.GetExitingEdges(start))
        {
            yield return (edge.End, edge);
            edgesToVisit.AddLast(edge);
        }
        
        while (edgesToVisit.First != null)
        {
            TEdge edge = edgesToVisit.First.Value;
            
            visitTracker.MarkVisited(edge.End);
            yield return (edge.End, edge);
        
            foreach (TEdge endNodeEdge in graph.GetExitingEdges(edge.End))
            {
                if (visitTracker.WasVisited(edge.End)) 
                    continue;
                
                yield return (endNodeEdge.End, endNodeEdge);
                edgesToVisit.AddLast(endNodeEdge);
            }
        }
    }
    
    public static IEnumerable<(TVertex, TEdge)> DepthFirst<TVertex, TEdge, TVisitor>(this IGraph<TVertex, TEdge> graph,
            TVertex start, TVisitor visitTracker)
        where TVisitor : INodeVisitTracker<TVertex>
        where TEdge : IEdge<TVertex>
    {
        Debug.Assert(!visitTracker.WasVisited(start), "Trying to DFS when root already visited");
        
        Stack<TVertex> visitStack = [];
        visitStack.Push(start);

        while (visitStack.TryPop(out TVertex? node))
        {
            visitTracker.MarkVisited(node);

            foreach (TEdge edge in graph.GetExitingEdges(node))
            {
                if (visitTracker.WasVisited(edge.End)) 
                    continue;
                
                yield return (edge.End, edge);
                visitStack.Push(edge.End);
            }
        }
    }
}