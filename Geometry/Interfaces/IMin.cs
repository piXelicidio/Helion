namespace Helion.Geometry.New.Interfaces;

public interface IMin<TSelf>
{
    TSelf Min(in TSelf other);
}

public interface IMinGeneric<TSelf>
{
    TSelf Min<TOther>(in TOther other) where TOther : IConvertTo<TSelf>;
}