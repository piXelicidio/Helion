﻿using System.Collections.Generic;
using System.Linq;
using Helion.Maps.Geometry.Lines;
using Helion.Maps.Special;
using Helion.Util.Container.Linkable;
using Helion.World.Entities;
using static Helion.Util.Assertion.Assert;

namespace Helion.Maps.Geometry
{
    public class Sector
    {
        public readonly int Id;
        public readonly List<Line> Lines = new List<Line>();
        public readonly List<SectorFlat> Flats = new List<SectorFlat>();
        public readonly LinkableList<Entity> Entities = new LinkableList<Entity>();
        public short LightLevel;
        public int Tag;

        public ISpecial? ActiveMoveSpecial;
        public bool IsMoving => ActiveMoveSpecial != null;

        public SectorFlat Floor => Flats[0];
        public SectorFlat Ceiling => Flats[1];
        public float UnitLightLevel => LightLevel / 255.0f;
        public ZSectorSpecialType SectorSpecialType;

        public Sector(int id, short lightLevel, SectorFlat floor, SectorFlat ceiling, ZSectorSpecialType special, int tag)
        {
            Precondition(floor.Z <= ceiling.Z, "Sector floor is above the ceiling");

            Id = id;
            LightLevel = lightLevel;
            SectorSpecialType = special;
            Tag = tag;
            
            Flats.Add(floor);
            Flats.Add(ceiling);
            Flats.ForEach(flat => flat.SetSector(this));
        }

        public void AddLine(Line line)
        {
            if (!Lines.Contains(line))
                Lines.Add(line);
        }

        public LinkableNode<Entity> Link(Entity entity)
        {
            // TODO: Precondition to assert the entity is in only once.
            
            return Entities.Add(entity);            
        }

        public void SetLightLevel(short lightLevel)
        {
            LightLevel = lightLevel;
            Floor.LightLevel = lightLevel;
            Ceiling.LightLevel = lightLevel;
        }

        public override bool Equals(object obj) => obj is Sector sector && Id == sector.Id;

        public override int GetHashCode() => Id.GetHashCode();
    }
}