namespace Helion.Geometry.New.Interfaces;

public interface IOverlaps<TElement>
{
    bool Overlaps(in TElement element);
}