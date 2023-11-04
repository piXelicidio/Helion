using System;
using GlmSharp;
using Helion.Render.Common.Enums;
using Helion.Render.OpenGL.Buffer.Array.Vertex;
using Helion.Render.OpenGL.Shared;
using Helion.Render.OpenGL.Vertex;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.SkyNew.Geometry;

public class SkyGeometry : IDisposable
{
    public bool Flipped;
    private readonly StaticVertexBuffer<SkyGeometryVertex> m_staticVbo;
    private readonly StaticVertexBuffer<SkyGeometryVertex> m_dynamicVbo;
    private readonly VertexArrayObject m_staticVao;
    private readonly VertexArrayObject m_dynamicVao;
    private readonly SkyGeometryProgram m_program = new(); 
    private bool m_disposed;

    public bool HasGeometry => !m_staticVbo.Empty || !m_dynamicVbo.Empty;

    public SkyGeometry(SkyHandle handle, string lookupNameLabel)
    {
        m_staticVbo = new($"Static sky geometry {lookupNameLabel} (handle {handle})");
        m_staticVao = new($"Static sky geometry {lookupNameLabel} (handle {handle})");
        m_dynamicVbo = new($"Dynamic sky geometry {lookupNameLabel} (handle {handle})");
        m_dynamicVao = new($"Dynamic sky geometry {lookupNameLabel} (handle {handle})");
    }
    
    ~SkyGeometry()
    {
        PerformDispose();
    }

    public void Add(GeometryType type, ReadOnlySpan<SkyGeometryVertex> vertices)
    {
        StaticVertexBuffer<SkyGeometryVertex> vbo = type switch
        {
            GeometryType.Static => m_staticVbo,
            GeometryType.Dynamic => m_dynamicVbo,
            _ => throw new($"Unknown sky geometry type {(int)type}")
        };
        
        for (int i = 0; i < vertices.Length; i++)
            vbo.Add(vertices[i]);
    }

    // Resets the dynamic geometry so it can be populated again.
    public void Reset()
    {
        m_dynamicVbo.Clear();
    }

    public void Render(RenderInfo info)
    {
        mat4 mvp = Renderer.CalculateMvpMatrix(info);
        
        m_program.Bind();
        m_program.Mvp(mvp);

        m_staticVao.Bind();
        m_staticVbo.DrawArrays();
        m_staticVao.Unbind();
        
        m_dynamicVao.Bind();
        m_dynamicVbo.DrawArrays();
        m_dynamicVao.Unbind();
        
        m_program.Unbind();
    }

    public void Dispose()
    {
        PerformDispose();
        GC.SuppressFinalize(this);
    }

    private void PerformDispose()
    {
        if (m_disposed)
            return;
    
        m_staticVbo.Dispose();
        m_staticVao.Dispose();
        m_dynamicVbo.Dispose();
        m_dynamicVao.Dispose();
        m_program.Dispose();

        m_disposed = true;
    }
}