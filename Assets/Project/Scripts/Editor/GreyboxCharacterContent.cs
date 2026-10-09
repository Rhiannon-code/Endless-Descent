using System.Collections.Generic;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.EditorTools
{
    // What the creator picks from. Classes and backgrounds are assets rather than tables in the
    // screen so that a save can name one and get it back, a class built at runtime cannot be
    // referenced by anything, which is the whole reason CharacterBlueprint exists
    public static class GreyboxCharacterContent
    {
        // Attribute bonuses in CharacterAttribute order: Str Int Wil Agi End Per Spd Luck
        static readonly (string asset, string id, string name, string blurb,
            SkillId[] primary, SkillId[] major, SkillId[] minor,
            int[] bonuses, int hpPerLevel, float magery, CharacterSpecial[] specials)[] ClassPlans =
        {
            ("Class_Knight", "class.knight", "Knight",
                "Trained to stand in the line and stay there. Heavy, slow, hard to put down.",
                new[] { SkillId.Longsword, SkillId.Block, SkillId.CriticalStrike },
                new[] { SkillId.Axe, SkillId.Etiquette, SkillId.Climbing },
                new[] { SkillId.BluntWeapon, SkillId.Archery, SkillId.Dodging, SkillId.Running, SkillId.HandToHand, SkillId.Mercantile },
                new[] { 8, -4, 2, 0, 8, 4, -4, 0 }, 12, 0.6f,
                new[] { CharacterSpecial.ImmunityToParalysis, CharacterSpecial.ExpertiseLongsword }),

            ("Class_Rogue", "class.rogue", "Rogue",
                "Fights where it is cheapest to fight: from behind, and only once.",
                new[] { SkillId.ShortBlade, SkillId.CriticalStrike, SkillId.Stealth },
                new[] { SkillId.Dodging, SkillId.Lockpicking, SkillId.Running },
                new[] { SkillId.Longsword, SkillId.Archery, SkillId.Climbing, SkillId.Mercantile, SkillId.Etiquette, SkillId.Block },
                new[] { 0, 2, -2, 8, 0, 4, 8, 2 }, 8, 0.8f,
                new[] { CharacterSpecial.ExpertiseShortBlade, CharacterSpecial.ForbiddenArmourPlate }),

            ("Class_Spellsword", "class.spellsword", "Spellsword",
                "A sword in one hand and just enough of the old workings in the other.",
                new[] { SkillId.Longsword, SkillId.Destruction, SkillId.Restoration },
                new[] { SkillId.Block, SkillId.Alteration, SkillId.Dodging },
                new[] { SkillId.ShortBlade, SkillId.Mysticism, SkillId.Illusion, SkillId.Climbing, SkillId.Running, SkillId.Etiquette },
                new[] { 4, 4, 4, 0, 2, 0, 0, 0 }, 9, 1f,
                new CharacterSpecial[0]),

            ("Class_Battlemage", "class.battlemage", "Battlemage",
                "Magic first, and a mace for when the magicka runs out. It always runs out.",
                new[] { SkillId.Destruction, SkillId.Alteration, SkillId.BluntWeapon },
                new[] { SkillId.Mysticism, SkillId.Restoration, SkillId.Block },
                new[] { SkillId.Illusion, SkillId.Longsword, SkillId.Dodging, SkillId.Etiquette, SkillId.Mercantile, SkillId.Climbing },
                new[] { 2, 8, 6, -2, 0, 0, -2, 0 }, 7, 2f,
                new[] { CharacterSpecial.IncreasedMagery, CharacterSpecial.ForbiddenArmourPlate }),

            ("Class_Thief", "class.thief", "Thief",
                "Nothing in this list is about winning a fight. That is the point of it.",
                new[] { SkillId.Stealth, SkillId.Lockpicking, SkillId.Climbing },
                new[] { SkillId.ShortBlade, SkillId.Dodging, SkillId.Mercantile },
                new[] { SkillId.Running, SkillId.CriticalStrike, SkillId.Archery, SkillId.Etiquette, SkillId.Illusion, SkillId.HandToHand },
                new[] { -2, 2, -2, 8, 0, 4, 8, 6 }, 7, 0.8f,
                new[] { CharacterSpecial.AcuteHearing, CharacterSpecial.AthleticismBonus,
                        CharacterSpecial.ForbiddenArmourPlate, CharacterSpecial.ForbiddenShield }),

            ("Class_Warden", "class.warden", "Warden",
                "Gravewarden training: undead, and the rites for putting them back.",
                new[] { SkillId.BluntWeapon, SkillId.Restoration, SkillId.Block },
                new[] { SkillId.Longsword, SkillId.Mysticism, SkillId.Etiquette },
                new[] { SkillId.Destruction, SkillId.Alteration, SkillId.Dodging, SkillId.Climbing, SkillId.Stealth, SkillId.Mercantile },
                new[] { 4, 2, 6, 0, 4, 2, -2, 0 }, 10, 1f,
                new[] { CharacterSpecial.BonusToHitUndead, CharacterSpecial.ImmunityToPoison,
                        CharacterSpecial.DamageFromHolyPlaces }),

            ("Class_Ranger", "class.ranger", "Ranger",
                "Used to being a long way from a road and further from help.",
                new[] { SkillId.Archery, SkillId.Running, SkillId.Stealth },
                new[] { SkillId.ShortBlade, SkillId.Climbing, SkillId.Dodging },
                new[] { SkillId.Longsword, SkillId.Restoration, SkillId.Alteration, SkillId.CriticalStrike, SkillId.Mercantile, SkillId.Block },
                new[] { 2, 0, 2, 6, 6, 0, 6, 2 }, 9, 0.8f,
                new[] { CharacterSpecial.BonusToHitAnimals, CharacterSpecial.ResistanceToFrost })
        };

        public static ClassDefinition[] BuildClasses()
        {
            List<ClassDefinition> built = new List<ClassDefinition>();

            foreach ((string asset, string id, string name, string blurb, SkillId[] primary, SkillId[] major,
                SkillId[] minor, int[] bonuses, int hp, float magery, CharacterSpecial[] specials) in ClassPlans)
            {
                ClassDefinition definition = GreyboxContentBuilder.Create<ClassDefinition>(asset);

                new AssetAuthoring(definition)
                    .Str("id", id).Str("displayName", name).Str("description", blurb)
                    .Enums("primarySkills", Ints(primary))
                    .Enums("majorSkills", Ints(major))
                    .Enums("minorSkills", Ints(minor))
                    .Apply("attributeBonuses", p =>
                    {
                        p.arraySize = bonuses.Length;
                        for (int i = 0; i < bonuses.Length; i++)
                            p.GetArrayElementAtIndex(i).intValue = bonuses[i];
                    })
                    .Int("hitPointsPerLevel", hp)
                    .Float("mageryMultiplier", magery)
                    .Enums("specials", Ints(specials))
                    .Save();

                built.Add(GreyboxContentBuilder.Load<ClassDefinition>(asset));
            }

            return built.ToArray();
        }

        // Backgrounds answer "why do you own this", which is the one question the class list never
        // asks. They are also where the starting kit comes from, so StartingKitApplier is only the
        // fallback for the playtest scenes that skip the creator
        static readonly (string asset, string id, string name, string blurb, string[] worn, string[] carried,
            int gold, SkillId[] trained, bool guild, int standing)[] BackgroundPlans =
        {
            ("Background_Discharged", "background.discharged", "Discharged Levy",
                "Three years in somebody else's war and a jerkin they let you keep.",
                new[] { "Item_Jerkin", "Item_Shortsword", "Item_Shield" },
                new[] { "Item_Bread", "Item_HealingPotion" }, 40,
                new[] { SkillId.Block, SkillId.Longsword }, false, 0),

            ("Background_Gravedigger", "background.gravedigger", "Gravedigger",
                "You have buried more of this parish than the priest has. The Gravewardens know you.",
                new[] { "Item_Jerkin", "Item_Mace" },
                new[] { "Item_DriedMeat", "Item_BoneDust" }, 25,
                new[] { SkillId.BluntWeapon, SkillId.Restoration }, true, 18),

            ("Background_Cutpurse", "background.cutpurse", "Cutpurse",
                "No trade anybody will name, and rather more coin than the others.",
                new[] { "Item_Dagger", "Item_LeatherCap" },
                new[] { "Item_Cheese", "Item_Ale" }, 140,
                new[] { SkillId.Stealth, SkillId.Lockpicking }, false, -6),

            ("Background_Scribe", "background.scribe", "Temple Scribe",
                "Copied registers until your eyes went. You can read what other people cannot.",
                new[] { "Item_LeatherGloves", "Item_Dagger" },
                new[] { "Item_Bread", "Item_HealingPotion", "Item_StrongPotion" }, 90,
                new[] { SkillId.Mysticism, SkillId.Etiquette }, false, 0),

            ("Background_Hedgewitch", "background.hedgewitch", "Hedge Witch",
                "The workings anybody can read, learned from somebody who should not have taught them.",
                new[] { "Item_LeatherCap" },
                new[] { "Item_HealingPotion", "Item_Nightshade", "Item_Cheese" }, 60,
                new[] { SkillId.Restoration, SkillId.Alteration }, false, -4),

            ("Background_Caravanner", "background.caravanner", "Caravan Guard",
                "Walked the Ashmere road so often you could price every stall on it.",
                new[] { "Item_Jerkin", "Item_LeatherBoots", "Item_Longsword" },
                new[] { "Item_DriedMeat", "Item_Ale" }, 110,
                new[] { SkillId.Mercantile, SkillId.Running }, false, 0)
        };

        public static BackgroundDefinition[] BuildBackgrounds(FactionDefinition guild)
        {
            List<BackgroundDefinition> built = new List<BackgroundDefinition>();

            foreach ((string asset, string id, string name, string blurb, string[] worn, string[] carried,
                int gold, SkillId[] trained, bool joins, int standing) in BackgroundPlans)
            {
                BackgroundDefinition definition = GreyboxContentBuilder.Create<BackgroundDefinition>(asset);

                new AssetAuthoring(definition)
                    .Str("id", id).Str("displayName", name).Str("description", blurb)
                    .Refs("worn", Items(worn))
                    .Refs("carried", Items(carried))
                    .Int("startingGold", gold)
                    .Enums("trainedSkills", Ints(trained))
                    .Int("trainedBonus", 8)
                    .Ref("faction", joins ? guild : null)
                    .Int("standing", standing)
                    .Save();

                built.Add(GreyboxContentBuilder.Load<BackgroundDefinition>(asset));
            }

            return built.ToArray();
        }

        // Loaded by path rather than kept from earlier in the build, an instance created before an
        // import can be destroyed by it, and assigning one then writes a silent null
        static Object[] Items(string[] assets)
        {
            Object[] items = new Object[assets.Length];

            for (int i = 0; i < assets.Length; i++)
            {
                items[i] = GreyboxContentBuilder.Load<ItemDefinition>(assets[i]);

                if (items[i] == null)
                    Debug.LogError($"Background wants {assets[i]}, which the item build did not make.");
            }

            return items;
        }

        // Convert, not an unboxing cast, boxing an enum and casting the box to int throws, because
        // unboxing only ever succeeds to the exact type that went in
        static int[] Ints<T>(T[] values) where T : System.Enum
        {
            int[] numbers = new int[values.Length];

            for (int i = 0; i < values.Length; i++)
                numbers[i] = System.Convert.ToInt32(values[i]);

            return numbers;
        }
    }
}
