using System.Collections.Generic;
using System.Linq;
using Helion.Audio;
using Helion.Models;
using Helion.Util;
using Helion.World.Geometry.Lines;
using Helion.World.Geometry.Sectors;
using Helion.World.Special.SectorMovement;

namespace Helion.World.Special.Specials;

public class StairSpecial : SectorMoveSpecial
{
    private readonly int m_stairDelay;
    private readonly double m_startZ;
    private readonly List<StairMove> m_stairs = new();
    private readonly bool m_crush;
    private int m_destroyCount;
    private int m_stairDelayTics;
    private int m_resetTics;
    private bool m_init;
    private bool m_buggedStairs;

    private class StairMove
    {
        public StairMove(Sector sector, int height)
        {
            Sector = sector;
            Height = height;
        }

        public Sector Sector { get; private set; }
        public int Height { get; private set; }
    }

    public IEnumerable<Sector> GetBuildSectors() => m_stairs.Select(x => x.Sector);

    public override bool MultiSector => true;

    public override IEnumerable<(Sector, SectorPlane)> GetSectors()
    {
        foreach (var stair in m_stairs)
        {
            if (stair.Sector.IsMoving)
                continue;

            yield return (stair.Sector, stair.Sector.GetSectorPlane(MoveData.SectorMoveType));
        }
    }

    public StairSpecial(IWorld world, Sector sector, double speed, int height, int delay, bool crush) :
        this(world, sector, speed, height, delay, crush, MoveDirection.Up, -1, false)
    {
    }

    public StairSpecial(IWorld world, Sector sector, double speed, int height, int delay, bool crush, MoveDirection direction,
        int resetTicks, bool ignoreTexture) :
        base(world, sector, 0, 0, new SectorMoveData(SectorPlaneFace.Floor, direction, MoveRepetition.None, speed, 0),
            new SectorSoundData(null, null, Constants.PlatStopSound))
    {
        m_buggedStairs = world.Config.Compatibility.Stairs;
        m_init = true;
        m_stairDelay = delay;
        m_resetTics = resetTicks == 0 ? -1 : resetTicks;
        m_startZ = Sector.Floor.Z;
        m_crush = crush;

        if (direction == MoveDirection.Down)
            height = -height;

        Sector.ActiveFloorMove = null;
        StairBuild(Sector, Sector.Floor.TextureHandle, height, ignoreTexture);
    }

    private void StairBuild(Sector sector, int floorpic, int stairHeight, bool ignoreTexture)
    {
        if (sector.IsMoving)
            return;

        int height = stairHeight;
        sector.ActiveFloorMove = this;
        m_stairs.Add(new StairMove(sector, height));

        bool keepBuilding = false;
        do
        {
            keepBuilding = false;
            for (int i = 0; i < sector.Lines.Count; i++)
            {
                Line line = sector.Lines[i];
                if (line.Back == null)
                    continue;

                if (line.Front.Sector.Id != sector.Id)
                    continue;

                if (!ignoreTexture && line.Back.Sector.Floor.TextureHandle != floorpic)
                    continue;

                // The original game had this bug where it would increment height before checking if th sector was already in motion
                if (m_buggedStairs)
                    height += stairHeight;

                if (line.Back.Sector.IsMoving)
                    continue;

                // Correctly add height after is moving check
                if (!m_buggedStairs)
                    height += stairHeight;

                sector = line.Back.Sector;
                sector.ActiveFloorMove = this;
                m_stairs.Add(new StairMove(sector, height));
                keepBuilding = true;
                break;                
            }
        } while (keepBuilding);
    }

    public StairSpecial(IWorld world, Sector sector, StairSpecialModel model) :
        base(world, sector, model.MoveSpecial)
    {
        m_stairDelay = model.Delay;
        m_startZ = model.StartZ;
        m_destroyCount = model.Destroy;
        m_stairDelayTics = model.DelayTics;
        m_resetTics = model.ResetTics;
        m_crush = model.Crush;

        for (int i = 0; i < model.SectorIds.Count && i < model.Heights.Count; i++)
        {
            if (!world.IsSectorIdValid(model.SectorIds[i]))
                continue;

            m_stairs.Add(new StairMove(world.Sectors[model.SectorIds[i]], model.Heights[i]));

            if (i >= m_destroyCount)
            {
                m_stairs[i].Sector.ActiveFloorMove = this;
                CreateMovementSound(m_stairs[i].Sector);
            }
        }
    }

