using System;
using System.Collections.Generic;
using Helion.Audio;
using Helion.Maps;
using Helion.Maps.Specials.Compatibility;
using Helion.Resources;
using Helion.Resources.Archives.Collection;
using Helion.Resources.Definitions.Locks;
using Helion.Util;
using Helion.Util.Configs;
using Helion.Util.RandomGenerators;
using Helion.World.Blockmap;
using Helion.World.Bsp;
using Helion.World.Entities;
using Helion.World.Entities.Definition.Properties.Components;
using Helion.World.Entities.Players;
using Helion.World.Geometry;
using Helion.World.Geometry.Lines;
using Helion.World.Geometry.Sectors;
using Helion.World.Geometry.Sides;
using Helion.World.Geometry.Walls;
using Helion.World.Physics;
using Helion.World.Physics.Blockmap;
using Helion.World.Sound;
using Helion.World.Special;
using NLog;
using static Helion.Util.Assertion.Assert;
using Helion.Resources.Definitions.MapInfo;
using Helion.Util.Container;
using Helion.World.Entities.Definition;
using Helion.Models;
using Helion.Util.Timing;
using Helion.World.Entities.Definition.Flags;
using System.Linq;
using Helion.Geometry.Boxes;
using Helion.Geometry.Segments;
using Helion.Geometry.Vectors;
using Helion.World.Cheats;
using Helion.World.Stats;
using Helion.World.Entities.Inventories.Powerups;
using Helion.World.Impl.SinglePlayer;
using Helion.World.Util;
using Helion.Resources.IWad;
using Helion.Dehacked;
using Helion.Resources.Archives;
using Helion.Util.Profiling;
using Helion.World.Entities.Inventories;
using Helion.Maps.Specials;
using Helion.World.Entities.Definition.States;
using Helion.World.Special.Specials;
using Helion.World.Static;
using Helion.Geometry.Grids;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Helion.Render.OpenGL.Renderers.Legacy.World.Primitives;

namespace Helion.World;

public abstract class WorldBase : IWorld
{
    private const double MaxPitch = 80.0 * Math.PI / 180.0;
    private const double MinPitch = -80.0 * Math.PI / 180.0;

    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public event EventHandler<LevelChangeEvent>? LevelExit;
    public event EventHandler? WorldResumed;
    public event EventHandler? ClearConsole;
    public event EventHandler? OnResetInterpolation;
    public event EventHandler<SectorPlane>? SectorMoveStart;
    public event EventHandler<SectorPlane>? SectorMoveComplete;
    public event EventHandler<SideTextureEvent>? SideTextureChanged;
    public event EventHandler<PlaneTextureEvent>? PlaneTextureChanged;
    public event EventHandler<Sector>? SectorLightChanged;
    public event EventHandler<PlayerMessageEvent>? PlayerMessage;
    public event EventHandler? OnTick;
    public event EventHandler? OnDestroying;

    public readonly long CreationTimeNanos;
    public string MapName { get; protected set; }
    public readonly BlockMap Blockmap;
    public WorldState WorldState { get; protected set; } = WorldState.Normal;
    public int Gametick { get; private set; }
    public int GameTicker { get; private set; }
    public int LevelTime { get; private set; }
    public double Gravity { get; private set; } = 1.0;
    public bool Paused { get; protected set; }
    public bool DrawPause { get; protected set; }
    public bool PlayingDemo { get; set; }
    public bool DemoEnded { get; set; }
    public bool SameAsPreviousMap { get; set; }
    public IRandom Random => m_random;
    public IRandom SecondaryRandom { get; private set; }
    public IList<Line> Lines => Geometry.Lines;
    public IList<Side> Sides => Geometry.Sides;
    public IList<Wall> Walls => Geometry.Walls;
    public IList<Sector> Sectors => Geometry.Sectors;
    public CompactBspTree BspTree => Geometry.CompactBspTree;
    public EntityManager EntityManager { get; }
    public WorldSoundManager SoundManager { get; }
    public BlockmapTraverser BlockmapTraverser => PhysicsManager.BlockmapTraverser;
    public BlockMap RenderBlockmap { get; private set; }
    public SpecialManager SpecialManager { get; private set; }
    public IConfig Config { get; private set; }
    public MapInfoDef MapInfo { get; private set; }
    public LevelStats LevelStats { get; } = new();
    public SkillDef SkillDefinition { get; private set; }
    public ArchiveCollection ArchiveCollection { get; protected set; }
    public GlobalData GlobalData { get; }
    public CheatManager CheatManager { get; } = new();
    public DataCache DataCache => ArchiveCollection.DataCache;
    public abstract Player Player { get; protected set; }
    public List<IMonsterCounterSpecial> BossDeathSpecials => m_bossDeathSpecials;
    public bool IsFastMonsters { get; private set; }
    public virtual bool IsChaseCamMode => false;
    public bool DrawHud { get; protected set; } = true;
    public bool AnyLayerObscuring { get; set; }
    public bool IsDisposed { get; private set; }
    public abstract ListenerParams GetListener();
    public int CurrentBossTarget { get; set; }
    public MarkSpecials MarkSpecials { get; } = new();

    public GameInfoDef GameInfo => ArchiveCollection.Definitions.MapInfoDefinition.GameDefinition;
    public TextureManager TextureManager => ArchiveCollection.TextureManager;

    public MapGeometry Geometry { get; }
    public PhysicsManager PhysicsManager { get; private set; }
    protected readonly IAudioSystem AudioSystem;
    protected readonly IMap Map;
    protected readonly Profiler Profiler;
    private readonly IRandom m_saveRandom;
    private IRandom m_random;
    private int m_exitTicks;
    private int m_easyBossBrain;
    private int m_soundCount;
    private int m_lastBumpActivateGametick;
    private int m_exitGametick;
    private LevelChangeType m_levelChangeType = LevelChangeType.Next;
    private LevelChangeFlags m_levelChangeFlags;
    private Entity[] m_bossBrainTargets = Array.Empty<Entity>();
    private readonly List<IMonsterCounterSpecial> m_bossDeathSpecials = new();
    private readonly byte[] m_lineOfSightReject = Array.Empty<byte>();
    private readonly Func<DamageFuncParams, int> m_defaultDamageAction;
    private readonly EntityDefinition? m_teleportFogDef;

    private RadiusExplosionData m_radiusExplosion;
    private readonly Action<Entity> m_radiusExplosionAction;

    private HealChaseData m_healChaseData;
    private readonly Action<Entity> m_healChaseAction;

    private NewTracerTargetData m_newTracerTargetData;
    private readonly Func<Entity, GridIterationStatus> m_setNewTracerTargetAction;

    private LineOfSightEnemyData m_lineOfSightEnemyData;
    private readonly Func<Entity, GridIterationStatus> m_lineOfSightEnemyAction;

    private readonly TryMoveData EmtpyTryMove = new();

    protected WorldBase(GlobalData globalData, IConfig config, ArchiveCollection archiveCollection,
        IAudioSystem audioSystem, Profiler profiler, MapGeometry geometry, MapInfoDef mapInfoDef,
        SkillDef skillDef, IMap map, WorldModel? worldModel = null, IRandom? random = null)
    {
        m_random = random ?? new DoomRandom();
        m_saveRandom = m_random;
        SecondaryRandom = m_random.Clone();

        CreationTimeNanos = Ticker.NanoTime();
        GlobalData = globalData;
        ArchiveCollection = archiveCollection;
        AudioSystem = audioSystem;
        Config = config;
        MapInfo = mapInfoDef;
        SkillDefinition = skillDef;
        MapName = map.Name;
        Profiler = profiler;
        Geometry = geometry;
        Map = map;

        if (Map.Reject != null)
            m_lineOfSightReject = Map.Reject;

        Blockmap = new BlockMap(Lines, 128);
        RenderBlockmap = new BlockMap(Blockmap.Bounds, 512);

        SoundManager = new WorldSoundManager(this, audioSystem);
        EntityManager = new EntityManager(this);
        PhysicsManager = new PhysicsManager(this, BspTree, Blockmap, m_random);
        SpecialManager = new SpecialManager(this, m_random);

        WorldStatic.FlushIntersectionReferences();
        IsFastMonsters = skillDef.IsFastMonsters(config);

        m_defaultDamageAction = DefaultDamage;
        m_radiusExplosionAction = HandleRadiusExplosion;
        m_healChaseAction = HandleHealChase;
        m_setNewTracerTargetAction = HandleSetNewTracerTarget;
        m_lineOfSightEnemyAction = HandleLineOfSightEnemy;
        
        m_teleportFogDef = EntityManager.DefinitionComposer.GetByName("TeleportFog");

        RegisterConfigChanges();
        SetWorldStatic();

        if (worldModel != null)
        {
            WorldState = worldModel.WorldState;
            Gametick = worldModel.Gametick;
            LevelTime = worldModel.LevelTime;
            m_soundCount = worldModel.SoundCount;
            Gravity = worldModel.Gravity;
            Random.Clone(worldModel.RandomIndex);
            CurrentBossTarget = worldModel.CurrentBossTarget;
            GlobalData.VisitedMaps = GetVisitedMaps(worldModel.VisitedMaps);
            GlobalData.TotalTime = worldModel.TotalTime;
            LevelStats.TotalMonsters = worldModel.TotalMonsters;
            LevelStats.TotalItems = worldModel.TotalItems;
            LevelStats.TotalSecrets = worldModel.TotalSecrets;
            LevelStats.KillCount = worldModel.KillCount;
            LevelStats.ItemCount = worldModel.ItemCount;
            LevelStats.SecretCount = worldModel.SecretCount;
        }
    }

    private void RegisterConfigChanges()
    {
        Config.SlowTick.Enabled.OnChanged += SlowTickEnabled_OnChanged;
        Config.SlowTick.ChaseFailureSkipCount.OnChanged += SlowTickChaseFailureSkipCount_OnChanged;
        Config.SlowTick.Distance.OnChanged += SlowTickDistance_OnChanged;
        Config.SlowTick.ChaseMultiplier.OnChanged += SlowTickChaseMultiplier_OnChanged;
        Config.SlowTick.LookMultiplier.OnChanged += SlowTickLookMultiplier_OnChanged;
        Config.SlowTick.TracerMultiplier.OnChanged += SlowTickTracerMultiplier_OnChanged;
        Config.Compatibility.MissileClip.OnChanged += MissileClip_OnChanged;
        Config.Compatibility.AllowItemDropoff.OnChanged += AllowItemDropoff_OnChanged;
        Config.Compatibility.InfinitelyTallThings.OnChanged += InfinitelyTallThings_OnChanged;
        Config.Compatibility.NoTossDrops.OnChanged += NoTossDrops_OnChanged;
    }

    private void UnRegisterConfigChanges()
    {
        Config.SlowTick.Enabled.OnChanged -= SlowTickEnabled_OnChanged;
        Config.SlowTick.ChaseFailureSkipCount.OnChanged -= SlowTickChaseFailureSkipCount_OnChanged;
        Config.SlowTick.Distance.OnChanged -= SlowTickDistance_OnChanged;
        Config.SlowTick.ChaseMultiplier.OnChanged -= SlowTickChaseMultiplier_OnChanged;
        Config.SlowTick.LookMultiplier.OnChanged -= SlowTickLookMultiplier_OnChanged;
        Config.SlowTick.TracerMultiplier.OnChanged -= SlowTickTracerMultiplier_OnChanged;
        Config.Compatibility.MissileClip.OnChanged -= MissileClip_OnChanged;
        Config.Compatibility.AllowItemDropoff.OnChanged -= AllowItemDropoff_OnChanged;
        Config.Compatibility.InfinitelyTallThings.OnChanged -= InfinitelyTallThings_OnChanged;
        Config.Compatibility.NoTossDrops.OnChanged -= NoTossDrops_OnChanged;
    }

