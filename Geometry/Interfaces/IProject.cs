namespace Helion.Geometry.New.Interfaces;

public interface IProject<TOther, TResult>
{
    TResult Project(in TOther other);
}