namespace Helion.Geometry.New.Interfaces;

public interface ILerp<TOther, F, TResult>
{
    TResult Lerp(in TOther other, in F amount);
}