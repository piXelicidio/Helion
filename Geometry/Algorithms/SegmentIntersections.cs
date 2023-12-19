using System.Numerics;
using Helion.Geometry.New.Segments;
using Helion.Geometry.New.Vectors;
using static Helion.Geometry.New.MathHelper;

namespace Helion.Geometry.New.Algorithms;

public static class SegmentIntersections
{
    public static F Intersection<F, TVec, TSeg>(this TSeg self, in TSeg seg)
        where F : IFloatingPoint<F>, IFloatingPointIeee754<F>
        where TVec : IVector2<F>
        where TSeg : ISeg2<TVec, F>
    {
        F areaStart = DoubleTriArea(self.Start.X, self.Start.Y, self.End.X, self.End.Y, seg.End.X, seg.End.Y);
        F areaEnd = DoubleTriArea(self.Start.X, self.Start.Y, self.End.X, self.End.Y, seg.Start.X, seg.Start.Y);

        if (areaStart.DifferentSign(areaEnd))
        {
            F areaThisStart = DoubleTriArea(seg.Start.X, seg.Start.Y, seg.End.X, seg.End.Y, self.Start.X, self.Start.Y);
            F areaThisEnd = DoubleTriArea(seg.Start.X, seg.Start.Y, seg.End.X, seg.End.Y, self.End.X, self.End.Y);
            if (areaThisStart.DifferentSign(areaThisEnd))
                return areaThisStart / (areaThisStart - areaThisEnd);
        }

        return F.NaN;
    }

    public static bool Intersects<F, TVec, TSeg>(this TSeg self, in TSeg seg, out F t)
        where F : IFloatingPoint<F>, IFloatingPointIeee754<F>
        where TVec : IVector2<F>
        where TSeg : ISeg2<TVec, F>
    {
        t = self.Intersection<F, TVec, TSeg>(seg);
        return t.InNormalRangeExclusive();
    }   
}