    private void SetWorldStatic()
    {
        Entity.ClosetChaseCount = 0;
        Entity.ClosetLookCount = 0;
        Entity.ChaseLoop = 0;
        Entity.ChaseFailureCount = 0;

        WorldStatic.World = this;
        WorldStatic.DataCache = DataCache;
        WorldStatic.EntityManager = EntityManager;
        WorldStatic.SoundManager = SoundManager;
        WorldStatic.EntityManager = EntityManager;
        WorldStatic.Frames = ArchiveCollection.Definitions.EntityFrameTable.Frames;
        WorldStatic.Random = Random;
        WorldStatic.SlowTickEnabled = Config.SlowTick.Enabled.Value;
        WorldStatic.SlowTickChaseFailureSkipCount = Config.SlowTick.ChaseFailureSkipCount;
        WorldStatic.SlowTickDistance = Config.SlowTick.Distance;
        WorldStatic.SlowTickChaseMultiplier = Config.SlowTick.ChaseMultiplier;
        WorldStatic.SlowTickLookMultiplier = Config.SlowTick.LookMultiplier;
        WorldStatic.SlowTickTracerMultiplier = Config.SlowTick.TracerMultiplier;
        WorldStatic.IsFastMonsters = IsFastMonsters;
        WorldStatic.IsSlowMonsters = SkillDefinition.SlowMonsters;
        WorldStatic.InfinitelyTallThings = Config.Compatibility.InfinitelyTallThings;
        WorldStatic.MissileClip = Config.Compatibility.MissileClip;
        WorldStatic.AllowItemDropoff = Config.Compatibility.AllowItemDropoff;
        WorldStatic.NoTossDrops = Config.Compatibility.NoTossDrops;
        WorldStatic.RespawnTimeSeconds = SkillDefinition.RespawnTime.Seconds;
        WorldStatic.ClosetLookFrameIndex = ArchiveCollection.EntityFrameTable.ClosetLookFrameIndex;
        WorldStatic.ClosetChaseFrameIndex = ArchiveCollection.EntityFrameTable.ClosetChaseFrameIndex;

        WorldStatic.DoomImpBall = EntityManager.DefinitionComposer.GetByName("DoomImpBall");
        WorldStatic.ArachnotronPlasma = EntityManager.DefinitionComposer.GetByName("ArachnotronPlasma");
        WorldStatic.Rocket = EntityManager.DefinitionComposer.GetByName("Rocket");
        WorldStatic.FatShot = EntityManager.DefinitionComposer.GetByName("FatShot");
        WorldStatic.CacodemonBall = EntityManager.DefinitionComposer.GetByName("CacodemonBall");
        WorldStatic.RevenantTracer = EntityManager.DefinitionComposer.GetByName("RevenantTracer");
        WorldStatic.BaronBall = EntityManager.DefinitionComposer.GetByName("BaronBall");
        WorldStatic.SpawnShot = EntityManager.DefinitionComposer.GetByName("SpawnShot");
        WorldStatic.BFGBall = EntityManager.DefinitionComposer.GetByName("BFGBall");
        WorldStatic.PlasmaBall = EntityManager.DefinitionComposer.GetByName("PlasmaBall");
    }

    private void NoTossDrops_OnChanged(object? sender, bool enabled) =>
        WorldStatic.NoTossDrops = enabled;
    private void InfinitelyTallThings_OnChanged(object? sender, bool enabled) =>
        WorldStatic.InfinitelyTallThings = enabled;
    private void AllowItemDropoff_OnChanged(object? sender, bool enabled) =>
        WorldStatic.AllowItemDropoff = enabled;
    private void MissileClip_OnChanged(object? sender, bool enabled) =>
        WorldStatic.MissileClip = enabled;
    private void SlowTickEnabled_OnChanged(object? sender, bool enabled) =>
        WorldStatic.SlowTickEnabled = enabled;
    private void SlowTickDistance_OnChanged(object? sender, int distance) =>
        WorldStatic.SlowTickDistance = distance;
    private void SlowTickChaseFailureSkipCount_OnChanged(object? sender, int value) =>
        WorldStatic.SlowTickChaseFailureSkipCount = value;
    private void SlowTickChaseMultiplier_OnChanged(object? sender, int value) =>
        WorldStatic.SlowTickChaseMultiplier = value;
    private void SlowTickLookMultiplier_OnChanged(object? sender, int value) =>
        WorldStatic.SlowTickLookMultiplier = value;
    private void SlowTickTracerMultiplier_OnChanged(object? sender, int value) =>
        WorldStatic.SlowTickTracerMultiplier = value;

    private IList<MapInfoDef> GetVisitedMaps(IList<string> visitedMaps)
    {
        List<MapInfoDef> maps = new();
        foreach (string mapName in visitedMaps)
        {
            var mapInfoDef = ArchiveCollection.Definitions.MapInfoDefinition.MapInfo.GetMap(mapName);
            if (mapInfoDef != null)
                maps.Add(mapInfoDef);
        }

        return maps;
    }

    ~WorldBase()
    {
        FailedToDispose(this);
        PerformDispose();
    }

    public void SetRandom(IRandom random)
    {
        WorldStatic.Random = random;
        m_random = random;
    }

    public virtual void Start(WorldModel? worldModel)
    {
        AddMapSpecial();
        InitBossBrainTargets();

        if (worldModel == null)
            SpecialManager.StartInitSpecials(LevelStats);

        StaticDataApplier.DetermineStaticData(this);
        SpecialManager.SectorSpecialDestroyed += SpecialManager_SectorSpecialDestroyed;
    }

    private void SpecialManager_SectorSpecialDestroyed(object? sender, ISectorSpecial special)
    {
        if (special is not SectorMoveSpecial move)
            return;

        SectorMoveComplete?.Invoke(this, move.SectorPlane);
    }

    public Player? GetLineOfSightPlayer(Entity entity, bool allaround)
    {
        for (int i = 0; i < EntityManager.Players.Count; i++)
        {
            Player player = EntityManager.Players[i];
            if (player.IsDead)
                continue;

            if (!allaround && !InFieldOfViewOrInMeleeDistance(entity, player))
                continue;

            if (CheckLineOfSight(entity, player))
                return player;
        }

        return null;
    }

    public Entity? GetLineOfSightEnemy(Entity entity, bool allaround)
    {
        m_lineOfSightEnemyData.Entity = entity;
        m_lineOfSightEnemyData.AllAround = allaround;
        m_lineOfSightEnemyData.SightEntity = null;
        Box2D box = new(entity.Position.XY, 1280);
        BlockmapTraverser.EntityTraverse(box, m_lineOfSightEnemyAction);
        return m_lineOfSightEnemyData.SightEntity;
    }

    private GridIterationStatus HandleLineOfSightEnemy(Entity checkEntity)
    {
        if (m_lineOfSightEnemyData.Entity.Id == checkEntity.Id || checkEntity.IsDead || m_lineOfSightEnemyData.Entity.Flags.Friendly == checkEntity.Flags.Friendly || checkEntity.IsPlayer)
            return GridIterationStatus.Continue;

        if (!m_lineOfSightEnemyData.AllAround && !InFieldOfViewOrInMeleeDistance(m_lineOfSightEnemyData.Entity, checkEntity))
            return GridIterationStatus.Continue;

        if (CheckLineOfSight(m_lineOfSightEnemyData.Entity, checkEntity))
        {
            m_lineOfSightEnemyData.SightEntity = checkEntity;
            return GridIterationStatus.Stop;
        }

        return GridIterationStatus.Continue;
    }

    public void NoiseAlert(Entity target, Entity source)
    {
        m_soundCount++;
        RecursiveSound(target, source.Sector, 0);
    }

    public void RecursiveSound(Entity target, Sector sector, int block)
    {
        if (sector.SoundValidationCount == m_soundCount && sector.SoundBlock <= block + 1)
            return;

        sector.SoundValidationCount = m_soundCount;
        sector.SoundBlock = block + 1;
        sector.SetSoundTarget(target);

        for (int i = 0; i < sector.Lines.Count; i++)
        {
            Line line = sector.Lines[i];
            if (line.Back == null || !LineOpening.IsOpen(line))
                continue;

            Sector other = line.Front.Sector == sector ? line.Back.Sector : line.Front.Sector;
            if (line.Flags.BlockSound)
            {
                // Has to cross two block sound lines to stop. This is how it was designed.
                if (block == 0)
                    RecursiveSound(target, other, 1);
            }
            else
            {
                RecursiveSound(target, other, block);
            }
        }
    }

    public void Link(Entity entity)
    {
        Precondition(entity.SectorNodes.Empty() && entity.BlockmapNodes.Empty(), "Forgot to unlink entity before linking");
        PhysicsManager.LinkToWorld(entity, null, false);
    }

    public void LinkClamped(Entity entity)
    {
        Precondition(entity.SectorNodes.Empty() && entity.BlockmapNodes.Empty(), "Forgot to unlink entity before linking");
        PhysicsManager.LinkToWorld(entity, null, true);
    }

    public virtual void Tick()
    {
        OnTick?.Invoke(this, EventArgs.Empty);
        DebugCheck();

        if (Paused)
        {
            TickPlayerStatusBars();
            GameTicker++;
            return;
        }

        Profiler.World.Total.Start();

        if (WorldState == WorldState.Exit)
        {
            SoundManager.Tick();
            m_exitTicks--;

            if (m_exitGametick == Gametick - 1)
                ResetInterpolation();

            if (m_exitTicks <= 0)
            {
                LevelChangeEvent changeEvent = new(m_levelChangeType, m_levelChangeFlags);
                LevelExit?.Invoke(this, changeEvent);
                if (changeEvent.Cancel)
                    WorldState = WorldState.Normal;
                else
                    WorldState = WorldState.Exited;

                m_random = m_saveRandom;
                HandleExitFlags();
            }
        }
        else if (WorldState == WorldState.Normal)
        {
            TickEntities();
            TickPlayers();
            SpecialManager.Tick();

            if (WorldState != WorldState.Exit)
            {
                ArchiveCollection.TextureManager.Tick();
                SoundManager.Tick();

                LevelTime++;
                GlobalData.TotalTime++;
            }
        }

        Gametick++;
        GameTicker++;

        Profiler.World.Total.Stop();
    }

    private void HandleExitFlags()
    {
        if ((m_levelChangeFlags & LevelChangeFlags.KillAllPlayers) != 0)
            KillAllPlayers();
        m_levelChangeFlags = LevelChangeFlags.None;
    }

    private void TickPlayerStatusBars()
    {
        foreach (Player player in EntityManager.Players)
            player.StatusBar.Tick();
    }

    [Conditional("DEBUG")]
    private static void DebugCheck()
    {
        if (WeakEntity.Default.Entity != null)
            Fail("Static WeakEntity default reference was changed.");
    }

    private void TickEntities()
    {
        Profiler.World.TickEntity.Start();
        var entity = EntityManager.Head;
        var nextEntity = entity;
        while (entity != null)
        {
            nextEntity = entity.Next;
            if (entity.PlayerObj != null && entity.PlayerObj.PlayerNumber == short.MaxValue)
            {
                entity = nextEntity;
                continue;
            }

            entity.Tick();

            if (WorldState == WorldState.Exit)
                break;

            // Entities can be disposed after Tick() (rocket explosion, blood spatter etc.)
            if (!entity.IsDisposed)
            {
                PhysicsManager.Move(entity);

                if (entity.Respawn)
                    HandleRespawn(entity);

                if (entity.Sector.SectorDamageSpecial != null)
                    entity.Sector.SectorDamageSpecial.Tick(entity);
            }

            entity = nextEntity;
        }

        Profiler.World.TickEntity.Stop();
    }

    private void TickPlayers()
    {
        Profiler.World.TickPlayer.Start();

        for (int i = 0; i < EntityManager.Players.Count; i++)
        {
            if (WorldState == WorldState.Exit)
                break;

            var player = EntityManager.Players[i];
            // Doom did not apply sector damage to voodoo dolls
            if (player.IsVooDooDoll || player.IsDisposed)
                continue;

            player.HandleTickCommand();
            player.TickCommand.TickHandled();

            if (player.Sector.Secret && player.OnSectorFloorZ(player.Sector))
            {
                DisplayMessage(player, null, "$SECRETMESSAGE");
                SoundManager.PlayStaticSound("misc/secret");
                player.Sector.SetSecret(false);
                LevelStats.SecretCount++;
                player.SecretsFound++;
            }
        }

        Profiler.World.TickPlayer.Stop();
    }

    public void SectorInstantKillEffect(Entity entity, InstantKillEffect effect)
    {
        // Damage rules apply for instant kill sectors. Doom did not apply sector damage to voodoo dolls
        if (entity.IsDead || (entity.PlayerObj != null && entity.PlayerObj.IsVooDooDoll))
            return;

        if (!entity.IsPlayer && (effect & InstantKillEffect.KillMonsters) != 0)
        {
            entity.ForceGib();
            return;
        }

        if (entity.PlayerObj == null)
            return;

        Player player = entity.PlayerObj;
        if ((effect & InstantKillEffect.KillAllPlayersExit) != 0)
            ExitLevel(LevelChangeType.Next, LevelChangeFlags.KillAllPlayers);

        if ((effect & InstantKillEffect.KillAllPlayersSecretExit) != 0)
            ExitLevel(LevelChangeType.SecretNext, LevelChangeFlags.KillAllPlayers);

        if ((effect & InstantKillEffect.KillUnprotectedPlayer) != 0 && !player.Flags.Invulnerable &&
            !player.Inventory.IsPowerupActive(PowerupType.IronFeet))
            player.ForceGib();

        if ((effect & InstantKillEffect.KillPlayer) != 0)
            player.ForceGib();
    }

