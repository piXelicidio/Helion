using System.Numerics;

namespace Helion.Geometry.New.Interfaces;

public interface IDistance<TSelf, F> where F : INumber<F>
{
    F Distance(in TSelf other);
    F DistanceSquared(in TSelf other);
}

public interface IDistanceGeneric<TSelf, F> where F : INumber<F>
{
    F Distance<TOther>(in TOther other) where TOther : IConvertTo<TSelf>;
    F DistanceSquared<TOther>(in TOther other) where TOther : IConvertTo<TSelf>;
}