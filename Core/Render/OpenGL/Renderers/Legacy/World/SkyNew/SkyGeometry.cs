using System;
using Helion.Render.OpenGL.Shared;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.SkyNew;

public class SkyGeometry : IDisposable
{
    private bool m_disposed;

    public bool HasGeometry => false;

    ~SkyGeometry()
    {
        PerformDispose();
    }

    public void RenderGeometry(RenderInfo info)
    {
        // TODO
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
    
        // TODO

        m_disposed = true;
    }
}