    private void KillAllPlayers()
    {
        foreach (var player in EntityManager.Players)
        {
            if (player.IsVooDooDoll)
                continue;

            player.ForceGib();
        }
    }

    public virtual void Pause(PauseOptions options = PauseOptions.None)
    {
        if (Paused)
            return;

        DrawPause = options.HasFlag(PauseOptions.DrawPause);
        ResetInterpolation();
        SoundManager.Pause();

        Paused = true;
    }

    public void ResetInterpolation()
    {
        for (var entity = EntityManager.Head; entity != null; entity = entity.Next)
            entity.ResetInterpolation();

        SpecialManager.ResetInterpolation();
        OnResetInterpolation?.Invoke(this, EventArgs.Empty);
    }

    public virtual void Resume()
    {
        DrawPause = false;
        if (!Paused || DemoEnded)
            return;

        SoundManager.Resume();
        Paused = false;
        WorldResumed?.Invoke(this, EventArgs.Empty);
    }

    public void BossDeath(Entity entity)
    {
        bool anyPlayerAlive = false;
        for (int i = 0; i < EntityManager.Players.Count; i++)
        {
            if (!EntityManager.Players[i].IsDead)
            {
                anyPlayerAlive = true;
                break;
            }
        }

        if (!anyPlayerAlive)
            return;

        for (int i = 0; i < m_bossDeathSpecials.Count; i++)
        {
            var special = m_bossDeathSpecials[i];
            if (special.EntityDefinitionId == entity.Definition.Id)
                special.Tick();
        }
    }

    private void AddMapSpecial()
    {
        switch (MapInfo.MapSpecial)
        {
            case MapSpecial.BaronSpecial:
                AddMonsterCountSpecial(m_bossDeathSpecials, (EntityFlags f) => f.E1M8Boss, 666, MapInfo.MapSpecialAction);
                break;
            case MapSpecial.CyberdemonSpecial:
                AddMonsterCountSpecial(m_bossDeathSpecials, (EntityFlags f) => f.E2M8Boss || f.E4M6Boss, 666, MapInfo.MapSpecialAction);
                break;
            case MapSpecial.SpiderMastermindSpecial:
                AddMonsterCountSpecial(m_bossDeathSpecials, (EntityFlags f) => f.E3M8Boss || f.E4M8Boss, 666, MapInfo.MapSpecialAction);
                break;
            case MapSpecial.Map07Special:
                AddMonsterCountSpecial(m_bossDeathSpecials, (EntityFlags f) => f.Map07Boss1, 666, MapSpecialAction.LowerFloor);
                AddMonsterCountSpecial(m_bossDeathSpecials, (EntityFlags f) => f.Map07Boss2, 667, MapSpecialAction.FloorRaiseByLowestTexture);
                break;
        }

        foreach (var bossAction in MapInfo.BossActions)
        {
            string translatedName = GetTranslatedDehackedName(bossAction.ActorName);
            var entityDef = EntityManager.DefinitionComposer.GetByName(translatedName);
            if (entityDef == null)
            {                
                Log.Warn($"Invalid actor name for boss action: {bossAction.ActorName}");
                continue;
            }

            m_bossDeathSpecials.Add(new BossActionMonsterCount(this, bossAction, entityDef.Id));
        }
    }

    private string GetTranslatedDehackedName(string actorName)
    {
        const string DehActor = "Deh_Actor_";
        if (actorName.StartsWith(DehActor, StringComparison.OrdinalIgnoreCase))
        {
            string stringIndex = actorName[DehActor.Length..];
            if (!int.TryParse(stringIndex, out int index))
                return actorName;

            return DehackedApplier.GetDehackedActorName(index - 1);
        }

        return actorName;
    }

    private IEnumerable<EntityDefinition> GetEntityDefinitionsByFlag(Func<EntityFlags, bool> isMatch)
    {
        foreach (var def in EntityManager.DefinitionComposer.GetEntityDefinitions())
            if (isMatch(def.Flags))
                yield return def;
    }

    private void AddMonsterCountSpecial(List<IMonsterCounterSpecial> monsterCountSpecials, Func<EntityFlags, bool> isMatch, int sectorTag, 
        MapSpecialAction mapSpecialAction)
    {
        foreach (var def in GetEntityDefinitionsByFlag(isMatch))
            AddMonsterCountSpecial(monsterCountSpecials, def.Name, sectorTag, mapSpecialAction);
    }

    private void AddMonsterCountSpecial(List<IMonsterCounterSpecial> monsterCountSpecials, string monsterName, int sectorTag, MapSpecialAction mapSpecialAction)
    {
        EntityDefinition? definition = EntityManager.DefinitionComposer.GetByName(monsterName);
        if (definition == null)
        {
            Log.Error($"Failed to find {monsterName} for {mapSpecialAction}");
            return;
        }

        monsterCountSpecials.Add(new MonsterCountSpecial(this, SpecialManager, definition.Id, sectorTag, mapSpecialAction));
    }

    private void InitBossBrainTargets()
    {
        List<Entity> targets = new();
        for (var entity = EntityManager.Head; entity != null; entity = entity.Next)
        {
            if (entity.Definition.Name.Equals("BOSSTARGET", StringComparison.OrdinalIgnoreCase))
                targets.Add(entity);
        }

        // Doom chose for some reason to iterate in reverse order.
        targets.Reverse();
        m_bossBrainTargets = targets.ToArray();
    }

    public IList<Sector> FindBySectorTag(int tag) =>
        Geometry.FindBySectorTag(tag);

    public IEnumerable<Entity> FindByTid(int tid) =>
        EntityManager.FindByTid(tid);

    public IEnumerable<Line> FindByLineId(int lineId) =>
        Geometry.FindByLineId(lineId);

    public void SetLineId(Line line, int lineId) =>
        Geometry.SetLineId(line, lineId);

    public void Dispose()
    {
        OnDestroying?.Invoke(this, EventArgs.Empty);
        SpecialManager.SectorSpecialDestroyed -= SpecialManager_SectorSpecialDestroyed;
        PerformDispose();
        GC.SuppressFinalize(this);
    }

    public void ExitLevel(LevelChangeType type, LevelChangeFlags flags = LevelChangeFlags.None)
    {
        SoundManager.ClearSounds();
        m_levelChangeType = type;
        m_levelChangeFlags = flags;
        WorldState = WorldState.Exit;
        // The exit ticks thing is fudge. Change random to secondary to not break demos later.
        m_random = SecondaryRandom;
        m_exitTicks = 15;
        m_exitGametick = Gametick;
    }

    public Entity[] GetBossTargets()
    {
        m_easyBossBrain ^= 1;
        if (SkillDefinition.EasyBossBrain && m_easyBossBrain == 0)
            return Array.Empty<Entity>();

        return m_bossBrainTargets;
    }

    public void TelefragBlockingEntities(Entity entity)
    {
        DynamicArray<Entity> blockingEntities = DataCache.GetEntityList();
        entity.GetIntersectingEntities3D(entity.Position, blockingEntities, true);
        for (int i = 0; i < blockingEntities.Length; i++)
            blockingEntities[i].ForceGib();
        DataCache.FreeEntityList(blockingEntities);
    }

    /// <summary>
    /// Executes use logic on the entity. EntityUseActivated event will
    /// fire if the entity activates a line special or is in range to hit
    /// a blocking line. PlayerUseFail will fire if the entity is a player
    /// and we hit a block line but didn't activate a special.
    /// </summary>
    /// <remarks>
    /// If the line has a special and we are hitting the front then we
    /// can use it (player Z does not apply here). If there's a LineOpening
    /// with OpeningHeight less than or equal to 0, it's a closed sector.
    /// The special line behind it cannot activate until the sector has an
    /// opening.
    /// </remarks>
    /// <param name="entity">The entity to execute use.</param>
    public virtual bool EntityUse(Entity entity)
    {
        if (entity.IsDead)
            return false;

        bool hitBlockLine = false;
        bool activateSuccess = false;
        Vec2D start = entity.Position.XY;
        Vec2D end = start + (Vec2D.UnitCircle(entity.AngleRadians) * entity.Properties.Player.UseRange);
        var intersections = WorldStatic.Intersections;
        intersections.Clear();
        BlockmapTraverser.UseTraverse(new Seg2D(start, end), intersections);

        for (int i = 0; i < intersections.Length; i++)
        {
            BlockmapIntersect bi = intersections[i];
            if (bi.Line == null)
                continue;

            if (bi.Line.Segment.OnRight(start))
            {
                OnTryEntityUseLine(entity, bi.Line);

                if (bi.Line.HasSpecial)
                    activateSuccess = ActivateSpecialLine(entity, bi.Line, ActivationContext.UseLine) || activateSuccess;

                if (activateSuccess && !bi.Line.Flags.PassThrough)
                    break;

                if (bi.Line.Back == null)
                {
                    hitBlockLine = true;
                    break;
                }
            }

            if (bi.Line.Back != null)
            {
                LineOpening opening = PhysicsManager.GetLineOpening(bi.Line);
                if (opening.OpeningHeight <= 0)
                {
                    hitBlockLine = true;
                    break;
                }

                // Keep checking if hit two-sided blocking line - this way the PlayerUserFail will be raised if no line special is hit
                if (!opening.CanPassOrStepThrough(entity))
                    hitBlockLine = true;
            }
        }

        if (!activateSuccess && hitBlockLine && entity.PlayerObj != null)
            entity.PlayerObj.PlayUseFailSound();

        return activateSuccess;
    }

    public virtual void OnTryEntityUseLine(Entity entity, Line line)
    {

    }

    private void PlayerBumpUse(Entity entity)
    {
        if (Gametick - m_lastBumpActivateGametick < 16)
            return;

        bool shouldUse = false;
        Vec2D start = entity.Position.XY;
        Vec2D end = start + (Vec2D.UnitCircle(entity.AngleRadians) * entity.Properties.Player.UseRange);
        var intersections = WorldStatic.Intersections;
        intersections.Clear();
        BlockmapTraverser.UseTraverse(new Seg2D(start, end), intersections);

        for (int i = 0; i < intersections.Length; i++)
        {
            BlockmapIntersect bi = intersections[i];
            if (bi.Line == null)
                continue;

            bool specialActivate = bi.Line.HasSpecial && bi.Line.Segment.OnRight(start);
            if (specialActivate)
                shouldUse = true;

            if (bi.Line.Back == null)
                continue;

            // This is mostly for doors. They can be reversed so ignore it if it's in motion.
            if (specialActivate && SideHasActiveMove(bi.Line.Back.Sector))
            {
                shouldUse = false;
                break;
            }
        }

        if (shouldUse)
        {
            EntityUse(entity);
            m_lastBumpActivateGametick = Gametick;
        }
    }

    private static bool SideHasActiveMove(Sector sector) => sector.ActiveCeilingMove != null || sector.ActiveFloorMove != null;

    public bool CanActivate(Entity entity, Line line, ActivationContext context)
    {
        bool success = line.Special.CanActivate(entity, line, context,
            ArchiveCollection.Definitions.LockDefininitions, out LockDef? lockFail);
        if (entity.PlayerObj != null && lockFail != null)
        {
            entity.PlayerObj.PlayUseFailSound();
            DisplayMessage(entity.PlayerObj, null, GetLockFailMessage(line, lockFail));
        }
        return success;
    }

    private string GetLockFailMessage(Line line, LockDef lockDef)
    {
        if (line.Special.LineSpecialCompatibility != null &&
            line.Special.LineSpecialCompatibility.CompatibilityType == LineSpecialCompatibilityType.KeyObject)
            return ArchiveCollection.Language.GetMessage(lockDef.ObjectMessage);
        else
            return ArchiveCollection.Language.GetMessage(lockDef.DoorMessage);
    }

    /// <summary>
    /// Attempts to activate a line special given the entity, line, and context.
    /// </summary>
    /// <remarks>
    /// Does not do any range checking. Only verifies if the entity can activate the line special in this context.
    /// </remarks>
    /// <param name="entity">The entity to execute special.</param>
    /// <param name="line">The line containing the special to execute.</param>
    /// <param name="context">The ActivationContext to attempt to execute the special.</param>
    public virtual bool ActivateSpecialLine(Entity entity, Line line, ActivationContext context)
    {
        if (!CanActivate(entity, line, context))
            return false;

        EntityActivateSpecial args = new(context, entity, line);
        return EntityActivatedSpecial(args);
    }

