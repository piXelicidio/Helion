﻿using System.Numerics;
using Helion.Geometry.New.Interfaces;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Segments;

public interface ISeg<TVec> : IDimensional
{
    TVec Start { get; }
    TVec End { get; }
}

public interface ISeg2<TVec, F> : ISeg<TVec> where TVec : IVector2<F> where F : INumber<F>;
public interface ISeg3<TVec, F> : ISeg<TVec> where TVec : IVector3<F> where F : INumber<F>;