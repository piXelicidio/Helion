namespace Helion.Geometry.New.Graphs;

public interface ITerminalEdge<TNode>
{
    TNode End { get; }
}

public interface IEdge<TNode> : ITerminalEdge<TNode>
{
    TNode Start { get; }
}