    public bool GetAutoAimEntity(Entity startEntity, in Vec3D start, double angle, double distance, out double pitch, out Entity? entity) =>
        GetAutoAimAngle(startEntity, start, angle, distance, out pitch, out _, out entity, 1, 0);

    public virtual Entity? FireProjectile(Entity shooter, double angle, double pitch, double autoAimDistance, bool autoAim, EntityDefinition projectileDef, out Entity? autoAimEntity,
        double addAngle = 0, double addPitch = 0, double zOffset = 0)
    {
        autoAimEntity = null;
        Player? player = shooter.PlayerObj;
        Vec3D start = shooter.ProjectileAttackPos;
        start.Z += zOffset;

        if (autoAim && player != null &&
            GetAutoAimAngle(shooter, start, shooter.AngleRadians, autoAimDistance, out double autoAimPitch, out double autoAimAngle,
                out autoAimEntity, tracers: Constants.AutoAimTracers))
        {
            pitch = autoAimPitch;
            angle = autoAimAngle;
        }

        pitch += addPitch;
        angle += addAngle;

        Entity projectile = EntityManager.Create(projectileDef, start, 0.0, angle, 0);
        // Doom set the owner as the target
        projectile.SetOwner(shooter);
        projectile.SetTarget(shooter);
        projectile.PlaySeeSound();

        if (projectile.Flags.Randomize)
            projectile.SetRandomizeTicks();

        if (projectile.Flags.NoClip)
            return projectile;

        Vec3D velocity = Vec3D.UnitSphere(angle, pitch) * projectile.Properties.MissileMovementSpeed;
        Vec3D testPos = projectile.Position;
        if (projectile.Properties.MissileMovementSpeed > 0)
            testPos += Vec3D.UnitSphere(angle, pitch) * (shooter.Radius - 2.0);

        // TryMoveXY will use the velocity of the projectile
        // A projectile spawned where it can't fit can cause BlockingSectorPlane or BlockingEntity (IsBlocked = true)
        if (!projectile.IsBlocked() && PhysicsManager.TryMoveXY(projectile, testPos.XY).Success)
        {
            projectile.Position = testPos;
            projectile.Velocity = velocity;
            return projectile;
        }

        projectile.Position = testPos;
        HandleEntityHit(projectile, velocity, null);
        return null;
    }

    public virtual void FirePlayerHitscanBullets(Player shooter, int bulletCount, double spreadAngleRadians, double spreadPitchRadians, double pitch, double distance, bool autoAim,
        Func<DamageFuncParams, int>? damageFunc = null, DamageFuncParams damageParams = default)
    {
        double originalPitch = pitch;

        if (damageFunc == null)
            damageFunc = m_defaultDamageAction;

        if (autoAim)
        {
            Vec3D start = shooter.HitscanAttackPos;
            if (GetAutoAimAngle(shooter, start, shooter.AngleRadians, distance, out double autoAimPitch, out _, out _,
                tracers: Constants.AutoAimTracers))
            {
                pitch = autoAimPitch;
            }
        }

        if (Config.Developer.Render.Tracers)
        {
            shooter.PlayerObj.Tracers.AddLookPath(shooter.HitscanAttackPos, shooter.AngleRadians, originalPitch, distance, Gametick);
            shooter.PlayerObj.Tracers.AddAutoAimPath(shooter.HitscanAttackPos, shooter.AngleRadians, pitch, distance, Gametick);
        }        

        if (!shooter.Refire && bulletCount == 1)
        {
            int damage = damageFunc(damageParams);
            FireHitscan(shooter, shooter.AngleRadians, pitch, distance, damage);
            return;
        }

        for (int i = 0; i < bulletCount; i++)
        {
            int damage = damageFunc(damageParams);
            double angle = shooter.AngleRadians + (m_random.NextDiff() * spreadAngleRadians / 255);
            double newPitch = pitch + (m_random.NextDiff() * spreadPitchRadians / 255);
            FireHitscan(shooter, angle, newPitch, distance, damage);
        }
    }

    private int DefaultDamage(DamageFuncParams damageParams) => 5 * ((m_random.NextByte() % 3) + 1);

    public virtual Entity? FireHitscan(Entity shooter, double angle, double pitch, double distance, int damage,
        HitScanOptions options = HitScanOptions.Default)
    {
        Vec3D start = shooter.HitscanAttackPos;
        Vec3D end = start + Vec3D.UnitSphere(angle, pitch) * distance;
        Vec3D intersect = Vec3D.Zero;

        BlockmapIntersect? bi = FireHitScan(shooter, start, end, angle, pitch, distance, damage, options,
            ref intersect, out _);

        if (shooter.PlayerObj != null && (options & HitScanOptions.DrawRail) != 0)
        {
            Vec3D railEnd = bi != null && bi.Value.Line != null ? intersect : end;
            shooter.PlayerObj.Tracers.AddTracer(PrimitiveRenderType.Rail, (start, railEnd), Gametick, (0.2f, 0.2f, 1), 35);
        }

        return bi?.Entity;
    }

    public virtual BlockmapIntersect? FireHitScan(Entity shooter, Vec3D start, Vec3D end, double angle, double pitch, double distance, int damage,
        HitScanOptions options, ref Vec3D intersect, out Sector? hitSector)
    {
        hitSector = null;
        BlockmapIntersect? returnValue = null;
        double floorZ, ceilingZ;
        bool passThrough = (options & HitScanOptions.PassThroughEntities) != 0;
        Seg2D seg = new(start.XY, end.XY);
        var intersections = WorldStatic.Intersections;
        intersections.Clear();
        BlockmapTraverser.ShootTraverse(seg, intersections);

        for (int i = 0; i < intersections.Length; i++)
        {
            BlockmapIntersect bi = intersections[i];
            if (bi.Line != null)
            {
                if (damage > 0 && bi.Line.HasSpecial && CanActivate(shooter, bi.Line, ActivationContext.HitscanImpactsWall))
                {
                    var args = new EntityActivateSpecial(ActivationContext.HitscanImpactsWall, shooter, bi.Line);
                    EntityActivatedSpecial(args);
                }

                intersect = bi.Intersection.To3D(start.Z + (Math.Tan(pitch) * bi.Distance2D));

                if (bi.Line.Back == null)
                {
                    floorZ = bi.Line.Front.Sector.ToFloorZ(intersect);
                    ceilingZ = bi.Line.Front.Sector.ToCeilingZ(intersect);

                    if (intersect.Z > floorZ && intersect.Z < ceilingZ)
                    {
                        returnValue = bi;
                        break;
                    }

                    if (IsSkyClipOneSided(bi.Line.Front.Sector, floorZ, ceilingZ, intersect))
                        break;

                    GetSectorPlaneIntersection(start, end, bi.Line.Front.Sector, floorZ, ceilingZ, ref intersect);
                    hitSector = bi.Line.Front.Sector;
                    returnValue = bi;
                    break;
                }

                GetOrderedSectors(bi.Line, start, out Sector front, out Sector back);
                if (IsSkyClipTwoSided(front, back, intersect))
                    break;

                floorZ = front.ToFloorZ(intersect);
                ceilingZ = front.ToCeilingZ(intersect);

                if (intersect.Z < floorZ || intersect.Z > ceilingZ)
                {
                    GetSectorPlaneIntersection(start, end, front, floorZ, ceilingZ, ref intersect);
                    hitSector = front;
                    returnValue = bi;
                    break;
                }

                LineOpening opening = PhysicsManager.GetLineOpening(bi.Line);
                if ((opening.FloorZ > intersect.Z && intersect.Z > floorZ) || (opening.CeilingZ < intersect.Z && intersect.Z < ceilingZ))
                {
                    returnValue = bi;
                    break;
                }
            }
            else if (bi.Entity != null && shooter.Id != bi.Entity.Id && bi.Entity.BoxIntersects(start, end, ref intersect))
            {
                returnValue = bi;
                if (damage > 0)
                {
                    DamageEntity(bi.Entity, shooter, damage, DamageType.AlwaysApply, Thrust.Horizontal);
                    CreateBloodOrPulletPuff(bi.Entity, intersect, angle, distance, damage);
                }
                if (!passThrough)
                    break;
            }
        }

        if (returnValue != null && damage > 0)
        {
            // Only move closer on a line hit
            if (returnValue.Value.Entity == null && hitSector == null)
                MoveIntersectCloser(start, ref intersect, angle, returnValue.Value.Distance2D);
            CreateBloodOrPulletPuff(returnValue.Value.Entity, intersect, angle, distance, damage);
        }

        return returnValue;
    }

    public virtual bool DamageEntity(Entity target, Entity? source, int damage, DamageType damageType,
        Thrust thrust = Thrust.HorizontalAndVertical, Sector? sectorSource = null)
    {
        if (!target.Flags.Shootable || damage == 0 || target.IsDead)
            return false;

        Vec3D thrustVelocity = Vec3D.Zero;
        if (source != null && thrust != Thrust.None)
        {
            Vec3D savePos = source.Position;
            // Check if the source is owned by this target and the same position and move to get a valid thrust angle. (player shot missile against wall)
            if (source.Owner.Entity == target && source.Position.XY == target.Position.XY)
            {
                Vec3D move = (source.Position.XY + Vec2D.UnitCircle(target.AngleRadians) * 2).To3D(source.Position.Z);
                source.Position = move;
            }

            Vec2D xyDiff = source.Position.XY - target.Position.XY;
            bool zEqual = Math.Abs(target.Position.Z - source.Position.Z) <= double.Epsilon;
            bool xyEqual = Math.Abs(xyDiff.X) <= 1.0 && Math.Abs(xyDiff.Y) <= 1.0;
            double pitch = 0.0;

            double angle = source.Position.Angle(target.Position);
            double thrustAmount = damage * source.ProjectileKickBack * 0.125 / target.Properties.Mass;

            // Silly vanilla doom feature that allows target to be thrown forward sometimes
            if (damage < 40 && damage > target.Health &&
                target.Position.Z - source.Position.Z > 64 && (m_random.NextByte() & 1) != 0)
            {
                angle += Math.PI;
                thrustAmount *= 4;
            }

            if (thrust == Thrust.HorizontalAndVertical)
            {
                // Player rocket jumping check, back up the source Z to get a valid pitch
                // Only done for players, otherwise blowing up enemies will launch them in the air
                if (zEqual && target.IsPlayer && source.Owner.Entity == target)
                {
                    Vec3D sourcePos = new Vec3D(source.Position.X, source.Position.Y, source.Position.Z - 1.0);
                    pitch = sourcePos.Pitch(target.Position, 0.0);
                }
                else if (source.Position.Z < target.Position.Z || source.Position.Z > target.Position.Z + target.Height)
                {
                    Vec3D sourcePos = source.CenterPoint;
                    Vec3D targetPos = target.Position;
                    if (source.Position.Z > target.Position.Z + target.Height)
                        targetPos.Z += target.Height;
                    pitch = sourcePos.Pitch(targetPos, sourcePos.XY.Distance(targetPos.XY));
                }

                if (!xyEqual)
                    thrustVelocity = Vec3D.UnitSphere(angle, 0.0);

                thrustVelocity.Z = Math.Sin(pitch);
            }
            else
            {
                thrustVelocity = Vec3D.UnitSphere(angle, 0.0);
            }

            thrustVelocity *= thrustAmount;
            if (savePos != source.Position)
                source.Position = savePos;
        }

        bool setPainState = m_random.NextByte() < target.Properties.PainChance;
        if (target.PlayerObj != null)
        {
            // Voodoo dolls did not take sector damage in the original
            if (target.PlayerObj.IsVooDooDoll && sectorSource != null)
                return false;
            // Sector damage is applied to real players, but not their voodoo dolls
            if (sectorSource == null)
                ApplyVooDooDamage(target.PlayerObj, damage, setPainState);
        }

        if (target.Damage(source, damage, setPainState, damageType) || target.IsInvulnerable)
            target.Velocity += thrustVelocity;

        return true;
    }

    public virtual bool GiveItem(Player player, Entity item, EntityFlags? flags, out EntityDefinition definition, bool pickupFlash = true)
    {
        definition = item.Definition;

        if (ArchiveCollection.Definitions.DehackedDefinition != null && GetDehackedPickup(ArchiveCollection.Definitions.DehackedDefinition, item, out var vanillaDef))
        {
            var saveFlags = flags;
            definition = vanillaDef;
            flags = GetCombinedPickupFlags(vanillaDef.Flags, flags);
        }

        if (player.IsVooDooDoll)
            return GiveVooDooItem(player, item, flags, pickupFlash);

        return player.GiveItem(definition, flags, pickupFlash);
    }

