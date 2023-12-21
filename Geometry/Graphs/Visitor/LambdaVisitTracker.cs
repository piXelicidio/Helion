namespace Helion.Geometry.New.Graphs.Visitor;

public class LambdaVisitTracker<TVertex>(Func<TVertex, bool> checkVisited, Action<TVertex> markVisited) : 
    INodeVisitTracker<TVertex>
{
    public bool WasVisited(TVertex vertex) => checkVisited(vertex);
    public void MarkVisited(TVertex vertex) => markVisited(vertex);
}