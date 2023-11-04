using System;
using GlmSharp;
using Helion.Render.Common.Enums;
using Helion.Render.OpenGL.Shared;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.SkyNew.Geometry;

public class SkyGeometry : IDisposable
{
    public bool Flipped;
    private readonly SkyGeometryProgram m_program = new(); 
    private bool m_disposed;

    public bool HasGeometry => false;

    ~SkyGeometry()
    {
        PerformDispose();
    }

    public void Add(GeometryType type, ReadOnlySpan<SkyGeometryVertex> vertices)
    {
        switch (type)
        {
        case GeometryType.Static:
            // TODO
            break;
        case GeometryType.Dynamic:
            // TODO
            break;
        }
    }

    public void Render(RenderInfo info)
    {
        mat4 mvp = Renderer.CalculateMvpMatrix(info);
        
        m_program.Bind();
        m_program.Mvp(mvp);

        // TODO: Render static geometry
        // TODO: Render dynamic geometry
        
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
    
        m_program.Dispose();

        m_disposed = true;
    }
}