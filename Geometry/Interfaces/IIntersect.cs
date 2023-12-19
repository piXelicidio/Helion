namespace Helion.Geometry.New.Interfaces;

public interface IIntersects<TElement>
{
    bool Intersects(in TElement element);
}

public interface IIntersects<TElement, TIntersectionResult>
{
    bool Intersects(in TElement seg, out TIntersectionResult result);
}

public interface IIntersection<TElement, TIntersectionResult>
{
    TIntersectionResult Intersection(in TElement seg);
}