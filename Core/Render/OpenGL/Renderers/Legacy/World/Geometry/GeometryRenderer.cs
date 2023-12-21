using System;
using System.Collections.Generic;
using Helion.Geometry.Vectors;
using Helion.Render.OpenGL.Renderers.Legacy.World.Data;
using Helion.Render.OpenGL.Renderers.Legacy.World.Geometry.Portals;
using Helion.Render.OpenGL.Renderers.Legacy.World.Geometry.Static;
using Helion.Render.OpenGL.Renderers.Legacy.World.Sky;
using Helion.Render.OpenGL.Renderers.Legacy.World.Sky.Sphere;
using Helion.Render.OpenGL.Shader;
using Helion.Render.OpenGL.Shared;
using Helion.Render.OpenGL.Shared.World;
using Helion.Render.OpenGL.Texture.Legacy;
using Helion.Render.OpenGL.Textures;
using Helion.Resources;
using Helion.Resources.Archives.Collection;
using Helion.Util;
using Helion.Util.Configs;
using Helion.Util.Container;
using Helion.World;
using Helion.World.Geometry.Lines;
using Helion.World.Geometry.Sectors;
using Helion.World.Geometry.Sides;
using Helion.World.Geometry.Subsectors;
using Helion.World.Geometry.Walls;
using Helion.World.Physics;
using Helion.World.Static;
using static Helion.World.Geometry.Sectors.Sector;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.Geometry;

public class GeometryRenderer : IDisposable
{
    private const double MaxSky = 16384;
    private static readonly Sector DefaultSector = CreateDefault();

    public readonly List<IRenderObject> AlphaSides = new();
    public readonly PortalRenderer Portals;
    private readonly IConfig m_config;
    private readonly RenderProgram m_program;
    private readonly LegacyGLTextureManager m_glTextureManager;
    private readonly LineDrawnTracker m_lineDrawnTracker = new();
    private readonly StaticCacheGeometryRenderer m_staticCacheGeometryRenderer;
    private readonly DynamicArray<TriangulatedWorldVertex> m_subsectorVertices = new();
    private readonly DynamicArray<LegacyVertex> m_vertices = new();
    private readonly DynamicArray<SkyGeometryVertex> m_skyVertices = new();
    private readonly LegacyVertex[] m_wallVertices = new LegacyVertex[6];
    private readonly SkyGeometryVertex[] m_skyWallVertices = new SkyGeometryVertex[6];
    private readonly RenderWorldDataManager m_worldDataManager;
    private readonly LegacySkyRenderer m_skyRenderer;
    private readonly ArchiveCollection m_archiveCollection;
    private GLBufferTexture? m_lightBuffer;
    private double m_tickFraction;
    private bool m_skyOverride;
    private bool m_floorChanged;
    private bool m_ceilingChanged;
    private bool m_sectorChangedLine;
    private bool m_cacheOverride;
    private Vec3D m_viewPosition;
    private Vec3D m_prevViewPosition;
    private Sector m_viewSector;
    private IWorld m_world;
    private TransferHeightView m_transferHeightsView = TransferHeightView.Middle;
    private bool m_buffer = true;
    private LegacyVertex[][] m_vertexLookup = Array.Empty<LegacyVertex[]>();
    private LegacyVertex[][] m_vertexLowerLookup = Array.Empty<LegacyVertex[]>();
    private LegacyVertex[][] m_vertexUpperLookup = Array.Empty<LegacyVertex[]>();
    private SkyGeometryVertex[][] m_skyWallVertexLowerLookup = Array.Empty<SkyGeometryVertex[]>();
    private SkyGeometryVertex[][] m_skyWallVertexUpperLookup = Array.Empty<SkyGeometryVertex[]>();
    private DynamicArray<LegacyVertex[][]> m_vertexFloorLookup = new(3);
    private DynamicArray<LegacyVertex[][]> m_vertexCeilingLookup = new(3);
    private DynamicArray<SkyGeometryVertex[][]> m_skyFloorVertexLookup = new(3);
    private DynamicArray<SkyGeometryVertex[][]> m_skyCeilingVertexLookup = new(3);
    // List of each subsector mapped to a sector id
    private DynamicArray<Subsector>[] m_subsectors = Array.Empty<DynamicArray<Subsector>>();
    private int[] m_drawnSides = Array.Empty<int>();
    private int m_maxDistanceSquared;

    private TextureManager TextureManager => m_archiveCollection.TextureManager;

    public GeometryRenderer(IConfig config, ArchiveCollection archiveCollection, LegacyGLTextureManager glTextureManager,
        RenderProgram program, RenderProgram staticProgram, RenderWorldDataManager worldDataManager)
    {
        m_config = config;
        m_program = program;
        m_glTextureManager = glTextureManager;
        m_worldDataManager = worldDataManager;
        Portals = new(archiveCollection, glTextureManager);
        m_skyRenderer = new LegacySkyRenderer(archiveCollection, glTextureManager);
        m_viewSector = DefaultSector;
        m_archiveCollection = archiveCollection;
        m_staticCacheGeometryRenderer = new(archiveCollection, glTextureManager, staticProgram, this);
        m_maxDistanceSquared = config.Render.MaxDistance * config.Render.MaxDistance;

        for (int i = 0; i < m_wallVertices.Length; i++)
            m_wallVertices[i].Alpha = 1.0f;

        m_world = null!;
    }

    ~GeometryRenderer()
    {
        ReleaseUnmanagedResources();
    }

    public void UpdateTo(IWorld world)
    {
        m_world = world;
        m_skyRenderer.Reset();
        m_lineDrawnTracker.UpdateToWorld(world);
        m_viewSector = DefaultSector;
        PreloadAllTextures(world);

        for (int i = 0; i < m_subsectors.Length; i++)
        {
            m_subsectors[i].FlushReferences();
            m_subsectors[i].Clear();
        }

        m_vertexLookup = new LegacyVertex[world.Sides.Count][];
        m_vertexLowerLookup = new LegacyVertex[world.Sides.Count][];
        m_vertexUpperLookup = new LegacyVertex[world.Sides.Count][];
        m_skyWallVertexLowerLookup = new SkyGeometryVertex[world.Sides.Count][];
        m_skyWallVertexUpperLookup = new SkyGeometryVertex[world.Sides.Count][];

        m_subsectors = new DynamicArray<Subsector>[world.Sectors.Count];
        for (int i = 0; i < world.Sectors.Count; i++)
            m_subsectors[i] = new();

        m_drawnSides = new int[world.Sides.Count];

        m_vertexFloorLookup = new(3);
        m_vertexCeilingLookup = new(3);
        m_skyFloorVertexLookup = new(3);
        m_skyCeilingVertexLookup = new(3);

        m_lightBuffer?.Dispose();
        const int FloatSize = 4;
        m_lightBuffer = new("Sector lights texture buffer",
            world.Sectors.Count * Constants.LightBuffer.BufferSize * FloatSize + (Constants.LightBuffer.SectorIndexStart * FloatSize));

        for (int i = 0; i < world.Sides.Count; i++)
            m_drawnSides[i] = -1;

        Clear(m_tickFraction, true);
        CacheData(world);

        Portals.UpdateTo(world);
        m_staticCacheGeometryRenderer.UpdateTo(world, m_lightBuffer);
    }

