using System;
using System.Collections.Generic;
using Helion.Render.OpenGL.Shared;
using OpenTK.Graphics.OpenGL;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.SkyNew;

public class SkyRenderer : IDisposable
{
    private readonly List<SkyGeometry> m_skies = new();
    private bool m_disposed;

    ~SkyRenderer()
    {
        PerformDispose();
    }
    
    public void Render(RenderInfo renderInfo)
    {
        if (m_skies.Count == 0)
            return;

        GL.Enable(EnableCap.StencilTest);
        GL.StencilMask(0xFF);
        GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);

        for (int i = 0; i < m_skies.Count; i++)
        {
            SkyGeometry sky = m_skies[i];
            if (!sky.HasGeometry)
                continue;

            int stencilIndex = i + 1;

            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.ColorMask(false, false, false, false);
            GL.StencilFunc(StencilFunction.Always, stencilIndex, 0xFF);

            sky.RenderWorldGeometry(renderInfo);

            GL.ColorMask(true, true, true, true);
            GL.StencilFunc(StencilFunction.Equal, stencilIndex, 0xFF);
            GL.Disable(EnableCap.DepthTest);

            sky.RenderSky(renderInfo);
                
            GL.Enable(EnableCap.DepthTest);
        }

        GL.Disable(EnableCap.StencilTest);
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
        
        foreach (SkyGeometry skyGeometry in m_skies)
            skyGeometry.Dispose();
        m_skies.Clear();

        m_disposed = true;
    }
}