namespace Helion.Geometry.New.Interfaces;

public interface IMax<TSelf>
{
    TSelf Max(in TSelf other);
}

public interface IMaxGeneric<TSelf>
{
    TSelf Max<TOther>(in TOther other) where TOther : IConvertTo<TSelf>;
}