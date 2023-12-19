namespace Helion.Geometry.New.Interfaces;

public interface IBound<TElement, TResult>
{
    TResult Bound(in TElement element);
}