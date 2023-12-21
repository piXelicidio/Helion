namespace Helion.Geometry.New.Graphs;

public interface IEdge<TNode>
{
    TNode Start { get; }
    TNode End { get; }
}