    private static EntityFlags GetCombinedPickupFlags(EntityFlags dehackedFlags, EntityFlags? flags)
    {
        // Need to carry over flags that are modified by the world and affect pickups
        if (flags.HasValue)
            dehackedFlags.Dropped = flags.Value.Dropped;

        return dehackedFlags;
    }

    private bool GetDehackedPickup(DehackedDefinition dehacked, Entity item, [NotNullWhen(true)] out EntityDefinition? definition)
    {
        // Vanilla determined pickups by the sprite name
        // E.g. batman doom has an enemy that drops a shotgun with the blue key sprite
        if (!dehacked.PickupLookup.TryGetValue(item.Frame.Sprite, out string? def))
        {
            definition = null;
            return false;
        }

        definition = ArchiveCollection.EntityDefinitionComposer.GetByName(def);
        return definition!= null;
    }

    public virtual void PerformItemPickup(Entity entity, Entity item)
    {
        if (entity.PlayerObj == null)
            return;

        int health = entity.PlayerObj.Health;
        if (!GiveItem(entity.PlayerObj, item, item.Flags, out EntityDefinition definition))
            return;

        if (item.IsDisposed)
            return;

        if (entity.PlayerObj != null)
            PlayerPickedUpItem(entity.PlayerObj, item, health, definition);
        EntityManager.Destroy(item);
    }

    private void PlayerPickedUpItem(Player player, Entity item, int previousHealth, EntityDefinition definition)
    {
        if (player.IsVooDooDoll)
        {
            var findPlayer = EntityManager.GetRealPlayer(player.PlayerNumber);
            if (findPlayer == null)
                return;
            player = findPlayer;
        }

        item.PickupPlayer = player;
        item.FrameState.SetState("Pickup", warn: false);

        if (item.Flags.CountItem)
        {
            LevelStats.ItemCount++;
            player.ItemCount++;
        }

        string message = definition.Properties.Inventory.PickupMessage;
        var healthProperty = definition.Properties.HealthProperty;
        if (healthProperty != null && previousHealth < healthProperty.Value.LowMessageHealth && healthProperty.Value.LowMessage.Length > 0)
            message = healthProperty.Value.LowMessage;

        DisplayMessage(player, null, message);

        if (!string.IsNullOrEmpty(definition.Properties.Inventory.PickupSound))
        {
            SoundManager.CreateSoundOn(player, definition.Properties.Inventory.PickupSound,
                new SoundParams(player, channel: SoundChannel.Item));
        }
    }

    public virtual void HandleEntityHit(Entity entity, in Vec3D previousVelocity, TryMoveData? tryMove)
    {
        if (entity.IsDisposed)
            return;

        entity.Hit(previousVelocity);

        if (tryMove != null && (entity.Flags.Missile || entity.IsPlayer))
        {
            for (int i = 0; i < tryMove.ImpactSpecialLines.Length; i++)
                ActivateSpecialLine(entity, tryMove.ImpactSpecialLines[i], ActivationContext.EntityImpactsWall);

            if (entity.IsPlayer && Config.Game.BumpUse)
                PlayerBumpUse(entity);
        }

        if (entity.ShouldDieOnCollision())
        {
            if (entity.BlockingEntity != null)
            {
                int damage = entity.Properties.Damage.Get(m_random);
                DamageEntity(entity.BlockingEntity, entity, damage, DamageType.Normal);
            }

            bool skyClip = false;

            if (entity.BlockingLine != null)
            {
                if (entity.BlockingLine.OneSided && IsSkyClipOneSided(entity.BlockingLine.Front.Sector, entity.BlockingLine.Front.Sector.ToFloorZ(entity.Position),
                    entity.BlockingLine.Front.Sector.ToCeilingZ(entity.Position), entity.Position))
                {
                    skyClip = true;
                }
                else if (!entity.BlockingLine.OneSided)
                {
                    GetOrderedSectors(entity.BlockingLine, entity.Position, out Sector front, out Sector back);
                    if (IsSkyClipTwoSided(front, back, entity.Position))
                        skyClip = true;
                }
            }

            if (entity.BlockingSectorPlane != null && ArchiveCollection.TextureManager.IsSkyTexture(entity.BlockingSectorPlane.TextureHandle))
                skyClip = true;

            if (skyClip)
                EntityManager.Destroy(entity);
            else
                entity.SetDeathState(null);
        }
        else if (entity.Flags.Touchy || (entity.BlockingEntity != null && entity.BlockingEntity.Flags.Touchy))
        {
            if (entity.BlockingEntity != null && ShouldDieFromTouch(entity, entity.BlockingEntity))
                entity.BlockingEntity.Kill(null);
            else if (entity.IsCrushing())
                entity.Kill(null);
        }
    }

    public virtual void HandleEntityIntersections(Entity entity, in Vec3D previousVelocity, TryMoveData? tryMove)
    {
        if (tryMove == null || tryMove.IntersectEntities2D.Length == 0)
            return;

        for (int i = 0; i < tryMove.IntersectEntities2D.Length; i++)
        {
            Entity intersectEntity = tryMove.IntersectEntities2D[i];
            if (!entity.OverlapsZ(intersectEntity) || entity.Id == intersectEntity.Id)
                continue;

            if (entity.Flags.Ripper && entity.Owner.Entity?.Id != intersectEntity.Id)
                RipDamage(entity, intersectEntity);
            if (intersectEntity.Flags.Touchy && ShouldDieFromTouch(entity, intersectEntity))
                intersectEntity.Kill(null);
        }
    }

    private void RipDamage(Entity source, Entity target)
    {
        int damage = source.Definition.Properties.Damage.Get(m_random);
        if (DamageEntity(target, source, damage, DamageType.Normal, Thrust.None))
        {
            CreateBloodOrPulletPuff(target, source.Position, source.AngleRadians, 0, damage, true);
            string sound = "misc/ripslop";
            if (source.Properties.RipSound.Length > 0)
                sound = source.Properties.RipSound;
            SoundManager.CreateSoundOn(source, sound, new SoundParams(source));
        }
    }

    private static bool ShouldDieFromTouch(Entity entity, Entity blockingEntity)
    {
        // The documentation on Touchy is horrible
        // Based on testing crushers will kill it and it will only be killed if something walks into it
        // But not the other way around...
        // LostSouls will not kill PainElementals
        const string painElemental = "PainElemental";
        const string lostSoul = "LostSoul";
        if (!blockingEntity.Flags.Touchy || !blockingEntity.CanDamage(entity, DamageType.Normal))
            return false;

        if (entity.Definition.IsType(painElemental) && blockingEntity.Definition.IsType(lostSoul))
            return false;

        if (entity.Definition.IsType(lostSoul) && blockingEntity.Definition.IsType(painElemental))
            return false;

        return true;
    }

    public virtual bool CheckLineOfSight(Entity from, Entity to)
    {
        if (IsLineOfSightRejected(from, to))
            return false;

        Vec2D start = from.Position.XY;
        Vec2D end = to.Position.XY;

        if (start == end)
            return true;

        if (from.Sector.TransferHeights != null && TransferHeightsLineOfSightBlocked(from, to, from.Sector.TransferHeights))
            return false;
        if (to.Sector.TransferHeights != null && TransferHeightsLineOfSightBlocked(to, from, to.Sector.TransferHeights))
            return false;

        Seg2D seg = new(start, end);
        var intersections = WorldStatic.Intersections;
        intersections.Clear();
        BlockmapTraverser.SightTraverse(seg, intersections, out bool hitOneSidedLine);
        if (hitOneSidedLine)
            return false;

        Vec3D sightPos = new(from.Position.X, from.Position.Y, from.Position.Z + (from.Height * 0.75));
        double distance2D = start.Distance(end);
        double topPitch = sightPos.Pitch(to.Position.Z + to.Height, distance2D);
        double bottomPitch = sightPos.Pitch(to.Position.Z, distance2D);

        TraversalPitchStatus status = GetBlockmapTraversalPitch(intersections, sightPos, from, topPitch, bottomPitch, out _, out _);
        return status != TraversalPitchStatus.Blocked;
    }

    private static bool TransferHeightsLineOfSightBlocked(Entity from, Entity to, TransferHeights heights)
    {
        var sector = heights.ControlSector;
        return (from.Position.Z + from.Height <= sector.Floor.Z && to.Position.Z >= sector.Floor.Z) ||
               (from.Position.Z >= sector.Ceiling.Z && to.Position.Z + to.Height <= sector.Ceiling.Z);
    }

    private bool IsLineOfSightRejected(Entity from, Entity to)
    {
        int pnum = from.Sector.Id * Sectors.Count + to.Sector.Id;
        int bytenum = pnum >> 3;

        if (m_lineOfSightReject.Length <= bytenum)
            return false;

        if ((m_lineOfSightReject[bytenum] & (1 << (pnum & 7))) != 0)
            return true;

        return false;
    }

    public virtual bool InFieldOfView(Entity from, Entity to, double fieldOfViewRadians)
    {
        Vec2D entityLookingVector = Vec2D.UnitCircle(from.AngleRadians);
        Vec2D entityToTarget = new(to.Position.X - from.Position.X, to.Position.Y - from.Position.Y);
        entityToTarget.Normalize();
        var angle = Math.Acos(entityToTarget.Dot(entityLookingVector));
        return angle < fieldOfViewRadians/2;
    }

    private static bool InFieldOfViewOrInMeleeDistance(Entity from, Entity to)
    {
        double distance = from.Position.ApproximateDistance2D(to.Position);
        Vec2D entityLookingVector = Vec2D.UnitCircle(from.AngleRadians);
        Vec2D entityToTarget = new(to.Position.X - from.Position.X, to.Position.Y - from.Position.Y);

        // Not in front 180 FOV
        if (entityToTarget.Dot(entityLookingVector) < 0 && distance > Constants.EntityMeleeDistance)
            return false;

        return true;
    }

    public virtual void RadiusExplosion(Entity damageSource, Entity attackSource, int radius, int maxDamage)
    {
        m_radiusExplosion.DamageSource = damageSource;
        m_radiusExplosion.AttackSource = attackSource;
        m_radiusExplosion.Radius = radius;
        m_radiusExplosion.MaxDamage = maxDamage;
        m_radiusExplosion.Thrust = damageSource.Flags.OldRadiusDmg ? Thrust.Horizontal : Thrust.HorizontalAndVertical;
        Vec2D pos2D = damageSource.Position.XY;
        Vec2D radius2D = new(radius, radius);
        Box2D explosionBox = new(pos2D - radius2D, pos2D + radius2D);

        BlockmapTraverser.ExplosionTraverse(explosionBox, m_radiusExplosionAction);
    }

    private void HandleRadiusExplosion(Entity entity)
    {
        if (!ShouldApplyExplosionDamage(entity, m_radiusExplosion.DamageSource))
            return;

        ApplyExplosionDamageAndThrust(m_radiusExplosion.DamageSource, m_radiusExplosion.AttackSource, entity,
            m_radiusExplosion.Radius, m_radiusExplosion.MaxDamage, m_radiusExplosion.Thrust,
            m_radiusExplosion.DamageSource.Flags.OldRadiusDmg || entity.Flags.OldRadiusDmg);
    }

    private bool ShouldApplyExplosionDamage(Entity entity, Entity damageSource)
    {
        if ((entity.Flags.Boss || entity.Flags.NoRadiusDmg) && !damageSource.Flags.ForceRadiusDmg)
            return false;

        if (!entity.CanApplyRadiusExplosionDamage(damageSource) || !CheckLineOfSight(entity, damageSource))
            return false;

        return true;
    }

    public virtual TryMoveData TryMoveXY(Entity entity, Vec2D position)
        => PhysicsManager.TryMoveXY(entity, position);

    public virtual bool IsPositionValid(Entity entity, Vec2D position) =>
        PhysicsManager.IsPositionValid(entity, position, PhysicsManager.TryMoveData);

    public virtual SectorMoveStatus MoveSectorZ(double speed, double destZ, SectorMoveSpecial moveSpecial)
    {
        if (moveSpecial.IsInitialMove)
            SectorMoveStart?.Invoke(this, moveSpecial.SectorPlane);

        return PhysicsManager.MoveSectorZ(speed, destZ, moveSpecial);
    }

    public virtual void HandleEntityDeath(Entity deathEntity, Entity? deathSource, bool gibbed)
    {
        PhysicsManager.HandleEntityDeath(deathEntity);
        CheckDropItem(deathEntity);

        if (deathEntity.Flags.CountKill && !deathEntity.Flags.Friendly)
            LevelStats.KillCount++;

        if (deathEntity.PlayerObj != null)
        {
            if (deathSource != null)
                HandleObituary(deathEntity.PlayerObj, deathSource);

            ApplyVooDooKill(deathEntity.PlayerObj, deathSource, gibbed);
        }
    }

