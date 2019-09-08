using Helion.Maps.Components;
using Helion.Maps.Shared;
using Helion.Maps.Specials.Vanilla;

namespace Helion.Maps.Doom.Components
{
    public class DoomLine : ILine
    {
        public int Id { get; }
        public MapLineFlags Flags { get; }
        public readonly DoomVertex Start;
        public readonly DoomVertex End;
        public readonly DoomSide Front;
        public readonly DoomSide? Back;
        public readonly VanillaLineSpecialType LineType;
        public readonly ushort SectorTag;
        
        internal DoomLine(int id, DoomVertex start, DoomVertex end, DoomSide front, DoomSide? back, 
            MapLineFlags flags, VanillaLineSpecialType lineType, ushort sectorTag)
        {
            Id = id;
            Start = start;
            End = end;
            Front = front;
            Back = back;
            Flags = flags;
            LineType = lineType;
            SectorTag = sectorTag;
        }

        public IVertex GetStart() => Start;
        public IVertex GetEnd() => End;
        public ISide GetFront() => Front;
        public ISide? GetBack() => Back;
    }
}