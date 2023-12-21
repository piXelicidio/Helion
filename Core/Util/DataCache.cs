using Helion.Audio;
using Helion.Audio.Impl;
using Helion.Audio.Impl.Components;
using Helion.Util.Container;
using Helion.World.Entities;
using Helion.World.Geometry.Sectors;
using System.Collections.Generic;
using Helion.Audio.Sounds;
using Helion.Render.OpenGL.Texture.Legacy;
using Helion.World.Entities.Definition;
using Helion.Models;
using Helion.Geometry.Vectors;
using NLog;
using Helion.Render.OpenGL.Texture.Fonts;
using Helion.Render.OpenGL.Renderers.Legacy.Hud;
using Helion.Render.OpenGL.Shared.World.ViewClipping;
using Helion.Render.Common.Enums;
using Helion.World.Special.Specials;
using Helion.World;
using Font = Helion.Graphics.Fonts.Font;
using Helion.Graphics;
using System;

namespace Helion.Util;

public class DataCache
{
    private const int DefaultLength = 1024;
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly DynamicArray<Entity> m_entities = new(DefaultLength);
    private readonly DynamicArray<LinkableNode<Entity>> m_entityNodes = new(DefaultLength);
    private readonly DynamicArray<LinkableNode<Sector>> m_sectorNodes = new(DefaultLength);
    private readonly DynamicArray<IAudioSource> m_audioSources = new();
    private readonly DynamicArray<DynamicArray<Entity>> m_entityLists = new();
    private readonly DynamicArray<DynamicArray<RenderableGlyph>> m_glyphs = new();
    private readonly DynamicArray<List<RenderableSentence>> m_sentences = new();
    private readonly DynamicArray<RenderableString> m_strings = new();
    private readonly DynamicArray<HudDrawBufferData> m_hudDrawBufferData = new();
    private readonly DynamicArray<LinkedListNode<ClipSpan>> m_clipSpans = new();
    private readonly DynamicArray<LinkedListNode<IAudioSource>> m_audioNodes = new();
    private readonly DynamicArray<LinkedListNode<WaitingSound>> m_waitingSoundNodes = new();
    private readonly DynamicArray<LightChangeSpecial> m_lightChanges = new();
    public WeakEntity?[] WeakEntities = new WeakEntity?[DefaultLength];

    public bool CacheEntities = true;

    // Clear pointers to references that could keep the world around and prevent garbage collection.
    public void FlushReferences()
    {
        for (int i = 0; i < m_entities.Capacity; i++)
        {
            Entity? entity = m_entities[i];
            if (entity == null!)
                continue;
            entity.IntersectSectors.FlushReferences();
            entity.IntersectMovementSectors.FlushReferences();
        }

        for (int i = 0; i < m_entityLists.Capacity; i++)
        {
            if (m_entityLists[i] == null!)
                continue;
            m_entityLists[i].FlushReferences();
        }
    }

    public Entity GetEntity(int id, int thingId, EntityDefinition definition, in Vec3D position, double angleRadians,
        Sector sector)
    {
        if (m_entities.Length > 0)
        {
            var entity = m_entities.RemoveLast();
            entity.Set(id, thingId, definition, position, angleRadians, sector);
            return entity;
        }

        Entity newEnity = new();
        newEnity.Set(id, thingId, definition, position, angleRadians, sector);
        return newEnity;
    }

    public Entity GetEntity(EntityModel entityModel, EntityDefinition definition, IWorld world)
    {
        if (m_entities.Length > 0)
        {
            var entity = m_entities.RemoveLast();
            entity.Set(entityModel, definition, world);
            return entity;
        }

        Entity newEnity = new();
        newEnity.Set(entityModel, definition, world);
        return newEnity;
    }

    public bool FreeEntity(Entity entity)
    {
        if (!CacheEntities || entity.IsPlayer)
            return false;

        m_entities.Add(entity);
        return true;
    }

    public LinkableNode<Entity> GetLinkableNodeEntity(Entity entity)
    {
        if (m_entityNodes.Length > 0)
        {
            var node = m_entityNodes.RemoveLast();
            node.Value = entity;
            return node;
        }
        
        return new LinkableNode<Entity> { Value = entity };
    }

    public void FreeLinkableNodeEntity(LinkableNode<Entity> node)
    {
        node.Previous = null!;
        node.Next = null;
        node.Value = null!;
        m_entityNodes.Add(node);
    }

    public LinkableNode<Sector> GetLinkableNodeSector(Sector sector)
    {
        LinkableNode<Sector> node;
        if (m_sectorNodes.Length > 0)
        {
            node = m_sectorNodes.RemoveLast();
            node.Value = sector;
            return node;
        }

        return new LinkableNode<Sector> { Value = sector };
    }

    public void FreeLinkableNodeSector(LinkableNode<Sector> node)
    {
        node.Previous = null!;
        node.Next = null;
        node.Value = null!;
        m_sectorNodes.Add(node);
    }

    public OpenALAudioSource GetAudioSource(OpenALAudioSourceManager owner, OpenALBuffer buffer, in AudioData audioData)
    {
        if (m_audioSources.Length > 0)
        {
            OpenALAudioSource audioSource = (OpenALAudioSource)m_audioSources.RemoveLast();
            audioSource.Set(owner, buffer, audioData);            
            return audioSource;
        }

        return new OpenALAudioSource(owner, buffer, audioData);
    }