    private void CheckDropItem(Entity deathEntity)
    {
        if (deathEntity.Definition.Properties.DropItem != null &&
            (deathEntity.Definition.Properties.DropItem.Probability == DropItemProperty.DefaultProbability ||
                m_random.NextByte() < deathEntity.Definition.Properties.DropItem.Probability))
        {
            for (int i = 0; i < deathEntity.Definition.Properties.DropItem.Amount; i++)
            {
                bool spawnInit = true;
                Vec3D pos = deathEntity.Position;
                pos.Z = deathEntity.Sector.Floor.Z;
                double addVelocity = 0;
                if (!WorldStatic.NoTossDrops)
                {
                    spawnInit = false;
                    pos.Z = deathEntity.Position.Z + deathEntity.Definition.Properties.Height / 2;
                    addVelocity = 4;
                }
                
                Entity? dropItem = EntityManager.Create(deathEntity.Definition.Properties.DropItem.ClassName, pos, init: spawnInit);
                if (dropItem == null)
                    continue;
                
                dropItem.Flags.Dropped = true;
                dropItem.Velocity.Z += addVelocity;
            }
        }
    }

    private void HandleObituary(Player player, Entity deathSource)
    {
        if (ArchiveCollection.IWadType == IWadBaseType.ChexQuest)
            return;

        // If the player killed themself then don't display the obituary message
        // There is probably a special string for this in multiplayer for later
        Entity killer = deathSource.Owner.Entity ?? deathSource;
        if (player.Id == killer.Id)
            return;

        // Monster obituaries can come from the projectile, while the player obituaries always come from the owner player
        Entity obituarySource = killer;
        if (killer.IsPlayer)
            obituarySource = deathSource;

        string? obituary;
        if (obituarySource == deathSource && obituarySource.Definition.Properties.HitObituary.Length > 0)
            obituary = obituarySource.Definition.Properties.HitObituary;
        else
            obituary = obituarySource.Definition.Properties.Obituary;

        if (!string.IsNullOrEmpty(obituary))
            DisplayMessage(player, killer.PlayerObj, obituary);
    }

    public virtual void DisplayMessage(string message) => DisplayMessage(null, null, message);

    public virtual void DisplayMessage(Player? player, Player? other, string message)
    {
        message = ArchiveCollection.Definitions.Language.GetMessage(player, other, message);
        if (message.Length > 0)
        {
            if (player == null || player.Id == GetCameraPlayer().Id)
                Log.Info(message);
            if (player != null && player.Id == GetCameraPlayer().Id)
                PlayerMessage?.Invoke(this, new PlayerMessageEvent(player, message));
        }
    }

    private void HandleRespawn(Entity entity)
    {
        entity.Respawn = false;
        if (entity.Definition.Flags.Solid && IsPositionBlockedByEntity(entity, entity.SpawnPoint))
            return;

        var newEntity = EntityManager.Create(entity.Definition, entity.SpawnPoint, 0, entity.AngleRadians, entity.ThingId, true);
        CreateTeleportFog(entity.Position);
        CreateTeleportFog(entity.SpawnPoint);

        newEntity.Flags.Friendly = entity.Flags.Friendly;
        newEntity.AngleRadians = entity.AngleRadians;
        newEntity.ReactionTime = 18;

        entity.Dispose();
    }

    public bool IsPositionBlockedByEntity(Entity entity, in Vec3D position)
    {
        if (!entity.Definition.Flags.Solid)
            return true;

        double oldHeight = entity.Height;
        entity.Flags.Solid = true;
        entity.Height = entity.Definition.Properties.Height;

        // This is original functionality, the original game only checked against other things
        // It didn't check if it would clip into map geometry
        bool blocked = !BlockmapTraverser.SolidBlockTraverse(entity, entity.Position, !WorldStatic.InfinitelyTallThings);

        entity.Flags.Solid = false;
        entity.Height = oldHeight;
        return blocked;
    }

    public bool IsPositionBlocked(Entity entity)
    {
        bool blocked = !BlockmapTraverser.SolidBlockTraverse(entity, entity.Position, !WorldStatic.InfinitelyTallThings);
        if (blocked)
            return true;

        if (!PhysicsManager.IsPositionValid(entity, entity.Position.XY, EmtpyTryMove))
            return true;

        return false;
    }

    public void ResetGametick() => Gametick = 0;

    private void ApplyExplosionDamageAndThrust(Entity source, Entity attackSource, Entity entity, double radius, int maxDamage, Thrust thrust,
        bool approxDistance2D)
    {
        double distance;
        if (thrust == Thrust.HorizontalAndVertical && (source.Position.Z < entity.Position.Z || source.Position.Z >= entity.TopZ))
        {
            Vec3D sourcePos = source.Position;
            Vec3D targetPos = entity.Position;

            if (source.Position.Z > entity.Position.Z)
                targetPos.Z += entity.Height;

            if (approxDistance2D)
                distance = Math.Max(0.0, sourcePos.ApproximateExplosionDistance2D(targetPos) - entity.Radius);
            else
                distance = Math.Max(0.0, sourcePos.Distance(targetPos) - entity.Radius);
        }
        else
        {
            if (approxDistance2D)
                distance = Math.Max(0.0, entity.Position.ApproximateExplosionDistance2D(source.Position) - entity.Radius);
            else
                distance = Math.Max(0.0, entity.Position.Distance(source.Position) - entity.Radius);
        }

        int applyDamage = Math.Clamp((int)(radius - distance), 0, maxDamage);
        if (applyDamage <= 0)
            return;

        Entity? originalOwner = source.Owner.Entity;
        source.SetOwner(attackSource);
        DamageEntity(entity, source, applyDamage, DamageType.AlwaysApply, thrust);
        source.SetOwner(originalOwner);
    }

    protected bool ChangeToMusic(int number)
    {
        if (this is SinglePlayerWorld singlePlayerWorld)
        {
            if (!MapWarp.GetMap(number, ArchiveCollection, out MapInfoDef? mapInfoDef) || mapInfoDef == null)
                return false;

            SinglePlayerWorld.PlayLevelMusic(Config, singlePlayerWorld.AudioSystem, mapInfoDef.Music, ArchiveCollection);
            return true;
        }

        return false;
    }

    protected void ResetLevel(bool loadLastWorldModel)
    {
        LevelChangeType type = loadLastWorldModel ? LevelChangeType.ResetOrLoadLast : LevelChangeType.Reset;
        LevelExit?.Invoke(this, new LevelChangeEvent(type, LevelChangeFlags.None));
    }

    protected virtual void PerformDispose()
    {
        IsDisposed = true;
        UnRegisterConfigChanges();
        SpecialManager.Dispose();
        EntityManager.Dispose();
        SoundManager.Dispose();

        Blockmap.Dispose();
        RenderBlockmap.Dispose();
    }

    private void CreateBloodOrPulletPuff(Entity? entity, Vec3D intersect, double angle, double attackDistance, int damage, bool ripper = false)
    {
        bool bulletPuff = entity == null || entity.Definition.Flags.NoBlood;
        EntityDefinition? def;
        if (bulletPuff)
        {
            def = EntityManager.DefinitionComposer.BulletPuffDefinition;
            intersect.Z += Random.NextDiff() * Constants.PuffRandZ;
        }
        else
        {
            def = entity!.GetBloodDefinition();
        }

        if (def == null)
            return;

        var create = EntityManager.Create(def, intersect, 0, angle, 0);
        if (bulletPuff)
        {
            create.Velocity.Z = 1;
            if (create.Flags.Randomize)
                create.SetRandomizeTicks();

            // Doom would skip the initial sparking state of the bullet puff for punches
            // Bulletpuff decorate has a MELEESTATE for this
            if (attackDistance == Constants.EntityMeleeDistance)
                create.SetMeleeState();
        }
        else
        {
            SetBloodValues(entity, create, damage, ripper);
        }
    }

    private void SetBloodValues(Entity? entity, Entity blood, int damage, bool ripper)
    {
        if (ripper)
        {
            if (entity != null)
            {
                blood.Velocity.X = entity.Velocity.X / 2;
                blood.Velocity.Y = entity.Velocity.Y / 2;
            }

            blood.Velocity.X += m_random.NextDiff() / 16.0;
            blood.Velocity.Y += m_random.NextDiff() / 16.0;
            blood.Velocity.Z += m_random.NextDiff() / 16.0;
            return;
        }

        blood.Velocity.Z = 2;

        int offset = 0;
        if (damage <= 12 && damage >= 9)
            offset = 1;
        else if (damage < 9)
            offset = 2;

        if (offset == 0)
            blood.SetRandomizeTicks();
        else if (blood.Definition.SpawnState != null)
            blood.FrameState.SetFrameIndex(blood.Definition.SpawnState.Value + offset);
    }

    private static void MoveIntersectCloser(in Vec3D start, ref Vec3D intersect, double angle, double distXY)
    {
        distXY -= 2.0;
        intersect.X = start.X + (Math.Cos(angle) * distXY);
        intersect.Y = start.Y + (Math.Sin(angle) * distXY);
    }

    /// <summary>
    /// Fires when an entity activates a line special with use or by crossing a line.
    /// </summary>
    /// <param name="shooter">The entity firing.</param>
    /// <param name="start">The position the enity is firing from.</param>
    /// <param name="angle">The angle the entity is firing.</param>
    /// <param name="distance">The distance to use for firing.</param>
    /// <param name="pitch">The pitch to use for the hit entity.</param>
    /// <param name="setAngle">The angle to use for the hit entity.</param>
    /// <param name="entity">The hit entity.</param>
    /// <param name="tracers">The number of tracers to use excluding the angle of the player. Vanilla doom used 2.</param>
    /// <returns>True if a valid entity is found and the pitch is set.</returns>
    /// <param name="tracerSpread">Doom would check at -5 degress and +5 degrees for a hit as well.
    /// Doom used the pitch for hitscan weapons, but would use the angle as well for projectiles.</param>
    private bool GetAutoAimAngle(Entity shooter, in Vec3D start, double angle, double distance,
        out double pitch, out double setAngle, out Entity? entity,
        int tracers = 0, double tracerSpread = Constants.DefaultSpreadAngle)
    {
        entity = null;
        pitch = 0;
        setAngle = angle;

        double spread;
        int iterateTracers;
        if (tracers <= 1)
        {
            spread = 0;
            tracers = 1;
            iterateTracers = 1;
        }
        else
        {
            spread = tracerSpread / (tracers / 2);
            iterateTracers = tracers + 1;
        }

        for (int i = 0; i < iterateTracers; i++)
        {
            Seg2D seg = new(start.XY, (start + Vec3D.UnitSphere(setAngle, 0) * distance).XY);
            var intersections = WorldStatic.Intersections;
            intersections.Clear();
            BlockmapTraverser.ShootTraverse(seg, intersections);

            TraversalPitchStatus status = GetBlockmapTraversalPitch(intersections, start, shooter, MaxPitch, MinPitch, out pitch, out entity);

            if (status == TraversalPitchStatus.PitchSet)
                return true;

            setAngle += spread;
            if (i == tracers / 2)
                setAngle = angle - tracerSpread;
        }

        return false;
    }

    private enum TraversalPitchStatus
    {
        Blocked,
        PitchSet,
        PitchNotSet,
    }

    private TraversalPitchStatus GetBlockmapTraversalPitch(DynamicArray<BlockmapIntersect> intersections, in Vec3D start, Entity startEntity, double topPitch, double bottomPitch,
        out double pitch, out Entity? entity)
    {
        pitch = 0.0;
        entity = null;

        for (int i = 0; i < intersections.Length; i++)
        {
            BlockmapIntersect bi = intersections[i];

            if (bi.Line != null)
            {
                if (bi.Line.Back == null)
                    return TraversalPitchStatus.Blocked;

                LineOpening opening = PhysicsManager.GetLineOpening(bi.Line);
                if (opening.FloorZ < opening.CeilingZ)
                {
                    double sectorPitch = start.Pitch(opening.FloorZ, bi.Distance2D);
                    if (sectorPitch > bottomPitch)
                        bottomPitch = sectorPitch;

                    sectorPitch = start.Pitch(opening.CeilingZ, bi.Distance2D);
                    if (sectorPitch < topPitch)
                        topPitch = sectorPitch;

                    if (topPitch <= bottomPitch)
                        return TraversalPitchStatus.Blocked;
                }
                else
                {
                    return TraversalPitchStatus.Blocked;
                }
            }
            else if (bi.Entity != null && startEntity.Id != bi.Entity.Id)
            {
                double thingTopPitch = start.Pitch(bi.Entity.TopZ, bi.Distance2D);
                if (thingTopPitch < bottomPitch)
                    continue;

                double thingBottomPitch = start.Pitch(bi.Entity.Position.Z, bi.Distance2D);
                if (thingBottomPitch > topPitch)
                    continue;

                if (thingBottomPitch > topPitch)
                    return TraversalPitchStatus.Blocked;
                if (thingTopPitch < bottomPitch)
                    return TraversalPitchStatus.Blocked;

                if (thingTopPitch < topPitch)
                    topPitch = thingTopPitch;
                if (thingBottomPitch > bottomPitch)
                    bottomPitch = thingBottomPitch;

                pitch = (bottomPitch + topPitch) / 2.0;
                entity = bi.Entity;
                return TraversalPitchStatus.PitchSet;
            }
        }

        return TraversalPitchStatus.PitchNotSet;
    }

