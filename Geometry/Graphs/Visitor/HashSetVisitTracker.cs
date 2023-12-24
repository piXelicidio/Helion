namespace Helion.Geometry.New.Graphs.Visitor;

public sealed class HashSetVisitTracker<TVertex> : INodeVisitTracker<TVertex>
{
    private readonly HashSet<TVertex> m_visited = [];

    public void Clear() => m_visited.Clear();
    public bool WasVisited(TVertex vertex) => m_visited.Contains(vertex);
    public void MarkVisited(TVertex vertex) => m_visited.Add(vertex);
}