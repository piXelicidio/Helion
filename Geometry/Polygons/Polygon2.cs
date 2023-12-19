using System.Collections;
using System.Numerics;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Polygons;

public class Polygon2<F, TVec>(TVec[] vertices) : IReadOnlyList<TVec> 
    where F : INumber<F>
    where TVec : IVector2<F> 
{
    private readonly TVec[] m_vertices = vertices;

    public int Count => m_vertices.Length;
    public ReadOnlySpan<TVec> Span => m_vertices;

    public Polygon2(IEnumerable<TVec> vertexEnumerable) : this(vertexEnumerable.ToArray())
    {
    }

    public TVec this[int index] => m_vertices[index];

    public IEnumerator<TVec> GetEnumerator() => (IEnumerator<TVec>)vertices.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => vertices.GetEnumerator();
}