    private bool IsSkyClipOneSided(Sector sector, double floorZ, double ceilingZ, in Vec3D intersect)
    {
        if (intersect.Z > ceilingZ && ArchiveCollection.TextureManager.IsSkyTexture(sector.Ceiling.TextureHandle))
            return true;
        else if (intersect.Z < floorZ && ArchiveCollection.TextureManager.IsSkyTexture(sector.Floor.TextureHandle))
            return true;

        return false;
    }

    private bool IsSkyClipTwoSided(Sector front, Sector back, in Vec3D intersect)
    {
        bool isFrontCeilingSky = ArchiveCollection.TextureManager.IsSkyTexture(front.Ceiling.TextureHandle);
        bool isBackCeilingSky = ArchiveCollection.TextureManager.IsSkyTexture(back.Ceiling.TextureHandle);

        if (isFrontCeilingSky && isBackCeilingSky && intersect.Z > back.ToCeilingZ(intersect))
            return true;

        if (isFrontCeilingSky && intersect.Z > front.ToCeilingZ(intersect))
            return true;

        if (ArchiveCollection.TextureManager.IsSkyTexture(front.Floor.TextureHandle) && intersect.Z < front.ToFloorZ(intersect))
            return true;

        return false;
    }

    private static void GetSectorPlaneIntersection(in Vec3D start, in Vec3D end, Sector sector, double floorZ, double ceilingZ, ref Vec3D intersect)
    {
        if (intersect.Z < floorZ)
        {
            sector.Floor.Plane.Intersects(start, end, ref intersect);
            intersect.Z = sector.ToFloorZ(intersect);
        }
        else if (intersect.Z > ceilingZ)
        {
            sector.Ceiling.Plane.Intersects(start, end, ref intersect);
            intersect.Z = sector.ToCeilingZ(intersect) - 4;
        }
    }

    private static void GetOrderedSectors(Line line, in Vec3D start, out Sector front, out Sector back)
    {
        if (line.Segment.OnRight(start))
        {
            front = line.Front.Sector;
            back = line.Back!.Sector;
        }
        else
        {
            front = line.Back!.Sector;
            back = line.Front.Sector;
        }
    }

    public void CreateTeleportFog(in Vec3D pos, bool playSound = true)
    {
        if (m_teleportFogDef == null)
            return;

        var teleport = EntityManager.Create(m_teleportFogDef, pos, 0.0, 0.0, 0);
        SoundManager.CreateSoundOn(teleport, Constants.TeleportSound, new SoundParams(teleport));
    }

    public void ActivateCheat(Player player, ICheat cheat)
    {
        if (!string.IsNullOrEmpty(cheat.CheatOn))
        {
            string msg;
            if (cheat.IsToggleCheat)
                msg = player.Cheats.IsCheatActive(cheat.CheatType) ? cheat.CheatOn : cheat.CheatOff;
            else
                msg = cheat.CheatOn;

            DisplayMessage(player, null, msg);
        }

        if (cheat is LevelCheat levelCheat)
        {
            if (levelCheat.CheatType == CheatType.ChangeLevel)
            {
                LevelExit?.Invoke(this, new LevelChangeEvent(levelCheat.LevelNumber, isCheat: true));
                return;
            }
            else if (levelCheat.CheatType == CheatType.ChangeMusic && !ChangeToMusic(levelCheat.LevelNumber))
            {
                return;
            }
        }

        switch (cheat.CheatType)
        {
            case CheatType.NoClip:
                player.Flags.NoClip = player.Cheats.IsCheatActive(cheat.CheatType);
                break;
            case CheatType.Fly:
                player.Flags.NoGravity = player.Cheats.IsCheatActive(cheat.CheatType);
                break;
            case CheatType.Kill:
                ClearConsole?.Invoke(this, EventArgs.Empty);
                player.ForceGib();
                break;
            case CheatType.Ressurect:
                ClearConsole?.Invoke(this, EventArgs.Empty);
                if (player.IsDead)
                    player.SetRaiseState();
                break;
            case CheatType.KillAllMonsters:
                ClearConsole?.Invoke(this, EventArgs.Empty);
                DisplayMessage(player, null, $"{KillAllMonsters()} {ArchiveCollection.Language.GetMessage(cheat.CheatOn)}");
                break;
            case CheatType.God:
                if (!player.IsDead)
                    SetGodModeHealth(player);
                player.Flags.Invulnerable = player.Cheats.IsCheatActive(cheat.CheatType);
                break;
            case CheatType.GiveAllNoKeys:
                GiveAllWeapons(player);
                GiveCheatArmor(player, cheat.CheatType);
                break;
            case CheatType.GiveAll:
                GiveAllWeapons(player);
                player.Inventory.GiveAllKeys(EntityManager.DefinitionComposer);
                GiveCheatArmor(player, cheat.CheatType);
                break;
            case CheatType.Chainsaw:
                GiveChainsaw(player);
                break;
            case CheatType.BeholdRadSuit:
            case CheatType.BeholdPartialInvisibility:
            case CheatType.BeholdInvulnerability:
            case CheatType.BeholdComputerAreaMap:
            case CheatType.BeholdLightAmp:
            case CheatType.BeholdBerserk:
            case CheatType.Automap:
                TogglePowerup(player, PowerupNameFromCheatType(cheat.CheatType), PowerupTypeFromCheatType(cheat.CheatType));
                break;
            case CheatType.Exit:
            case CheatType.ExitSecret:
                ClearConsole?.Invoke(this, EventArgs.Empty);
                ExitLevel(cheat.CheatType == CheatType.ExitSecret ? LevelChangeType.SecretNext : LevelChangeType.Next);
                break;
        }
    }

    private int KillAllMonsters()
    {
        int killCount = 0;
        for (var entity = EntityManager.Head; entity != null; entity = entity.Next)
        {
            if (!entity.IsDead && (entity.Flags.CountKill || entity.Flags.IsMonster))
            {
                entity.ForceGib();
                killCount++;
            }
        }

        return killCount;
    }

    private void SetGodModeHealth(Player player)
    {
        if (ArchiveCollection.Dehacked != null && ArchiveCollection.Dehacked.Misc != null && ArchiveCollection.Dehacked.Misc.GodModeHealth.HasValue)
        {
            if (ArchiveCollection.Dehacked.Misc.GodModeHealth.Value > 0)
                player.Health = ArchiveCollection.Dehacked.Misc.GodModeHealth.Value;
        }
        else
        {
            player.Health = player.Definition.Properties.Player.MaxHealth;
        }
    }

    public int EntityCount(int entityDefinitionId) =>
        EntityCount(entityDefinitionId, true);

    public int EntityAliveCount(int entityDefinitionId) =>
        EntityCount(entityDefinitionId, true);

    private int EntityCount(int entityDefinitionId, bool checkAlive)
    {
        int count = 0;
        for (var entity = EntityManager.Head; entity != null; entity = entity.Next)
        {
            if (entity.Definition.Id == entityDefinitionId && (!checkAlive || !entity.IsDead))
                count++;
        }
        return count;
    }

    public bool HealChase(Entity entity, EntityFrame healState, string healSound)
    {
        m_healChaseData.HealEntity = entity;
        m_healChaseData.HealState = healState;
        m_healChaseData.HealSound = healSound;
        m_healChaseData.Healed = false;
        Box2D nextBox = new(entity.GetNextEnemyPos(), entity.Radius);
        BlockmapTraverser.HealTraverse(nextBox, m_healChaseAction);

        return m_healChaseData.Healed;
    }

    private void HandleHealChase(Entity entity)
    {
        var healChaseEntity = m_healChaseData.HealEntity;
        m_healChaseData.Healed = true;
        entity.Flags.Solid = true;
        entity.Height = entity.Definition.Properties.Height;

        Entity? saveTarget = healChaseEntity.Target.Entity;
        healChaseEntity.SetTarget(entity);
        EntityActionFunctions.A_FaceTarget(healChaseEntity);
        healChaseEntity.SetTarget(saveTarget);
        healChaseEntity.FrameState.SetState(m_healChaseData.HealState);

        if (m_healChaseData.HealSound.Length > 0)
            WorldStatic.SoundManager.CreateSoundOn(entity, m_healChaseData.HealSound, new SoundParams(entity));

        entity.SetRaiseState();
        entity.Flags.Friendly = healChaseEntity.Flags.Friendly;
    }

    public void TracerSeek(Entity entity, double threshold, double maxTurnAngle, GetTracerVelocityZ velocityZ)
    {
        if (entity.Tracer.Entity == null || entity.Tracer.Entity.IsDead)
            return;

        SetTracerAngle(entity, threshold, maxTurnAngle);

        double z = entity.Velocity.Z;
        entity.Velocity = Vec3D.UnitSphere(entity.AngleRadians, 0.0) * entity.Definition.Properties.MissileMovementSpeed;
        entity.Velocity.Z = z;

        entity.Velocity.Z = velocityZ(entity, entity.Tracer.Entity);
    }

    public void SetNewTracerTarget(Entity entity, double fieldOfViewRadians, double radius)
    {
        m_newTracerTargetData.Entity = entity;
        m_newTracerTargetData.Owner = entity.Owner.Entity ?? entity;
        m_newTracerTargetData.FieldOfViewRadians = fieldOfViewRadians;
        BlockmapTraverser.EntityTraverse(new Box2D(entity.Position.XY, radius), m_setNewTracerTargetAction);
    }

    private GridIterationStatus HandleSetNewTracerTarget(Entity checkEntity)
    {
        if (!checkEntity.Flags.Shootable)
            return GridIterationStatus.Continue;

        if (!m_newTracerTargetData.Owner.ValidEnemyTarget(checkEntity))
            return GridIterationStatus.Continue;

        if (m_newTracerTargetData.FieldOfViewRadians > 0 && 
            !InFieldOfView(m_newTracerTargetData.Entity, checkEntity, m_newTracerTargetData.FieldOfViewRadians))
            return GridIterationStatus.Continue;

        if (!CheckLineOfSight(m_newTracerTargetData.Entity, checkEntity))
            return GridIterationStatus.Continue;

        m_newTracerTargetData.Entity.SetTracer(checkEntity);
        return GridIterationStatus.Stop;
    }

    public void SetEntityPosition(Entity entity, Vec3D pos)
    {
        entity.ResetInterpolation();
        entity.UnlinkFromWorld();
        entity.Position = pos;
        Link(entity);
    }

    private static void SetTracerAngle(Entity entity, double threshold, double maxTurnAngle)
    {
        if (entity.Tracer.Entity == null)
            return;
        // Doom's angles were always 0-360 and did not allow negatives (thank you arithmetic overflow)
        // To keep this code familiar GetPositiveAngle will keep angle between 0 and 2pi
        double exact = MathHelper.GetPositiveAngle(entity.Position.Angle(entity.Tracer.Entity.Position));
        double currentAngle = MathHelper.GetPositiveAngle(entity.AngleRadians);
        double diff = MathHelper.GetPositiveAngle(exact - currentAngle);

        if (!MathHelper.AreEqual(exact, currentAngle))
        {
            if (diff > Math.PI)
            {
                entity.AngleRadians = MathHelper.GetPositiveAngle(entity.AngleRadians - maxTurnAngle);
                if (MathHelper.GetPositiveAngle(exact - entity.AngleRadians) < threshold)
                    entity.AngleRadians = exact;
            }
            else
            {
                entity.AngleRadians = MathHelper.GetPositiveAngle(entity.AngleRadians + maxTurnAngle);
                if (MathHelper.GetPositiveAngle(exact - entity.AngleRadians) > threshold)
                    entity.AngleRadians = exact;
            }
        }
    }

