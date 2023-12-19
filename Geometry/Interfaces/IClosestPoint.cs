namespace Helion.Geometry.New.Interfaces;

public interface IClosestPoint<TPoint, TResult>
{
    TResult ClosestPoint(in TPoint point);
}