using System;
using System.Collections.Generic;
using System.Linq;
using Helion.Maps.Specials.ZDoom;
using Helion.Resources;
using Helion.Util.Container.Linkable;
using Helion.Util.Extensions;
using Helion.Util.Geometry;
using Helion.Util.Geometry.Boxes;
using Helion.Util.Geometry.Segments;
using Helion.Util.Geometry.Vectors;
using Helion.Util.RandomGenerators;
using Helion.World.Blockmap;
using Helion.World.Bsp;
using Helion.World.Entities;
using Helion.World.Entities.Definition;
using Helion.World.Entities.Players;
using Helion.World.Geometry.Lines;
using Helion.World.Geometry.Sectors;
using Helion.World.Geometry.Subsectors;
using Helion.World.Physics.Blockmap;
using Helion.World.Sound;
using Helion.World.Special.SectorMovement;
using static Helion.Util.Assertion.Assert;

namespace Helion.World.Physics
{
    /// <summary>
    /// Responsible for handling all the physics and collision detection in a
    /// world.
    /// </summary>
    public class PhysicsManager
    {
        private const int MaxSlides = 3;
        private const double Gravity = 1.0;
        private const double Friction = 0.90625;
        private const double SlideStepBackTime = 1.0 / 32.0;
        private const double MinMovementThreshold = 0.06;
        private const double SetEntityToFloorSpeedMax = 9;
        private const double MaxPitch = 80.0 * Math.PI / 180.0;
        private const double MinPitch = -80.0 * Math.PI / 180.0;

        public static readonly double LowestPossibleZ = Fixed.Lowest().ToDouble();

        public BlockmapTraverser BlockmapTraverser { get; private set; }

        private readonly IWorld m_world;
        private readonly BspTree m_bspTree;
        private readonly BlockMap m_blockmap;
        private readonly SoundManager m_soundManager;
        private readonly EntityManager m_entityManager;
        private readonly LineOpening m_lineOpening = new LineOpening();
        private readonly IRandom m_random;
        private DateTime m_shootTest = DateTime.Now;

        /// <summary>
        /// Fires when an entity activates a line special with use or by crossing a line.
        /// </summary>
        public event EventHandler<EntityActivateSpecialEventArgs>? EntityActivatedSpecial;

        /// <summary>
        /// Creates a new physics manager which utilizes the arguments for any
        /// collision detection or linking to the world.
        /// </summary>
        /// <param name="world">The world to operate on.</param>
        /// <param name="bspTree">The BSP tree for the world.</param>
        /// <param name="blockmap">The blockmap for the world.</param>
        /// <param name="soundManager">The sound manager to play sounds from.</param>
        /// <param name="entityManager">entity manager.</param>
        /// <param name="random">Random number generator to use.</param>
        public PhysicsManager(IWorld world, BspTree bspTree, BlockMap blockmap, SoundManager soundManager, EntityManager entityManager, IRandom random)
        {
            m_world = world;
            m_bspTree = bspTree;
            m_blockmap = blockmap;
            m_soundManager = soundManager;
            m_entityManager = entityManager;
            m_random = random;
            BlockmapTraverser = new BlockmapTraverser(m_blockmap);
        }

        /// <summary>
        /// Links an entity to the world.
        /// </summary>
        /// <param name="entity">The entity to link.</param>
        /// <param name="linkSpecialLines">Will check for special intersecting lines with this entity - see IntersectSpecialLines.</param>
        public void LinkToWorld(Entity entity, bool linkSpecialLines = false)
        {
            if (!entity.Flags.NoBlockmap)
                m_blockmap.Link(entity);

            LinkToSectors(entity, linkSpecialLines);

            ClampBetweenFloorAndCeiling(entity);
        }

        /// <summary>
        /// Performs all the movement logic on the entity.
        /// </summary>
        /// <param name="entity">The entity to move.</param>
        public void Move(Entity entity)
        {
            MoveXY(entity);
            MoveZ(entity);
        }

        public SectorMoveStatus MoveSectorZ(Sector sector, SectorPlane sectorPlane, SectorPlaneType moveType,
            MoveDirection direction, double speed, double destZ, CrushData? crush)
        {
            // Save the Z value because we are only checking if the dest is valid
            // If the move is invalid because of a blocking entity then it will not be set to destZ
            List<Entity> crushEntities = new List<Entity>();
            Entity? highestBlockEntity = null;
            double? highestBlockHeight = 0.0;
            SectorMoveStatus status = SectorMoveStatus.Success;
            double startZ = sectorPlane.Z;
            sectorPlane.PrevZ = startZ;
            sectorPlane.Z = destZ;
            sectorPlane.Plane.MoveZ(destZ - startZ);

            // Move lower entities first to handle stacked entities
            var entities = sector.Entities.OrderBy(x => x.Box.Bottom).ToList();

            for (int i = 0; i < entities.Count; i++)
            {
                Entity entity = entities[i];
                entity.SaveZ = entity.Position.Z;

                // At slower speeds we need to set entities to the floor
                // Otherwise the player will fall and hit the floor repeatedly creating a weird bouncing effect
                if (moveType == SectorPlaneType.Floor && direction == MoveDirection.Down && -speed < SetEntityToFloorSpeedMax &&
                    entity.OnGround && !entity.Flags.NoGravity && entity.HighestFloorSector == sector)
                {
                    entity.SetZ(entity.OnEntity?.Box.Top ?? destZ, false);
                }

                ClampBetweenFloorAndCeiling(entity);

                double thingZ = entity.OnGround ? entity.HighestFloorZ : entity.Position.Z;
                // Clipped something that wasn't directly on this entity before the move and now it will be
                // Push the entity up, and the next loop will verify it is legal
                if (entity.OverEntity == null && thingZ + entity.Height > entity.LowestCeilingZ)
                    PushUpBlockingEntity(entity);
            }

            for (int i = 0; i < entities.Count; i++)
            {
                Entity entity = entities[i];
                ClampBetweenFloorAndCeiling(entity);

                if ((moveType == SectorPlaneType.Ceiling && direction == MoveDirection.Up) || (moveType == SectorPlaneType.Floor && direction == MoveDirection.Down))
                    continue;

                double thingZ = entity.OnGround ? entity.HighestFloorZ : entity.Position.Z;

                if (thingZ + entity.Height > entity.LowestCeilingZ)
                {
                    // Corpses can be crushed but are flagged as not solid
                    // Things like bullets and blood will have both flags false so ignore them
                    if (!entity.Flags.Solid && !entity.Flags.Corpse)
                        continue;

                    if (crush != null)
                    {
                        if (crush.CrushMode == ZDoomCrushMode.Hexen)
                        {
                            highestBlockEntity = entity;
                            highestBlockHeight = entity.Height;
                        }

                        status = SectorMoveStatus.Crush;
                        crushEntities.Add(entity);
                    }
                    else
                    {
                        // Need to gib things even when not crushing and do not count as blocking
                        if (entity.Flags.Corpse && !entity.Flags.DontGib)
                        {
                            SetToGiblets(entity);
                            continue;
                        }

                        highestBlockEntity = entity;
                        highestBlockHeight = entity.Height;
                        status = SectorMoveStatus.Blocked;
                    }
                }
            }

            if (highestBlockEntity != null && highestBlockHeight.HasValue && !highestBlockEntity.IsDead)
            {
                double thingZ = highestBlockEntity.OnGround ? highestBlockEntity.HighestFloorZ : highestBlockEntity.Position.Z;
                // Set the sector Z to the difference of the blocked height
                double diff = Math.Abs(startZ - destZ) - (thingZ + highestBlockHeight.Value - highestBlockEntity.LowestCeilingZ);
                if (speed < 0)
                    diff = -diff;

                sectorPlane.Z = startZ + diff;
                sectorPlane.Plane.MoveZ(startZ - destZ + diff);

                // Entity blocked movement, reset all entities in moving sector after resetting sector Z
                foreach (var relinkEntity in entities)
                {
                    // Check for entities that may be dead from being crushed
                    if (relinkEntity.IsDisposed)
                        continue;
                    relinkEntity.UnlinkFromWorld();
                    relinkEntity.SetZ(relinkEntity.SaveZ + diff, false);
                    LinkToWorld(relinkEntity);
                }
            }

            if (crush != null && crushEntities.Count > 0)
                CrushEntities(crushEntities, sector, crush);

            return status;
        }

