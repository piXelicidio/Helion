using System.Collections;
using System.Numerics;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Polygons;

public abstract class Polygon2<F, TVec> : IReadOnlyList<TVec> 
    where F : INumber<F>
    where TVec : IVector2<F> 
{
    protected readonly TVec[] Vertices;

    public int Count => Vertices.Length;
    public ReadOnlySpan<TVec> Span => Vertices;

    protected Polygon2(TVec[] vertices)
    {
        Vertices = vertices;
    }

    protected Polygon2(IEnumerable<TVec> vertices) : this(vertices.ToArray())
    {
    }

    public TVec this[int index] => Vertices[index];

    public IEnumerator<TVec> GetEnumerator() => (IEnumerator<TVec>)Vertices.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Vertices.GetEnumerator();
}