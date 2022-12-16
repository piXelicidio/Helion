﻿using Helion.Geometry.Vectors;
using Helion.Render.OpenGL.Buffer.Array.Vertex;
using Helion.Render.OpenGL.Shader;
using Helion.Render.OpenGL.Shared;
using Helion.Render.OpenGL.Shared.World.ViewClipping;
using Helion.Render.OpenGL.Texture;
using Helion.Render.OpenGL.Texture.Legacy;
using Helion.Render.OpenGL.Textures;
using Helion.Render.OpenGL.Vertex;
using Helion.Resources;
using Helion.Util.Container;
using Helion.Util.Extensions;
using Helion.World.Entities;
using Helion.World.Geometry.Sectors;
using Helion.World.Geometry.Subsectors;
using NLog;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.Entities.Optimized;

internal class OptimizedEntityData
{
    public readonly VertexArrayObject Vao;
    public readonly StreamVertexBuffer<EntityVertex> Vbo;
    public readonly GLLegacyTexture Texture;
    public int RenderCount;

    internal OptimizedEntityData(GLLegacyTexture texture, ProgramAttributes attributes)
    {
        Texture = texture;
        Vao = new(texture.Name);
        Vbo = new(texture.Name);
        Attributes.BindAndApply(Vbo, Vao, attributes);
    }
}

public class OptimizedEntityRenderer : IDisposable
{
    private readonly OptimizedEntityProgram m_program = new();
    private readonly LegacyGLTextureManager m_textureManager;
    private LookupArray<OptimizedEntityData> m_dataLookup = new();
    private List<OptimizedEntityData> m_renderData = new();
    private Entity m_cameraEntity = null!;
    private float m_tickFraction;
    private Vec2F m_viewRightNormal;
    private bool m_disposed;
    private int m_renderCount = 1;

    public OptimizedEntityRenderer(LegacyGLTextureManager textureManager)
    {
        m_textureManager = textureManager;
    }

    ~OptimizedEntityRenderer()
    {
        Dispose(false);
    }

    public void Clear(float tickFraction, Entity cameraEntity)
    {
        m_tickFraction = tickFraction;
        m_cameraEntity = cameraEntity;

        foreach (var data in m_renderData)
            data.Vbo.Clear();
        m_renderData.Clear();

        m_renderCount++;
    }

    public void SetViewDirection(Vec2D viewDir)
    {
        m_viewRightNormal = viewDir.RotateRight90().Unit().Float;
    }

    public bool ShouldNotDraw(Entity entity)
    {
        //m_EntityDrawnTracker.HasDrawn(entity)
        return entity.Frame.IsInvisible || entity.Flags.Invisible || entity.Flags.NoSector || ReferenceEquals(m_cameraEntity, entity);
    }

    public void RenderSubsector(Sector viewSector, Subsector subsector, in Vec3D position)
    {
        // TODO: Remove me later!
    }

    public void Add(Vec2D viewPos, Entity entity)
    {
        Vec3F position = entity.PrevPosition.Float.Interpolate(entity.Position.Float, m_tickFraction);
        byte lightLevel = (byte)entity.Sector.LightLevel.Clamp(0, 255);

        var spriteDef = m_textureManager.GetSpriteDefinition(entity.Frame.SpriteIndex);
        uint rotation;

        if (spriteDef != null && spriteDef.HasRotations)
        {
            uint viewAngle = ViewClipper.ToDiamondAngle(viewPos, position.XY.Double);
            uint entityAngle = ViewClipper.DiamondAngleFromRadians(entity.AngleRadians);
            rotation = CalculateRotation(viewAngle, entityAngle);
        }
        else
        {
            rotation = 0;
        }

        SpriteRotation spriteRotation;
        if (spriteDef != null)
            spriteRotation = m_textureManager.GetSpriteRotation(spriteDef, entity.Frame.Frame, rotation);
        else
            spriteRotation = m_textureManager.NullSpriteRotation;
        GLLegacyTexture texture = spriteRotation.Texture.RenderStore == null ? m_textureManager.NullTexture : (GLLegacyTexture)spriteRotation.Texture.RenderStore;

        if (!m_dataLookup.TryGetValue(texture.TextureId, out var entityData))
        {
            entityData = new(texture, m_program.Attributes);
            m_dataLookup.Set(texture.TextureId, entityData);
        }

        if (entityData.RenderCount != m_renderCount)
        {
            entityData.RenderCount = m_renderCount;
            m_renderData.Add(entityData);
        }

        EntityVertex vertex = new(position, lightLevel);
        entityData.Vbo.Add(vertex);
    }

    private static uint CalculateRotation(uint viewAngle, uint entityAngle)
    {
        const uint SpriteFrameRotationAngle = 9 * (uint.MaxValue / 16);

        // This works as follows:
        //
        // First we find the angle that we have to the entity. Since
        // facing along with the actor (ex: looking at their back) wants to
        // give us the opposite rotation side, we add 180 degrees to our
        // angle delta.
        //
        // Then we add 22.5 degrees to that as well because we don't want
        // a transition when we hit 180 degrees... we'd rather have ranges
        // of [180 - 22.5, 180 + 22.5] be the angle rather than the range
        // [180 - 45, 180].
        //
        // Then we can do a bit shift trick which converts the higher order
        // three bits into the angle rotation between 0 - 7.
        return unchecked((viewAngle - entityAngle + SpriteFrameRotationAngle) >> 29);
    }

    public void Render(RenderInfo renderInfo)
    {
        m_program.Bind();
        m_program.BoundTexture(TextureUnit.Texture0);
        m_program.ViewRightNormal(m_viewRightNormal);
        m_program.Mvp(Renderer.CalculateMvpMatrix(renderInfo));

        for (int i = 0; i < m_renderData.Count; i++)
        {
            var data = m_renderData[i];
            data.Texture.Bind();

            data.Vao.Bind();
            data.Vbo.Bind();
            data.Vbo.Upload();

            GL.DrawArrays(PrimitiveType.Points, 0, data.Vbo.Count);

            data.Vbo.Unbind();
            data.Vao.Unbind();
        }

        m_program.Unbind();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (m_disposed)
            return;
        for (int i = 0; i < m_renderData.Count; i++)
        {
            var data = m_renderData[i];
            data.Vbo.Dispose();
            data.Vao.Dispose();
        }
        m_program.Dispose();

        m_disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
