using System.Numerics;
using Helion.Geometry.New.Interfaces;

namespace Helion.Geometry.New.Boxes.Algorithms;

public static class Bounding
{
    public static TBoxResult BoundPoints<TVecInput, TVecResult, TBoxResult>(ReadOnlySpan<TVecInput> points)
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
    
    public static TBoxResult BoundPoints<TVecInput, TVecResult, TBoxResult>(IEnumerable<TVecInput> points)
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
    
    public static TBoxResult BoundBoxes<TBoxInput, TBoxVector, TBoxResult>(ReadOnlySpan<TBoxInput> boxes)
        where TBoxInput : IBox<TBoxVector>
        where TBoxVector : IMinMaxValue<TBoxVector>, IMin<TBoxVector>, IMax<TBoxVector>
        where TBoxResult : ICreatable2<TBoxVector, TBoxVector, TBoxResult>
    {
        TBoxVector min = TBoxVector.MaxValue;
        TBoxVector max = TBoxVector.MinValue;

        foreach (TBoxInput box in boxes)
        {
            min = min.Min(box.Min);
            max = min.Max(box.Max);
        }

        return TBoxResult.Create(min, max);
    }
    
    public static TBoxResult BoundBoxes<TBoxInput, TBoxVector, TBoxResult>(IEnumerable<TBoxInput> boxes)
        where TBoxInput : IBox<TBoxVector>
        where TBoxVector : IMinMaxValue<TBoxVector>, IMin<TBoxVector>, IMax<TBoxVector>
        where TBoxResult : ICreatable2<TBoxVector, TBoxVector, TBoxResult>
    {
        TBoxVector min = TBoxVector.MaxValue;
        TBoxVector max = TBoxVector.MinValue;

        foreach (TBoxInput box in boxes)
        {
            min = min.Min(box.Min);
            max = min.Max(box.Max);
        }

        return TBoxResult.Create(min, max);
    }
}