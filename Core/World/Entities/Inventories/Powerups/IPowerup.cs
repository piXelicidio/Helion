using Helion.Models;
using Helion.World.Entities.Definition;
using Helion.World.Entities.Players;
using System.Drawing;

namespace Helion.World.Entities.Inventories.Powerups;

public interface IPowerup
{
    EntityDefinition EntityDefinition { get; }
    PowerupType PowerupType { get; }
    Color? DrawColor { get; }
    float DrawAlpha { get; }
    bool DrawPowerupEffect { get; }
    bool DrawEffectActive { get; }
    PowerupEffectType EffectType { get; }
    InventoryTickStatus Tick(Player player);
    void Reset();
    PowerupModel ToPowerupModel();
}