    public override ISpecialModel ToSpecialModel()
    {
        StairSpecialModel model = new StairSpecialModel()
        {
            Delay = m_stairDelay,
            StartZ = m_startZ,
            Destroy = m_destroyCount,
            DelayTics = m_stairDelayTics,
            ResetTics = m_resetTics,
            Crush = m_crush,
            MoveSpecial = (SectorMoveSpecialModel)base.ToSpecialModel()
        };

        List<int> sectors = new(m_stairs.Count);
        List<int> heights = new(m_stairs.Count);

        for (int i = 0; i < m_stairs.Count; i++)
        {
            sectors.Add(m_stairs[i].Sector.Id);
            heights.Add(m_stairs[i].Height);
        }

        model.SectorIds = sectors;
        model.Heights = heights;

        return model;
    }

    public override SpecialTickStatus Tick()
    {
        bool setInitialMove = false;
        if (m_init)
        {
            setInitialMove = true;
            m_init = false;
            InitStairMovement();
        }

        if (m_resetTics > 0)
        {
            m_resetTics--;
            if (m_resetTics == 0)
            {
                m_destroyCount = 0;
                FlipMovementDirection(false);
                for (int i = 0; i < m_stairs.Count; i++)
                {
                    m_stairs[i].Sector.ActiveFloorMove = this;
                    CreateMovementSound(m_stairs[i].Sector);
                }
            }
        }

        if (m_stairDelayTics > 0)
        {
            for (int i = m_destroyCount - 1; i < m_stairs.Count; i++)
            {
                if (!ReferenceEquals(m_stairs[i].Sector.ActiveFloorMove, this))
                    continue;

                m_stairs[i].Sector.Floor.PrevZ = m_stairs[i].Sector.Floor.Z;
            }

            m_stairDelayTics--;
            return SpecialTickStatus.Continue;
        }

        SpecialTickStatus currentStatus = SpecialTickStatus.Continue;
        double height = 0;

        for (int i = 0; i < m_stairs.Count; i++)
        {
            IsInitialMove = setInitialMove;
            height = m_stairs[i].Height;
            Sector = m_stairs[i].Sector;
            SectorPlane = Sector.Floor;
            if (m_resetTics == 0)
                DestZ = m_startZ;
            else
                DestZ = m_startZ + height;
            if (OwnsPlane(Sector))
                currentStatus = base.Tick();

            if (currentStatus == SpecialTickStatus.Destroy)
            {
                SectorPlane.PrevZ = SectorPlane.Z;
                m_destroyCount++;
                m_stairDelayTics = m_stairDelay;
            }

            if (m_destroyCount == m_stairs.Count && m_resetTics <= 0)
            {
                ClearMovementLock();
                return SpecialTickStatus.Destroy;
            }
        }

        return SpecialTickStatus.Continue;
    }

    private void InitStairMovement()
    {
        foreach (var stairMove in m_stairs)
        {
            if (stairMove.Sector.ActiveFloorMove == null || OwnsPlane(stairMove.Sector))
            {
                CreateMovementSound(stairMove.Sector);

                if (m_resetTics > 0)
                    stairMove.Sector.DataChanges |= SectorDataTypes.MovementLocked;
            }
        }
    }

    private void ClearMovementLock()
    {
        for (int i = 0; i < m_stairs.Count; i++)
            m_stairs[i].Sector.DataChanges &= ~SectorDataTypes.MovementLocked;
    }

    public override void FinalizeDestroy()
    {
        for (int i = 0; i < m_stairs.Count; i++)
        {
            Sector sector = m_stairs[i].Sector;
            // Other specials can interact with a sector before this entire special is complete.
            // Only reset interpolation if this stair special is the active floor move.
            if (!OwnsPlane(sector))
                continue;

            sector.Floor.PrevZ = sector.Floor.Z;
        }
    }

    private void CreateMovementSound(Sector sector) =>
        m_world.SoundManager.CreateSoundOn(sector.Floor, Constants.PlatMoveSound, new SoundParams(sector.Floor, true));

    private bool OwnsPlane(Sector sector)
    {
        if (MoveData.SectorMoveType == SectorPlaneFace.Floor)
            return ReferenceEquals(sector.ActiveFloorMove, this);

        return ReferenceEquals(sector.ActiveCeilingMove, this);
    }
}
