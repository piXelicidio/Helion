namespace Helion.Geometry.New.Interfaces;

public interface IContains<TElement>
{
    bool Contains(in TElement element);
}