namespace Helion.Geometry.New.Interfaces;

public interface ICreatable2<TFirst, TSecond, out TResult>
{
    static abstract TResult Create(in TFirst first, in TSecond second);
}