        private void CrushEntities(List<Entity> crushEntities, Sector sector, CrushData crush)
        {
            if (crush.Damage == 0 || (m_world.Gametick & 3) == 0)
                return;

            // Check for stacked entities, so we can crush the stack
            List<Entity> stackCrush = new List<Entity>();
            foreach (Entity checkEntity in sector.Entities)
            {
                if (checkEntity.OverEntity != null && crushEntities.Contains(checkEntity.OverEntity))
                    stackCrush.Add(checkEntity);
            }

            stackCrush = stackCrush.Union(crushEntities).Distinct().ToList();

            foreach (Entity crushEntity in stackCrush)
            {              
                if (crushEntity.IsDead)
                {
                    if (!crushEntity.Flags.DontGib)
                        SetToGiblets(crushEntity);
                }
                else if (DamageEntity(crushEntity, null, crush.Damage))
                {
                    Vec3D pos = crushEntity.Position;
                    pos.Z += crushEntity.Height / 2;
                    Entity? blood = CreateEntity(pos, crushEntity.GetBloodType());
                    if (blood != null)
                    {
                        blood.Velocity.X += m_random.NextDiff() / 16.0;
                        blood.Velocity.Y += m_random.NextDiff() / 16.0;
                    }
                }
            }
        }

        private void SetToGiblets(Entity entity)
        {
            if (!entity.SetCrushState())
            {
                m_entityManager.Destroy(entity);
                var gibsDef = m_entityManager.DefinitionComposer.GetByName("REALGIBS");
                if (gibsDef != null)
                    m_entityManager.Create(gibsDef, entity.Position, 0.0, 0.0, 0);
            }
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
        public void EntityUse(Entity entity)
        {
            Line? activateLine = null;
            bool hitBlockLine = false;
            Vec2D start = entity.Position.To2D();
            Vec2D end = start + (Vec2D.RadiansToUnit(entity.AngleRadians) * entity.Properties.Player.UseRange);
            List<BlockmapIntersect> intersections = BlockmapTraverser.GetBlockmapIntersections(new Seg2D(start, end), BlockmapTraverseFlags.Lines);

            for (int i = 0; i < intersections.Count; i++)
            {
                BlockmapIntersect bi = intersections[i];
                if (bi.Line != null)
                {
                    if (bi.Line.Segment.OnRight(start))
                    {
                        if (bi.Line.HasSpecial)
                        {
                            activateLine = bi.Line;
                            break;
                        }

                        if (bi.Line.Back == null)
                        {
                            hitBlockLine = true;
                            break;
                        }
                    }

                    if (bi.Line.Back != null)
                    {
                        LineOpening opening = GetLineOpening(bi.Line.Front.Sector, bi.Line.Back.Sector);
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
            }

            if (activateLine != null && activateLine.Special.CanActivate(entity, activateLine.Flags, ActivationContext.UseLine))
            {
                var args = new EntityActivateSpecialEventArgs(ActivationContext.UseLine, entity, activateLine);
                EntityActivatedSpecial?.Invoke(this, args);
            }
            else if (hitBlockLine && entity is Player player)
            {
                player.PlayUseFailSound();
            }
        }

        public void FireProjectile(Entity shooter, double pitch, double distance, bool autoAim, string projectClassName)
        {
            if (DateTime.Now.Subtract(m_shootTest).TotalMilliseconds < 500)
                return;

            m_shootTest = DateTime.Now;

            if (autoAim)
            {
                Vec3D start = shooter.AttackPosition;
                Vec3D end = start + Vec3D.UnitTimesValue(shooter.AngleRadians, pitch, distance);
                if (GetAutoAimAngle(shooter, start, end, out double autoAimPitch))
                    pitch = autoAimPitch;
            }

            var projectileDef = m_entityManager.DefinitionComposer.GetByName(projectClassName);
            if (projectileDef != null)
            {
                Entity projectile = m_entityManager.Create(projectileDef, shooter.AttackPosition, 0.0, shooter.AngleRadians, 0);
                Vec3D velocity = Vec3D.UnitTimesValue(shooter.AngleRadians, pitch, projectile.Definition.Properties.Speed);
                Vec3D testPos = projectile.Position + Vec3D.UnitTimesValue(shooter.AngleRadians, pitch, shooter.Radius - 2.0);
                projectile.Owner = shooter;
                projectile.Velocity = testPos - projectile.Position;

                // TryMoveXY will use the velocity of the projectile
                // Velocity is temporarily set to the test if the movement can reach testPos
                // A projectile spawned where it can't fit can cause BlockingSectorPlane or BlockingEntity (IsBlocked = true)
                if (!projectile.IsBlocked() && TryMoveXY(projectile))
                {
                    projectile.Velocity = velocity;
                }
                else
                {
                    projectile.SetPosition(testPos);
                    HandleEntityHit(projectile);
                }
            }
        }

        public void FireHitscanBullets(Entity shooter, int bulletCount, double spreadAngleRadians, double spreadPitchRadians, double pitch, double distance, bool autoAim)
        {
            if (DateTime.Now.Subtract(m_shootTest).TotalMilliseconds < 200)
                return;

            m_shootTest = DateTime.Now;

            if (autoAim)
            {
                Vec3D start = shooter.AttackPosition;
                Vec3D end = start + Vec3D.UnitTimesValue(shooter.AngleRadians, pitch, distance);
                if (GetAutoAimAngle(shooter, start, end, out double autoAimPitch))
                    pitch = autoAimPitch;
            }

            if (!shooter.Refire && bulletCount == 1)
            {
                int damage = 5 * ((m_random.NextByte() % 3) + 1);
                FireHitscan(shooter, shooter.AngleRadians, pitch, distance, damage);
            }
            else
            {
                for (int i = 0; i < bulletCount; i++)
                {
                    int damage = 5 * ((m_random.NextByte() % 3) + 1);
                    double angle = shooter.AngleRadians + (m_random.NextDiff() * spreadAngleRadians / 255);
                    double newPitch = pitch + (m_random.NextDiff() * spreadPitchRadians / 255);
                    FireHitscan(shooter, angle, newPitch, distance, damage);
                }
            }
        }

        public void FireHitscan(Entity shooter, double angle, double pitch, double distance, int damage)
        {
            Vec3D start = shooter.AttackPosition;
            Vec3D end = start + Vec3D.UnitTimesValue(angle, pitch, distance);
            Vec3D intersect = new Vec3D(0, 0, 0);

            BlockmapIntersect? bi = FireHitScan(shooter, start, end, pitch, ref intersect);

            if (bi != null)
            {
                Line? line = bi.Value.Line;
                if (line != null && line.HasSpecial && line.Special.CanActivate(shooter, line.Flags, ActivationContext.ProjectileHitLine))
                {
                    var args = new EntityActivateSpecialEventArgs(ActivationContext.ProjectileHitLine, shooter, line);
                    EntityActivatedSpecial?.Invoke(this, args);
                }

                // Only move closer on a line hit
                if (bi.Value.Entity == null && bi.Value.Sector == null)
                    MoveIntersectCloser(start, ref intersect, angle, bi.Value.Distance2D);
                DebugHitscanTest(bi.Value, intersect);

                if (bi.Value.Entity != null)
                    DamageEntity(bi.Value.Entity, shooter, damage);
            }
        }

        public BlockmapIntersect? FireHitScan(Entity shooter, Vec3D start, Vec3D end, double pitch, ref Vec3D intersect)
        {
            double floorZ, ceilingZ;
            Seg2D seg = new Seg2D(start.To2D(), end.To2D());
            List<BlockmapIntersect> intersections = BlockmapTraverser.GetBlockmapIntersections(seg,
                BlockmapTraverseFlags.Entities | BlockmapTraverseFlags.Lines,
                BlockmapTraverseEntityFlags.Shootable | BlockmapTraverseEntityFlags.Solid);

            for (int i = 0; i < intersections.Count; i++)
            {
                BlockmapIntersect bi = intersections[i];

                if (bi.Line != null)
                {
                    intersect = bi.Intersection.To3D(start.Z + (Math.Tan(pitch) * bi.Distance2D));

                    if (bi.Line.Back == null)
                    {
                        floorZ = bi.Line.Front.Sector.ToFloorZ(intersect);
                        ceilingZ = bi.Line.Front.Sector.ToCeilingZ(intersect);

                        if (intersect.Z > floorZ && intersect.Z < ceilingZ)
                            return bi;

                        if (IsSkyClipOneSided(bi.Line.Front.Sector, floorZ, ceilingZ, intersect))
                            return null;

                        GetSectorPlaneIntersection(start, end, bi.Line.Front.Sector, floorZ, ceilingZ, ref intersect);
                        bi.Sector = bi.Line.Front.Sector;
                        return bi;
                    }

                    GetOrderedSectors(bi.Line, start, out Sector front, out Sector back);
                    if (IsSkyClipTwoSided(front, back, intersect))
                        return null;

                    floorZ = front.ToFloorZ(intersect);
                    ceilingZ = front.ToCeilingZ(intersect);

                    if (intersect.Z < floorZ || intersect.Z > ceilingZ)
                    {
                        GetSectorPlaneIntersection(start, end, front, floorZ, ceilingZ, ref intersect);
                        bi.Sector = front;
                        return bi;
                    }

                    LineOpening opening = GetLineOpening(bi.Line.Front.Sector, bi.Line.Back.Sector);
                    if ((opening.FloorZ > intersect.Z && intersect.Z > floorZ) || (opening.CeilingZ < intersect.Z && intersect.Z < ceilingZ))
                        return bi;
                }
                else if (bi.Entity != null && !ReferenceEquals(shooter, bi.Entity) && bi.Entity.Box.Intersects(start, end, ref intersect))
                {
                    return bi;
                }
            }

            return null;
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

        public bool DamageEntity(Entity target, Entity? source, int damage, bool applyThrustZ = true)
        {
            if (!target.Flags.Shootable || damage == 0)
                return false;

            if (source != null)
            {
                Vec2D xyDiff = source.Position.To2D() - target.Position.To2D();
                bool zEqual = Math.Abs(target.Position.Z - source.Position.Z) <= double.Epsilon;
                bool xyEqual = Math.Abs(xyDiff.X) <= 1.0 && Math.Abs(xyDiff.Y) <= 1.0;
                double pitch = 0.0;

                double angle = source.Position.Angle(target.Position);
                double thrust = damage * source.Definition.Properties.ProjectileKickBack * 0.125 / target.Properties.Mass;

                // Silly vanilla doom feature that allows target to be thrown forward sometimes
                if (damage < 40 && damage > target.Health &&
                    target.Position.Z - source.Position.Z > 64 && (m_random.NextByte() & 1) != 0)
                {
                    angle += Math.PI;
                    thrust *= 4;
                }

                Vec3D velocity = Vec3D.Zero;

                if (applyThrustZ)
                {
                    // Player rocket jumping check, back up the source Z to get a valid pitch
                    // Only done for players, otherwise blowing up enemies will launch them in the air
                    if (zEqual && target is Player && source.Owner == target)
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
                        pitch = sourcePos.Pitch(targetPos, sourcePos.To2D().Distance(targetPos.To2D()));
                    }

                    if (!xyEqual)
                        velocity = Vec3D.Unit(angle, 0.0);

                    velocity.Z = Math.Sin(pitch);
                }
                else
                {
                    velocity = Vec3D.Unit(angle, 0.0);
                }

                velocity.Multiply(thrust);
                target.Velocity += velocity;
            }

            target.Damage(damage, m_random.NextByte() < target.Properties.PainChance);

            if (target.IsDead)
                HandleEntityDeath(target);

            return true;
        }

        private bool PushUpBlockingEntity(Entity pusher)
        {
            // Check if the pusher is blocked by an entity
            // If true then push that entity up
            if (!(pusher.LowestCeilingObject is Entity))
                return false;

            Entity entity = (Entity)pusher.LowestCeilingObject;
            entity.SetZ(pusher.Box.Top, false);

            return true;
        }

        private void HandleEntityDeath(Entity deathEntity)
        {
            if (deathEntity.OnEntity != null || deathEntity.OverEntity != null)
                HandleStackedEntityPhysics(deathEntity);
        }

        public void RadiusExplosion(Entity source, int radius)
        {
            // Barrels do not apply Z thrust - TODO better way to check?
            bool applyThrustZ = source.Definition.Name != "ExplosiveBarrel";
            Vec2D pos2D = source.Position.To2D();
            Vec2D radius2D = new Vec2D(radius, radius);
            Box2D explosionBox = new Box2D(pos2D - radius2D, pos2D + radius2D);

            List<BlockmapIntersect> intersections = BlockmapTraverser.GetBlockmapIntersections(explosionBox, BlockmapTraverseFlags.Entities,
                BlockmapTraverseEntityFlags.Shootable | BlockmapTraverseEntityFlags.Solid);
            for (int i = 0; i < intersections.Count; i++)
            {
                BlockmapIntersect bi = intersections[i];
                if (bi.Entity != null && CheckLineOfSight(bi.Entity, source))
                    ApplyExplosionDamageAndThrust(source, bi.Entity, radius, applyThrustZ);
            }
        }

        private void ApplyExplosionDamageAndThrust(Entity source, Entity entity, double radius, bool applyThrustZ)
        {
            double distance;

            if (applyThrustZ && (source.Position.Z < entity.Position.Z || source.Position.Z >= entity.Box.Top))
            {
                Vec3D sourcePos = source.Position;
                Vec3D targetPos = entity.Position;

                if (source.Position.Z > entity.Position.Z)
                    targetPos.Z += entity.Height;

                distance = Math.Max(0.0, sourcePos.Distance(targetPos) - entity.Radius);
            }
            else
            {
                distance = entity.Position.To2D().Distance(source.Position.To2D()) - entity.Radius;
            }

            int damage = (int)(radius - distance);
            if (damage <= 0)
                return;

            DamageEntity(entity, source, damage, applyThrustZ);
        }

        public bool CheckLineOfSight(Entity entity, Entity other)
        {
            Vec2D start = entity.Position.To2D();
            Vec2D end = other.Position.To2D();

            if (start == end)
                return true;

            Vec3D sightPos = new Vec3D(entity.Position.X, entity.Position.Y, entity.Position.Z + (entity.Height * 0.75));
            Seg2D seg = new Seg2D(start, end);
            double distance2D = start.Distance(end);
            double topPitch = sightPos.Pitch(other.Position.Z + other.Height, distance2D);
            double bottomPitch = sightPos.Pitch(other.Position.Z, distance2D);

            List<BlockmapIntersect> intersections = BlockmapTraverser.GetBlockmapIntersections(seg, BlockmapTraverseFlags.Lines);
            return GetBlockmapTraversalPitch(intersections, sightPos, entity, topPitch, bottomPitch, out _) != TraversalPitchStatus.Blocked;
        }

        private bool IsSkyClipOneSided(Sector sector, double floorZ, double ceilingZ, in Vec3D intersect)
        {
            if (intersect.Z > ceilingZ && TextureManager.Instance.IsSkyTexture(sector.Ceiling.TextureHandle))
                return true;
            else if (intersect.Z < floorZ && TextureManager.Instance.IsSkyTexture(sector.Floor.TextureHandle))
                return true;

            return false;
        }

        private bool IsSkyClipTwoSided(Sector front, Sector back, in Vec3D intersect)
        {
            bool isFrontCeilingSky = TextureManager.Instance.IsSkyTexture(front.Ceiling.TextureHandle);
            bool isBackCeilingSky = TextureManager.Instance.IsSkyTexture(back.Ceiling.TextureHandle);

            if (isFrontCeilingSky && isBackCeilingSky && intersect.Z > back.ToCeilingZ(intersect))
                return true;

            if (isFrontCeilingSky && intersect.Z > front.ToCeilingZ(intersect))
                return true;

            if (TextureManager.Instance.IsSkyTexture(front.Floor.TextureHandle) && intersect.Z < front.ToFloorZ(intersect))
                return true;

            return false;
        }

        public bool GetAutoAimAngle(Entity shooter, Vec3D start, Vec3D end, out double pitch)
        {
            Seg2D seg = new Seg2D(start.To2D(), end.To2D());

            List<BlockmapIntersect> intersections = BlockmapTraverser.GetBlockmapIntersections(seg,
                BlockmapTraverseFlags.Entities | BlockmapTraverseFlags.Lines,
                BlockmapTraverseEntityFlags.Shootable | BlockmapTraverseEntityFlags.Solid);

            return GetBlockmapTraversalPitch(intersections, start, shooter, MaxPitch, MinPitch, out pitch) == TraversalPitchStatus.PitchSet;
        }

        private static int CalculateSteps(Vec2D velocity, double radius)
        {
            Invariant(radius > 0.5, "Actor radius too small for safe XY physics movement");

            // We want to pick some atomic distance to keep moving our bounding
            // box. It can't be bigger than the radius because we could end up
            // skipping over a line.
            double moveDistance = radius - 0.5;
            double biggerAxis = Math.Max(Math.Abs(velocity.X), Math.Abs(velocity.Y));
            return (int)(biggerAxis / moveDistance) + 1;
        }

        private static void ApplyFriction(Entity entity)
        {
            entity.Velocity.X *= Friction;
            entity.Velocity.Y *= Friction;
        }

        private static void StopXYMovementIfSmall(Entity entity)
        {
            if (Math.Abs(entity.Velocity.X) < MinMovementThreshold)
                entity.Velocity.X = 0;
            if (Math.Abs(entity.Velocity.Y) < MinMovementThreshold)
                entity.Velocity.Y = 0;
        }

        private static bool EntityBlocksEntityZ(Entity entity, Entity other)
        {
            double maxStepHeight = entity.GetMaxStepHeight();
            return other.Box.Top - entity.Box.Bottom > maxStepHeight ||
                   entity.LowestCeilingZ - other.Box.Top < entity.Height;
        }

        private static bool PreviouslyClipped(Entity entity, Entity other)
        {
            // Can't check for entities without CanPass as the movement code allows them to potentially clip
            if (!entity.Flags.CanPass)
                return false;

            return Box3D.Overlaps(entity.PrevPosition, entity.Radius, entity.Height,
                other.PrevPosition, other.Radius, other.Height);
        }

        private static bool CanMoveOutOfEntity(Entity entity, Entity other, in Vec2D nextPosition)
        {
            Vec2D otherPos = other.Position.To2D();
            double oldDistance = entity.Position.To2D().Distance(otherPos);
            double newDistance = nextPosition.Distance(otherPos);
            return newDistance < oldDistance;
        }

        private enum TraversalPitchStatus
        {
            Blocked,
            PitchSet,
            PitchNotSet,
        }

        private TraversalPitchStatus GetBlockmapTraversalPitch(List<BlockmapIntersect> intersections, Vec3D start, Entity startEntity, double topPitch, double bottomPitch, out double pitch)
        {
            pitch = 0.0;

            for (int i = 0; i < intersections.Count; i++)
            {
                BlockmapIntersect bi = intersections[i];

                if (bi.Line != null)
                {
                    if (bi.Line.Back == null)
                        return TraversalPitchStatus.Blocked;

                    LineOpening opening = GetLineOpening(bi.Line.Front.Sector, bi.Line.Back.Sector);
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
                else if (bi.Entity != null && !ReferenceEquals(startEntity, bi.Entity))
                {
                    double thingTopPitch = start.Pitch(bi.Entity.Box.Max.Z, bi.Distance2D);
                    double thingBottomPitch = start.Pitch(bi.Entity.Box.Min.Z, bi.Distance2D);

                    if (thingBottomPitch > topPitch)
                        return TraversalPitchStatus.Blocked;
                    if (thingTopPitch < bottomPitch)
                        return TraversalPitchStatus.Blocked;

                    if (thingTopPitch < topPitch)
                        topPitch = thingTopPitch;
                    if (thingBottomPitch > bottomPitch)
                        bottomPitch = thingBottomPitch;

                    pitch = (bottomPitch + topPitch) / 2.0;
                    return TraversalPitchStatus.PitchSet;
                }
            }

            return TraversalPitchStatus.PitchNotSet;
        }

        private bool LineBlocksEntity(Entity entity, Line line)
        {
            if (line.BlocksEntity(entity))
                return true;
            if (line.Back == null)
                return false;

            LineOpening opening = GetLineOpening(line.Front.Sector, line.Back.Sector);
            return !opening.CanPassOrStepThrough(entity);
        }

        private void DebugHitscanTest(in BlockmapIntersect bi, Vec3D intersect)
        {
            string className = bi.Entity == null || bi.Entity.Definition.Flags.NoBlood ? "BulletPuff" : bi.Entity.GetBloodType();
            CreateEntity(intersect, className);
        }

        private Entity? CreateEntity(Vec3D intersect, string className)
        {
            var puff = m_entityManager.DefinitionComposer.GetByName(className);
            if (puff != null)
                return m_entityManager.Create(puff, intersect, 0.0, 0.0, 0);
            return null;
        }

        private void MoveIntersectCloser(in Vec3D start, ref Vec3D intersect, double angle, double distXY)
        {
            distXY -= 2.0;
            intersect.X = start.X + (Math.Cos(angle) * distXY);
            intersect.Y = start.Y + (Math.Sin(angle) * distXY);
        }

        private LineOpening GetLineOpening(Sector front, Sector back)
        {
            m_lineOpening.Set(front, back);
            return m_lineOpening;
        }

        private void SetEntityOnFloorOrEntity(Entity entity, double floorZ, bool smoothZ)
        {
            if (!entity.OnGround && entity is Player player)
                player.SetHitZ(IsHardHitZ(entity));

            // Additionally check to smooth camera when stepping up to an entity
            entity.SetZ(floorZ, smoothZ);

            // For now we remove any negative velocity. If upward velocity is
            // reset to zero then the jump we apply to players is lost and they
            // can never jump. Maybe we want to fix this in the future by doing
            // application of jumping after the XY movement instead of before?
            entity.Velocity.Z = Math.Max(0, entity.Velocity.Z);
        }

        private bool IsHardHitZ(Entity entity) => entity.Velocity.Z < -(Gravity * 8);

        private void ClampBetweenFloorAndCeiling(Entity entity)
        {
            // TODO fixme
            if (entity.Definition.Name == "BulletPuff")
                return;
            if (entity.NoClip && entity.Flags.NoGravity)
                return;

            object lastHighestFloorObject = entity.HighestFloorObject;
            SetEntityBoundsZ(entity);

            double lowestCeil = entity.LowestCeilingZ;
            double highestFloor = entity.HighestFloorZ;

            if (entity.Box.Top > lowestCeil)
            {
                entity.SetZ(lowestCeil - entity.Height, false);
                entity.Velocity.Z = 0;

                if (entity.LowestCeilingObject is Entity blockEntity)
                    entity.BlockingEntity = blockEntity;
                else
                    entity.BlockingSectorPlane = entity.LowestCeilingSector.Ceiling;
            }

            if (entity.Box.Bottom <= highestFloor)
            {
                if (entity.HighestFloorObject is Entity highestEntity && 
                    highestEntity.Box.Top <= entity.Box.Bottom + entity.GetMaxStepHeight())
                {
                    entity.OnEntity = highestEntity;
                }

                if (entity.OnEntity != null)
                    entity.OnEntity.OverEntity = entity;

                SetEntityOnFloorOrEntity(entity, highestFloor, lastHighestFloorObject != entity.HighestFloorObject);

                if (entity.HighestFloorObject is Entity blockEntity)
                    entity.BlockingEntity = blockEntity;
                else
                    entity.BlockingSectorPlane = entity.HighestFloorSector.Floor;
            }

            entity.OnGround = entity.Box.Bottom == highestFloor;
        }

        private void SetEntityBoundsZ(Entity entity)
        {
            Sector highestFloor = entity.Sector;
            Sector lowestCeiling = entity.Sector;
            Entity? highestFloorEntity = null;
            Entity? lowestCeilingEntity = null;
            double highestFloorZ = highestFloor.ToFloorZ(entity.Position);
            double lowestCeilZ = lowestCeiling.ToCeilingZ(entity.Position);

            entity.OnEntity = null;

            // Only check against other entities if CanPass is set (height sensitive clip detection)
            if (entity.Flags.CanPass)
            {
                foreach (Sector sector in entity.IntersectSectors)
                {
                    double floorZ = sector.ToFloorZ(entity.Position);
                    if (floorZ > highestFloorZ)
                    {
                        highestFloor = sector;
                        highestFloorZ = floorZ;
                    }

                    double ceilZ = sector.ToCeilingZ(entity.Position);
                    if (ceilZ < lowestCeilZ)
                    {
                        lowestCeiling = sector;
                        lowestCeilZ = ceilZ;
                    }
                }

                // Get intersecting entities here - They are not stored in the entity because other entities can move around after this entity has linked
                List<Entity> intersectEntities = entity.GetIntersectingEntities2D();

                for (int i = 0; i < intersectEntities.Count; i++)
                {
                    Entity intersectEntity = intersectEntities[i];
                    // Check if we are stuck inside this entity and skip because it
                    // is invalid for setting floor/ceiling.
                    if (PreviouslyClipped(entity, intersectEntity))
                        continue;

                    bool above = entity.PrevPosition.Z >= intersectEntity.Box.Top;
                    bool below = entity.PrevPosition.Z + entity.Height <= intersectEntity.Box.Bottom;
                    bool clipped = false;
                    if (above && entity.Box.Bottom < intersectEntity.Box.Top)
                        clipped = true;
                    else if (below && entity.Box.Top > intersectEntity.Box.Bottom)
                        clipped = true;

                    if (above)
                    {
                        // Need to check clipping coming from above, if we're above
                        // or clipped through then this is our floor.
                        if ((clipped || entity.Box.Bottom >= intersectEntity.Box.Top) && intersectEntity.Box.Top > highestFloorZ)
                        {
                            highestFloorEntity = intersectEntity;
                            highestFloorZ = intersectEntity.Box.Top;
                        }
                    }
                    else if (below)
                    {
                        // Same check as above but checking clipping the ceiling.
                        if ((clipped || entity.Box.Top <= intersectEntity.Box.Bottom) && intersectEntity.Box.Bottom < lowestCeilZ)
                        {
                            lowestCeilingEntity = intersectEntity;
                            lowestCeilZ = intersectEntity.Box.Bottom;
                        }
                    }

                    // Need to check if we can step up to this floor.
                    if (entity.Box.Bottom + entity.GetMaxStepHeight() >= intersectEntity.Box.Top && intersectEntity.Box.Top > highestFloorZ)
                    {
                        highestFloorEntity = intersectEntity;
                        highestFloorZ = intersectEntity.Box.Top;
                    }
                }
            }

            entity.HighestFloorZ = highestFloorZ;
            entity.LowestCeilingZ = lowestCeilZ;
            entity.HighestFloorSector = highestFloor;
            entity.LowestCeilingSector = lowestCeiling;

            if (highestFloorEntity != null && highestFloorEntity.Box.Top > highestFloor.ToFloorZ(entity.Position))
                entity.HighestFloorObject = highestFloorEntity;
            else
                entity.HighestFloorObject = highestFloor;

            if (lowestCeilingEntity != null && lowestCeilingEntity.Box.Top < lowestCeiling.ToCeilingZ(entity.Position))
                entity.LowestCeilingObject = lowestCeilingEntity;
            else
                entity.LowestCeilingObject = lowestCeiling;
        }

        private void LinkToSectors(Entity entity, bool linkSpecialLines)
        {
            Precondition(entity.SectorNodes.Empty(), "Forgot to unlink entity from blockmap");

            if (linkSpecialLines)
            {
                if (entity.IntersectSpecialLines == null)
                    entity.IntersectSpecialLines = new List<Line>();
                else
                    entity.IntersectSpecialLines.Clear();
            }

            Subsector centerSubsector = m_bspTree.ToSubsector(entity.Position);
            Sector centerSector = centerSubsector.Sector;
            HashSet<Sector> sectors = new HashSet<Sector> { centerSector };
            HashSet<Subsector> subsectors = new HashSet<Subsector> { centerSubsector };

            // TODO: Can we replace this by iterating over the blocks were already in?
            Box2D box = entity.Box.To2D();
            m_blockmap.Iterate(box, SectorOverlapFinder);

            entity.Sector = centerSector;
            entity.IntersectSectors = sectors.ToList();

            if (!entity.Flags.NoSector && !entity.NoClip)
            {
                for (int i = 0; i < entity.IntersectSectors.Count; i++)
                    entity.SectorNodes.Add(entity.IntersectSectors[i].Link(entity));
                foreach (Subsector subsector in subsectors)
                    entity.SubsectorNodes.Add(subsector.Link(entity));
            }

            GridIterationStatus SectorOverlapFinder(Block block)
            {
                // Doing iteration over enumeration for performance reasons.
                for (int i = 0; i < block.Lines.Count; i++)
                {
                    Line line = block.Lines[i];
                    if (line.Segment.Intersects(box))
                    {
                        if (entity.IntersectSpecialLines != null && !entity.NoClip)
                        {
                            if (line.HasSpecial && !FindLine(entity.IntersectSpecialLines, line.Id))
                                entity.IntersectSpecialLines.Add(line);
                        }

                        foreach (Subsector subsector in line.Subsectors)
                            subsectors.Add(subsector);

                        sectors.Add(line.Front.Sector);
                        if (line.Back != null)
                            sectors.Add(line.Back.Sector);
                    }
                }

                return GridIterationStatus.Continue;
            }
        }

        private bool FindLine(List<Line> lines, int id)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Id == id)
                    return true;
            }

            return false;
        }

        private void ClearVelocityXY(Entity entity)
        {
            entity.Velocity.X = 0;
            entity.Velocity.Y = 0;
        }

        private bool TryMoveXY(Entity entity)
        {
            Precondition(entity.Velocity.To2D() != Vec2D.Zero, "Cannot move with zero horizontal velocity");

            int slidesLeft = MaxSlides;
            Vec2D velocity = entity.Velocity.To2D();

            if (entity.NoClip)
            {
                HandleNoClip(entity, velocity);
                return true;
            }

            // TODO: Temporary until we know how this actually works.
            if (entity.IsCrushing())
                return false;

            bool success = true;
            // We advance in small steps that are smaller than the radius of
            // the actor so we don't skip over any lines or things due to fast
            // entity speed.
            int numMoves = CalculateSteps(velocity, entity.Radius);
            Vec2D stepDelta = velocity / numMoves;

            for (int movesLeft = numMoves; movesLeft > 0; movesLeft--)
            {
                if (stepDelta == Vec2D.Zero)
                    break;

                Vec2D nextPosition = entity.Position.To2D() + stepDelta;

                if (CanMoveTo(entity, nextPosition))
                {
                    MoveTo(entity, nextPosition);
                    continue;
                }

                if (entity.Flags.SlidesOnWalls && slidesLeft > 0)
                {
                    HandleSlide(entity, ref stepDelta, ref movesLeft);
                    slidesLeft--;
                    continue;
                }

                success = false;
                ClearVelocityXY(entity);
                break;
            }

            if (entity.OverEntity != null)
                HandleStackedEntityPhysics(entity);

            return success;
        }

        private void HandleStackedEntityPhysics(Entity entity)
        {
            Entity? currentOverEntity = entity.OverEntity;

            if (entity.OnEntity != null)
                ClampBetweenFloorAndCeiling(entity.OnEntity);

            while (currentOverEntity != null)
            {
                foreach (var relinkEntity in entity.Sector.Entities)
                {
                    if (relinkEntity.OnEntity == entity)
                        ClampBetweenFloorAndCeiling(relinkEntity);
                }

                entity = currentOverEntity;
                Entity? next = currentOverEntity.OverEntity;
                if (currentOverEntity.OverEntity != null && currentOverEntity.OverEntity.OnEntity != entity)
                    currentOverEntity.OverEntity = null;
                currentOverEntity = next;
            }
        }

        private void HandleEntityHit(Entity entity)
        {
            if (entity.Flags.Missile)
            {
                if (entity.BlockingEntity != null)
                {
                    int damage = entity.Properties.Damage.Get(m_random);
                    DamageEntity(entity.BlockingEntity, entity, damage);
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

                if (entity.BlockingSectorPlane != null && TextureManager.Instance.IsSkyTexture(entity.BlockingSectorPlane.TextureHandle))
                    skyClip = true;

                if (skyClip)
                    m_entityManager.Destroy(entity);
                else
                    entity.SetDeathState();

                HandleEntityDeath(entity);
            }
        }

        private void HandleNoClip(Entity entity, Vec2D velocity)
        {
            entity.UnlinkFromWorld();

            var pos = entity.Position.To2D() + velocity;
            entity.SetXY(pos);

            LinkToWorld(entity);
        }

        private bool CanMoveTo(Entity entity, Vec2D nextPosition)
        {
            Box2D nextBox = Box2D.CopyToOffset(nextPosition, entity.Radius);
            return !m_blockmap.Iterate(nextBox, CheckForBlockers);

            GridIterationStatus CheckForBlockers(Block block)
            {
                // This may need to come after if we want to do plasma bumping.
                for (int i = 0; i < block.Lines.Count; i++)
                {
                    Line line = block.Lines[i];
                    if (line.Segment.Intersects(nextBox) && LineBlocksEntity(entity, line))
                    {
                        entity.BlockingLine = line;
                        return GridIterationStatus.Stop;
                    }
                }

                if (entity.Flags.Solid || entity.Flags.Missile)
                {
                    LinkableNode<Entity>? entityNode = block.Entities.Head;
                    while (entityNode != null)
                    {
                        Entity nextEntity = entityNode.Value;

                        if (nextEntity.Box.Overlaps2D(nextBox) && entity.Box.OverlapsZ(nextEntity.Box))
                        {
                            if (entity.Flags.Pickup && EntityCanPickupItem(entity, nextEntity))
                            {
                                PerformItemPickup(entity, nextEntity);
                            }
                            else if (entity.CanBlockEntity(nextEntity) && EntityBlocksEntityZ(entity, nextEntity))
                            {
                                bool clipped = true;
                                //If we are stuck inside another entity's box then only allow movement if we try to move out the box
                                if (PreviouslyClipped(entity, nextEntity))
                                    clipped = CanMoveOutOfEntity(entity, nextEntity, nextPosition);

                                if (clipped)
                                {
                                    nextEntity.BlockingEntity = nextEntity;
                                    return GridIterationStatus.Stop;
                                }
                            }
                        }

                        entityNode = entityNode.Next;
                    }
                }

                return GridIterationStatus.Continue;
            }
        }

        private bool EntityCanPickupItem(Entity entity, Entity item)
        {
            // TODO: Eventually we need to respect how many items are in the
            //       inventory so that we don't automatically pick up all the
            //       items even if we can't anymore (ex: maxed out on ammo).
            return item.Definition.IsType(EntityDefinitionType.Inventory);
        }

        private void PerformItemPickup(Entity entity, Entity item)
        {
            entity.GivePickedUpItem(item);
            m_entityManager.Destroy(item);
        }

        private void MoveTo(Entity entity, Vec2D nextPosition)
        {
            entity.UnlinkFromWorld();

            Vec2D previousPosition = entity.Position.To2D();
            entity.SetXY(nextPosition);

            LinkToWorld(entity, true);

            if (entity.IntersectSpecialLines != null)
            {
                for (int i = 0; i < entity.IntersectSpecialLines.Count; i++)
                    CheckLineSpecialActivation(entity, entity.IntersectSpecialLines[i], previousPosition);
            }
        }

        private void CheckLineSpecialActivation(Entity entity, Line line, Vec2D previousPosition)
        {
            if (!line.Special.CanActivate(entity, line.Flags, ActivationContext.CrossLine))
                return;

            bool fromFront = line.Segment.OnRight(previousPosition);
            if (fromFront != line.Segment.OnRight(entity.Position.To2D()))
            {
                if (line.Special.IsTeleport() && !fromFront)
                    return;

                EntityActivateSpecialEventArgs args = new EntityActivateSpecialEventArgs(
                    ActivationContext.CrossLine, entity, line);
                EntityActivatedSpecial?.Invoke(this, args);
            }
        }

        private void HandleSlide(Entity entity, ref Vec2D stepDelta, ref int movesLeft)
        {
            if (FindClosestBlockingLine(entity, stepDelta, out MoveInfo moveInfo))
            {
                if (MoveCloseToBlockingLine(entity, stepDelta, moveInfo, out Vec2D residualStep))
                {
                    ReorientToSlideAlong(entity, moveInfo.BlockingLine!, residualStep, ref stepDelta, ref movesLeft);
                    return;
                }
            }

            if (AttemptAxisMove(entity, stepDelta, Axis2D.Y))
                return;
            if (AttemptAxisMove(entity, stepDelta, Axis2D.X))
                return;

            // If we cannot find the line or thing that is blocking us, then we
            // are fully done moving horizontally.
            ClearVelocityXY(entity);
            stepDelta.X = 0;
            stepDelta.Y = 0;
            movesLeft = 0;
        }

        private BoxCornerTracers CalculateCornerTracers(Box2D currentBox, Vec2D stepDelta)
        {
            Vec2D[] corners;

            if (stepDelta.X >= 0)
            {
                corners = stepDelta.Y >= 0 ?
                    new[] { currentBox.TopLeft, currentBox.TopRight, currentBox.BottomRight } :
                    new[] { currentBox.TopRight, currentBox.BottomRight, currentBox.BottomLeft };
            }
            else
            {
                corners = stepDelta.Y >= 0 ?
                    new[] { currentBox.TopRight, currentBox.TopLeft, currentBox.BottomLeft } :
                    new[] { currentBox.TopLeft, currentBox.BottomLeft, currentBox.BottomRight };
            }

            Seg2DBase first = new Seg2DBase(corners[0], corners[0] + stepDelta);
            Seg2DBase second = new Seg2DBase(corners[1], corners[1] + stepDelta);
            Seg2DBase third = new Seg2DBase(corners[2], corners[2] + stepDelta);
            return new BoxCornerTracers(first, second, third);
        }

        private void CheckCornerTracerIntersection(Seg2DBase cornerTracer, Entity entity, ref MoveInfo moveInfo)
        {
            bool hit = false;
            double hitTime = double.MaxValue;
            Line? blockingLine = null;

            m_blockmap.Iterate(cornerTracer, CheckForTracerHit);

            if (hit && hitTime < moveInfo.LineIntersectionTime)
                moveInfo = MoveInfo.From(blockingLine!, hitTime);

            GridIterationStatus CheckForTracerHit(Block block)
            {
                for (int i = 0; i < block.Lines.Count; i++)
                {
                    Line line = block.Lines[i];

                    if (cornerTracer.Intersection(line.Segment, out double time) &&
                        LineBlocksEntity(entity, line) &&
                        time < hitTime)
                    {
                        hit = true;
                        hitTime = time;
                        blockingLine = line;
                    }
                }

                return GridIterationStatus.Continue;
            }
        }

        private bool FindClosestBlockingLine(Entity entity, Vec2D stepDelta, out MoveInfo moveInfo)
        {
            moveInfo = MoveInfo.Empty();

            // We shoot out 3 tracers from the corners in the direction we're
            // travelling to see if there's a blocking line as follows:
            //    _  _
            //    /| /|   If we're travelling northeast, then from the
            //   /  /_    top right corners of the bounding box we will
            //  o--o /|   shoot out tracers in the direction we are going
            //  |  |/     to step to see if we hit anything
            //  o--o
            //
            // This obviously can miss things, but this is how vanilla does it
            // and we want to have compatibility with the mods that use.
            Box2D currentBox = entity.Box.To2D();
            BoxCornerTracers tracers = CalculateCornerTracers(currentBox, stepDelta);
            CheckCornerTracerIntersection(tracers.First, entity, ref moveInfo);
            CheckCornerTracerIntersection(tracers.Second, entity, ref moveInfo);
            CheckCornerTracerIntersection(tracers.Third, entity, ref moveInfo);

            return moveInfo.IntersectionFound;
        }

        private bool MoveCloseToBlockingLine(Entity entity, Vec2D stepDelta, MoveInfo moveInfo, out Vec2D residualStep)
        {
            Precondition(moveInfo.LineIntersectionTime >= 0, "Blocking line intersection time should never be negative");
            Precondition(moveInfo.IntersectionFound, "Should not be moving close to a line if we didn't hit one");

            // If it's close enough that stepping back would move us further
            // back than we currently are (or move us nowhere), we don't need
            // to do anything. This also means the residual step is equal to
            // the entire step since we're not stepping anywhere.
            if (moveInfo.LineIntersectionTime <= SlideStepBackTime)
            {
                residualStep = stepDelta;
                return true;
            }

            double t = moveInfo.LineIntersectionTime - SlideStepBackTime;
            Vec2D usedStepDelta = stepDelta * t;
            residualStep = stepDelta - usedStepDelta;

            Vec2D closeToLinePosition = entity.Position.To2D() + usedStepDelta;
            if (CanMoveTo(entity, closeToLinePosition))
            {
                MoveTo(entity, closeToLinePosition);
                return true;
            }

            return false;
        }

        private void ReorientToSlideAlong(Entity entity, Line blockingLine, Vec2D residualStep, ref Vec2D stepDelta,
            ref int movesLeft)
        {
            // Our slide direction depends on if we're going along with the
            // line or against the line. If the dot product is negative, it
            // means we are facing away from the line and should slide in
            // the opposite direction from the way the line is pointing.
            Vec2D unitDirection = blockingLine.Segment.Delta.Unit();
            if (stepDelta.Dot(unitDirection) < 0)
                unitDirection = -unitDirection;

            // Because we moved up to the wall, it's almost always the case
            // that we didn't make 100% of a step. For example if we have some
            // movement of 5 map units towards a wall and run into the wall at
            // 3 (leaving 2 map units unhandled), we want to work that residual
            // map unit movement into the existing step length. The following
            // does that by finding the total movement scalar and applying it
            // to the direction we need to slide.
            //
            // We also must take into account that we're adding some scalar to
            // another scalar, which means we'll end up with usually a larger
            // one. This means our step delta could grow beyond the size of the
            // radius of the entity and cause it to skip lines in pathological
            // situations. I haven't encountered such a case yet but it is at
            // least theoretically possible this can happen. Because of this,
            // the movesLeft is incremented by 1 to make sure the stepDelta
            // at the end of this function stays smaller than the radius.
            // TODO: If we have the unit vector, is projection overkill? Can we
            //       just multiply by the component instead?
            Vec2D stepProjection = stepDelta.Projection(unitDirection);
            Vec2D residualProjection = residualStep.Projection(unitDirection);

            // TODO: This is almost surely not how it's done, but it feels okay
            //       enough right now to leave as is.
            entity.Velocity.X = stepProjection.X * Friction;
            entity.Velocity.Y = stepProjection.Y * Friction;

            double totalRemainingDistance = ((stepProjection * movesLeft) + residualProjection).Length();
            movesLeft += 1;
            stepDelta = unitDirection * totalRemainingDistance / movesLeft;
        }

        private bool AttemptAxisMove(Entity entity, Vec2D stepDelta, Axis2D axis)
        {
            if (axis == Axis2D.X)
            {
                Vec2D nextPosition = entity.Position.To2D() + new Vec2D(stepDelta.X, 0);
                if (CanMoveTo(entity, nextPosition))
                {
                    MoveTo(entity, nextPosition);
                    entity.Velocity.Y = 0;
                    stepDelta.Y = 0;
                    return true;
                }
            }
            else
            {
                Vec2D nextPosition = entity.Position.To2D() + new Vec2D(0, stepDelta.Y);
                if (CanMoveTo(entity, nextPosition))
                {
                    MoveTo(entity, nextPosition);
                    entity.Velocity.X = 0;
                    stepDelta.X = 0;
                    return true;
                }
            }

            return false;
        }

        private void MoveXY(Entity entity)
        {
            if (entity.Velocity.To2D() == Vec2D.Zero)
                return;

            if (!TryMoveXY(entity))
                HandleEntityHit(entity);
            if (entity.ShouldApplyFriction())
                ApplyFriction(entity);
            StopXYMovementIfSmall(entity);
        }

        private void MoveZ(Entity entity)
        {
            if (entity.Flags.NoGravity && entity.ShouldApplyFriction())
                entity.Velocity.Z *= Friction;
            if (entity.ShouldApplyGravity())
                entity.Velocity.Z -= Gravity;

            if (entity.Velocity.Z == 0)
                return;

            entity.SetZ(entity.Position.Z + entity.Velocity.Z, false);
            ClampBetweenFloorAndCeiling(entity);

            if (entity.IsBlocked())
                HandleEntityHit(entity);

            if (entity.OverEntity != null)
                HandleStackedEntityPhysics(entity);
        }
    }
}