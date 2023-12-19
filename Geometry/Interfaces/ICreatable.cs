namespace Helion.Geometry.New.Interfaces;

public interface ICreatable2<TFirst, TSecond, out TResult>
{
    static abstract TResult Create(in TFirst first, in TSecond second);
}

public interface ICreatable3<TFirst, TSecond, TThird, out TResult>
{
    static abstract TResult Create(in TFirst first, in TSecond second, in TThird third);
}