using System.Collections;
using System.Numerics;
using Helion.Geometry.New.Interfaces;
using Helion.Geometry.New.Triangles;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Polygons;

public class ConvexPolygon2<F, TVec>(TVec[] vertices) : IReadOnlyList<TVec> 
    where F : INumber<F>
    where TVec : IVector2<F> 
{
    private readonly TVec[] m_vertices = vertices;

    public int Count => m_vertices.Length;
    public ReadOnlySpan<TVec> Span => m_vertices;

    public ConvexPolygon2(IEnumerable<TVec> vertexEnumerable) : this(vertexEnumerable.ToArray())
    {
    }

    public TVec this[int index] => m_vertices[index];

    public IEnumerable<TTriangle> GetTriangles<TTriangle>() 
        where TTriangle : Triangle2<F, TVec>, ICreatable3<TVec, TVec, TVec, TTriangle>
    {
        for (int i = 1; i < vertices.Length - 2; i++)
            yield return TTriangle.Create(vertices[0], vertices[i], vertices[i + 1]);
    }

    public IEnumerator<TVec> GetEnumerator() => (IEnumerator<TVec>)vertices.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => vertices.GetEnumerator();
}