namespace Helion.Geometry.New.Graphs;

public interface IEdgeSource<TNode, TEdge>
{
    IEnumerable<TEdge> GetEdges(in TNode node);
}