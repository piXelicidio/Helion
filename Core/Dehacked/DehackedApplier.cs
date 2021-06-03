﻿using Helion.Resources.Definitions;
using Helion.Resources.Definitions.Language;
using Helion.World.Entities.Definition;
using Helion.World.Entities.Definition.Composer;
using System;
using System.Text.RegularExpressions;
using static Helion.Dehacked.DehackedDefinition;

namespace Helion.Dehacked
{
    public static class DehackedApplier
    {
        public static void Apply(DehackedDefinition dehacked, DefinitionEntries definitionEntries, EntityDefinitionComposer composer)
        {
            ApplyThings(dehacked, composer);
            ApplyText(dehacked, definitionEntries.Language);
        }

        private static void ApplyThings(DehackedDefinition dehacked, EntityDefinitionComposer composer)
        {
            foreach (var thing in dehacked.Things)
            {
                int index = thing.Number - 1;
                if (index < 0 || index >= ActorNames.Length)
                {
                    // Log.Error
                    continue;
                }

                string actorName = ActorNames[index];
                var definition = composer.GetByName(actorName);
                if (definition == null)
                    continue;

                var properties = definition.Properties;
                if (thing.Hitpoints.HasValue)
                    properties.Health = thing.Hitpoints.Value;
                if (thing.ReactionTime.HasValue)
                    properties.ReactionTime = thing.ReactionTime.Value;
                if (thing.PainChance.HasValue)
                    properties.PainChance = thing.PainChance.Value;
                if (thing.Speed.HasValue)
                    properties.Speed = thing.Speed.Value;
                if (thing.Width.HasValue)
                    properties.Radius = GetDouble(thing.Width.Value);
                if (thing.Height.HasValue)
                    properties.Height = GetDouble(thing.Height.Value);
                if (thing.Mass.HasValue)
                    properties.Mass = thing.Mass.Value;
                if (thing.MisileDamage.HasValue)
                    properties.Damage.Value = thing.MisileDamage.Value;
                if (thing.Bits.HasValue)
                    SetActorFlags(definition, thing.Bits.Value);

                if (thing.AlertSound.HasValue)
                    properties.SeeSound = GetSound(thing.AlertSound.Value);
                if (thing.AttackSound.HasValue)
                    properties.AttackSound = GetSound(thing.AttackSound.Value);
                if (thing.PainSound.HasValue)
                    properties.PainSound = GetSound(thing.PainSound.Value);
                if (thing.DeathSound.HasValue)
                    properties.DeathSound = GetSound(thing.DeathSound.Value);
            }
        }

        private static void ApplyText(DehackedDefinition dehacked, LanguageDefinition language)
        {
            string levelRegex = @"level \d+: ";
            foreach (var text in dehacked.Strings)
            {
                var match = Regex.Match(text.OldString, levelRegex);
                if (match.Success)
                    text.OldString = text.OldString.Replace(match.Value, string.Empty);

                match = Regex.Match(text.NewString, levelRegex);
                if (match.Success)
                    text.NewString = text.NewString.Replace(match.Value, string.Empty);

                if (language.GetKeyByValue(text.OldString, out string? key) && key != null)
                    language.SetValue(key, text.NewString);
            }
        }

        private static void SetActorFlags(EntityDefinition def, uint value)
        {
            ThingProperties thingProperties = (ThingProperties)value;
            def.Flags.Special = thingProperties.HasFlag(ThingProperties.SPECIAL);
            def.Flags.Solid = thingProperties.HasFlag(ThingProperties.SOLID);
            def.Flags.Shootable = thingProperties.HasFlag(ThingProperties.SHOOTABLE);
            def.Flags.NoSector = thingProperties.HasFlag(ThingProperties.NOSECTOR);
            def.Flags.NoBlockmap = thingProperties.HasFlag(ThingProperties.NOBLOCKMAP);
            def.Flags.Ambush = thingProperties.HasFlag(ThingProperties.AMBUSH);
            def.Flags.JustHit = thingProperties.HasFlag(ThingProperties.JUSTHIT);
            def.Flags.JustAttacked = thingProperties.HasFlag(ThingProperties.JUSTATTACKED);
            def.Flags.SpawnCeiling = thingProperties.HasFlag(ThingProperties.SPAWNCEILING);
            def.Flags.NoGravity = thingProperties.HasFlag(ThingProperties.NOGRAVITY);
            def.Flags.Dropoff = thingProperties.HasFlag(ThingProperties.DROPOFF);
            def.Flags.Pickup = thingProperties.HasFlag(ThingProperties.PICKUP);
            def.Flags.NoClip = thingProperties.HasFlag(ThingProperties.NOCLIP);
            def.Flags.SlidesOnWalls = thingProperties.HasFlag(ThingProperties.SLIDE);
            def.Flags.Float = thingProperties.HasFlag(ThingProperties.FLOAT);
            def.Flags.Teleport = thingProperties.HasFlag(ThingProperties.TELEPORT);
            def.Flags.Missile = thingProperties.HasFlag(ThingProperties.MISSILE);
            def.Flags.Dropped = thingProperties.HasFlag(ThingProperties.DROPPED);
            def.Flags.Shadow = thingProperties.HasFlag(ThingProperties.SHADOW);
            def.Flags.NoBlood = thingProperties.HasFlag(ThingProperties.NOBLOOD);
            def.Flags.Corpse = thingProperties.HasFlag(ThingProperties.CORPSE);
            def.Flags.CountKill = thingProperties.HasFlag(ThingProperties.COUNTKILL);
            def.Flags.CountItem = thingProperties.HasFlag(ThingProperties.COUNTITEM);
            def.Flags.Skullfly = thingProperties.HasFlag(ThingProperties.SKULLFLY);
            def.Flags.NotDMatch = thingProperties.HasFlag(ThingProperties.NOTDMATCH);
            def.Flags.NotDMatch = thingProperties.HasFlag(ThingProperties.NOTDMATCH);
            def.Flags.Touchy = thingProperties.HasFlag(ThingProperties.TOUCHY);
            def.Flags.MbfBouncer = thingProperties.HasFlag(ThingProperties.BOUNCES);
            def.Flags.Friendly = thingProperties.HasFlag(ThingProperties.FRIEND);

            // TODO can we support these?
            //if (thingProperties.HasFlag(ThingProperties.TRANSLATION1))
            //if (thingProperties.HasFlag(ThingProperties.TRANSLATION2))
            //if (thingProperties.HasFlag(ThingProperties.INFLOAT))
        }

        private static double GetDouble(int value) => value / 65536.0;

        private static string GetSound(int sound)
        {
            if (sound < 0 || sound >= SoundStrings.Length)
            {
                // Log.Error
                return string.Empty;
            }

            return SoundStrings[sound];
        }

    }
}