    private void CacheData(IWorld world)
    {
        Vec2D pos = m_viewPosition.XY;
        for (int i = 0; i < world.BspTree.Subsectors.Length; i++)
        {
            Subsector subsector = world.BspTree.Subsectors[i];
            DynamicArray<Subsector> subsectors = m_subsectors[subsector.Sector.Id];
            subsectors.Add(subsector);

            if (subsector.Sector.TransferHeights != null)
                continue;

            m_viewSector = subsector.Sector;
            List<SubsectorSegment> edges = subsector.ClockwiseEdges;
            for (int j = 0; j < edges.Count; j++)
            {
                SubsectorSegment edge = edges[j];
                if (edge.SideId == null)
                    continue;

                var side = m_world.Sides[edge.SideId.Value];
                if (side.IsTwoSided)
                {
                    RenderSide(side, side.IsFront);
                    RenderSide(side.PartnerSide!, side.PartnerSide!.IsFront);
                    continue;
                }
                
                RenderSide(side, true);
            }
        }

        for (int i = 0; i < m_subsectors.Length; i++)
        {
            DynamicArray<Subsector> subsectors = m_subsectors[i];
            if (subsectors.Length == 0)
                continue;

            var sector = subsectors[0].Sector;
            var renderSector = sector.GetRenderSector(TransferHeightView.Middle);

            // Set position Z within plane view so it's not culled
            m_viewPosition.Z = sector.Floor.Plane.ToZ(pos) + 1;
            RenderFlat(subsectors, renderSector.Floor, true, out _, out _);
            m_viewPosition.Z = sector.Ceiling.Plane.ToZ(pos) - 1;
            RenderFlat(subsectors, renderSector.Ceiling, false, out _, out _);
        }
    }

    public void Clear(double tickFraction, bool newTick)
    {
        m_tickFraction = tickFraction;
        if (newTick)
            m_skyRenderer.Clear();
        Portals.Clear();
        m_lineDrawnTracker.ClearDrawnLines();
        AlphaSides.Clear();
        m_maxDistanceSquared = m_config.Render.MaxDistance * m_config.Render.MaxDistance;
    }
    public void RenderStaticGeometry() =>
        m_staticCacheGeometryRenderer.Render();

    public void RenderPortalsAndSkies(RenderInfo renderInfo)
    {
        m_skyRenderer.Render(renderInfo);
        Portals.Render(renderInfo);
        m_staticCacheGeometryRenderer.RenderSkies(renderInfo);
    }

    public void RenderSector(Sector viewSector, Sector sector, in Vec3D viewPosition, in Vec3D prevViewPosition)
    {
        m_buffer = true;
        m_viewSector = viewSector;
        m_viewPosition = viewPosition;
        m_prevViewPosition = prevViewPosition;

        SetSectorRendering(sector);

        if (sector.TransferHeights != null)
        {
            RenderSectorWalls(sector, viewPosition.XY, prevViewPosition.XY);
            if (!sector.AreFlatsStatic)
                RenderSectorFlats(sector, sector.GetRenderSector(m_viewSector, viewPosition.Z), sector.TransferHeights.ControlSector);
            return;
        }

        m_cacheOverride = false;
        m_transferHeightsView = TransferHeightView.Middle;

        RenderSectorWalls(sector, viewPosition.XY, prevViewPosition.XY);
        if (!sector.AreFlatsStatic)
            RenderSectorFlats(sector, sector, sector);
    }

    public void RenderSectorWall(Sector viewSector, Sector sector, Line line, Vec3D viewPosition, Vec3D prevViewPosition)
    {
        m_buffer = true;
        m_viewSector = viewSector;
        m_viewPosition = viewPosition;
        m_prevViewPosition = prevViewPosition;
        SetSectorRendering(sector);
        RenderSectorSideWall(sector, line.Front, viewPosition.XY, prevViewPosition.XY, true);
        if (line.Back != null)
            RenderSectorSideWall(sector, line.Back, viewPosition.XY, prevViewPosition.XY, false);
    }

    private void SetSectorRendering(Sector sector)
    {
        if (sector.TransferHeights != null)
        {
            m_floorChanged = m_floorChanged || sector.TransferHeights.ControlSector.Floor.CheckRenderingChanged();
            m_ceilingChanged = m_ceilingChanged || sector.TransferHeights.ControlSector.Ceiling.CheckRenderingChanged();
            m_transferHeightsView = TransferHeights.GetView(m_viewSector, m_viewPosition.Z);
            // Walls can only cache if middle view
            m_cacheOverride = m_transferHeightsView != TransferHeightView.Middle;
            return;
        }

        m_floorChanged = sector.Floor.CheckRenderingChanged();
        m_ceilingChanged = sector.Ceiling.CheckRenderingChanged();
        m_transferHeightsView = TransferHeightView.Middle;
        m_cacheOverride = false;
    }

    public void SetPlaneChanged(bool set)
    {
        m_floorChanged = set;
        m_ceilingChanged = set;
    }

