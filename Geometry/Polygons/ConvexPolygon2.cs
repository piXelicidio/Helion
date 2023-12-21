using System.Numerics;
using Helion.Geometry.New.Interfaces;
using Helion.Geometry.New.Triangles;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Polygons;

public abstract class ConvexPolygon2<F, TVec> : Polygon2<F, TVec> where F : INumber<F> where TVec : IVector2<F> 
{
    protected ConvexPolygon2(TVec[] vertices) : base(vertices)
    {
    }

    protected ConvexPolygon2(IEnumerable<TVec> vertices) : this(vertices.ToArray())
    {
    }

    public IEnumerable<TTriangle> GetTriangles<TTriangle>() 
        where TTriangle : Triangle2<F, TVec>, ICreatable3<TVec, TVec, TVec, TTriangle>
    {
        for (int i = 1; i < Vertices.Length - 1; i++)
            yield return TTriangle.Create(Vertices[0], Vertices[i], Vertices[i + 1]);
    }
}