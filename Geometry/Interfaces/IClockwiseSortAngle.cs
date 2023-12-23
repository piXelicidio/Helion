using System.Numerics;

namespace Helion.Geometry.New.Interfaces;

public interface IClockwiseSortAngle<TOther, F>
{
    F CalculateClockwiseSortAngle(in TOther other);
}