    private void GiveCheatArmor(Player player, CheatType cheatType)
    {
        bool autoGive = true;
        int? setAmount = null;
        if (ArchiveCollection.Dehacked != null && ArchiveCollection.Dehacked.Misc != null)
        {
            var misc = ArchiveCollection.Dehacked.Misc;
            if ((cheatType == CheatType.GiveAll && misc.IdkfaArmorClass == DehackedDefinition.GreenArmorClassNum) ||
                (cheatType == CheatType.GiveAllNoKeys && misc.IdfaArmorClass == DehackedDefinition.GreenArmorClassNum))
            {
                var armorDef = EntityManager.DefinitionComposer.GetByName(DehackedDefinition.GreenArmorClassName);
                if (armorDef != null)
                    player.GiveItem(armorDef, null, false);
                autoGive = false;
            }

            if (cheatType == CheatType.GiveAll)
                setAmount = misc.IdkfaArmor;
            else if (cheatType == CheatType.GiveAllNoKeys)
                setAmount = misc.IdfaArmor;
        }

        if (autoGive)
        {
            var armor = EntityManager.DefinitionComposer.GetEntityDefinitions().Where(x => x.IsType(Inventory.ArmorClassName) && x.EditorId.HasValue)
                .OrderByDescending(x => x.Properties.Armor.SaveAmount).ToList();

            if (armor.Any())
                player.GiveItem(armor.First(), null, pickupFlash: false);
        }

        if (setAmount.HasValue)
            player.Armor = setAmount.Value;
    }

    private void TogglePowerup(Player player, string powerupDefinition, PowerupType powerupType)
    {
        if (string.IsNullOrEmpty(powerupDefinition) || powerupType == PowerupType.None)
            return;

        var def = EntityManager.DefinitionComposer.GetByName(powerupDefinition);
        if (def == null)
            return;

        // Not really a powerup, part of inventory
        if (powerupType == PowerupType.ComputerAreaMap)
        {
            if (player.Inventory.HasItem(def.Name))
                player.Inventory.Remove(def.Name, 1);
            else
                player.Inventory.Add(def, 1);
        }
        else
        {
            var existingPowerup = player.Inventory.Powerups.FirstOrDefault(x => x.PowerupType == powerupType);
            if (existingPowerup != null)
                player.Inventory.RemovePowerup(existingPowerup);
            else
                player.Inventory.Add(def, 1);
        }
    }

    private static string PowerupNameFromCheatType(CheatType cheatType)
    {
        switch (cheatType)
        {
            case CheatType.Automap:
                return "Allmap";
            case CheatType.BeholdRadSuit:
                return "RadSuit";
            case CheatType.BeholdPartialInvisibility:
                return "BlurSphere";
            case CheatType.BeholdInvulnerability:
                return "InvulnerabilitySphere";
            case CheatType.BeholdComputerAreaMap:
                return "Allmap";
            case CheatType.BeholdLightAmp:
                return "Infrared";
            case CheatType.BeholdBerserk:
                return "Berserk";
        }

        return string.Empty;
    }

    private static PowerupType PowerupTypeFromCheatType(CheatType cheatType)
    {
        switch (cheatType)
        {
            case CheatType.BeholdRadSuit:
                return PowerupType.IronFeet;
            case CheatType.BeholdPartialInvisibility:
                return PowerupType.Invisibility;
            case CheatType.BeholdInvulnerability:
                return PowerupType.Invulnerable;
            case CheatType.BeholdComputerAreaMap:
                return PowerupType.ComputerAreaMap;
            case CheatType.BeholdLightAmp:
                return PowerupType.LightAmp;
            case CheatType.BeholdBerserk:
                return PowerupType.Strength;
            case CheatType.Automap:
                return PowerupType.ComputerAreaMap;
        }

        return PowerupType.None;
    }

    private void GiveChainsaw(Player player)
    {
        var chainsaw = EntityManager.DefinitionComposer.GetByName("chainsaw");
        if (chainsaw != null)
            player.GiveWeapon(chainsaw);
    }

    private void GiveAllWeapons(Player player)
    {
        foreach (string name in player.Inventory.Weapons.GetWeaponDefinitionNames())
        {
            var weapon = EntityManager.DefinitionComposer.GetByName(name);
            if (weapon != null)
                player.GiveWeapon(weapon, autoSwitch: false);
        }

        player.Inventory.GiveAllAmmo(EntityManager.DefinitionComposer);
    }

    private void ApplyVooDooDamage(Player player, int damage, bool setPainState)
    {
        if (!player.IsVooDooDoll || EntityManager.VoodooDolls.Count == 0 || player.IsSyncVooDoo)
            return;

        SyncVooDooDollWithPlayer(player);
        Player? updatePlayer = EntityManager.GetRealPlayer(player.PlayerNumber);
        if (updatePlayer == null)
            return;

        updatePlayer.Damage(null, damage, setPainState, DamageType.AlwaysApply);
        CompleteVooDooDollSync();
    }

    private void ApplyVooDooKill(Player player, Entity? source, bool forceGib)
    {
        if (EntityManager.VoodooDolls.Count == 0 || player.IsSyncVooDoo)
            return;

        SyncVooDooDollWithPlayer(player);
        Player? updatePlayer = EntityManager.GetRealPlayer(player.PlayerNumber);
        if (updatePlayer == null)
            return;

        if (forceGib)
        {
            updatePlayer.ForceGib();
            player.ForceGib();
        }
        else
        {
            updatePlayer.Kill(source);
            player.Kill(source);
        }

        CompleteVooDooDollSync();
    }

    private bool GiveVooDooItem(Player player, Entity item, EntityFlags? flags, bool pickupFlash)
    {
        Player? updatePlayer = EntityManager.GetRealPlayer(player.PlayerNumber);
        if (updatePlayer == null)
            return false;

        bool success = updatePlayer.GiveItem(item.Definition, flags, pickupFlash);
        if (!success)
            return false;

        return true;
    }

    private void SyncVooDooDollWithPlayer(Player voodooDoll)
    {
        Player? realPlayer = EntityManager.GetRealPlayer(voodooDoll.PlayerNumber);
        if (realPlayer == null)
            return;

        for (int i = 0; i < EntityManager.Players.Count; i++)
            EntityManager.Players[i].IsSyncVooDoo = true;

        voodooDoll.IsSyncVooDoo = true;
        voodooDoll.VoodooSync(realPlayer);
    }

    private void CompleteVooDooDollSync()
    {
        for (int i = 0; i < EntityManager.Players.Count; i++)
            EntityManager.Players[i].IsSyncVooDoo = false;

        for (int i = 0; i < EntityManager.VoodooDolls.Count; i++)
            EntityManager.VoodooDolls[i].IsSyncVooDoo = false;
    }

    public void SetSideTexture(Side side, WallLocation location, int textureHandle)
    {
        int previousTextureHandle;
        Wall wall;
        switch (location)
        {
            case WallLocation.Upper:
                previousTextureHandle = side.Upper.TextureHandle;
                wall = side.Upper;
                wall.SetTexture(textureHandle, SideDataTypes.UpperTexture);
                break;
            case WallLocation.Lower:
                previousTextureHandle = side.Lower.TextureHandle;
                wall = side.Lower;
                wall.SetTexture(textureHandle, SideDataTypes.LowerTexture);
                break;
            case WallLocation.Middle:
            default:
                previousTextureHandle = side.Middle.TextureHandle;
                wall = side.Middle;
                wall.SetTexture(textureHandle, SideDataTypes.MiddleTexture);
                break;
        }

        SideTextureChanged?.Invoke(this, new SideTextureEvent(side, wall, textureHandle, previousTextureHandle));
    }

    public void SetPlaneTexture(SectorPlane plane, int textureHandle)
    {
        int previousTextureHandle = plane.TextureHandle;
        plane.SetTexture(textureHandle, Gametick);
        PlaneTextureChanged?.Invoke(this, new PlaneTextureEvent(plane, textureHandle, previousTextureHandle));
    }

    public void SetSectorLightLevel(Sector sector, short lightLevel)
    {
        sector.SetLightLevel(lightLevel, Gametick);
        SectorLightChanged?.Invoke(this, sector);
    }

    public void SetSectorFloorLightLevel(Sector sector, short lightLevel)
    {
        sector.SetFloorLightLevel(lightLevel, Gametick);
        SectorLightChanged?.Invoke(this, sector);
    }

    public void SetSectorCeilingLightLevel(Sector sector, short lightLevel)
    {
        sector.SetCeilingLightLevel(lightLevel, Gametick);
        SectorLightChanged?.Invoke(this, sector);
    }

    private bool EntityActivatedSpecial(in EntityActivateSpecial args) =>
        SpecialManager.TryAddActivatedLineSpecial(args);

    public WorldModel ToWorldModel()
    {
        List<SectorModel> sectorModels = new();
        List<SectorDamageSpecialModel> sectorDamageSpecialModels = new();
        SetSectorModels(sectorModels, sectorDamageSpecialModels);

        return new WorldModel()
        {
            ConfigValues = GetConfigValuesModel(),
            Files = GetGameFilesModel(),
            MapName = MapName,
            WorldState = WorldState,
            Gametick = Gametick,
            LevelTime = LevelTime,
            SoundCount = m_soundCount,
            Gravity = Gravity,
            RandomIndex = Random.RandomIndex,
            Skill = ArchiveCollection.Definitions.MapInfoDefinition.MapInfo.GetSkillLevel(SkillDefinition),
            CurrentBossTarget = CurrentBossTarget,

            Players = GetPlayerModels(),
            Entities = GetEntityModels(),
            Sectors = sectorModels,
            DamageSpecials = sectorDamageSpecialModels,
            Lines = GetLineModels(),
            Specials = SpecialManager.GetSpecialModels(),
            VisitedMaps = GlobalData.VisitedMaps.Select(x => x.MapName).ToList(),
            TotalTime = GlobalData.TotalTime,

            TotalMonsters = LevelStats.TotalMonsters,
            TotalItems = LevelStats.TotalItems,
            TotalSecrets = LevelStats.TotalSecrets,
            KillCount = LevelStats.KillCount,
            ItemCount = LevelStats.ItemCount,
            SecretCount = LevelStats.SecretCount
        };
    }

    private IList<ConfigValueModel> GetConfigValuesModel()
    {
        List<ConfigValueModel> items = new();
        foreach (var (path, component) in Config.GetComponents())
        {
            if (!component.Attribute.Serialize)
                continue;

            items.Add(new ConfigValueModel(path, component.Value.ObjectValue));
        }
        return items;
    }

    public GameFilesModel GetGameFilesModel()
    {
        return new GameFilesModel()
        {
            IWad = GetIWadFileModel(),
            Files = GetFileModels(),
        };
    }

    public virtual void ToggleChaseCameraMode()
    {
    }

    private IList<PlayerModel> GetPlayerModels()
    {
        List<PlayerModel> playerModels = new(EntityManager.Players.Count);
        EntityManager.Players.ForEach(player => playerModels.Add(player.ToPlayerModel()));
        EntityManager.VoodooDolls.ForEach(player => playerModels.Add(player.ToPlayerModel()));
        return playerModels;
    }

    private FileModel GetIWadFileModel()
    {
        Archive? archive = ArchiveCollection.IWad;
        if (archive != null)
            return archive.ToFileModel();

        return new FileModel();
    }

    private IList<FileModel> GetFileModels()
    {
        List<FileModel> fileModels = new();
        var archives = ArchiveCollection.Archives;
        foreach (var archive in archives)
        {
            if (archive.ExtractedFrom != null || archive.MD5 == Archive.DefaultMD5)
                continue;
            fileModels.Add(archive.ToFileModel());
        }

        return fileModels;
    }

    private List<EntityModel> GetEntityModels()
    {
        List<EntityModel> entityModels = new();
        for (var entity = EntityManager.Head; entity != null; entity = entity.Next)
        {
            if (!entity.IsPlayer)
                entityModels.Add(entity.ToEntityModel(new EntityModel()));
        }
        return entityModels;
    }

    private void SetSectorModels(List<SectorModel> sectorModels, List<SectorDamageSpecialModel> sectorDamageSpecialModels)
    {
        for (int i = 0; i < Sectors.Count; i++)
        {
            Sector sector = Sectors[i];
            if (sector.SoundTarget.Entity != null || sector.DataChanged)
                sectorModels.Add(sector.ToSectorModel(this));
            if (sector.SectorDamageSpecial != null)
                sectorDamageSpecialModels.Add(sector.SectorDamageSpecial.ToSectorDamageSpecialModel());
        }
    }

    private List<LineModel> GetLineModels()
    {
        List<LineModel> lineModels = new();
        for (int i = 0; i < Lines.Count; i++)
        {
            Line line = Lines[i];
            if (!line.DataChanged)
                continue;

            lineModels.Add(line.ToLineModel(this));
        }

        return lineModels;
    }

    public virtual Player GetCameraPlayer() => Player;
}
