namespace Helion.Geometry.New.Graphs;

public interface IMarkable
{
    bool IsMarked { get; }
    
    void Mark();
    void Unmark();
}