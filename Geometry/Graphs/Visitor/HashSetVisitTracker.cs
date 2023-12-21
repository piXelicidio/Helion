namespace Helion.Geometry.New.Graphs.Visitor;

public class HashSetVisitTracker<TVertex> : INodeVisitTracker<TVertex>
{
    private readonly HashSet<TVertex> m_visited = [];

    public bool WasVisited(TVertex vertex) => m_visited.Contains(vertex);
    public void MarkVisited(TVertex vertex) => m_visited.Add(vertex);
}