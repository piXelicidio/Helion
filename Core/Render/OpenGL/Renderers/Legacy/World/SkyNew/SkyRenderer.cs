using System;
using System.Collections.Generic;
using System.Diagnostics;
using Helion.Render.OpenGL.Renderers.Legacy.World.SkyNew.Sphere;
using Helion.Render.OpenGL.Shared;
using Helion.Util.Container;
using OpenTK.Graphics.OpenGL;
using ResourceTexture = Helion.Resources.Texture;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.SkyNew;

public class SkyRenderer : IDisposable
{
    private readonly Dictionary<string, SkyHandle> m_nameToHandle = new(StringComparer.OrdinalIgnoreCase);
    private readonly DynamicArray<SkyTexture> m_handleToSkyTexture = new();
    private readonly DynamicArray<SkyGeometry> m_skyHandleToGeometry = new();
    private readonly Dictionary<int, SkyTexture> m_textureIdxToSkyTexture = new();
    private readonly SkySphere m_skySphere = new();
    private int m_nextAvailableHandleIdx;
    private bool m_disposed;

    ~SkyRenderer()
    {
        PerformDispose();
    }

    public void Clear()
    {
        m_nextAvailableHandleIdx = 0;
        m_nameToHandle.Clear();
        m_handleToSkyTexture.Clear();
        DisposeAndClearSkyGeometry();
        DisposeAndClearSkyTextures();
    }

    public SkyHandle GetOrAllocateHandle(string lookupName)
    {
        if (m_nameToHandle.TryGetValue(lookupName, out SkyHandle handle)) 
            return handle;

        // We don't expect anyone right now to have more than 254 skies. This is
        // a limitation for the stencil buffer. It can be worked around in the
        // future if there is a case where this matters. Right now we will return
        // the last allocated sky so it doesn't crash. It will render incorrectly
        // by drawing the wrong texture, but we will accept that for now.
        if (m_nextAvailableHandleIdx >= 255)
            return new(m_nextAvailableHandleIdx - 1);
        
        handle = new(m_nextAvailableHandleIdx++);
        m_nameToHandle[lookupName] = handle;
        return handle;
    }

    public void SetHandleTexture(SkyHandle handle, ResourceTexture texture)
    {
        Debug.Assert(handle < m_nextAvailableHandleIdx, "Trying to use a stale sky handle");
        Debug.Assert(texture.Image != null, "Trying to use a texture which does not have a loaded image");

        if (!m_textureIdxToSkyTexture.TryGetValue(texture.Index, out SkyTexture? skyTexture))
        {
            skyTexture = new(texture);
            m_textureIdxToSkyTexture[texture.Index] = skyTexture;
        }

        m_handleToSkyTexture[handle] = skyTexture;
    }

    private bool HasNoSkyGeometry()
    {
        for (int i = 0; i < m_skyHandleToGeometry.Length; i++)
            if (m_skyHandleToGeometry[i].HasGeometry)
                return false;
        return true;
    }
    
    public void Render(RenderInfo renderInfo)
    {
        if (HasNoSkyGeometry())
            return;

        GL.Enable(EnableCap.StencilTest);
        GL.StencilMask(0xFF);
        GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
        
        for (int handleIdx = 0; handleIdx < m_skyHandleToGeometry.Length; handleIdx++)
        {
            SkyGeometry geometry = m_skyHandleToGeometry[handleIdx];
            if (!geometry.HasGeometry)
                continue;

            int stencilIndex = handleIdx + 1;

            GL.Clear(ClearBufferMask.StencilBufferBit);
            GL.ColorMask(false, false, false, false);
            GL.StencilFunc(StencilFunction.Always, stencilIndex, 0xFF);

            geometry.RenderGeometry(renderInfo);

            GL.ColorMask(true, true, true, true);
            GL.StencilFunc(StencilFunction.Equal, stencilIndex, 0xFF);
            GL.Disable(EnableCap.DepthTest);

            SkyTexture texture = m_handleToSkyTexture[handleIdx];
            RenderSphere(texture);
                
            GL.Enable(EnableCap.DepthTest);
        }

        GL.Disable(EnableCap.StencilTest);
    }

    private void RenderSphere(SkyTexture texture)
    {
        // TODO
    }

    private void DisposeAndClearSkyGeometry()
    {
        for (int i = 0; i < m_skyHandleToGeometry.Length; i++)
            m_skyHandleToGeometry[i].Dispose();
        m_skyHandleToGeometry.Clear();
    }

    private void DisposeAndClearSkyTextures()
    {
        foreach (SkyTexture skyTexture in m_textureIdxToSkyTexture.Values)
            skyTexture.Dispose();
        m_textureIdxToSkyTexture.Clear();
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
        
        m_nameToHandle.Clear();
        m_handleToSkyTexture.Clear();
        DisposeAndClearSkyGeometry();
        DisposeAndClearSkyTextures();
        m_skySphere.Dispose();

        m_disposed = true;
    }
}