    // The set sector is optional for the transfer heights control sector.
    // This is so the LastRenderGametick can be set for both the sector and transfer heights sector.
    private void RenderSectorFlats(Sector sector, Sector renderSector, Sector set)
    {
        DynamicArray<Subsector> subsectors = m_subsectors[sector.Id];
        sector.LastRenderGametick = m_world.Gametick;

        double floorZ = renderSector.Floor.Z;
        double ceilingZ = renderSector.Ceiling.Z;

        bool floorVisible = m_viewPosition.Z >= floorZ || m_prevViewPosition.Z >= floorZ;
        bool ceilingVisible = m_viewPosition.Z <= ceilingZ || m_prevViewPosition.Z <= ceilingZ;
        if (floorVisible && !sector.IsFloorStatic)
        {
            sector.Floor.LastRenderGametick = m_world.Gametick;
            set.Floor.LastRenderGametick = m_world.Gametick;
            RenderFlat(subsectors, renderSector.Floor, true, out _, out _);
        }
        if (ceilingVisible && !sector.IsCeilingStatic)
        {
            sector.Ceiling.LastRenderGametick = m_world.Gametick;
            set.Ceiling.LastRenderGametick = m_world.Gametick;
            RenderFlat(subsectors, renderSector.Ceiling, false, out _, out _);
        }
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    private void PreloadAllTextures(IWorld world)
    {
        if (world.SameAsPreviousMap)
            return;

        //TODO don't do this if the map didn't change
        HashSet<int> textures = new();
        for (int i = 0; i < world.Lines.Count; i++)
        {
            AddSideTextures(world, textures, world.Lines[i].Front);

            if (world.Lines[i].Back == null)
                continue;

            AddSideTextures(world, textures, world.Lines[i].Back);
        }

        for (int i = 0; i < world.Sectors.Count; i++)
        {
            textures.Add(world.Sectors[i].Floor.TextureHandle);
            textures.Add(world.Sectors[i].Ceiling.TextureHandle);
        }

        TextureManager.LoadTextureImages(textures);
    }

    private static void AddSideTextures(IWorld world, HashSet<int> textures, Side side)
    {
        textures.Add(side.Lower.TextureHandle);
        textures.Add(side.Middle.TextureHandle);
        textures.Add(side.Upper.TextureHandle);
    }

    private void RenderSectorWalls(Sector sector, Vec2D pos2D, Vec2D prevPos2D)
    {
        for (int i = 0; i < sector.Lines.Count; i++)
        {
            Line line = sector.Lines[i];
            bool onFront = line.Segment.OnRight(pos2D);
            bool onBothSides = onFront != line.Segment.OnRight(prevPos2D);

            if (line.Back != null)
                CheckFloodFillLine(line.Front, line.Back);

            // Need to force render for alernative flood fill from the front side.
            if (onFront || onBothSides || line.Front.LowerFloodKey2 > 0 || line.Front.UpperFloodKey2 > 0)
                RenderSectorSideWall(sector, line.Front, pos2D, prevPos2D, true);
            // Need to force render for alernative flood fill from the back side.
            if (line.Back != null && (!onFront || onBothSides || line.Back.LowerFloodKey2 > 0 || line.Back.UpperFloodKey2 > 0))
                RenderSectorSideWall(sector, line.Back, pos2D, prevPos2D, false);
        }
    }

    private void CheckFloodFillLine(Side front, Side back)
    {
        const RenderChangeOptions options = RenderChangeOptions.None;
        if (front.IsDynamic && m_drawnSides[front.Id] != WorldStatic.CheckCounter &&
            (back.Sector.CheckRenderingChanged(m_world.Gametick, options) ||
            front.Sector.CheckRenderingChanged(m_world.Gametick, options)))
            m_staticCacheGeometryRenderer.CheckForFloodFill(front, back, back.Sector, true);

        if (back.IsDynamic && m_drawnSides[back.Id] != WorldStatic.CheckCounter &&
            (front.Sector.CheckRenderingChanged(m_world.Gametick, options) || 
            back.Sector.CheckRenderingChanged(m_world.Gametick, options)))
            m_staticCacheGeometryRenderer.CheckForFloodFill(back, front, front.Sector, false);
    }

    private void RenderSectorSideWall(Sector sector, Side side, Vec2D pos2D, Vec2D prevPos2D, bool onFrontSide)
    {
        if (m_drawnSides[side.Id] == WorldStatic.CheckCounter)
            return;

        m_drawnSides[side.Id] = WorldStatic.CheckCounter;
        if (m_config.Render.TextureTransparency && side.Line.Alpha < 1)
        {
            var lineCenter = side.Line.Segment.FromTime(0.5);
            double dx = Math.Max(lineCenter.X - pos2D.X, Math.Max(0, pos2D.X - lineCenter.X));
            double dy = Math.Max(lineCenter.Y - pos2D.Y, Math.Max(0, pos2D.Y - lineCenter.Y));
            side.RenderDistanceSquared = dx * dx + dy * dy;
            AlphaSides.Add(side);
        }

        bool transferHeights = false;
        // Transfer heights has to be drawn by the transfer heights sector
        if (side.Sector.TransferHeights != null && (sector.TransferHeights == null || sector.TransferHeights.ControlSector != side.Sector.TransferHeights.ControlSector))
        {
            SetSectorRendering(side.Sector);
            transferHeights = true;
        }

        if (!side.IsStatic)
            RenderSide(side, onFrontSide);

        // Restore to original sector
        if (transferHeights)
            SetSectorRendering(sector);
    }

    private void RenderWalls(Subsector subsector, in Vec3D position, Vec2D pos2D)
    {
        List<SubsectorSegment> edges = subsector.ClockwiseEdges;
        for (int i = 0; i < edges.Count; i++)
        {
            SubsectorSegment edge = edges[i];
            if (edge.SideId == null)
                continue;

            Line line = m_world.Sides[edge.SideId.Value].Line;
            if (m_lineDrawnTracker.HasDrawn(line))
                continue;

            line.MarkSeenOnAutomap();
                
            bool onFrontSide = line.Segment.OnRight(pos2D);
            if (!onFrontSide && line.OneSided)
                continue;

            Side? side = onFrontSide ? line.Front : line.Back;
            if (side == null)
                throw new NullReferenceException("Trying to draw the wrong side of a one sided line (or a miniseg)");

            if (m_config.Render.TextureTransparency && side.Line.Alpha < 1)
            {
                var lineCenter = side.Line.Segment.FromTime(0.5);
                double dx = Math.Max(lineCenter.X - position.X, Math.Max(0, position.X - lineCenter.X));
                double dy = Math.Max(lineCenter.Y - position.Y, Math.Max(0, position.Y - lineCenter.Y));
                side.RenderDistanceSquared = dx * dx + dy * dy;
                AlphaSides.Add(side);
            }

            if (!side.IsStatic)
                RenderSide(side, onFrontSide);
            m_lineDrawnTracker.MarkDrawn(line);

            line.Sky = m_skyOverride;
        }
    }

    public void RenderAlphaSide(Side side, bool isFrontSide)
    {
        if (side.Line.Back == null)
            return;

        if (side.Middle.TextureHandle != Constants.NoTextureIndex)
        {
            Side otherSide = side.PartnerSide!;
            m_sectorChangedLine = otherSide.Sector.CheckRenderingChanged(side.LastRenderGametickAlpha) || side.Sector.CheckRenderingChanged(side.LastRenderGametickAlpha);
            Sector facingSector = side.Sector.GetRenderSector(m_viewSector, m_viewPosition.Z);
            Sector otherSector = otherSide.Sector.GetRenderSector(m_viewSector, m_viewPosition.Z);
            RenderTwoSidedMiddle(side, side.PartnerSide!, facingSector, otherSector, isFrontSide, out _);
            side.LastRenderGametickAlpha = m_world.Gametick;
        }
    }

    public void RenderSide(Side side, bool isFrontSide)
    {
        m_skyOverride = false;
        if (side.IsTwoSided)
            RenderTwoSided(side, isFrontSide);
        else
            RenderOneSided(side, out _, out _);
    }

    public void RenderOneSided(Side side, out LegacyVertex[]? veticies, out SkyGeometryVertex[]? skyVerticies)
    {
        m_sectorChangedLine = side.Sector.CheckRenderingChanged(side.LastRenderGametick);
        side.LastRenderGametick = m_world.Gametick;

        GLLegacyTexture texture = m_glTextureManager.GetTexture(side.Middle.TextureHandle);
        LegacyVertex[]? data = m_vertexLookup[side.Id];

        var renderSector = side.Sector.GetRenderSector(m_viewSector, m_viewPosition.Z);

        SectorPlane floor = renderSector.Floor;
        SectorPlane ceiling = renderSector.Ceiling;
        RenderSkySide(side, renderSector, null, texture, out skyVerticies);

        if (side.OffsetChanged || m_sectorChangedLine || data == null || m_cacheOverride)
        {
            int lightIndex = StaticCacheGeometryRenderer.GetLightBufferIndex(renderSector, LightBufferType.Wall);
            WallVertices wall = WorldTriangulator.HandleOneSided(side, floor, ceiling, texture.UVInverse);
            if (m_cacheOverride)
            {
                data = m_wallVertices;
                SetWallVertices(data, wall, GetLightLevelAdd(side), lightIndex);
            }
            else if (data == null)
                data = GetWallVertices(wall, GetLightLevelAdd(side), lightIndex);
            else
                SetWallVertices(data, wall, GetLightLevelAdd(side), lightIndex);

            if (!m_cacheOverride)
                m_vertexLookup[side.Id] = data;
        }

        if (m_buffer)
        {
            RenderWorldData renderData = m_worldDataManager.GetRenderData(texture, m_program);
            renderData.Vbo.Add(data);
        }
        veticies = data;
    }

    private int GetLightLevelAdd(Side side)
    {
        if (!m_config.Render.FakeContrast)
            return 0;

        if (side.Line.StartPosition.Y == side.Line.EndPosition.Y)
            return -16;
        else if (side.Line.StartPosition.X == side.Line.EndPosition.X)
            return 16;

        return 0;
    }

    public void SetRenderOneSided(Side side)
    {
        m_sectorChangedLine = side.Sector.CheckRenderingChanged(side.LastRenderGametick);
    }

    public void SetRenderTwoSided(Side facingSide)
    {
        Side otherSide = facingSide.PartnerSide!;
        m_sectorChangedLine = otherSide.Sector.CheckRenderingChanged(facingSide.LastRenderGametick) || facingSide.Sector.CheckRenderingChanged(facingSide.LastRenderGametick);
    }

    public void SetRenderFloor(SectorPlane floor)
    {
        floor = floor.Sector.GetRenderSector(TransferHeightView.Middle).Floor;
        m_floorChanged = floor.CheckRenderingChanged();
    }

    public void SetRenderCeiling(SectorPlane ceiling)
    {
        ceiling = ceiling.Sector.GetRenderSector(TransferHeightView.Middle).Ceiling;
        m_ceilingChanged = ceiling.CheckRenderingChanged();
    }

    private void RenderTwoSided(Side facingSide, bool isFrontSide)
    {
        Side otherSide = facingSide.PartnerSide!;
        Sector facingSector = facingSide.Sector.GetRenderSector(m_viewSector, m_viewPosition.Z);
        Sector otherSector = otherSide.Sector.GetRenderSector(m_viewSector, m_viewPosition.Z);

        m_sectorChangedLine = otherSide.Sector.CheckRenderingChanged(facingSide.LastRenderGametick) || facingSide.Sector.CheckRenderingChanged(facingSide.LastRenderGametick);
        facingSide.LastRenderGametick = m_world.Gametick;
        if (facingSide.Lower.IsDynamic && LowerIsVisible(facingSide, facingSector, otherSector))
            RenderTwoSidedLower(facingSide, otherSide, facingSector, otherSector, isFrontSide, out _, out _);
        if ((!m_config.Render.TextureTransparency || facingSide.Line.Alpha >= 1) && facingSide.Middle.TextureHandle != Constants.NoTextureIndex && 
            facingSide.Middle.IsDynamic)
            RenderTwoSidedMiddle(facingSide, otherSide, facingSector, otherSector, isFrontSide, out _);
        if (facingSide.Upper.IsDynamic && UpperOrSkySideIsVisible(facingSide, facingSector, otherSector, out _))
            RenderTwoSidedUpper(facingSide, otherSide, facingSector, otherSector, isFrontSide, out _, out _, out _);
    }

    public bool LowerIsVisible(Side facingSide, Sector facingSector, Sector otherSector)
    {
        return facingSector.Floor.Z < otherSector.Floor.Z || facingSector.Floor.PrevZ < otherSector.Floor.PrevZ ||
            facingSide.LowerFloodKey > 0;
    }

    public bool UpperIsVisible(Side facingSide, Side otherSide, Sector facingSector, Sector otherSector)
    {
        return facingSector.Ceiling.Z > otherSector.Ceiling.Z || facingSector.Ceiling.PrevZ > otherSector.Ceiling.PrevZ ||
            facingSide.UpperFloodKey > 0;
    }

    public bool UpperOrSkySideIsVisible(Side facingSide, Sector facingSector, Sector otherSector, out bool skyHack)
    {
        skyHack = false;
        double facingZ = facingSector.Ceiling.Z;
        double otherZ = otherSector.Ceiling.Z;
        double prevFacingZ = facingSector.Ceiling.PrevZ;
        double prevOtherZ = otherSector.Ceiling.PrevZ;
        bool isFacingSky = TextureManager.IsSkyTexture(facingSector.Ceiling.TextureHandle);
        bool isOtherSky = TextureManager.IsSkyTexture(otherSector.Ceiling.TextureHandle);

        if (isFacingSky && isOtherSky)
        {
            // The sky is only drawn if there is no opening height
            // Otherwise ignore this line for sky effects
            skyHack = LineOpening.GetOpeningHeight(facingSide.Line) <= 0 && facingZ != otherZ;
            return skyHack;
        }

        bool upperVisible = facingZ > otherZ || prevFacingZ > prevOtherZ;
        // Return true if the upper is not visible so DrawTwoSidedUpper can attempt to draw sky hacks
        if (isFacingSky)
        {
            if ((facingSide.FloodTextures & SideTexture.Upper) != 0)
                return true;

            if (facingSide.Upper.TextureHandle == Constants.NoTextureIndex)
            {
                skyHack = facingZ <= otherZ || prevFacingZ <= prevOtherZ;
                return skyHack;
            }

            // Need to draw sky upper if other sector is not sky.
            skyHack = !isOtherSky;
            return skyHack;
        }

        return upperVisible;
    }

    public void RenderTwoSidedLower(Side facingSide, Side otherSide, Sector facingSector, Sector otherSector, bool isFrontSide, 
        out LegacyVertex[]? verticies, out SkyGeometryVertex[]? skyVerticies)
    {
        Wall lowerWall = facingSide.Lower;
        bool isSky = TextureManager.IsSkyTexture(otherSide.Sector.Floor.TextureHandle) && lowerWall.TextureHandle == Constants.NoTextureIndex;
        bool skyRender = isSky && TextureManager.IsSkyTexture(otherSide.Sector.Floor.TextureHandle);

        if (facingSide.LowerFloodKey > 0 || facingSide.LowerFloodKey2 > 0)
        {
            verticies = null;
            skyVerticies = null;
            Portals.UpdateStaticFloodFillSide(facingSide, otherSide, otherSector, SideTexture.Lower, isFrontSide);
            return;
        }

        if (lowerWall.TextureHandle == Constants.NoTextureIndex && !skyRender)
        {
            verticies = null;
            skyVerticies = null;
            return;
        }

        GLLegacyTexture texture = m_glTextureManager.GetTexture(lowerWall.TextureHandle);
        RenderWorldData renderData = m_worldDataManager.GetRenderData(texture, m_program);

        SectorPlane top = otherSector.Floor;
        SectorPlane bottom = facingSector.Floor;

        if (isSky)
        {
            SkyGeometryVertex[]? data = m_skyWallVertexLowerLookup[facingSide.Id];

            if (facingSide.OffsetChanged || m_sectorChangedLine || data == null)
            {
                WallVertices wall = WorldTriangulator.HandleTwoSidedLower(facingSide, top, bottom, texture.UVInverse, isFrontSide);
                if (data == null)
                    data = CreateSkyWallVertices(wall);
                else
                    SetSkyWallVertices(data, wall);
                m_skyWallVertexLowerLookup[facingSide.Id] = data;
            }

            m_skyRenderer.Add(data, data.Length, otherSide.Sector.SkyTextureHandle, otherSide.Sector.FlipSkyTexture);
            verticies = null;
            skyVerticies = data;
        }
        else
        {
            LegacyVertex[]? data = m_vertexLowerLookup[facingSide.Id];

            if (facingSide.OffsetChanged || m_sectorChangedLine || data == null || m_cacheOverride)
            {
                int lightIndex = StaticCacheGeometryRenderer.GetLightBufferIndex(facingSector, LightBufferType.Wall);
                // This lower would clip into the upper texture. Pick the upper as the priority and stop at the ceiling.
                if (top.Z > otherSector.Ceiling.Z && !TextureManager.IsSkyTexture(otherSector.Ceiling.TextureHandle))
                    top = otherSector.Ceiling;                    

                WallVertices wall = WorldTriangulator.HandleTwoSidedLower(facingSide, top, bottom, texture.UVInverse, isFrontSide);
                if (m_cacheOverride)
                {
                    data = m_wallVertices;
                    SetWallVertices(data, wall, GetLightLevelAdd(facingSide), lightIndex);
                }
                else if (data == null)
                    data = GetWallVertices(wall, GetLightLevelAdd(facingSide), lightIndex);
                else
                    SetWallVertices(data, wall, GetLightLevelAdd(facingSide), lightIndex);

                if (!m_cacheOverride)
                    m_vertexLowerLookup[facingSide.Id] = data;
            }

            // See RenderOneSided() for an ASCII image of why we do this.
            if (m_buffer)
                renderData.Vbo.Add(data);
            verticies = data;
            skyVerticies = null;
        }
    }

    public void RenderTwoSidedUpper(Side facingSide, Side otherSide, Sector facingSector, Sector otherSector, bool isFrontSide,
        out LegacyVertex[]? verticies, out SkyGeometryVertex[]? skyVerticies, out SkyGeometryVertex[]? skyVerticies2)
    {
        SectorPlane plane = otherSector.Ceiling;
        bool isSky = TextureManager.IsSkyTexture(plane.TextureHandle) && TextureManager.IsSkyTexture(facingSector.Ceiling.TextureHandle);
        Wall upperWall = facingSide.Upper;

        if (facingSide.UpperFloodKey > 0 || facingSide.UpperFloodKey2 > 0)
        {
            verticies = null;
            skyVerticies = null;
            skyVerticies2 = null;
            Portals.UpdateStaticFloodFillSide(facingSide, otherSide, otherSector, SideTexture.Upper, isFrontSide);
            return;
        }

        if (!TextureManager.IsSkyTexture(facingSector.Ceiling.TextureHandle) &&
            upperWall.TextureHandle == Constants.NoTextureIndex)
        {
            if (TextureManager.IsSkyTexture(otherSector.Ceiling.TextureHandle))
                m_skyOverride = true;
            verticies = null;
            skyVerticies = null;
            skyVerticies2 = null;
            return;
        }

        GLLegacyTexture texture = m_glTextureManager.GetTexture(upperWall.TextureHandle);
        RenderWorldData renderData = m_worldDataManager.GetRenderData(texture, m_program);

        SectorPlane top = facingSector.Ceiling;
        SectorPlane bottom = otherSector.Ceiling;

        RenderSkySide(facingSide, facingSector, otherSector, texture, out skyVerticies2);

        if (isSky)
        {
            SkyGeometryVertex[]? data = m_skyWallVertexUpperLookup[facingSide.Id];

            if (TextureManager.IsSkyTexture(otherSide.Sector.Ceiling.TextureHandle))
            {
                m_skyOverride = true;
                verticies = null;
                skyVerticies = null;
                return;
            }

            if (facingSide.OffsetChanged || m_sectorChangedLine || data == null)
            {
                WallVertices wall = WorldTriangulator.HandleTwoSidedUpper(facingSide, top, bottom, texture.UVInverse,
                    isFrontSide, MaxSky);
                if (data == null)
                    data = CreateSkyWallVertices(wall);
                else
                    SetSkyWallVertices(data, wall);
                m_skyWallVertexUpperLookup[facingSide.Id] = data;
            }

            m_skyRenderer.Add(data, data.Length, plane.Sector.SkyTextureHandle, plane.Sector.FlipSkyTexture);
            verticies = null;
            skyVerticies = data;
        }
        else
        {
            if (facingSide.Upper.TextureHandle == Constants.NoTextureIndex && skyVerticies2 != null || 
                !UpperIsVisible(facingSide, otherSide, facingSector, otherSector))
            {
                // This isn't the best spot for this but separating this logic would be difficult. (Sector 72 in skyshowcase.wad)
                facingSide.FloodTextures &= ~SideTexture.Upper;
                verticies = null;
                skyVerticies = null;
                return;
            }

            LegacyVertex[]? data = m_vertexUpperLookup[facingSide.Id];

            if (facingSide.OffsetChanged || m_sectorChangedLine || data == null || m_cacheOverride)
            {
                int lightIndex = StaticCacheGeometryRenderer.GetLightBufferIndex(facingSector, LightBufferType.Wall);
                WallVertices wall = WorldTriangulator.HandleTwoSidedUpper(facingSide, top, bottom, texture.UVInverse, isFrontSide);
                if (m_cacheOverride)
                {
                    data = m_wallVertices;
                    SetWallVertices(data, wall, GetLightLevelAdd(facingSide), lightIndex);
                }
                else if (data == null)
                    data = GetWallVertices(wall, GetLightLevelAdd(facingSide), lightIndex);
                else
                    SetWallVertices(data, wall, GetLightLevelAdd(facingSide), lightIndex);

                if (!m_cacheOverride)
                    m_vertexUpperLookup[facingSide.Id] = data;
            }

            // See RenderOneSided() for an ASCII image of why we do this.
            if (m_buffer)
                renderData.Vbo.Add(data);
            verticies = data;
            skyVerticies = null;
        }
    }

    private void RenderSkySide(Side facingSide, Sector facingSector, Sector? otherSector, GLLegacyTexture texture, out SkyGeometryVertex[]? skyVerticies)
    {
        skyVerticies = null;
        if (otherSector == null)
        {
            if (!TextureManager.IsSkyTexture(facingSector.Ceiling.TextureHandle))
                return;
        }
        else
        {
            if (!TextureManager.IsSkyTexture(facingSector.Ceiling.TextureHandle) &&
                !TextureManager.IsSkyTexture(otherSector.Ceiling.TextureHandle))
                return;
        }

        bool isFront = facingSide.IsFront;
        SectorPlane floor = facingSector.Floor;
        SectorPlane ceiling = facingSector.Ceiling;

        WallVertices wall;
        if (facingSide.IsTwoSided && otherSector != null && LineOpening.IsRenderingBlocked(facingSide.Line) &&
            SkyUpperRenderFromFloorCheck(facingSide, facingSector, otherSector))
        {
            wall = WorldTriangulator.HandleOneSided(facingSide, floor, ceiling, texture.UVInverse,
                overrideFloor: facingSide.PartnerSide!.Sector.Floor.Z, overrideCeiling: MaxSky, isFront);
        }
        else
        {
            wall = WorldTriangulator.HandleOneSided(facingSide, floor, ceiling, texture.UVInverse,
                overrideFloor: facingSector.Ceiling.Z, overrideCeiling: MaxSky, isFront);
        }

        SetSkyWallVertices(m_skyWallVertices, wall);
        m_skyRenderer.Add(m_skyWallVertices, m_skyWallVertices.Length, facingSide.Sector.SkyTextureHandle, facingSide.Sector.FlipSkyTexture);
        skyVerticies = m_skyWallVertices;
    }

    private bool SkyUpperRenderFromFloorCheck(Side twoSided, Sector facingSector, Sector otherSector)
    {
        if (twoSided.Upper.TextureHandle == Constants.NoTextureIndex)
            return true;

        if (TextureManager.IsSkyTexture(facingSector.Ceiling.TextureHandle) &&
            TextureManager.IsSkyTexture(otherSector.Ceiling.TextureHandle))
            return true;

        return false;
    }

    public void RenderTwoSidedMiddle(Side facingSide, Side otherSide, Sector facingSector, Sector otherSector, bool isFrontSide,
        out LegacyVertex[]? verticies)
    {
        Wall middleWall = facingSide.Middle;
        GLLegacyTexture texture = m_glTextureManager.GetTexture(middleWall.TextureHandle, repeatY: false);

        float alpha = m_config.Render.TextureTransparency ? facingSide.Line.Alpha : 1.0f;
        LegacyVertex[]? data = m_vertexLookup[facingSide.Id];
        RenderWorldData renderData = alpha < 1 ? 
            m_worldDataManager.GetAlphaRenderData(texture, m_program) : 
            m_worldDataManager.GetRenderData(texture, m_program);

        if (facingSide.OffsetChanged || m_sectorChangedLine || data == null || m_cacheOverride)
        {
            (double bottomZ, double topZ) = FindOpeningFlats(facingSector, otherSector);
            (double prevBottomZ, double prevTopZ) = FindOpeningFlatsPrev(facingSector, otherSector);
            double offset = GetTransferHeightHackOffset(facingSide, otherSide, bottomZ, topZ, previous: false);
            double prevOffset = 0;

            if (offset != 0)
                prevOffset = GetTransferHeightHackOffset(facingSide, otherSide, bottomZ, topZ, previous: true);

            int lightIndex = StaticCacheGeometryRenderer.GetLightBufferIndex(facingSector, LightBufferType.Wall);
            // Not going to do anything with out nothingVisible for now
            WallVertices wall = WorldTriangulator.HandleTwoSidedMiddle(facingSide,
                texture.Dimension, texture.UVInverse, bottomZ, topZ, prevBottomZ, prevTopZ, isFrontSide, out _, offset, prevOffset);

            if (m_cacheOverride)
            {
                data = m_wallVertices;
                SetWallVertices(data, wall, GetLightLevelAdd(facingSide), lightIndex, alpha, addAlpha: 0);
            }
            else if (data == null)
                data = GetWallVertices(wall, GetLightLevelAdd(facingSide), lightIndex, alpha, addAlpha: 0);
            else
                SetWallVertices(data, wall, GetLightLevelAdd(facingSide), lightIndex, alpha, addAlpha: 0);

            if (!m_cacheOverride)
                m_vertexLookup[facingSide.Id] = data;
        }

        // See RenderOneSided() for an ASCII image of why we do this.
        if (m_buffer)
            renderData.Vbo.Add(data);
        verticies = data;
    }

    // There is some issue with how the original code renders middle textures with transfer heights.
    // It appears to incorrectly draw from the floor of the original sector instead of the transfer heights sector.
    // Alternatively, I could be dumb and this is dumb but it appears to work.
    private double GetTransferHeightHackOffset(Side facingSide, Side otherSide, double bottomZ, double topZ, bool previous)
    {
        if (otherSide.Sector.TransferHeights == null && facingSide.Sector.TransferHeights == null)
            return 0;

        (double originalBottomZ, double originalTopZ) = previous ? 
            FindOpeningFlatsPrev(facingSide.Sector, otherSide.Sector) :
            FindOpeningFlats(facingSide.Sector, otherSide.Sector);

        if (facingSide.Line.Flags.Unpegged.Lower)
            return originalBottomZ - bottomZ;

        return originalTopZ - topZ;
    }

    public static (double bottomZ, double topZ) FindOpeningFlats(Sector facingSector, Sector otherSector)
    {
        SectorPlane facingFloor = facingSector.Floor;
        SectorPlane facingCeiling = facingSector.Ceiling;
        SectorPlane otherFloor = otherSector.Floor;
        SectorPlane otherCeiling = otherSector.Ceiling;

        double facingFloorZ = facingFloor.Z;
        double facingCeilingZ = facingCeiling.Z;
        double otherFloorZ = otherFloor.Z;
        double otherCeilingZ = otherCeiling.Z;

        double bottomZ = facingFloorZ;
        double topZ = facingCeilingZ;
        if (otherFloorZ > facingFloorZ)
            bottomZ = otherFloorZ;
        if (otherCeilingZ < facingCeilingZ)
            topZ = otherCeilingZ;

        return (bottomZ, topZ);
    }

    public static (double bottomZ, double topZ) FindOpeningFlatsPrev(Sector facingSector, Sector otherSector)
    {
        SectorPlane facingFloor = facingSector.Floor;
        SectorPlane facingCeiling = facingSector.Ceiling;
        SectorPlane otherFloor = otherSector.Floor;
        SectorPlane otherCeiling = otherSector.Ceiling;

        double facingFloorZ = facingFloor.PrevZ;
        double facingCeilingZ = facingCeiling.PrevZ;
        double otherFloorZ = otherFloor.PrevZ;
        double otherCeilingZ = otherCeiling.PrevZ;

        double bottomZ = facingFloorZ;
        double topZ = facingCeilingZ;
        if (otherFloorZ > facingFloorZ)
            bottomZ = otherFloorZ;
        if (otherCeilingZ < facingCeilingZ)
            topZ = otherCeilingZ;

        return (bottomZ, topZ);
    }

    public void SetTransferHeightView(TransferHeightView view) => m_transferHeightsView = view;
    public void SetBuffer(bool set) => m_buffer = set;
    public void SetViewSector(Sector sector) => m_viewSector = sector;

    public void RenderSectorFlats(Sector sector, SectorPlane flat, bool floor, out LegacyVertex[]? verticies, out SkyGeometryVertex[]? skyVerticies)
    {
        if (sector.Id >= m_subsectors.Length)
        {
            verticies = null;
            skyVerticies = null;
            return;
        }

        DynamicArray<Subsector> subsectors = m_subsectors[sector.Id];
        RenderFlat(subsectors, flat, floor, out verticies, out skyVerticies);
    }

    private void RenderFlat(DynamicArray<Subsector> subsectors, SectorPlane flat, bool floor, out LegacyVertex[]? verticies, out SkyGeometryVertex[]? skyVerticies)
    {
        bool isSky = TextureManager.IsSkyTexture(flat.TextureHandle);
        GLLegacyTexture texture = m_glTextureManager.GetTexture(flat.TextureHandle);
        RenderWorldData renderData = m_worldDataManager.GetRenderData(texture, m_program);
        bool flatChanged = FlatChanged(flat);
        int id = subsectors[0].Sector.Id;
        Sector renderSector = subsectors[0].Sector.GetRenderSector(m_viewSector, m_viewPosition.Z);

        if (isSky)
        {
            SkyGeometryVertex[]? lookupData = GetSkySectorVerticies(subsectors, floor, id, out bool generate);
            if (generate || flatChanged)
            {
                int indexStart = 0;
                for (int j = 0; j < subsectors.Length; j++)
                {
                    Subsector subsector = subsectors[j];
                    WorldTriangulator.HandleSubsector(subsector, flat, texture.Dimension, m_subsectorVertices,
                        floor ? flat.Z : MaxSky);
                    TriangulatedWorldVertex root = m_subsectorVertices[0];
                    m_skyVertices.Clear();
                    for (int i = 1; i < m_subsectorVertices.Length - 1; i++)
                    {
                        TriangulatedWorldVertex second = m_subsectorVertices[i];
                        TriangulatedWorldVertex third = m_subsectorVertices[i + 1];
                        CreateSkyFlatVertices(m_skyVertices, root, second, third);
                    }

                    Array.Copy(m_skyVertices.Data, 0, lookupData, indexStart, m_skyVertices.Length);
                    indexStart += m_skyVertices.Length;
                }
            }

            verticies = null;
            skyVerticies = lookupData;
            m_skyRenderer.Add(lookupData, lookupData.Length, subsectors[0].Sector.SkyTextureHandle, subsectors[0].Sector.FlipSkyTexture);
        }
        else
        {
            LegacyVertex[]? lookupData = GetSectorVerticies(subsectors, floor, id, out bool generate);

            if (generate || flatChanged)
            {
                int indexStart = 0;
                int lightIndex = floor ? StaticCacheGeometryRenderer.GetLightBufferIndex(renderSector, LightBufferType.Floor) : 
                            StaticCacheGeometryRenderer.GetLightBufferIndex(renderSector, LightBufferType.Ceiling);
                for (int j = 0; j < subsectors.Length; j++)
                {
                    Subsector subsector = subsectors[j];
                    WorldTriangulator.HandleSubsector(subsector, flat, texture.Dimension, m_subsectorVertices);
                    TriangulatedWorldVertex root = m_subsectorVertices[0];
                    m_vertices.Clear();
                    for (int i = 1; i < m_subsectorVertices.Length - 1; i++)
                    {
                        TriangulatedWorldVertex second = m_subsectorVertices[i];
                        TriangulatedWorldVertex third = m_subsectorVertices[i + 1];
                        GetFlatVertices(m_vertices, ref root, ref second, ref third, lightIndex);
                    }

                    Array.Copy(m_vertices.Data, 0, lookupData, indexStart, m_vertices.Length);
                    indexStart += m_vertices.Length;
                }
            }

            skyVerticies = null;
            verticies = lookupData;
            renderData.Vbo.Add(lookupData);
        }
    }

    private LegacyVertex[] GetSectorVerticies(DynamicArray<Subsector> subsectors, bool floor, int id, out bool generate)
    {
        LegacyVertex[][]? lookupView = floor ? m_vertexFloorLookup[(int)m_transferHeightsView] : m_vertexCeilingLookup[(int)m_transferHeightsView];
        if (lookupView == null)
        {
            lookupView ??= new LegacyVertex[m_world.Sectors.Count][];
            if (floor)
                m_vertexFloorLookup[(int)m_transferHeightsView] = lookupView;
            else
                m_vertexCeilingLookup[(int)m_transferHeightsView] = lookupView;
        }

        LegacyVertex[]? data = lookupView[id];
        generate = data == null;
        data ??= InitSectorVerticies(subsectors, floor, id, lookupView);
        return data;
    }

    private SkyGeometryVertex[] GetSkySectorVerticies(DynamicArray<Subsector> subsectors, bool floor, int id, out bool generate)
    {
        SkyGeometryVertex[][]? lookupView = floor ? m_skyFloorVertexLookup[(int)m_transferHeightsView] : m_skyCeilingVertexLookup[(int)m_transferHeightsView];
        if (lookupView == null)
        {
            lookupView ??= new SkyGeometryVertex[m_world.Sectors.Count][];
            if (floor)
                m_skyFloorVertexLookup[(int)m_transferHeightsView] = lookupView;
            else
                m_skyCeilingVertexLookup[(int)m_transferHeightsView] = lookupView;
        }

        SkyGeometryVertex[]? data = lookupView[id];
        generate = data == null;
        data ??= InitSkyVerticies(subsectors, floor, id, lookupView);
        return data;
    }

    private static LegacyVertex[] InitSectorVerticies(DynamicArray<Subsector> subsectors, bool floor, int id, LegacyVertex[][] lookup)
    {
        int count = 0;
        for (int j = 0; j < subsectors.Length; j++)
            count += (subsectors[j].ClockwiseEdges.Count - 2) * 3;

        LegacyVertex[] data = new LegacyVertex[count];
        if (floor)
            lookup[id] = data;
        else
            lookup[id] = data;

        return data;
    }

    private static SkyGeometryVertex[] InitSkyVerticies(DynamicArray<Subsector> subsectors, bool floor, int id, SkyGeometryVertex[][] lookup)
    {
        int count = 0;
        for (int j = 0; j < subsectors.Length; j++)
            count += (subsectors[j].ClockwiseEdges.Count - 2) * 3;

        SkyGeometryVertex[]? data = new SkyGeometryVertex[count];
        if (floor)
            lookup[id] = data;
        else
            lookup[id] = data;

        return data;
    }

    private bool FlatChanged(SectorPlane flat)
    {
        if (flat.Facing == SectorPlaneFace.Floor)
            return m_floorChanged;
        else
            return m_ceilingChanged;
    }

    private static void SetSkyWallVertices(SkyGeometryVertex[] data, in WallVertices wv)
    {
        data[0].X = wv.TopLeft.X;
        data[0].Y = wv.TopLeft.Y;
        data[0].Z = wv.TopLeft.Z;

        data[1].X = wv.BottomLeft.X;
        data[1].Y = wv.BottomLeft.Y;
        data[1].Z = wv.BottomLeft.Z;

        data[2].X = wv.TopRight.X;
        data[2].Y = wv.TopRight.Y;
        data[2].Z = wv.TopRight.Z;

        data[3].X = wv.TopRight.X;
        data[3].Y = wv.TopRight.Y;
        data[3].Z = wv.TopRight.Z;

        data[4].X = wv.BottomLeft.X;
        data[4].Y = wv.BottomLeft.Y;
        data[4].Z = wv.BottomLeft.Z;

        data[5].X = wv.BottomRight.X;
        data[5].Y = wv.BottomRight.Y;
        data[5].Z = wv.BottomRight.Z;
    }

    private static SkyGeometryVertex[] CreateSkyWallVertices(in WallVertices wv)
    {
        SkyGeometryVertex[] data = new SkyGeometryVertex[6];
        data[0].X = wv.TopLeft.X;
        data[0].Y = wv.TopLeft.Y;
        data[0].Z = wv.TopLeft.Z;

        data[1].X = wv.BottomLeft.X;
        data[1].Y = wv.BottomLeft.Y;
        data[1].Z = wv.BottomLeft.Z;

        data[2].X = wv.TopRight.X;
        data[2].Y = wv.TopRight.Y;
        data[2].Z = wv.TopRight.Z;

        data[3].X = wv.TopRight.X;
        data[3].Y = wv.TopRight.Y;
        data[3].Z = wv.TopRight.Z;

        data[4].X = wv.BottomLeft.X;
        data[4].Y = wv.BottomLeft.Y;
        data[4].Z = wv.BottomLeft.Z;

        data[5].X = wv.BottomRight.X;
        data[5].Y = wv.BottomRight.Y;
        data[5].Z = wv.BottomRight.Z;

        return data;
    }

    private static void CreateSkyFlatVertices(DynamicArray<SkyGeometryVertex> vertices, in TriangulatedWorldVertex root, in TriangulatedWorldVertex second, in TriangulatedWorldVertex third)
    {
        vertices.Add(new SkyGeometryVertex()
        { 
            X = root.X,
            Y = root.Y,
            Z = root.Z,
        });

        vertices.Add(new SkyGeometryVertex()
        {
            X = second.X,
            Y = second.Y,
            Z = second.Z,
        });

        vertices.Add(new SkyGeometryVertex()
        {
            X = third.X,
            Y = third.Y,
            Z = third.Z,
        });
    }

    private static void SetWallVertices(LegacyVertex[] data, in WallVertices wv, float lightLevelAdd, int lightBufferIndex,
        float alpha = 1.0f, float addAlpha = 1.0f)
    {
        data[0].X = wv.TopLeft.X;
        data[0].Y = wv.TopLeft.Y;
        data[0].Z = wv.TopLeft.Z;
        data[0].PrevX = wv.TopLeft.X;
        data[0].PrevY = wv.TopLeft.Y;
        data[0].PrevZ = wv.PrevTopZ;
        data[0].U = wv.TopLeft.U;
        data[0].V = wv.TopLeft.V;
        data[0].PrevU = wv.TopLeft.PrevU;
        data[0].PrevV = wv.TopLeft.PrevV;
        data[0].Alpha = alpha;
        data[0].AddAlpha = addAlpha;
        data[0].LightLevelBufferIndex = lightBufferIndex;

        data[1].X = wv.BottomLeft.X;
        data[1].Y = wv.BottomLeft.Y;
        data[1].Z = wv.BottomLeft.Z;
        data[1].PrevX = wv.BottomLeft.X;
        data[1].PrevY = wv.BottomLeft.Y;
        data[1].PrevZ = wv.PrevBottomZ;
        data[1].U = wv.BottomLeft.U;
        data[1].V = wv.BottomLeft.V;
        data[1].PrevU = wv.BottomLeft.PrevU;
        data[1].PrevV = wv.BottomLeft.PrevV;
        data[1].Alpha = alpha;
        data[1].AddAlpha = addAlpha;
        data[1].LightLevelBufferIndex = lightBufferIndex;

        data[2].X = wv.TopRight.X;
        data[2].Y = wv.TopRight.Y;
        data[2].Z = wv.TopRight.Z;
        data[2].PrevX = wv.TopRight.X;
        data[2].PrevY = wv.TopRight.Y;
        data[2].PrevZ = wv.PrevTopZ;
        data[2].U = wv.TopRight.U;
        data[2].V = wv.TopRight.V;
        data[2].PrevU = wv.TopRight.PrevU;
        data[2].PrevV = wv.TopRight.PrevV;
        data[2].Alpha = alpha;
        data[2].AddAlpha = addAlpha;
        data[2].LightLevelBufferIndex = lightBufferIndex;

        data[3].X = wv.TopRight.X;
        data[3].Y = wv.TopRight.Y;
        data[3].Z = wv.TopRight.Z;
        data[3].PrevX = wv.TopRight.X;
        data[3].PrevY = wv.TopRight.Y;
        data[3].PrevZ = wv.PrevTopZ;
        data[3].U = wv.TopRight.U;
        data[3].V = wv.TopRight.V;
        data[3].PrevU = wv.TopRight.PrevU;
        data[3].PrevV = wv.TopRight.PrevV;
        data[3].Alpha = alpha;
        data[3].AddAlpha = addAlpha;
        data[3].LightLevelBufferIndex = lightBufferIndex;

        data[4].X = wv.BottomLeft.X;
        data[4].Y = wv.BottomLeft.Y;
        data[4].Z = wv.BottomLeft.Z;
        data[4].PrevX = wv.BottomLeft.X;
        data[4].PrevY = wv.BottomLeft.Y;
        data[4].PrevZ = wv.PrevBottomZ;
        data[4].U = wv.BottomLeft.U;
        data[4].V = wv.BottomLeft.V;
        data[4].PrevU = wv.BottomLeft.PrevU;
        data[4].PrevV = wv.BottomLeft.PrevV;
        data[4].Alpha = alpha;
        data[4].AddAlpha = addAlpha;
        data[4].LightLevelBufferIndex = lightBufferIndex;

        data[5].X = wv.BottomRight.X;
        data[5].Y = wv.BottomRight.Y;
        data[5].Z = wv.BottomRight.Z;
        data[5].PrevX = wv.BottomRight.X;
        data[5].PrevY = wv.BottomRight.Y;
        data[5].PrevZ = wv.PrevBottomZ;
        data[5].U = wv.BottomRight.U;
        data[5].V = wv.BottomRight.V;
        data[5].PrevU = wv.BottomRight.PrevU;
        data[5].PrevV = wv.BottomRight.PrevV;
        data[5].Alpha = alpha;
        data[5].AddAlpha = addAlpha;
        data[5].LightLevelBufferIndex = lightBufferIndex;
    }

    private static LegacyVertex[] GetWallVertices(in WallVertices wv, float lightLevelAdd, int lightBufferIndex,
        float alpha = 1.0f, float addAlpha = 1.0f)
    {
        LegacyVertex[] data = new LegacyVertex[6];
        // Our triangle is added like:
        //    0--2
        //    | /  3
        //    |/  /|
        //    1  / |
        //      4--5
        data[0].X = wv.TopLeft.X;
        data[0].Y = wv.TopLeft.Y;
        data[0].Z = wv.TopLeft.Z;
        data[0].PrevX = wv.TopLeft.X;
        data[0].PrevY = wv.TopLeft.Y;
        data[0].PrevZ = wv.PrevTopZ;
        data[0].U = wv.TopLeft.U;
        data[0].V = wv.TopLeft.V;
        data[0].PrevU = wv.TopLeft.PrevU;
        data[0].PrevV = wv.TopLeft.PrevV;
        data[0].Alpha = alpha;
        data[0].AddAlpha = addAlpha;
        data[0].LightLevelBufferIndex = lightBufferIndex;

        data[1].X = wv.BottomLeft.X;
        data[1].Y = wv.BottomLeft.Y;
        data[1].Z = wv.BottomLeft.Z;
        data[1].PrevX = wv.BottomLeft.X;
        data[1].PrevY = wv.BottomLeft.Y;
        data[1].PrevZ = wv.PrevBottomZ;
        data[1].U = wv.BottomLeft.U;
        data[1].V = wv.BottomLeft.V;
        data[1].PrevU = wv.BottomLeft.PrevU;
        data[1].PrevV = wv.BottomLeft.PrevV;
        data[1].Alpha = alpha;
        data[1].AddAlpha = addAlpha;
        data[1].LightLevelBufferIndex = lightBufferIndex;

        data[2].X = wv.TopRight.X;
        data[2].Y = wv.TopRight.Y;
        data[2].Z = wv.TopRight.Z;
        data[2].PrevX = wv.TopRight.X;
        data[2].PrevY = wv.TopRight.Y;
        data[2].PrevZ = wv.PrevTopZ;
        data[2].U = wv.TopRight.U;
        data[2].V = wv.TopRight.V;
        data[2].PrevU = wv.TopRight.PrevU;
        data[2].PrevV = wv.TopRight.PrevV;
        data[2].Alpha = alpha;
        data[2].AddAlpha = addAlpha;
        data[2].LightLevelBufferIndex = lightBufferIndex;

        data[3].X = wv.TopRight.X;
        data[3].Y = wv.TopRight.Y;
        data[3].Z = wv.TopRight.Z;
        data[3].PrevX = wv.TopRight.X;
        data[3].PrevY = wv.TopRight.Y;
        data[3].PrevZ = wv.PrevTopZ;
        data[3].U = wv.TopRight.U;
        data[3].V = wv.TopRight.V;
        data[3].PrevU = wv.TopRight.PrevU;
        data[3].PrevV = wv.TopRight.PrevV;
        data[3].Alpha = alpha;
        data[3].AddAlpha = addAlpha;
        data[3].LightLevelBufferIndex = lightBufferIndex;

        data[4].X = wv.BottomLeft.X;
        data[4].Y = wv.BottomLeft.Y;
        data[4].Z = wv.BottomLeft.Z;
        data[4].PrevX = wv.BottomLeft.X;
        data[4].PrevY = wv.BottomLeft.Y;
        data[4].PrevZ = wv.PrevBottomZ;
        data[4].U = wv.BottomLeft.U;
        data[4].V = wv.BottomLeft.V;
        data[4].PrevU = wv.BottomLeft.PrevU;
        data[4].PrevV = wv.BottomLeft.PrevV;
        data[4].Alpha = alpha;
        data[4].AddAlpha = addAlpha;
        data[4].LightLevelBufferIndex = lightBufferIndex;

        data[5].X = wv.BottomRight.X;
        data[5].Y = wv.BottomRight.Y;
        data[5].Z = wv.BottomRight.Z;
        data[5].PrevX = wv.BottomRight.X;
        data[5].PrevY = wv.BottomRight.Y;
        data[5].PrevZ = wv.PrevBottomZ;
        data[5].U = wv.BottomRight.U;
        data[5].V = wv.BottomRight.V;
        data[5].PrevU = wv.BottomRight.PrevU;
        data[5].PrevV = wv.BottomRight.PrevV;
        data[5].Alpha = alpha;
        data[5].AddAlpha = addAlpha;
        data[5].LightLevelBufferIndex = lightBufferIndex;

        return data;
    }

    private static void GetFlatVertices(DynamicArray<LegacyVertex> vertices, ref TriangulatedWorldVertex root, ref TriangulatedWorldVertex second, ref TriangulatedWorldVertex third, 
        int lightLevelBufferIndex)
    {
        vertices.Add(new LegacyVertex()
        {
            X = root.X,
            Y = root.Y,
            Z = root.Z,
            PrevX = root.X,
            PrevY = root.Y,
            PrevZ = root.PrevZ,
            U = root.U,
            V = root.V,
            PrevU = root.PrevU,
            PrevV = root.PrevV,
            Alpha = 1.0f,
            LightLevelBufferIndex = lightLevelBufferIndex,
        });

        vertices.Add(new LegacyVertex()
        {
            X = second.X,
            Y = second.Y,
            Z = second.Z,
            PrevX = second.X,
            PrevY = second.Y,
            PrevZ = second.PrevZ,
            U = second.U,
            V = second.V,
            PrevU = second.PrevU,
            PrevV = second.PrevV,
            Alpha = 1.0f,
            LightLevelBufferIndex = lightLevelBufferIndex,
        });

        vertices.Add(new LegacyVertex()
        {
            X = third.X,
            Y = third.Y,
            Z = third.Z,
            PrevX = third.X,
            PrevY = third.Y,
            PrevZ = third.PrevZ,
            U = third.U,
            V = third.V,
            PrevU = third.PrevU,
            PrevV = third.PrevV,
            Alpha = 1.0f,
            LightLevelBufferIndex = lightLevelBufferIndex,
        });
    }

    private void ReleaseUnmanagedResources()
    {
        m_staticCacheGeometryRenderer.Dispose();
        m_skyRenderer.Dispose();
        Portals.Dispose();
        if (m_lightBuffer != null)
            m_lightBuffer.Dispose();
    }
}
