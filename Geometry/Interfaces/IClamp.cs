namespace Helion.Geometry.New.Interfaces;

public interface IClamp<TMin, TMax, TResult>
{
    TResult Clamp(in TMin min, in TMax max);
}