namespace Helion.Geometry.New.Graphs;

public interface IGraph<TVertex, TEdge>
{
    public IEnumerable<TEdge> GetExitingEdges(in TVertex node);
}