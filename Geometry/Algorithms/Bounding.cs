using System.Numerics;
using Helion.Geometry.New.Interfaces;

namespace Helion.Geometry.New.Algorithms;

public static class Bounding
{
    public static TBoxResult Bound<TVecInput, TVecResult, TBoxResult>(ReadOnlySpan<TVecInput> points)
        where TVecInput : IConvertTo<TVecResult>
        where TVecResult : IMinMaxValue<TVecResult>, IMin<TVecResult>, IMax<TVecResult>
        where TBoxResult : ICreatable2<TVecResult, TVecResult, TBoxResult>
    {
        TVecResult min = TVecResult.MaxValue;
        TVecResult max = TVecResult.MinValue;

        foreach (TVecInput point in points)
        {
            TVecResult vec = point.ConvertTo();
            min = min.Min(vec);
            max = min.Max(vec);
        }

        return TBoxResult.Create(min, max);
    }
    
    public static TBoxResult Bound<TVecInput, TVecResult, TBoxResult>(IEnumerable<TVecInput> points)
        where TVecInput : IConvertTo<TVecResult>
        where TVecResult : IMinMaxValue<TVecResult>, IMin<TVecResult>, IMax<TVecResult>
        where TBoxResult : ICreatable2<TVecResult, TVecResult, TBoxResult>
    {
        TVecResult min = TVecResult.MaxValue;
        TVecResult max = TVecResult.MinValue;

        foreach (TVecInput point in points)
        {
            TVecResult vec = point.ConvertTo();
            min = min.Min(vec);
            max = min.Max(vec);
        }

        return TBoxResult.Create(min, max);
    }
}