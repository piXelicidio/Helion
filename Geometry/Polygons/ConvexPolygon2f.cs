using System.Diagnostics;
using Helion.Geometry.New.Algorithms;
using Helion.Geometry.New.Boxes;
using Helion.Geometry.New.Segments;
using Helion.Geometry.New.Triangles;
using Helion.Geometry.New.Vectors;

namespace Helion.Geometry.New.Polygons;

public readonly ref struct ConvexPolygon2f
{
    public readonly ReadOnlySpan<Vec2> Vertices;
    
    public int Count => Vertices.Length;

    public ConvexPolygon2f(ReadOnlySpan<Vec2> vertices)
    {
        Vertices = vertices;
    }
    
    public Vec2 this[int index] => Vertices[index];

    public bool Contains(in Vec2 vec)
    {
        for (int i = 0; i < Vertices.Length - 1; i++)
            if (!new Seg2(Vertices[i], Vertices[i + 1]).OnRight(vec))
                return false;
        return true;
    }

    public Box2 CalculateBox()
    {
        return Bounding.BoundPoints<Vec2, Vec2, Box2>(Vertices);
    }
    
    public void GetTriangles(Span<Triangle2f> triangles) 
    {
        Debug.Assert(triangles.Length < Vertices.Length - 2, "Not enough stack space to make triangles from convex polygon");
        
        for (int i = 1; i < Vertices.Length - 1; i++)
            triangles[i - 1] = new(Vertices[0], Vertices[i], Vertices[i + 1]);
    }
}