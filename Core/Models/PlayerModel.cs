﻿using Helion.Util.Geometry.Vectors;
using Newtonsoft.Json;

namespace Helion.Models
{
    public class PlayerModel : EntityModel
    {
        public int Number { get; set; }
        public double PitchRadians { get; set; }
        public int DamageCount { get; set; }
        public int BonusCount { get; set; }
        public int ExtraLight { get; set; }
        public bool IsJumping { get; set; }
        public int JumpTics { get; set; }
        public int DeathTics { get; set; }
        public double ViewHeight { get; set; }
        public double ViewZ { get; set; }
        public double DeltaViewHeight { get; set; }
        public double Bob { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? Killer { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? Attacker { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Weapon { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? PendingWeapon { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? AnimationWeapon { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Vec2D WeaponOffset { get; set; }
        public int WeaponSlot { get; set; }
        public int WeaponSubSlot { get; set; }
        public InventoryModel Inventory { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public FrameStateModel? AnimationWeaponFrame { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public FrameStateModel? WeaponFlashFrame { get; set; }
    }
}
