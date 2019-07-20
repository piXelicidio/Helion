using System;
using System.Collections.Generic;
using Helion.Maps.Geometry;
using Helion.Resources.Definitions.Decorate;
using Helion.Util.Container.Linkable;
using Helion.Util.Geometry;

namespace Helion.World.Entities
{
    /// <summary>
    /// An actor in a world.
    /// </summary>
    public class Entity : IDisposable
    {
        /// <summary>
        /// The definition that makes up this entity.
        /// </summary>
        public readonly ActorDefinition Definition;

        /// <summary>
        /// The bounding box of the entity.
        /// </summary>
        public EntityBox Box;

        /// <summary>
        /// The last position of this entity, used for interpolation.
        /// </summary>
        public Vec3D PrevPosition;
        
        /// <summary>
        /// The center bottom position (the center of the body at the feet).
        /// </summary>
        public Vec3D Position => Box.Position;
        
        /// <summary>
        /// The angle in radians the entity is facing.
        /// </summary>
        public double Angle { get; internal set; }
        
        /// <summary>
        /// The movement of the entity.
        /// </summary>
        public Vec3D Velocity = Vec3D.Zero;
        
        /// <summary>
        /// A cached value to tell whether we are on the ground or not.
        /// </summary>
        public bool OnGround { get; internal set; }

        /// <summary>
        /// The sector that is at the center of the entity.
        /// </summary>
        public Sector Sector;
        
        /// <summary>
        /// The sector that is at the center of the entity.
        /// </summary>
        public Sector LowestCeilingSector;
        
        /// <summary>
        /// The sector that is at the center of the entity.
        /// </summary>
        public Sector HighestFloorSector;

        /// <summary>
        /// The node in the linked list of entities.
        /// </summary>
        internal LinkableNode<Entity> EntityListNode;

        /// <summary>
        /// All the linked list nodes for blocks that this entity belongs to.
        /// </summary>
        internal List<LinkableNode<Entity>> BlockmapNodes = new List<LinkableNode<Entity>>();

        /// <summary>
        /// All the linked list nodes for sectors that this entity belongs to.
        /// </summary>
        internal List<LinkableNode<Entity>> SectorNodes = new List<LinkableNode<Entity>>();

        /// <summary>
        /// A (shorter to type) reference to the definition's height value.
        /// </summary>
        public double Height => Definition.Properties.Height;
        
        /// <summary>
        /// Creates an entity with the following information.
        /// </summary>
        /// <param name="definition">The definitions for the entity.</param>
        /// <param name="position">The location in the world.</param>
        /// <param name="angleRadians">The angle in radians.</param>
        /// <param name="sector">The sector that the center of the entity is on.
        /// </param>
        public Entity(ActorDefinition definition, Vec3D position, double angleRadians, Sector sector)
        {
            Definition = definition;
            Angle = angleRadians;
            Box = new EntityBox(position, definition.Properties.Radius, definition.Properties.Height);
            PrevPosition = Box.Position;
            Sector = sector;
            LowestCeilingSector = sector;
            HighestFloorSector = sector;
            OnGround = CheckIfOnGround();

            // TODO: Link to sector
        }
        
        /// <summary>
        /// Sets the bottom of the entity's center to be at the Z coordinate
        /// provided.
        /// </summary>
        /// <param name="z">The Z coordinate.</param>
        public void SetZ(double z)
        {
            Box.SetZ(z);
        }

        /// <summary>
        /// Sets the entity to the new X/Y coordinates.
        /// </summary>
        /// <param name="position">The new position.</param>
        public void SetXY(Vec2D position)
        {
            Box.SetXY(position);
        }

        /// <summary>
        /// Resets any interpolation tracking variables.
        /// </summary>
        /// <remarks>
        /// Intended to be used when we have some kind of movement which we do
        /// not want any interpolation with the previous spot being done in the
        /// renderer. An example of this would be going through a teleporter.
        /// </remarks>
        public void ResetInterpolation()
        {
            PrevPosition = Position;
        }

        /// <summary>
        /// Unlinks this entity from the world it is in, but not the entity
        /// list it belongs to.
        /// </summary>
        /// <remarks>
        /// When moving from position to position, we want to unlink from
        /// everything except the entity list (which should be unlinked from
        /// when the entity is fully removed from the world).
        /// </remarks>
        public void UnlinkFromWorld()
        {
            // We need max speed for stuff like this because of how frequently
            // it is called. As of 2017, there is a 61% performance loss by
            // doing foreach instead of for. We can do foreach one day when the
            // compiler does this for us. This may need revisiting since the
            // JIT probably is pretty smart now though.
            //
            // Also maybe doing Clear() is overkill, but then again not doing
            // clearing may cause garbage collection to not be run for any
            // lingering elements in the list.
            for (int i = 0; i < SectorNodes.Count; i++)
                SectorNodes[i].Unlink();
            SectorNodes.Clear();
            
            for (int i = 0; i < BlockmapNodes.Count; i++)
                BlockmapNodes[i].Unlink();
            BlockmapNodes.Clear();
        }

        /// <summary>
        /// Runs any tickable logic on the entity.
        /// </summary>
        public void Tick()
        {
            PrevPosition = Box.Position;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            UnlinkFromWorld();
            EntityListNode.Unlink();
        }

        private bool CheckIfOnGround() => HighestFloorSector.Floor.Plane.ToZ(Position) >= Position.Z;
    }
}