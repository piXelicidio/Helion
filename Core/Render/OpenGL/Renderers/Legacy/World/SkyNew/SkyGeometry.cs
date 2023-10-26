using System;
using Helion.Render.OpenGL.Shared;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.SkyNew;

public class SkyGeometry : IDisposable
{
    //private readonly Dictionary<int, (Color Upper, Color Lower)> m_textureIdToFadeColors = new();
    private bool m_disposed;

    public bool HasGeometry => false;

    ~SkyGeometry()
    {
        PerformDispose();
    }

    public void RenderWorldGeometry(RenderInfo renderInfo)
    {
        // TODO
    }

    public void RenderSky(RenderInfo renderInfo)
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