    public void FreeAudioSource(IAudioSource audioSource)
    {
        if (audioSource is not OpenALAudioSource)
            return;

        audioSource.AudioData.SoundSource.ClearSound(audioSource, audioSource.AudioData.SoundChannelType);
        audioSource.CacheFree();
        m_audioSources.Add(audioSource);
    }

    public DynamicArray<Entity> GetEntityList()
    {
        if (m_entityLists.Length > 0)
            return m_entityLists.RemoveLast();

        return new DynamicArray<Entity>();
    }

    public void FreeEntityList(DynamicArray<Entity> list)
    {
        list.Clear();
        m_entityLists.Add(list);
    }

    public List<RenderableSentence> GetRenderableSentences()
    {
        if (m_sentences.Length > 0)
            return m_sentences.RemoveLast();

        return new List<RenderableSentence>();
    }

    private void FreeRenderableSentences(List<RenderableSentence> list)
    {
        list.Clear();
        m_sentences.Add(list);
    }

    public DynamicArray<RenderableGlyph> GetRenderableGlyphs()
    {
        if (m_glyphs.Length > 0)
            return m_glyphs.RemoveLast();

        return new DynamicArray<RenderableGlyph>(256);
    }

    private void FreeRenderableGlyphs(DynamicArray<RenderableGlyph> list)
    {
        list.Clear();
        m_glyphs.Add(list);
    }

    public RenderableString GetRenderableString(ReadOnlySpan<char> str, Font font, int fontSize, TextAlign align = TextAlign.Left,
        int maxWidth = int.MaxValue, Color? drawColor = null)
    {
        if (m_strings.Length > 0)
        {
            var renderableString = m_strings.RemoveLast();
            renderableString.Set(this, str, font, fontSize, align, maxWidth, drawColor);
            return renderableString;
        }

        return new RenderableString(this, str, font, fontSize, align, maxWidth, drawColor);
    }

    public void FreeRenderableString(RenderableString renderableString)
    {
        if (!renderableString.ShouldFree)
            return;

        FreeRenderableStringData(renderableString);
        m_strings.Add(renderableString);
    }

    public void FreeRenderableStringData(RenderableString renderableString)
    {
        for (int i = 0; i < renderableString.Sentences.Count; i++)
            FreeRenderableGlyphs(renderableString.Sentences[i].Glyphs);
        FreeRenderableSentences(renderableString.Sentences);

        renderableString.Sentences = null!;
        renderableString.Font = null!;
    }

    public HudDrawBufferData GetDrawHudBufferData(GLLegacyTexture texture)
    {
        if (m_hudDrawBufferData.Length > 0)
        {
            var buffer = m_hudDrawBufferData.RemoveLast();
            buffer.Set(texture);
            return buffer;
        }

        return new HudDrawBufferData(texture);
    }

    public void FreeDrawHudBufferData(HudDrawBufferData data)
    {
        data.Texture = null!;
        data.Vertices.Clear();
        m_hudDrawBufferData.Add(data);
    }
    
    public LinkedListNode<ClipSpan> GetClipSpan(ClipSpan clipSpan)
    {
        if (m_clipSpans.Length > 0)
        {
            var node = m_clipSpans.RemoveLast();
            node.Value = clipSpan;
            return node;
        }

        return new LinkedListNode<ClipSpan>(clipSpan);
    }

    public void FreeClipSpan(LinkedListNode<ClipSpan> clipSpan)
    {
        m_clipSpans.Add(clipSpan);
    }
    
    public LinkedListNode<IAudioSource> GetAudioNode(IAudioSource audio)
    {
        if (m_audioNodes.Length > 0)
        {
            var node = m_audioNodes.RemoveLast();
            node.Value = audio;
            return node;
        }

        return new LinkedListNode<IAudioSource>(audio);
    }

    public void FreeAudioNode(LinkedListNode<IAudioSource> audio)
    {
        audio.Value = null!;
        m_audioNodes.Add(audio);
    }
    
    public LinkedListNode<WaitingSound> GetWaitingSoundNode(WaitingSound sound)
    {
        if (m_waitingSoundNodes.Length > 0)
        {
            var node = m_waitingSoundNodes.RemoveLast();
            node.Value = sound;
            return node;
        }

        return new LinkedListNode<WaitingSound>(sound);
    }

    public void FreeWaitingSoundNode(LinkedListNode<WaitingSound> audio)
    {
        audio.Value = default;
        m_waitingSoundNodes.Add(audio);
    }

    public LightChangeSpecial GetLightChangeSpecial(IWorld world, Sector sector, short lightLevel, int fadeTics)
    {
        if (m_lightChanges.Length > 0)
        {
            var spec = m_lightChanges.RemoveLast();
            spec.Set(world, sector, lightLevel, fadeTics);
            return spec;
        }

        return new LightChangeSpecial(world, sector, lightLevel, fadeTics);
    }

    public void FreeLightChangeSpecial(LightChangeSpecial special)
    {
        special.World = null!;
        special.Sector = null!;
        m_lightChanges.Add(special);
    }
}
