using System;
using System.Collections.Generic;
using System.Diagnostics;
using Helion.Render.Common.Enums;
using Helion.Render.OpenGL.Renderers.Legacy.World.SkyNew.Geometry;
using Helion.Render.OpenGL.Renderers.Legacy.World.SkyNew.Sphere;
using Helion.Render.OpenGL.Shared;
using Helion.Util.Container;
using OpenTK.Graphics.OpenGL;
using ResourceTexture = Helion.Resources.Texture;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.SkyNew;

public class SkyRenderer : IDisposable
{
    public const string Sky1HandleName = "SKY1";
    public const string Sky2HandleName = "SKY2";
    
    private readonly Dictionary<string, SkyHandle> m_nameToHandle = new(StringComparer.OrdinalIgnoreCase);
    private readonly DynamicArray<SkyTexture> m_handleToSkyTexture = new(); // Does not own the value.
    private readonly DynamicArray<SkyGeometry> m_skyHandleToGeometry = new(); // Owns the value (must dispose).
    private readonly Dictionary<int, SkyTexture> m_textureIdxToSkyTexture = new(); // Owns the value (must dispose).
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

    public void AddGeometry(SkyHandle handle, GeometryType type, ReadOnlySpan<SkyGeometryVertex> vertices)
    {
        m_skyHandleToGeometry[handle].Add(type, vertices);
    }

    public SkyHandle GetOrAllocateHandle(string lookupName)
    {
        if (m_nameToHandle.TryGetValue(lookupName, out SkyHandle handle)) 
            return handle;

        // We don't expect anyone right now to have more than 254 skies. This is
        // a limitation for the stencil buffer. It can be worked around in the
        // future if there is a case where this matters. Right now we will return
        // the last allocated sky so it doesn't affect sky1/sky2. It will render
        // incorrectly by drawing the wrong texture, but we will accept that for now.
        if (m_nextAvailableHandleIdx >= 255)
            return new(m_nextAvailableHandleIdx - 1);
        
        Debug.Assert(m_nextAvailableHandleIdx == m_skyHandleToGeometry.Length, "Sky handle-to-geometry desync");
        SkyGeometry geometry = new();
        m_skyHandleToGeometry.Add(geometry);
        
        handle = new(m_nextAvailableHandleIdx++);
        m_nameToHandle[lookupName] = handle;
        
        return handle;
    }
    
    public void SetHandleFlipped(SkyHandle handle, bool flipped)
    {
        Debug.Assert(handle < m_skyHandleToGeometry.Length, "Sky handle is out of range when setting 'flipped', may be a stale handle");
        
        m_skyHandleToGeometry[handle].Flipped = flipped;
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

            geometry.Render(renderInfo);

            GL.ColorMask(true, true, true, true);
            GL.StencilFunc(StencilFunction.Equal, stencilIndex, 0xFF);
            GL.Disable(EnableCap.DepthTest);

            SkyTexture texture = m_handleToSkyTexture[handleIdx];
            m_skySphere.Render(renderInfo, texture, geometry.Flipped);
                
            GL.Enable(EnableCap.DepthTest);
        }

        GL.Disable(EnableCap.StencilTest);
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