namespace Helion.Geometry.New.Graphs.Visitor;

public interface INodeVisitTracker<TVertex>
{
    bool WasVisited(TVertex vertex);
    void MarkVisited(TVertex vertex);
}