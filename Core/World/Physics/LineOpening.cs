using System;
using Helion.Util.Assertion;
using Helion.World.Entities;
using Helion.World.Geometry.Lines;
using Helion.World.Geometry.Sectors;

namespace Helion.World.Physics;

/// <summary>
/// Represents the opening space for a line segment.
/// </summary>
public class LineOpening
{
    /// <summary>
    /// The top of the opening. This is not guaranteed to be above the
    /// ceiling value.
    /// </summary>
    public double CeilingZ;

    /// <summary>
    /// The bottom of the opening. This is not guaranteed to be below the
    /// ceiling value.
    /// </summary>
    public double FloorZ;

    /// <summary>
    /// How tall the opening is along the Z axis.
    /// </summary>
    public double OpeningHeight;

    public double DropOffZ;

    public static bool IsOpen(Line line)
    {
        Assert.Precondition(line.Back != null, "Cannot create LineOpening with one sided line");

        Sector front = line.Front.Sector;
        Sector back = line.Back!.Sector;

        return Math.Min(front.Ceiling.Z, back.Ceiling.Z) - Math.Max(front.Floor.Z, back.Floor.Z) > 0;
    }

    public static double GetOpeningHeight(Line line)
    {
        Assert.Precondition(line.Back != null, "Cannot create LineOpening with one sided line");

        return Math.Min(line.Front.Sector.Ceiling.Z, line.Back!.Sector.Ceiling.Z) -
            Math.Max(line.Front.Sector.Floor.Z, line.Back!.Sector.Floor.Z);
    }

    /// <summary>
    ///  This function is required for rendering because of an oversight in the 'closed door check' in the original game.
    ///  Rendering tricks work based off of this oversight and needs to be used in rendering checks in place of GetOpeningHeight.
    /// </summary>
    /// <param name="line">The line to check against</param>
    /// <returns>True if the rendering is blocked.</returns>
    public static bool IsRenderingBlocked(Line line)
    {
        Assert.Precondition(line.Back != null, "Cannot create LineOpening with one sided line");

        // TODO This can be smarter. This is just to allow rendering tricks for invisible platforms.
        if (line.Front.Sector.TransferHeights != null || line.Back!.Sector.TransferHeights != null)
            return false;

        // Closed door check. This check isn't really correct, but is required for some old rendering tricks to work.
        // E.g. TNT Map02 - see through window that opens as a door
        if (line.Back!.Sector.Ceiling.Z <= line.Front.Sector.Floor.Z || line.Back.Sector.Floor.Z >=  line.Front.Sector.Ceiling.Z)
            return true;

        return false;
    }

    public LineOpening()
    {
        CeilingZ = 0;
        FloorZ = 0;
        DropOffZ = 0;
        OpeningHeight = 0;
    }

    public void Set(Line line)
    {
        Assert.Precondition(line.Back != null, "Cannot create LineOpening with one sided line");

        Sector front = line.Front.Sector;
        Sector back = line.Back!.Sector;
        CeilingZ = Math.Min(front.Ceiling.Z, back.Ceiling.Z);
        FloorZ = Math.Max(front.Floor.Z, back.Floor.Z);
        OpeningHeight = CeilingZ - FloorZ;
    }

    public void SetTop(TryMoveData tryMove, Entity other, bool missileClipCompat)
    {
        CeilingZ = tryMove.LowestCeilingZ;
        double otherHeight = missileClipCompat ? other.GetMissileClipHeight(missileClipCompat) : other.Height;
        FloorZ = other.Position.Z + otherHeight;
        OpeningHeight = CeilingZ - FloorZ;
        DropOffZ = FloorZ;
    }

    public void SetBottom(TryMoveData tryMove, Entity other)
    {
        CeilingZ = other.Position.Z;
        FloorZ = tryMove.HighestFloorZ;
        OpeningHeight = CeilingZ - FloorZ;
        DropOffZ = FloorZ;
    }

    public bool CanStepUpInto(Entity entity)
    {
        return entity.Position.Z < FloorZ && entity.Position.Z >= FloorZ - entity.GetMaxStepHeight();
    }

    public bool Fits(Entity entity) => entity.Height <= OpeningHeight;

    public bool CanPassOrStepThrough(Entity entity)
    {
        if (!Fits(entity) || entity.TopZ > CeilingZ)
            return false;

        if (entity.Position.Z < FloorZ)
            return CanStepUpInto(entity);

        return true;
    }
}
