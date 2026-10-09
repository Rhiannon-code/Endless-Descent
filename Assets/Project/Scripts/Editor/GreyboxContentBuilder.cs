using System.Collections.Generic;
using System.IO;
using EndlessDescent.Combat;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Dungeons;
using EndlessDescent.World;
using EndlessDescent.Items;
using EndlessDescent.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace EndlessDescent.EditorTools
{
    public static class GreyboxContentBuilder
    {
        const string ContentRoot = "Assets/Project/Content/Greybox";
        const string PrefabRoot = "Assets/Project/Prefabs/Greybox";
        const string MaterialRoot = "Assets/Project/Content/Greybox/Materials";

        // Raise this whenever the builder's output shape changes, so scenes built against older
        // content are refused rather than quietly wired to defaults
        public const int ContentVersion = 14;

        const float RoomSize = 8f;
        const float WallHeight = 4f;
        const float BossSize = 12f;
        const float CorridorSize = 4f;
        const float CorridorLength = 14f;
        const float WallThickness = 0.2f;
        const float DoorWidth = 2.4f;
        const float DoorHeight = 2.8f;
        const float LevelDrop = 5f;
        const float StairRun = 9f;
        const float TorchLumens = 1500f;
        const float TorchRange = 16f;
        const float TorchHeight = 2.4f;

        [MenuItem("Endless Descent/Build Greybox Content")]
        public static void Build()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Build greybox content",
                $"This DELETES and regenerates:\n\n{ContentRoot}\n{PrefabRoot}\n\n" +
                "Anything you hand-edited in those two folders is lost. Nothing outside them is touched.\n\n" +
                "Commit first if you have unsaved work.",
                "Build", "Cancel");

            if (!proceed)
                return;

            EditorApplication.delayCall += () =>
            {
                try
                {
                    Generate();
                    Verify();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Greybox build failed: {e}");
                }
            };
        }

        // Also the -executeMethod entry point: no dialog, no delayCall
        public static void BuildNow()
        {
            try
            {
                Generate();
                Verify();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Greybox build failed: {e}");
            }
        }

        static bool HasWideEnoughModule(ModuleSet set, RoomRole role, int required)
        {
            List<RoomModuleDefinition> matching = new List<RoomModuleDefinition>();
            set.CollectFor(role, matching);

            foreach (RoomModuleDefinition module in matching)
            {
                RoomModule room = module.Prefab != null ? module.Prefab.GetComponent<RoomModule>() : null;
                if (room != null && room.Connectors != null && room.Connectors.Count >= required)
                    return true;
            }

            return false;
        }

        static void Verify()
        {
            int problems = 0;

            foreach ((string setName, string display, string[] _) in SetPlans)
            {
                if (IsSettlement(setName))
                    continue;

                DungeonTypeDefinition type = Load<DungeonTypeDefinition>($"DungeonType_{setName}");

                if (type == null)
                {
                    Debug.LogError($"DungeonType_{setName} did not survive the import.");
                    problems++;
                    continue;
                }

                if (type.ModuleSet == null) { Debug.LogError($"DungeonType_{setName} has no module set."); problems++; }
                if (type.CorridorStraight == null) { Debug.LogError($"DungeonType_{setName} has no straight corridor."); problems++; }
                if (type.CorridorCorner == null) { Debug.LogError($"DungeonType_{setName} has no corner corridor."); problems++; }
                if (type.Stair == null) { Debug.LogError($"DungeonType_{setName} has no stair."); problems++; }
                if (type.DoorPlug == null) { Debug.LogError($"DungeonType_{setName} has no door plug."); problems++; }
                if (type.Door == null) { Debug.LogError($"DungeonType_{setName} has no door."); problems++; }

                if (type.ModuleSet == null)
                    continue;

                if (!type.ModuleSet.CoversEveryRole(out RoomRole uncovered))
                {
                    Debug.LogError($"ModuleSet_{setName} has no usable module for role '{uncovered}'; it will not build.");
                    problems++;
                }

                // Role coverage is not enough, an entrance or junction that never has maxRoomExits
                // connectors fails only on the seeds that happen to need them
                foreach (RoomRole role in new[] { RoomRole.Entrance, RoomRole.Junction })
                {
                    if (!HasWideEnoughModule(type.ModuleSet, role, type.MaxRoomExits))
                    {
                        Debug.LogError($"ModuleSet_{setName} has no '{role}' module with {type.MaxRoomExits} " +
                                       "connectors; some seeds will not build.");
                        problems++;
                    }
                }
            }

            // Settlements answer to a different set of questions, ground to build on, a trade for
            // every plot, and an interior behind every front door
            foreach ((string set, bool _w, SettlementShore _s, Growth growth) in SettlementPlans)
            {
                SettlementDefinition settlement = Load<SettlementDefinition>($"Settlement_{set}");

                if (settlement == null)
                {
                    Debug.LogError($"Settlement_{set} did not survive the import.");
                    problems++;
                    continue;
                }

                if (settlement.BuildingPurposes.Length == 0)
                {
                    Debug.LogError($"Settlement_{set} has no building types; every plot would be a house.");
                    problems++;
                }

                if (settlement.Relief <= 0f)
                {
                    Debug.LogError($"Settlement_{set} has no relief; the ground would be a flat plate.");
                    problems++;
                }

                if (growth.Civic > 0f && settlement.CivicPurposes.Length == 0)
                {
                    Debug.LogError($"Settlement_{set} has a civic quarter with nothing grand to put in it.");
                    problems++;
                }

                if (settlement.DoorPlug == null)
                {
                    Debug.LogError($"Settlement_{set} has no door plug; interiors would open onto nothing.");

                if (CircleDoorFor(set) != null && settlement.HiddenCircle == null)
                    Debug.LogError($"Settlement_{set} should hide a circle and does not; that circle " +
                                   "cannot be joined anywhere in the game.");
                    problems++;
                }

                foreach (RoomPurpose purpose in settlement.BuildingPurposes)
                {
                    if (settlement.InteriorFor(purpose) == null)
                    {
                        Debug.LogError($"Settlement_{set} has no interior for '{purpose}'; that door will not open.");
                        problems++;
                    }

                    if (settlement.TintFor(purpose) == null)
                    {
                        Debug.LogError($"Settlement_{set} has no tint for '{purpose}'; it cannot be told apart.");
                        problems++;
                    }
                }
            }

            if (Load<ClassDefinition>("Class_Adventurer") == null)
            {
                Debug.LogError("Class_Adventurer did not survive the import; a skipped creator has no class.");
                problems++;
            }

            RegionMap region = Load<RegionMap>("RegionMap");

            if (region == null || region.Locations.Count != OrdovanPlaces.All.Length)
            {
                Debug.LogError($"RegionMap is missing or short: {region?.Locations.Count ?? 0} " +
                               $"of {OrdovanPlaces.All.Length} places.");
                problems++;
            }
            else
            {
                foreach (RegionLocation location in region.Locations)
                {
                    if (location.Settlement != null || location.Dungeon != null)
                        continue;

                    Debug.LogError($"Region place '{location.DisplayName}' resolves to neither a settlement nor a dungeon.");
                    problems++;
                }
            }

            foreach ((string key, string name, string _w, SkillId _s, string _b) in CirclePlans)
            {
                FactionDefinition circle = Load<FactionDefinition>($"Circle_{key}");

                if (circle == null)
                {
                    Debug.LogError($"Circle_{key} did not survive the import; its tradition cannot be learned.");
                    problems++;
                }
            }

            foreach (string spell in new[] { "Spell_Staunch", "Spell_Ashfall", "Spell_NineHands" })
            {
                SpellDefinition definition = Load<SpellDefinition>(spell);

                if (definition != null && definition.Tradition == null)
                {
                    Debug.LogError($"{spell} has no tradition; a circle spell anyone can learn is not a circle spell.");
                    problems++;
                }
            }

            EnemyDefinition skeleton = Load<EnemyDefinition>("Enemy_Skeleton");

            if (skeleton == null || skeleton.Prefab == null)
            {
                Debug.LogError("Enemy_Skeleton has no prefab; no enemies will spawn.");
                problems++;
            }

            if (problems == 0)
                Debug.Log($"Greybox content verified on disk: {SetPlans.Length - SettlementPlans.Length} dungeon " +
                          $"types, {SettlementPlans.Length} settlements, {OrdovanPlaces.All.Length} places on the region " +
                          $"map, {RoomPlans.Length} themed rooms, corridors, stairs, plugs and enemy prefab.");
        }

        static void Generate()
        {
            // No StartAssetEditing here, it defers the import of each prefab this method saves, so
            // PrefabUtility.SaveAsPrefabAsset hands back an object with no stable file id and every
            // definition that references one serialises a reference that dangles after the import
            try
            {
                Reset(ContentRoot);
                Reset(PrefabRoot);

                GameObject pickupPrefab = BuildPickupPrefab();
                GameObject keyPrefab = BuildKeyPrefab();

                System.IO.Directory.CreateDirectory(MaterialRoot);
                AssetDatabase.Refresh();

                Dictionary<string, RoomModuleDefinition> modules = BuildModules();

                BuildCorridor("Mod_CorridorStraight", new[] { 0, 2 }, new Vector2(CorridorSize, CorridorLength));
                BuildCorridor("Mod_CorridorCorner", new[] { 0, 1 }, new Vector2(CorridorSize, CorridorSize));
                BuildStair();
                BuildDoorPlug();
                BuildDoorPrefab();

                foreach ((string setName, string display, string[] members) in SetPlans)
                {
                    // A settlement needs its modules as interiors, not as a set the packer draws
                    // from, there is no room graph to cover every role for
                    if (IsSettlement(setName))
                        continue;

                    // Loaded by path, not taken from the dictionary, an instance created earlier in
                    // this method can be destroyed by any import since, and a reference to a
                    // destroyed object serialises as null without complaining
                    RoomModuleDefinition[] chosen = new RoomModuleDefinition[members.Length];
                    for (int i = 0; i < members.Length; i++)
                        chosen[i] = Load<RoomModuleDefinition>($"Module_{members[i]}");

                    ModuleSet set = Create<ModuleSet>($"ModuleSet_{setName}");
                    new AssetAuthoring(set).Refs("modules", chosen).Save();
                }

                // Loaded back by path rather than reused, an instance created earlier in this method
                // can be invalidated by a later import, and assigning one then writes a silent null
                foreach ((string setName, string display, string[] _) in SetPlans)
                {
                    if (IsSettlement(setName))
                        continue;

                    DungeonTypeDefinition dungeonType = Create<DungeonTypeDefinition>($"DungeonType_{setName}");
                    new AssetAuthoring(dungeonType)
                        .Str("displayName", display)
                        .Ref("moduleSet", Load<ModuleSet>($"ModuleSet_{setName}"))
                        .Ref("corridorStraight", Load<RoomModuleDefinition>("Module_Mod_CorridorStraight"))
                        .Ref("corridorCorner", Load<RoomModuleDefinition>("Module_Mod_CorridorCorner"))
                        .Ref("stair", Load<RoomModuleDefinition>("Module_Mod_Stair"))
                        .Ref("doorPlug", AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Mod_DoorPlug.prefab"))
                        .Ref("door", AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Mod_Door.prefab"))
                        .Int("maxCorridorSegments", 4)
                        .Int("maxRoomExits", 4)
                        .Bool("lockDoors", false)
                        .Save();

                    Scale(setName);
                }

                BuildItems();
                ItemDefinition scrap = Load<ItemDefinition>("Item_BoneDust");

                BuildProjectilePrefab();
                BuildInventoryRowPrefab();
                BuildViewmodelPrefabs();
                BuildCircles();

                // The three the player starts with are pre-Ban common workings, they survived in
                // books anybody can read, which is why an unaffiliated character has any magic at all
                Spell("Spell_Firebolt", "spell.firebolt", "Firebolt", SpellKind.Projectile, 12, 0.7f, 22, 0f,
                    22f, new Color(1f, 0.5f, 0.15f), SkillId.Destruction);
                Spell("Spell_Mend", "spell.mend", "Mend", SpellKind.Heal, 25, 4f, 30, 0f,
                    0f, new Color(0.4f, 1f, 0.5f), SkillId.Restoration);
                Spell("Spell_Ward", "spell.ward", "Ward", SpellKind.Ward, 20, 8f, 6, 10f,
                    0f, new Color(0.45f, 0.6f, 1f), SkillId.Alteration);

                // One per circle, to prove the gate works and to give each tradition something of
                // its own that no other circle will ever teach
                Spell("Spell_Staunch", "spell.staunch", "Staunch", SpellKind.Heal, 14, 2f, 18, 0f,
                    0f, new Color(0.5f, 0.95f, 0.6f), SkillId.Restoration, "Lamplighters");
                Spell("Spell_Render", "spell.render", "Read the Marks", SpellKind.Ward, 18, 6f, 4, 30f,
                    0f, new Color(0.75f, 0.7f, 0.4f), SkillId.Mysticism, "DenrowsTable");
                Spell("Spell_Countenance", "spell.countenance", "Countenance", SpellKind.Ward, 22, 12f, 5, 40f,
                    0f, new Color(0.8f, 0.75f, 0.9f), SkillId.Illusion, "NinthChair");
                Spell("Spell_Keeping", "spell.keeping", "Keeping", SpellKind.Ward, 10, 3f, 3, 90f,
                    0f, new Color(0.85f, 0.8f, 0.5f), SkillId.Alteration, "QuietLedger");
                Spell("Spell_Slackwater", "spell.slackwater", "Slackwater", SpellKind.Ward, 16, 5f, 7, 20f,
                    0f, new Color(0.4f, 0.65f, 0.7f), SkillId.Alteration, "SaltmereCoil");
                Spell("Spell_NineHands", "spell.ninehands", "The Nine Hands", SpellKind.Heal, 40, 10f, 55, 0f,
                    0f, new Color(0.6f, 0.9f, 0.75f), SkillId.Restoration, "HaskWidows");
                Spell("Spell_Ashfall", "spell.ashfall", "Ashfall", SpellKind.Projectile, 26, 1.2f, 44, 0f,
                    18f, new Color(0.9f, 0.35f, 0.2f), SkillId.Destruction, "Ashwake");
                Spell("Spell_ThinPlace", "spell.thinplace", "Thin Place", SpellKind.Ward, 30, 15f, 8, 25f,
                    0f, new Color(0.6f, 0.45f, 0.8f), SkillId.Mysticism, "ThirdTestament");
                Spell("Spell_Unlooked", "spell.unlooked", "Unlooked For", SpellKind.Ward, 20, 8f, 6, 35f,
                    0f, new Color(0.45f, 0.45f, 0.5f), SkillId.Illusion, "Unlit");

                BuildLootTables();

                Dictionary<string, EnemyDefinition> bestiary = BuildEnemies();
                BuildTrapPrefabs();
                BuildPlantPrefabs();
                BuildDoorwayPrefabs();
                BuildSecretDoorPrefab();
                BuildFurnishingPrefabs();

                foreach ((string setName, string display, string[] _) in SetPlans)
                {
                    if (IsSettlement(setName))
                        continue;

                    (EnemyDefinition[] roster, EnemyDefinition boss) = Roster(setName, bestiary);

                    new AssetAuthoring(Load<DungeonTypeDefinition>($"DungeonType_{setName}"))
                        .Refs("enemies", roster)
                        .Ref("boss", boss)
                        .Ref("loot", Load<LootTableDefinition>($"Loot_{setName}"))
                        .Ref("bossLoot", Load<LootTableDefinition>($"Loot_{setName}_Boss"))
                        .Ref("secretDoor", AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Mod_SecretDoor.prefab"))
                        .Refs("traps",
                            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Trap_Spikes.prefab"),
                            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Trap_Dart.prefab"),
                            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Trap_Blade.prefab"),
                            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Trap_Gas.prefab"))
                        .Float("trapChance", 0.18f)
                        .Refs("furnishings",
                            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Camp.prefab"),
                            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/RepairBench.prefab"),
                            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Trainer.prefab"),
                            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/GuildHall.prefab"),
                            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Spellmaker.prefab"),
                            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/EnchantingTable.prefab"),
                            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Constable.prefab"))
                        .Float("furnishingChance", 0.05f)
                        .Save();
                }

                FactionDefinition guild = Create<FactionDefinition>("Faction_Gravewardens");
                new AssetAuthoring(guild)
                    .Str("id", "faction.gravewardens").Str("displayName", "Gravewardens")
                    .Int("startingStanding", 0)
                    .Bool("isGuild", true)
                    .Int("joinAtStanding", -20)
                    .Enums("services", (int)GuildService.Repair, (int)GuildService.Training, (int)GuildService.Healing)
                    .Apply("serviceRequiresRank", p =>
                    {
                        p.arraySize = 3;
                        p.GetArrayElementAtIndex(0).intValue = 0;
                        p.GetArrayElementAtIndex(1).intValue = 2;
                        p.GetArrayElementAtIndex(2).intValue = 3;
                    })
                    .Save();

                BuildGuildHallPrefab(guild);
                BuildCircleDoorPrefabs();

                LocationDefinition town = Create<LocationDefinition>("Location_Ashmere");
                new AssetAuthoring(town)
                    .Str("id", "loc.ashmere").Str("displayName", "Ashmere")
                    .Enum("kind", (int)LocationKind.Settlement).Save();

                // Act I's two dungeons. A location is matched to the world by its display name, so these
                // must be the names the places carry in OrdovanPlaces
                LocationDefinition undercroftPlace = Create<LocationDefinition>("Location_Undercroft");
                new AssetAuthoring(undercroftPlace)
                    .Str("id", "loc.undercroft").Str("displayName", "Undercroft")
                    .Enum("kind", (int)LocationKind.DungeonEntrance).Save();

                LocationDefinition cryptPlace = Create<LocationDefinition>("Location_Crypt");
                new AssetAuthoring(cryptPlace)
                    .Str("id", "loc.crypt").Str("displayName", "Crypt")
                    .Enum("kind", (int)LocationKind.DungeonEntrance).Save();

                ScheduleDefinition schedule = Create<ScheduleDefinition>("Schedule_Shopkeeper");
                AssetAuthoring scheduleAuthor = new AssetAuthoring(schedule);
                scheduleAuthor.Apply("blocks", p =>
                {
                    (int hour, NpcActivity activity)[] blocks =
                    {
                        (0, NpcActivity.Sleeping), (8, NpcActivity.Working), (19, NpcActivity.Socialising), (23, NpcActivity.Sleeping)
                    };

                    p.arraySize = blocks.Length;
                    for (int i = 0; i < blocks.Length; i++)
                    {
                        SerializedProperty block = p.GetArrayElementAtIndex(i);
                        block.FindPropertyRelative("StartHour").intValue = blocks[i].hour;
                        block.FindPropertyRelative("Activity").enumValueIndex = (int)blocks[i].activity;
                        block.FindPropertyRelative("Location").objectReferenceValue = town;
                    }
                });
                scheduleAuthor.Save();

                NpcDefinition halric = Create<NpcDefinition>("Npc_Halric");
                new AssetAuthoring(halric)
                    .Str("id", "npc.halric").Str("displayName", "Halric")
                    .Ref("faction", guild).Ref("schedule", schedule).Ref("home", town)
                    .Enums("motives", (int)NpcMotive.Debt, (int)NpcMotive.Fear)
                    .Str("situation", "Owes the Gravewardens more than he can pay.")
                    .Bool("isMerchant", true).Int("startingGold", 300)
                    .Save();

                NpcDefinition sister = Create<NpcDefinition>("Npc_Ysolde");
                new AssetAuthoring(sister)
                    .Str("id", "npc.ysolde").Str("displayName", "Ysolde")
                    .Ref("faction", guild).Ref("schedule", schedule).Ref("home", town)
                    .Enums("motives", (int)NpcMotive.Grief)
                    .Str("situation", "Halric's sister. Knows exactly what he owes and to whom.")
                    .Save();

                new AssetAuthoring(halric).Refs("relations", sister).Save();
                new AssetAuthoring(sister).Refs("relations", halric).Save();

                RecipeDefinition recipe = Create<RecipeDefinition>("Recipe_BoneMeal");
                AssetAuthoring recipeAuthor = new AssetAuthoring(recipe);
                recipeAuthor.Str("id", "recipe.bonemeal").Str("displayName", "Bone Meal")
                    .Ref("output", Load<ItemDefinition>("Item_HealingPotion")).Int("outputCount", 1)
                    .Enum("station", (int)CraftingStation.AlchemyBench).Int("craftingMinutes", 20);
                recipeAuthor.Apply("inputs", p =>
                {
                    p.arraySize = 1;
                    SerializedProperty input = p.GetArrayElementAtIndex(0);
                    input.FindPropertyRelative("Item").objectReferenceValue = scrap;
                    input.FindPropertyRelative("Count").intValue = 3;
                });
                recipeAuthor.Save();

                PropertyDefinition house = Create<PropertyDefinition>("Property_AshmereRoom");
                new AssetAuthoring(house)
                    .Str("id", "property.ashmere.room").Str("displayName", "Rented Room, Ashmere")
                    .Ref("location", town).Int("purchasePrice", 3500).Int("rentPerDay", 12)
                    .Int("storageSlots", 30).Bool("hasBed", true)
                    .Save();

                Object[] templates = BuildQuestTemplates();

                AuthoredQuestDefinition authored = BuildAuthoredQuest(halric, sister, guild, scrap);

                BuildResidentPrefabs(halric, Load<LootTableDefinition>("Loot_Shop"));
                BuildBedPrefab();
                BuildQuestBoardPrefab();

                Dictionary<string, NpcDefinition[]> casts = BuildCasts(schedule, guild);

                // The opener is posted by somebody who lives in Ashmere, where the game starts
                NpcDefinition clerk = casts.TryGetValue("Ashmere", out NpcDefinition[] ashmere) && ashmere.Length > 0
                    ? ashmere[0] : halric;

                // Whatever trade the cast table dealt them, Act I needs them to be the parish clerk, kept
                // at the temple, so the record, the dialogue and where they stand all agree
                new AssetAuthoring(clerk)
                    .Str("situation", "Is the parish clerk in Ashmere, and keeps a register he has stopped writing in.")
                    .Enum("workplace", (int)RoomPurpose.Temple)
                    .Save();

                AuthoredQuestDefinition undercroft = BuildUndercroftQuest(clerk, guild, undercroftPlace);

                // The Act I line and the two trees that carry it. The clerk gives it out and the
                // warden is the third party it turns on, so both are people who already live here
                NpcDefinition warden = ashmere != null && ashmere.Length > 1 ? ashmere[1] : sister;

                AuthoredQuestDefinition[] actOne = GreyboxQuestContent.BuildActOne(
                    clerk, warden, guild, undercroftPlace, cryptPlace, scrap,
                    Load<ItemDefinition>("Item_SilverGoblet"),
                    Load<EnemyDefinition>("Enemy_Skeleton"));

                DialogueDefinition[] dialogues = GreyboxDialogueContent.Build(clerk, warden, guild);

                BuildSettlements();
                BuildRegionMap(casts);
                BuildDefaultClass();

                ClassDefinition[] classes = GreyboxCharacterContent.BuildClasses();
                BackgroundDefinition[] backgrounds = GreyboxCharacterContent.BuildBackgrounds(guild);

                GameDatabase database = Create<GameDatabase>("GameDatabase");
                new AssetAuthoring(database)
                    .Int("contentVersion", ContentVersion)
                    .Refs("items", AllItems())
                    .Refs("enemies", AllOf(bestiary))
                    .Refs("npcs", AllOfType<NpcDefinition>())
                    .Refs("factions", AllFactions(guild))
                    .Refs("locations", AllOfType<LocationDefinition>())
                    .Refs("recipes", recipe)
                    .Refs("properties", house)
                    .Refs("quests", AllQuests(authored, undercroft, actOne))
                    .Refs("questTemplates", templates)
                    .Refs("classes", AllClasses(classes))
                    .Refs("backgrounds", backgrounds)
                    .Refs("dialogues", dialogues)
                    .Save();

                Debug.Log($"Greybox content built. Database at {ContentRoot}/GameDatabase.asset, " +
                          $"pickup prefab {AssetDatabase.GetAssetPath(pickupPrefab)}, key prefab {AssetDatabase.GetAssetPath(keyPrefab)}.");
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        // The default class is in the list too, it is what the creator's Custom option is measured
        // against, and a save naming it has to be able to find it
        static Object[] AllClasses(ClassDefinition[] presets)
        {
            List<Object> all = new List<Object> { Load<ClassDefinition>("Class_Adventurer") };

            foreach (ClassDefinition definition in presets)
                if (definition != null) all.Add(definition);

            return all.ToArray();
        }

        static Object[] AllQuests(AuthoredQuestDefinition debt, AuthoredQuestDefinition undercroft,
            AuthoredQuestDefinition[] act)
        {
            List<Object> all = new List<Object> { debt, undercroft };

            foreach (AuthoredQuestDefinition quest in act)
                if (quest != null) all.Add(quest);

            return all.ToArray();
        }

        static AuthoredQuestDefinition BuildAuthoredQuest(NpcDefinition giver, NpcDefinition sister,
            FactionDefinition faction, ItemDefinition item)
        {
            AuthoredQuestDefinition quest = Create<AuthoredQuestDefinition>("Quest_TheDebt");
            AssetAuthoring author = new AssetAuthoring(quest);

            author.Str("id", "quest.the_debt").Str("title", "What Halric Owes")
                .Ref("giver", giver).Ref("faction", faction)
                .Str("reason", "Halric owes the Gravewardens for his father's burial rites and cannot pay. " +
                               "Ysolde, his sister, will tell you the real sum if you ask her.")
                .Str("complication", "The Gravewardens already sold the debt on. Paying Halric's price does not clear it.");

            author.Apply("stages", p =>
            {
                p.arraySize = 1;
                SerializedProperty stage = p.GetArrayElementAtIndex(0);
                stage.FindPropertyRelative("Title").stringValue = "Gather what he owes";
                stage.FindPropertyRelative("Summary").stringValue = "Bring Halric 5 bone dust from the crypt.";

                SerializedProperty objectives = stage.FindPropertyRelative("Objectives");
                objectives.arraySize = 1;
                SerializedProperty objective = objectives.GetArrayElementAtIndex(0);
                objective.FindPropertyRelative("Kind").enumValueIndex = (int)QuestObjectiveKind.CollectItem;
                objective.FindPropertyRelative("Description").stringValue = "Collect 5 Bone Dust";
                objective.FindPropertyRelative("Item").objectReferenceValue = item;
                objective.FindPropertyRelative("Count").intValue = 5;
            });

            author.Apply("onComplete", p =>
            {
                p.arraySize = 3;

                SerializedProperty gold = p.GetArrayElementAtIndex(0);
                gold.FindPropertyRelative("Kind").enumValueIndex = (int)QuestConsequenceKind.GiveGold;
                gold.FindPropertyRelative("Amount").intValue = 90;

                SerializedProperty standing = p.GetArrayElementAtIndex(1);
                standing.FindPropertyRelative("Kind").enumValueIndex = (int)QuestConsequenceKind.FactionStanding;
                standing.FindPropertyRelative("Faction").objectReferenceValue = faction;
                standing.FindPropertyRelative("Amount").intValue = 6;

                SerializedProperty situation = p.GetArrayElementAtIndex(2);
                situation.FindPropertyRelative("Kind").enumValueIndex = (int)QuestConsequenceKind.SetNpcSituation;
                situation.FindPropertyRelative("Npc").objectReferenceValue = sister;
                situation.FindPropertyRelative("Text").stringValue =
                    "Knows you paid her brother's debt, and that it bought him nothing.";
            });

            author.Save();
            return quest;
        }

        // One pool of themed rooms, each dungeon type draws the subset that suits it. Sizes are all
        // different on purpose, uniform squares are what made the first pass read as a grid
        static readonly (string name, RoomRole[] roles, RoomPurpose purpose, int[] sides, Vector2 size)[] RoomPlans =
        {
            // Entrances carry four ways out, maxRoomExits is 4, and room 0 is a branch anchor like
            // any other, so a one-door entrance would fail to build over half the time
            ("Mod_Gatehouse", new[] { RoomRole.Entrance }, RoomPurpose.Gatehouse, new[] { 0, 1, 2, 3 }, new Vector2(11f, 11f)),
            ("Mod_Stairwell", new[] { RoomRole.Entrance }, RoomPurpose.Stairwell, new[] { 0, 1, 2, 3 }, new Vector2(9f, 9f)),
            ("Mod_CaveMouth", new[] { RoomRole.Entrance }, RoomPurpose.CaveMouth, new[] { 0, 1, 2, 3 }, new Vector2(13f, 12f)),

            // Every set needs at least one four-way junction for the same reason
            ("Mod_Crossing", new[] { RoomRole.Junction }, RoomPurpose.Crossing, new[] { 0, 1, 2, 3 }, new Vector2(10f, 10f)),
            ("Mod_Rotunda", new[] { RoomRole.Junction }, RoomPurpose.Rotunda, new[] { 0, 1, 2, 3 }, new Vector2(13f, 13f)),
            ("Mod_Cavern", new[] { RoomRole.Junction }, RoomPurpose.Cavern, new[] { 0, 1, 2, 3 }, new Vector2(16f, 14f)),
            ("Mod_Hall", new[] { RoomRole.Junction }, RoomPurpose.Hall, new[] { 0, 1, 3 }, new Vector2(15f, 9f)),
            ("Mod_Nave", new[] { RoomRole.Junction }, RoomPurpose.Nave, new[] { 0, 1, 3 }, new Vector2(11f, 17f)),
            ("Mod_Gallery", new[] { RoomRole.Junction }, RoomPurpose.Gallery, new[] { 0, 2 }, new Vector2(17f, 7f)),
            ("Mod_Guardroom", new[] { RoomRole.Junction }, RoomPurpose.Guardroom, new[] { 0, 2 }, new Vector2(9f, 13f)),

            ("Mod_Barracks", new[] { RoomRole.Key, RoomRole.Secret }, RoomPurpose.Barracks, new[] { 0 }, new Vector2(14f, 9f)),
            ("Mod_Armoury", new[] { RoomRole.Treasure, RoomRole.Key }, RoomPurpose.Armoury, new[] { 0 }, new Vector2(11f, 8f)),
            ("Mod_Chapel", new[] { RoomRole.SetPiece }, RoomPurpose.Chapel, new[] { 0, 2 }, new Vector2(10f, 17f)),
            ("Mod_Library", new[] { RoomRole.SetPiece, RoomRole.Treasure }, RoomPurpose.Library, new[] { 0, 1 }, new Vector2(13f, 12f)),
            ("Mod_Crypt", new[] { RoomRole.Key, RoomRole.Treasure, RoomRole.Secret }, RoomPurpose.Crypt, new[] { 0 }, new Vector2(12f, 10f)),
            ("Mod_Ossuary", new[] { RoomRole.Key, RoomRole.Treasure, RoomRole.Secret }, RoomPurpose.Ossuary, new[] { 0 }, new Vector2(11f, 11f)),
            ("Mod_Barrow", new[] { RoomRole.Key, RoomRole.Treasure, RoomRole.Secret }, RoomPurpose.Barrow, new[] { 0 }, new Vector2(12f, 12f)),
            ("Mod_Reliquary", new[] { RoomRole.Treasure, RoomRole.Secret }, RoomPurpose.Reliquary, new[] { 0 }, new Vector2(9f, 9f)),
            ("Mod_Storeroom", new[] { RoomRole.Key, RoomRole.Treasure }, RoomPurpose.Storeroom, new[] { 0 }, new Vector2(8f, 9f)),
            ("Mod_Vault", new[] { RoomRole.Treasure, RoomRole.Key }, RoomPurpose.Vault, new[] { 0 }, new Vector2(10f, 10f)),
            ("Mod_Kitchen", new[] { RoomRole.Secret, RoomRole.Treasure }, RoomPurpose.Kitchen, new[] { 0 }, new Vector2(10f, 12f)),
            ("Mod_Cistern", new[] { RoomRole.Secret, RoomRole.SetPiece }, RoomPurpose.Cistern, new[] { 0 }, new Vector2(13f, 13f)),
            ("Mod_Well", new[] { RoomRole.Key, RoomRole.Secret }, RoomPurpose.Well, new[] { 0 }, new Vector2(9f, 9f)),
            ("Mod_Grotto", new[] { RoomRole.Secret, RoomRole.SetPiece }, RoomPurpose.Grotto, new[] { 0 }, new Vector2(14f, 11f)),
            ("Mod_Forge", new[] { RoomRole.SetPiece, RoomRole.Treasure }, RoomPurpose.Forge, new[] { 0, 1 }, new Vector2(12f, 11f)),
            ("Mod_Mine", new[] { RoomRole.Key, RoomRole.Treasure }, RoomPurpose.Mine, new[] { 0, 2 }, new Vector2(15f, 8f)),
            ("Mod_Alchemy", new[] { RoomRole.SetPiece, RoomRole.Treasure }, RoomPurpose.Alchemy, new[] { 0, 1 }, new Vector2(11f, 10f)),
            ("Mod_Cells", new[] { RoomRole.Key, RoomRole.Secret }, RoomPurpose.Cell, new[] { 0 }, new Vector2(13f, 8f)),
            ("Mod_Torture", new[] { RoomRole.Key, RoomRole.Secret }, RoomPurpose.Torture, new[] { 0 }, new Vector2(11f, 9f)),

            ("Mod_Arena", new[] { RoomRole.SetPiece, RoomRole.Boss }, RoomPurpose.Arena, new[] { 0, 2 }, new Vector2(26f, 26f)),
            ("Mod_GreatHall", new[] { RoomRole.SetPiece, RoomRole.Junction }, RoomPurpose.GreatHall, new[] { 0, 1, 2, 3 }, new Vector2(28f, 18f)),
            // Town rooms. A town is a dungeon type with no enemies, so these reuse the same builder
            ("Mod_Square", new[] { RoomRole.Entrance }, RoomPurpose.Square, new[] { 0, 1, 2, 3 }, new Vector2(20f, 20f)),
            ("Mod_Street", new[] { RoomRole.Junction }, RoomPurpose.Square, new[] { 0, 1, 2, 3 }, new Vector2(14f, 14f)),
            ("Mod_Market", new[] { RoomRole.SetPiece, RoomRole.Treasure }, RoomPurpose.Market, new[] { 0, 2 }, new Vector2(18f, 14f)),
            ("Mod_Smithy", new[] { RoomRole.Treasure, RoomRole.Key }, RoomPurpose.Smithy, new[] { 0 }, new Vector2(12f, 12f)),
            ("Mod_Inn", new[] { RoomRole.Secret, RoomRole.Treasure }, RoomPurpose.Inn, new[] { 0, 1 }, new Vector2(16f, 13f)),
            ("Mod_Guildhouse", new[] { RoomRole.SetPiece, RoomRole.Secret }, RoomPurpose.Guildhouse, new[] { 0 }, new Vector2(15f, 15f)),
            ("Mod_Townhouse", new[] { RoomRole.Key, RoomRole.Secret }, RoomPurpose.Townhouse, new[] { 0 }, new Vector2(10f, 10f)),
            ("Mod_Keep", new[] { RoomRole.Boss }, RoomPurpose.ThroneRoom, new[] { 0 }, new Vector2(16f, 16f)),
            ("Mod_Manor", new[] { RoomRole.Boss }, RoomPurpose.Manor, new[] { 0 }, new Vector2(13f, 13f)),

            // Waterfront
            ("Mod_Dock", new[] { RoomRole.Junction }, RoomPurpose.Dock, new[] { 0, 1, 2, 3 }, new Vector2(20f, 11f)),
            ("Mod_Warehouse", new[] { RoomRole.Treasure, RoomRole.Key }, RoomPurpose.Warehouse, new[] { 0 }, new Vector2(17f, 13f)),
            ("Mod_Fishmarket", new[] { RoomRole.SetPiece, RoomRole.Treasure }, RoomPurpose.Market, new[] { 0, 2 }, new Vector2(15f, 12f)),
            ("Mod_Shipyard", new[] { RoomRole.SetPiece, RoomRole.Secret }, RoomPurpose.Shipyard, new[] { 0, 1 }, new Vector2(19f, 15f)),
            ("Mod_Lighthouse", new[] { RoomRole.Boss }, RoomPurpose.Lighthouse, new[] { 0 }, new Vector2(11f, 11f)),

            // Trades and civic buildings, so a city is not a town with more houses
            ("Mod_Temple", new[] { RoomRole.SetPiece, RoomRole.Secret }, RoomPurpose.Temple, new[] { 0, 2 }, new Vector2(14f, 17f)),
            ("Mod_Bakery", new[] { RoomRole.Treasure, RoomRole.Key }, RoomPurpose.Bakery, new[] { 0 }, new Vector2(9f, 9f)),
            ("Mod_Alchemist", new[] { RoomRole.Treasure, RoomRole.Secret }, RoomPurpose.Alchemist, new[] { 0 }, new Vector2(10f, 9f)),

            // Daggerfall's shop board, near enough: every one of these is a storefront you can enter
            ("Mod_Armorer", new[] { RoomRole.Treasure, RoomRole.Key }, RoomPurpose.Armorer, new[] { 0 }, new Vector2(11f, 10f)),
            ("Mod_Bookseller", new[] { RoomRole.Secret, RoomRole.Treasure }, RoomPurpose.Bookseller, new[] { 0 }, new Vector2(10f, 11f)),
            ("Mod_ClothingStore", new[] { RoomRole.Treasure, RoomRole.Key }, RoomPurpose.ClothingStore, new[] { 0 }, new Vector2(10f, 10f)),
            ("Mod_FurnitureStore", new[] { RoomRole.Key, RoomRole.Treasure }, RoomPurpose.FurnitureStore, new[] { 0 }, new Vector2(12f, 11f)),
            ("Mod_GemStore", new[] { RoomRole.Treasure, RoomRole.Secret }, RoomPurpose.GemStore, new[] { 0 }, new Vector2(9f, 9f)),
            ("Mod_GeneralStore", new[] { RoomRole.Treasure, RoomRole.Key }, RoomPurpose.GeneralStore, new[] { 0, 1 }, new Vector2(13f, 11f)),
            ("Mod_PawnShop", new[] { RoomRole.Secret, RoomRole.Treasure }, RoomPurpose.PawnShop, new[] { 0 }, new Vector2(10f, 10f)),
            ("Mod_Palace", new[] { RoomRole.Boss }, RoomPurpose.Palace, new[] { 0 }, new Vector2(20f, 18f)),
            ("Mod_Tannery", new[] { RoomRole.Key, RoomRole.Treasure }, RoomPurpose.Tannery, new[] { 0 }, new Vector2(11f, 9f)),
            ("Mod_Stables", new[] { RoomRole.Key, RoomRole.Secret }, RoomPurpose.Stables, new[] { 0, 1 }, new Vector2(15f, 10f)),
            ("Mod_Watchhouse", new[] { RoomRole.Junction, RoomRole.Key }, RoomPurpose.Watchhouse, new[] { 0, 2 }, new Vector2(11f, 12f)),
            ("Mod_Graveyard", new[] { RoomRole.Secret, RoomRole.SetPiece }, RoomPurpose.Graveyard, new[] { 0 }, new Vector2(16f, 14f)),
            ("Mod_Bathhouse", new[] { RoomRole.Secret, RoomRole.Treasure }, RoomPurpose.Bathhouse, new[] { 0 }, new Vector2(13f, 12f)),
            ("Mod_Courthouse", new[] { RoomRole.SetPiece, RoomRole.Boss }, RoomPurpose.Courthouse, new[] { 0 }, new Vector2(16f, 15f)),
            ("Mod_Bank", new[] { RoomRole.Treasure, RoomRole.Key }, RoomPurpose.Bank, new[] { 0 }, new Vector2(11f, 11f)),
            ("Mod_Mill", new[] { RoomRole.Treasure, RoomRole.Secret }, RoomPurpose.Mill, new[] { 0 }, new Vector2(12f, 12f)),
            ("Mod_Tavern", new[] { RoomRole.SetPiece, RoomRole.Treasure }, RoomPurpose.Tavern, new[] { 0, 1 }, new Vector2(15f, 12f)),

            ("Mod_ThroneRoom", new[] { RoomRole.Boss }, RoomPurpose.ThroneRoom, new[] { 0 }, new Vector2(18f, 18f)),
            ("Mod_Sanctum", new[] { RoomRole.Boss }, RoomPurpose.Sanctum, new[] { 0 }, new Vector2(17f, 17f)),
            ("Mod_Lair", new[] { RoomRole.Boss }, RoomPurpose.Lair, new[] { 0 }, new Vector2(20f, 16f))
        };

        // Ten dungeon types. Each must cover every RoomRole on its own and carry a four-connector
        // entrance and junction, verify checks both rather than trusting this table by eye
        static readonly (string name, string display, string[] modules)[] SetPlans =
        {
            ("Crypt", "Crypt", new[]
            {
                "Mod_Gatehouse", "Mod_Crossing", "Mod_Hall",
                "Mod_Crypt", "Mod_Ossuary", "Mod_Chapel", "Mod_Cistern", "Mod_Arena", "Mod_GreatHall", "Mod_ThroneRoom"
            }),
            ("Barracks", "Barracks", new[]
            {
                "Mod_Gatehouse", "Mod_Crossing", "Mod_Guardroom",
                "Mod_Barracks", "Mod_Armoury", "Mod_Forge", "Mod_Kitchen", "Mod_Cells", "Mod_Arena", "Mod_GreatHall", "Mod_ThroneRoom"
            }),
            ("Undercroft", "Undercroft", new[]
            {
                "Mod_Gatehouse", "Mod_Crossing", "Mod_Hall",
                "Mod_Storeroom", "Mod_Cistern", "Mod_Library", "Mod_Kitchen", "Mod_Vault", "Mod_Arena", "Mod_GreatHall", "Mod_ThroneRoom"
            }),
            ("Monastery", "Monastery", new[]
            {
                "Mod_Stairwell", "Mod_Rotunda", "Mod_Nave",
                "Mod_Chapel", "Mod_Library", "Mod_Reliquary", "Mod_Kitchen", "Mod_Crypt", "Mod_Arena", "Mod_GreatHall", "Mod_Sanctum"
            }),
            ("Gaol", "Gaol", new[]
            {
                "Mod_Gatehouse", "Mod_Crossing", "Mod_Guardroom",
                "Mod_Cells", "Mod_Torture", "Mod_Storeroom", "Mod_Armoury", "Mod_Alchemy", "Mod_Arena", "Mod_GreatHall", "Mod_Lair"
            }),
            ("Mine", "Deep Mine", new[]
            {
                "Mod_CaveMouth", "Mod_Cavern", "Mod_Gallery",
                "Mod_Mine", "Mod_Grotto", "Mod_Forge", "Mod_Storeroom", "Mod_Well", "Mod_Arena", "Mod_GreatHall", "Mod_Lair"
            }),
            ("Sanctum", "Arcane Sanctum", new[]
            {
                "Mod_Stairwell", "Mod_Rotunda", "Mod_Gallery",
                "Mod_Alchemy", "Mod_Library", "Mod_Reliquary", "Mod_Vault", "Mod_Barrow", "Mod_Arena", "Mod_GreatHall", "Mod_Sanctum"
            }),
            ("Barrow", "Barrow", new[]
            {
                "Mod_CaveMouth", "Mod_Cavern", "Mod_Hall",
                "Mod_Barrow", "Mod_Ossuary", "Mod_Crypt", "Mod_Grotto", "Mod_Well", "Mod_Arena", "Mod_GreatHall", "Mod_Lair"
            }),
            ("Keep", "Keep", new[]
            {
                "Mod_Gatehouse", "Mod_Crossing", "Mod_Hall", "Mod_Gallery", "Mod_Guardroom",
                "Mod_Armoury", "Mod_Kitchen", "Mod_Barracks", "Mod_Library", "Mod_Arena", "Mod_GreatHall", "Mod_ThroneRoom"
            }),
            ("Hamlet", "Wren's Rest (hamlet)", new[]
            {
                "Mod_Square", "Mod_Street", "Mod_Townhouse", "Mod_Inn", "Mod_Smithy",
                "Mod_Bakery", "Mod_Stables", "Mod_Mill", "Mod_Market", "Mod_Manor"
            }),
            ("Village", "Dunmoor (village)", new[]
            {
                "Mod_Square", "Mod_Street", "Mod_Townhouse", "Mod_Inn", "Mod_Smithy",
                "Mod_Bakery", "Mod_Stables", "Mod_Mill", "Mod_Tavern", "Mod_Temple",
                "Mod_Alchemist", "Mod_GeneralStore", "Mod_Market", "Mod_Guildhouse", "Mod_Manor"
            }),
            ("Town", "Ashmere (town)", new[]
            {
                "Mod_Square", "Mod_Street", "Mod_Market", "Mod_Smithy", "Mod_Inn", "Mod_Tavern",
                "Mod_Temple", "Mod_Alchemist", "Mod_Bakery", "Mod_Tannery", "Mod_Stables",
                "Mod_Watchhouse", "Mod_Bathhouse", "Mod_Graveyard", "Mod_Bank",
                "Mod_Armorer", "Mod_GeneralStore", "Mod_ClothingStore", "Mod_PawnShop",
                "Mod_Guildhouse", "Mod_Townhouse", "Mod_Keep"
            }),
            ("City", "Highwall (city)", new[]
            {
                "Mod_Square", "Mod_Street", "Mod_GreatHall", "Mod_Market", "Mod_Smithy", "Mod_Inn",
                "Mod_Tavern", "Mod_Temple", "Mod_Alchemist", "Mod_Bakery", "Mod_Tannery",
                "Mod_Stables", "Mod_Watchhouse", "Mod_Bathhouse", "Mod_Graveyard", "Mod_Bank",
                "Mod_Mill", "Mod_Courthouse", "Mod_Armorer", "Mod_Bookseller", "Mod_ClothingStore",
                "Mod_FurnitureStore", "Mod_GemStore", "Mod_GeneralStore", "Mod_PawnShop",
                "Mod_Guildhouse", "Mod_Townhouse", "Mod_Palace"
            }),
            ("Harbour", "Saltmere (harbour)", new[]
            {
                "Mod_Square", "Mod_Dock", "Mod_Warehouse", "Mod_Fishmarket",
                "Mod_Shipyard", "Mod_Inn", "Mod_Tavern", "Mod_Townhouse", "Mod_Lighthouse"
            }),
            // A port of ~200 buildings has somewhere to pray, bank and be arrested, without any of
            // those its civic quarter is a grid of empty plots
            ("Port", "Kelder Wharf (port)", new[]
            {
                "Mod_Square", "Mod_Dock", "Mod_Street", "Mod_Warehouse", "Mod_Fishmarket",
                "Mod_Shipyard", "Mod_Inn", "Mod_Tavern", "Mod_Smithy", "Mod_Market",
                "Mod_Alchemist", "Mod_Stables", "Mod_Townhouse", "Mod_Lighthouse",
                "Mod_Temple", "Mod_Bank", "Mod_Watchhouse", "Mod_Guildhouse", "Mod_Bathhouse"
            }),
            ("PortCity", "Grand Anchor (port city)", new[]
            {
                "Mod_Square", "Mod_Dock", "Mod_Street", "Mod_GreatHall", "Mod_Warehouse",
                "Mod_Fishmarket", "Mod_Shipyard", "Mod_Market", "Mod_Inn", "Mod_Tavern",
                "Mod_Temple", "Mod_Alchemist", "Mod_Bakery", "Mod_Tannery", "Mod_Stables",
                "Mod_Watchhouse", "Mod_Bathhouse", "Mod_Bank", "Mod_Courthouse",
                "Mod_Armorer", "Mod_Bookseller", "Mod_ClothingStore", "Mod_GemStore",
                "Mod_GeneralStore", "Mod_PawnShop", "Mod_Guildhouse", "Mod_Townhouse", "Mod_Palace"
            }),
            ("Waterworks", "Waterworks", new[]
            {
                "Mod_Stairwell", "Mod_Cavern", "Mod_Gallery",
                "Mod_Cistern", "Mod_Well", "Mod_Grotto", "Mod_Storeroom", "Mod_Mine",
                "Mod_Arena", "Mod_GreatHall", "Mod_Sanctum"
            })
        };

        // Four creatures turn up anywhere, the rest belong to a place. Stats are flat by depth,
        // scaling was deliberately deferred until the prototype has been fought in
        static readonly string[] GeneralEnemies = { "Enemy_Rat", "Enemy_Spider", "Enemy_Bandit", "Enemy_Skeleton" };

        static readonly (string set, string[] locals, string boss)[] RosterPlans =
        {
            ("Crypt", new[] { "Enemy_Zombie", "Enemy_Ghoul", "Enemy_Wraith" }, "Enemy_BoneLord"),
            ("Barracks", new[] { "Enemy_Guard", "Enemy_Warhound" }, "Enemy_Warlord"),
            ("Undercroft", new[] { "Enemy_Kobold", "Enemy_Slime", "Enemy_Bat" }, "Enemy_BoneLord"),
            ("Monastery", new[] { "Enemy_Cultist", "Enemy_Wraith" }, "Enemy_Archmage"),
            ("Gaol", new[] { "Enemy_Guard", "Enemy_Warhound", "Enemy_Ghoul" }, "Enemy_Warlord"),
            ("Mine", new[] { "Enemy_Kobold", "Enemy_Slime", "Enemy_Bat" }, "Enemy_Warlord"),
            ("Sanctum", new[] { "Enemy_Cultist", "Enemy_Imp", "Enemy_Wraith" }, "Enemy_Archmage"),
            ("Barrow", new[] { "Enemy_Zombie", "Enemy_Ghoul", "Enemy_Wraith" }, "Enemy_BoneLord"),
            ("Keep", new[] { "Enemy_Guard", "Enemy_Warhound" }, "Enemy_Warlord"),
            ("Waterworks", new[] { "Enemy_Slime", "Enemy_Bat", "Enemy_Kobold" }, "Enemy_BoneLord")
        };

        // Grown, not laid out. Every number here was measured in a harness over five
        // seeds per settlement before it landed, see the ADR for the resulting building counts
        struct Growth
        {
            public Vector2 Extent;
            public float Square;
            public int Arteries;
            public int ArteryLength;
            public int Rings;
            public int Lanes;
            public float Civic;
            public float Relief;
        }

        static readonly (string set, bool walled, SettlementShore shore, Growth growth)[] SettlementPlans =
        {
            ("Hamlet", false, SettlementShore.Inland, new Growth
                { Extent = new Vector2(150f, 140f), Square = 13f, Arteries = 3, ArteryLength = 3,
                  Rings = 1, Lanes = 8, Civic = 0f, Relief = 2.5f }),
            ("Village", false, SettlementShore.Inland, new Growth
                { Extent = new Vector2(280f, 260f), Square = 17f, Arteries = 4, ArteryLength = 5,
                  Rings = 2, Lanes = 22, Civic = 0f, Relief = 4f }),
            ("Harbour", false, SettlementShore.Coastal, new Growth
                { Extent = new Vector2(280f, 270f), Square = 17f, Arteries = 4, ArteryLength = 5,
                  Rings = 2, Lanes = 22, Civic = 0f, Relief = 3f }),
            ("Town", true, SettlementShore.Inland, new Growth
                { Extent = new Vector2(480f, 450f), Square = 24f, Arteries = 6, ArteryLength = 7,
                  Rings = 4, Lanes = 60, Civic = 110f, Relief = 9f }),
            ("Port", true, SettlementShore.Coastal, new Growth
                { Extent = new Vector2(560f, 520f), Square = 26f, Arteries = 6, ArteryLength = 8,
                  Rings = 5, Lanes = 75, Civic = 110f, Relief = 7f }),
            ("City", true, SettlementShore.Inland, new Growth
                { Extent = new Vector2(920f, 860f), Square = 34f, Arteries = 9, ArteryLength = 13,
                  Rings = 9, Lanes = 190, Civic = 170f, Relief = 16f }),
            ("PortCity", true, SettlementShore.Coastal, new Growth
                { Extent = new Vector2(1000f, 920f), Square = 36f, Arteries = 9, ArteryLength = 14,
                  Rings = 9, Lanes = 200, Civic = 170f, Relief = 11f })
        };

        static bool IsSettlement(string set)
        {
            foreach ((string name, bool _, SettlementShore __, Growth ___) in SettlementPlans)
                if (name == set) return true;

            return false;
        }

        // The grand buildings, which want the civic grid rather than a crooked lane
        static readonly RoomPurpose[] CivicTrades =
        {
            RoomPurpose.Palace, RoomPurpose.Courthouse, RoomPurpose.Bank, RoomPurpose.Temple,
            RoomPurpose.Guildhouse, RoomPurpose.ThroneRoom, RoomPurpose.Manor,
            RoomPurpose.Watchhouse, RoomPurpose.Bathhouse, RoomPurpose.Library
        };

        static bool IsCivic(RoomPurpose purpose)
        {
            foreach (RoomPurpose grand in CivicTrades)
                if (grand == purpose) return true;

            return false;
        }

        static (EnemyDefinition[] roster, EnemyDefinition boss) Roster(string set,
            Dictionary<string, EnemyDefinition> bestiary)
        {
            foreach ((string name, string[] locals, string boss) in RosterPlans)
            {
                if (name != set)
                    continue;

                List<EnemyDefinition> roster = new List<EnemyDefinition>();

                foreach (string general in GeneralEnemies)
                    roster.Add(Load<EnemyDefinition>(general));

                foreach (string local in locals)
                    roster.Add(Load<EnemyDefinition>(local));

                return (roster.ToArray(), Load<EnemyDefinition>(boss));
            }

            return (new EnemyDefinition[0], null);
        }

        static Dictionary<string, RoomModuleDefinition> BuildModules()
        {
            Dictionary<string, RoomModuleDefinition> byName = new Dictionary<string, RoomModuleDefinition>();

            foreach ((string name, RoomRole[] roles, RoomPurpose purpose, int[] sides, Vector2 size) in RoomPlans)
            {
                BuildRoomPrefab(name, sides, size, Floor, purpose);

                int[] roleValues = new int[roles.Length];
                for (int i = 0; i < roles.Length; i++)
                    roleValues[i] = (int)roles[i];

                RoomModuleDefinition definition = Create<RoomModuleDefinition>($"Module_{name}");
                new AssetAuthoring(definition)
                    .Ref("prefab", Prefab(name))
                    .Int("weight", 1)
                    .Enum("purpose", (int)purpose)
                    .Enums("roles", roleValues)
                    .Save();

                byName[name] = definition;
            }

            return byName;
        }

        static void BuildCorridor(string name, int[] openSides, Vector2 size)
        {
            BuildRoomPrefab(name, openSides, size, CorridorFloor, RoomPurpose.None, 1);

            RoomModuleDefinition definition = Create<RoomModuleDefinition>($"Module_{name}");
            new AssetAuthoring(definition).Ref("prefab", Prefab(name)).Int("weight", 1).Enums("roles").Save();
        }

        // Descends one level over its run. Both connectors stay horizontal, so the room it feeds is
        // still axis aligned, only its height changes
        static RoomModuleDefinition BuildStair()
        {
            const string name = "Mod_Stair";

            GameObject root = new GameObject(name);

            float slope = Mathf.Sqrt(StairRun * StairRun + LevelDrop * LevelDrop);
            float angle = Mathf.Atan2(LevelDrop, StairRun) * Mathf.Rad2Deg;
            float half = StairRun * 0.5f;

            AddBox(root.transform, "Ramp", new Vector3(0f, -LevelDrop * 0.5f - WallThickness * 0.5f, 0f),
                Quaternion.Euler(angle, 0f, 0f), new Vector3(CorridorSize, WallThickness, slope), StairSurface);

            AddBox(root.transform, "Ceiling", new Vector3(0f, WallHeight - LevelDrop * 0.5f, 0f),
                Quaternion.Euler(angle, 0f, 0f), new Vector3(CorridorSize, WallThickness, slope), Ceiling);

            float sideHeight = WallHeight + LevelDrop;
            float sideY = (WallHeight - LevelDrop) * 0.5f;

            AddBox(root.transform, "Side_L", new Vector3(-CorridorSize * 0.5f, sideY, 0f), Quaternion.identity,
                new Vector3(WallThickness, sideHeight, StairRun), StairSurface);

            AddBox(root.transform, "Side_R", new Vector3(CorridorSize * 0.5f, sideY, 0f), Quaternion.identity,
                new Vector3(WallThickness, sideHeight, StairRun), StairSurface);

            AddTorch(root.transform, new Vector3(-CorridorSize * 0.5f + 0.3f, TorchHeight, -half + 1f),
                Quaternion.LookRotation(Vector3.right));

            AddTorch(root.transform, new Vector3(CorridorSize * 0.5f - 0.3f, TorchHeight - LevelDrop, half - 1f),
                Quaternion.LookRotation(Vector3.left));

            GameObject top = new GameObject("Wall_Top");
            top.transform.SetParent(root.transform, false);
            top.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, -half), Quaternion.LookRotation(Vector3.back));
            BuildDoorway(top.transform, CorridorSize);

            GameObject entry = new GameObject("Connector_Top");
            entry.transform.SetParent(root.transform, false);
            entry.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, -half), Quaternion.LookRotation(Vector3.back));
            entry.AddComponent<ModuleConnector>();

            GameObject bottom = new GameObject("Wall_Bottom");
            bottom.transform.SetParent(root.transform, false);
            bottom.transform.SetLocalPositionAndRotation(new Vector3(0f, -LevelDrop, half), Quaternion.LookRotation(Vector3.forward));
            BuildDoorway(bottom.transform, CorridorSize);

            GameObject exit = new GameObject("Connector_Bottom");
            exit.transform.SetParent(root.transform, false);
            exit.transform.SetLocalPositionAndRotation(new Vector3(0f, -LevelDrop, half), Quaternion.LookRotation(Vector3.forward));
            exit.AddComponent<ModuleConnector>();

            GameObject prefab = Finish(root, name,
                new Bounds(new Vector3(0f, sideY, 0f), new Vector3(CorridorSize, sideHeight, StairRun)));

            RoomModuleDefinition definition = Create<RoomModuleDefinition>($"Module_{name}");
            new AssetAuthoring(definition).Ref("prefab", Prefab(name)).Int("weight", 1).Enums("roles").Save();

            return definition;
        }

        static SpellDefinition Spell(string assetName, string id, string display, SpellKind kind, int cost,
            float cooldown, int power, float duration, float speed, Color colour,
            SkillId school = SkillId.Destruction, string circle = null)
        {
            SpellDefinition spell = Create<SpellDefinition>(assetName);
            new AssetAuthoring(spell)
                .Str("id", id).Str("displayName", display)
                .Enum("kind", (int)kind)
                .Int("manaCost", cost).Float("cooldown", cooldown)
                .Int("power", power).Float("duration", duration)
                .Float("projectileSpeed", speed)
                .Colour("colour", colour)
                .Enum("school", (int)school)
                .Ref("tradition", circle == null ? null : Load<FactionDefinition>($"Circle_{circle}"))
                .Save();

            return spell;
        }

        // The nine, as factions, because that is what they are, rank, standing, services and a
        // membership test the spell system already knows how to ask. Their tradition is
        // what gates learning, and no two of them will teach the same working
        static readonly (string key, string name, string where, SkillId school, string blurb)[] CirclePlans =
        {
            ("Lamplighters", "The Lamplighters", "Highwall", SkillId.Restoration,
                "Healers. Will teach anyone who has never killed with a spell, and they check."),
            ("DenrowsTable", "Denrow's Table", "Tallow Cross", SkillId.Mysticism,
                "Scholars. Hold the largest surviving fragment of the Denrow Account."),
            ("NinthChair", "The Ninth Chair", "Casrenne", SkillId.Illusion,
                "Patricians. Will not teach you; will use you."),
            ("QuietLedger", "The Quiet Ledger", "Verrin", SkillId.Alteration,
                "Merchants. Sell scrolls, train nobody \u2014 a trained mage is a competitor."),
            ("SaltmereCoil", "Saltmere Coil", "Saltmere", SkillId.Alteration,
                "Fisher-folk. Their working is older than the imperial schools and does not map onto them."),
            ("HaskWidows", "The Hask Widows", "Hask", SkillId.Restoration,
                "Nine women, hereditary, two hundred years. They admit nobody."),
            ("Ashwake", "Ashwake", "Marrowgate", SkillId.Destruction,
                "Children of the Choir's victims, who learned anyway. Will help you and despise you for needing it."),
            ("ThirdTestament", "The Third Testament", "Colder", SkillId.Mysticism,
                "Believe the Choir was a sacrament that half-worked and should be finished."),
            ("Unlit", "The Unlit", "no fixed home", SkillId.Illusion,
                "Couriers between the other eight, and distrusted by all of them for it.")
        };

        // Six of the nine set tests the game cannot yet ask: an escort at night, an unread letter
        // carried across a province, a favour done for a senator without being thanked. Those get no
        // door rather than a pretend one, and the seventh (Hask) admits nobody by design. What is
        // left is what is here, and of these, only a circle whose home settlement actually exists
        // can be placed at all
        static readonly (string circle, string set, string contact, CircleTest test)[] CircleDoors =
        {
            ("Lamplighters", "City", "Wenna Ford", CircleTest.NeverKilledWithMagic),
            ("ThirdTestament", "Colder", "Fen Marrow", CircleTest.TakePart)
        };

        static void BuildCircleDoorPrefabs()
        {
            foreach ((string key, string _set, string contact, CircleTest test) in CircleDoors)
            {
                FactionDefinition circle = Load<FactionDefinition>($"Circle_{key}");
                if (circle == null)
                    continue;

                GameObject door = new GameObject($"CircleDoor_{key}");

                AddVisual(door.transform, "Leaf", new Vector3(0f, 1.05f, 0f), Quaternion.identity,
                    new Vector3(0.9f, 2.1f, 0.12f), Surface("Mat_CircleDoor", new Color(0.18f, 0.16f, 0.14f)));

                BoxCollider trigger = door.AddComponent<BoxCollider>();
                trigger.size = new Vector3(1.2f, 2.2f, 1.2f);
                trigger.center = new Vector3(0f, 1.1f, 0.3f);

                new AssetAuthoring(door.AddComponent<CircleDoor>())
                    .Ref("circle", circle)
                    .Str("contact", contact)
                    .Enum("test", (int)test)
                    .Save();

                PrefabUtility.SaveAsPrefabAsset(door, $"{PrefabRoot}/CircleDoor_{key}.prefab");
                Object.DestroyImmediate(door);
            }
        }

        static Object CircleDoorFor(string set)
        {
            foreach ((string key, string home, string _c, CircleTest _t) in CircleDoors)
                if (home == set)
                    return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/CircleDoor_{key}.prefab");

            return null;
        }

        // The Gravewardens plus the nine circles, loaded by path so an import cannot null them
        static Object[] AllFactions(FactionDefinition guild)
        {
            List<Object> all = new List<Object> { Load<FactionDefinition>("Faction_Gravewardens") };

            foreach ((string key, string _n, string _w, SkillId _s, string _b) in CirclePlans)
            {
                FactionDefinition circle = Load<FactionDefinition>($"Circle_{key}");
                if (circle != null) all.Add(circle);
            }

            return all.ToArray();
        }

        static void BuildCircles()
        {
            foreach ((string key, string name, string where, SkillId school, string blurb) in CirclePlans)
            {
                FactionDefinition circle = Create<FactionDefinition>($"Circle_{key}");
                new AssetAuthoring(circle)
                    .Str("id", $"circle.{key.ToLowerInvariant()}")
                    .Str("displayName", name)
                    .Str("description", $"{blurb}  ({where}; {school} by the old classification.)")
                    .Int("startingStanding", -40)
                    .Bool("isGuild", true)
                    .Int("joinAtStanding", 10)
                    .Enums("services", (int)GuildService.Training, (int)GuildService.Spellmaking)
                    .Save();
            }

            // They dislike each other. Standing with one bleeds into the rest, which is the whole
            // reason a player cannot simply collect all nine
            foreach ((string key, string _n, string _w, SkillId _s, string _b) in CirclePlans)
            {
                List<Object> rivals = new List<Object>();

                foreach ((string other, string _n2, string _w2, SkillId _s2, string _b2) in CirclePlans)
                    if (other != key) rivals.Add(Load<FactionDefinition>($"Circle_{other}"));

                new AssetAuthoring(Load<FactionDefinition>($"Circle_{key}"))
                    .Refs("rivals", rivals.ToArray())
                    .Float("rivalBleed", 0.35f)
                    .Save();
            }
        }

        // First person hands. No colliders on any of it, these sit centimetres from the camera and
        // would otherwise shove the player around
        static void BuildViewmodelPrefabs()
        {
            Material steel = Surface("Mat_Steel", new Color(0.66f, 0.68f, 0.72f), 0.55f);
            Material leather = Surface("Mat_Leather", new Color(0.28f, 0.18f, 0.11f));
            Material oak = Surface("Mat_Oak", new Color(0.40f, 0.27f, 0.15f));

            // Kept named Vm_Sword because the scene builder and the poses both know it by that
            // name, what changed is that it now holds one shape per weapon class and shows the one
            // matching what is in your hand. The child order IS the contract, PlayerViewmodel
            // indexes it by WeaponClass, so it is taken from the enum, never written out
            GameObject sword = new GameObject("Vm_Sword");

            foreach (WeaponClass weapon in System.Enum.GetValues(typeof(WeaponClass)))
            {
                GameObject shape = new GameObject($"Shape_{weapon}");
                shape.transform.SetParent(sword.transform, false);
                GreyboxSilhouettes.WeaponShape(shape.transform, weapon, steel, leather);
                shape.SetActive(weapon == WeaponClass.Blade);
            }

            SaveViewmodel(sword);

            GameObject shield = new GameObject("Vm_Shield");
            AddVisual(shield.transform, "Face", Vector3.zero, Quaternion.identity,
                new Vector3(0.42f, 0.54f, 0.05f), oak);
            AddVisual(shield.transform, "Boss", new Vector3(0f, 0f, -0.04f), Quaternion.identity,
                new Vector3(0.14f, 0.14f, 0.05f), steel);
            SaveViewmodel(shield);

            GameObject focus = new GameObject("Vm_Focus");
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Orb";
            orb.transform.SetParent(focus.transform, false);
            orb.transform.localScale = Vector3.one * 0.16f;
            orb.GetComponent<MeshRenderer>().sharedMaterial = Emissive("Mat_Focus", new Color(0.7f, 0.8f, 1f), 6f);
            Object.DestroyImmediate(orb.GetComponent<Collider>());
            SaveViewmodel(focus);
        }

        static void SaveViewmodel(GameObject root)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{root.name}.prefab");
            Object.DestroyImmediate(root);
        }

        internal static GameObject AddVisual(Transform parent, string name, Vector3 position, Quaternion rotation,
            Vector3 scale, Material material)
        {
            GameObject box = AddBox(parent, name, position, rotation, scale, material);
            Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }

        struct Part
        {
            public Vector3 Centre;
            public Quaternion Rotation;
            public Vector3 Size;
            public int Material;
        }

        // A way into a dungeon, standing on the open world, and the stair back out of one. Both are
        // greybox, a lintel you can see from a distance and walk up to
        static void BuildDoorwayPrefabs()
        {
            Surface("Mat_OpenWater", new Color(0.16f, 0.27f, 0.33f), 0.85f);
            Material stone = Surface("Mat_DoorStone", new Color(0.33f, 0.32f, 0.30f));
            Material dark = Surface("Mat_DoorDark", new Color(0.05f, 0.05f, 0.06f));

            GameObject arch = new GameObject("Mod_DungeonEntrance");
            AddVisual(arch.transform, "JambLeft", new Vector3(-1.7f, 1.8f, 0f), Quaternion.identity,
                new Vector3(0.9f, 3.6f, 1.2f), stone);
            AddVisual(arch.transform, "JambRight", new Vector3(1.7f, 1.8f, 0f), Quaternion.identity,
                new Vector3(0.9f, 3.6f, 1.2f), stone);
            AddVisual(arch.transform, "Lintel", new Vector3(0f, 3.9f, 0f), Quaternion.identity,
                new Vector3(4.3f, 0.9f, 1.4f), stone);
            AddVisual(arch.transform, "Mound", new Vector3(0f, 0.5f, -1.6f), Quaternion.identity,
                new Vector3(6.2f, 1.0f, 3.2f), stone);
            AddVisual(arch.transform, "Dark", new Vector3(0f, 1.7f, 0.35f), Quaternion.identity,
                new Vector3(2.5f, 3.4f, 0.3f), dark);

            // A thin panel across the opening, not a volume around it. Interaction is a ray from the eye
            // and a ray that starts inside a collider never hits it, so the old 5 m box lost its prompt
            // as soon as you stepped close enough to be standing in it
            BoxCollider reach = arch.AddComponent<BoxCollider>();
            reach.isTrigger = true;
            reach.size = new Vector3(2.6f, 3.4f, 0.8f);
            reach.center = new Vector3(0f, 1.7f, 0.35f);

            arch.AddComponent<DungeonEntrance>();
            PrefabUtility.SaveAsPrefabAsset(arch, $"{PrefabRoot}/Mod_DungeonEntrance.prefab");
            Object.DestroyImmediate(arch);

            // A door set against a wall of the entrance room, standing on the floor, its face towards
            // the room. The pale leaf is what tells it from every other door down there
            GameObject exit = new GameObject("Mod_DungeonExit");
            AddVisual(exit.transform, "JambLeft", new Vector3(-0.95f, 1.35f, 0.1f), Quaternion.identity,
                new Vector3(0.3f, 2.7f, 0.3f), stone);
            AddVisual(exit.transform, "JambRight", new Vector3(0.95f, 1.35f, 0.1f), Quaternion.identity,
                new Vector3(0.3f, 2.7f, 0.3f), stone);
            AddVisual(exit.transform, "Lintel", new Vector3(0f, 2.85f, 0.1f), Quaternion.identity,
                new Vector3(2.2f, 0.3f, 0.3f), stone);
            AddVisual(exit.transform, "Leaf", new Vector3(0f, 1.3f, 0.05f), Quaternion.identity,
                new Vector3(1.6f, 2.6f, 0.1f), Surface("Mat_DoorDay", new Color(0.72f, 0.74f, 0.68f)));

            BoxCollider leaf = exit.AddComponent<BoxCollider>();
            leaf.isTrigger = true;
            leaf.size = new Vector3(1.6f, 2.6f, 0.4f);
            leaf.center = new Vector3(0f, 1.3f, 0.1f);

            exit.AddComponent<DungeonExit>();
            PrefabUtility.SaveAsPrefabAsset(exit, $"{PrefabRoot}/Mod_DungeonExit.prefab");
            Object.DestroyImmediate(exit);
        }

        static void BuildPlantPrefabs()
        {
            Material bark = Surface("Mat_Bark", new Color(0.29f, 0.23f, 0.17f));
            Material birchBark = Surface("Mat_BirchBark", new Color(0.72f, 0.70f, 0.64f));
            Material leaf = Surface("Mat_Leaf", new Color(0.24f, 0.35f, 0.18f));
            Material lightLeaf = Surface("Mat_LeafLight", new Color(0.38f, 0.47f, 0.22f));
            Material willowLeaf = Surface("Mat_LeafWillow", new Color(0.30f, 0.42f, 0.26f));
            Material needle = Surface("Mat_Needle", new Color(0.17f, 0.28f, 0.21f));
            Material dead = Surface("Mat_DeadWood", new Color(0.42f, 0.38f, 0.31f));
            Material scrub = Surface("Mat_Scrub", new Color(0.31f, 0.34f, 0.21f));
            Material reed = Surface("Mat_Reed", new Color(0.44f, 0.44f, 0.26f));
            Material sedge = Surface("Mat_Sedge", new Color(0.38f, 0.42f, 0.24f));
            Material heather = Surface("Mat_Heather", new Color(0.40f, 0.29f, 0.35f));
            Material gorse = Surface("Mat_Gorse", new Color(0.52f, 0.48f, 0.16f));
            Material thorn = Surface("Mat_Thorn", new Color(0.36f, 0.34f, 0.26f));
            Material juniper = Surface("Mat_Juniper", new Color(0.20f, 0.30f, 0.24f));
            Material bramble = Surface("Mat_Bramble", new Color(0.26f, 0.30f, 0.19f));
            Material fern = Surface("Mat_Fern", new Color(0.28f, 0.40f, 0.21f));

            Plant(WorldVegetation.Plant.Broadleaf, new[] { bark, leaf }, 0.45f, 4.2f, new List<Part>
            {
                Trunk(0.45f, 4.2f),
                Box(0f, 5.4f, 0f, 4.6f, 3.4f, 4.6f, 1),
                Box(0.4f, 6.8f, -0.3f, 3.2f, 2.4f, 3.2f, 1)
            });

            Plant(WorldVegetation.Plant.Pine, new[] { bark, needle }, 0.38f, 5.0f, new List<Part>
            {
                Trunk(0.38f, 5.0f),
                Box(0f, 4.4f, 0f, 3.4f, 2.2f, 3.4f, 1),
                Box(0f, 6.2f, 0f, 2.4f, 2.2f, 2.4f, 1),
                Box(0f, 7.8f, 0f, 1.3f, 2.0f, 1.3f, 1)
            });

            Plant(WorldVegetation.Plant.Birch, new[] { birchBark, lightLeaf }, 0.28f, 5.4f, new List<Part>
            {
                Trunk(0.28f, 5.4f),
                Box(0f, 6.2f, 0f, 2.9f, 2.6f, 2.9f, 1),
                Box(-0.3f, 7.4f, 0.2f, 2.0f, 1.8f, 2.0f, 1)
            });

            Plant(WorldVegetation.Plant.Willow, new[] { bark, willowLeaf }, 0.52f, 2.8f, new List<Part>
            {
                Trunk(0.52f, 2.8f),
                Box(0f, 3.9f, 0f, 5.4f, 1.9f, 5.4f, 1),
                Box(0f, 3.0f, 0f, 4.4f, 1.4f, 4.4f, 1)
            });

            Plant(WorldVegetation.Plant.Dead, new[] { dead }, 0.34f, 3.6f, new List<Part>
            {
                Trunk(0.34f, 3.6f),
                Turned(0.7f, 3.4f, 0f, 0f, 0f, 38f, 1.8f, 0.18f, 0.18f, 0),
                Turned(-0.6f, 4.2f, 0.2f, 0f, 40f, -32f, 1.5f, 0.16f, 0.16f, 0)
            });

            Plant(WorldVegetation.Plant.Bush, new[] { scrub }, 0f, 0f, new List<Part>
            {
                Box(0f, 0.55f, 0f, 1.5f, 1.1f, 1.4f, 0),
                Turned(0.5f, 0.9f, 0.2f, 0f, 30f, 0f, 1.0f, 0.8f, 0.9f, 0)
            });

            Plant(WorldVegetation.Plant.Bramble, new[] { bramble }, 0f, 0f, new List<Part>
            {
                Box(0f, 0.34f, 0f, 2.2f, 0.68f, 2.0f, 0),
                Turned(0.6f, 0.55f, -0.4f, 0f, 24f, 0f, 1.3f, 0.5f, 1.2f, 0)
            });

            Plant(WorldVegetation.Plant.Fern, new[] { fern }, 0f, 0f, Fronds(5, 0.62f, 0.20f, fernSpread: 0.30f));

            Plant(WorldVegetation.Plant.Reeds, new[] { reed }, 0f, 0f, Fronds(5, 1.70f, 0.09f, fernSpread: 0.22f));

            Plant(WorldVegetation.Plant.Sedge, new[] { sedge }, 0f, 0f, Fronds(6, 0.78f, 0.11f, fernSpread: 0.26f));

            Plant(WorldVegetation.Plant.Heather, new[] { heather }, 0f, 0f, new List<Part>
            {
                Box(0f, 0.20f, 0f, 1.25f, 0.40f, 1.15f, 0),
                Turned(0.35f, 0.28f, 0.25f, 0f, 35f, 0f, 0.85f, 0.34f, 0.80f, 0)
            });

            Plant(WorldVegetation.Plant.Gorse, new[] { gorse }, 0f, 0f, new List<Part>
            {
                Box(0f, 0.48f, 0f, 1.30f, 0.96f, 1.20f, 0),
                Turned(-0.30f, 0.78f, 0.20f, 0f, 42f, 0f, 0.85f, 0.70f, 0.80f, 0)
            });

            Plant(WorldVegetation.Plant.Thorn, new[] { thorn }, 0f, 0f, new List<Part>
            {
                Turned(0f, 0.62f, 0f, 0f, 0f, 9f, 0.16f, 1.24f, 0.16f, 0),
                Turned(0.28f, 0.50f, 0.16f, 0f, 55f, -14f, 0.14f, 1.00f, 0.14f, 0),
                Turned(-0.24f, 0.42f, -0.18f, 0f, 110f, 12f, 0.13f, 0.84f, 0.13f, 0)
            });

            Plant(WorldVegetation.Plant.Juniper, new[] { juniper }, 0f, 0f, new List<Part>
            {
                Box(0f, 0.70f, 0f, 1.5f, 1.40f, 1.4f, 0),
                Box(0f, 1.55f, 0f, 0.9f, 0.80f, 0.9f, 0)
            });
        }

        static List<Part> Fronds(int count, float height, float thickness, float fernSpread)
        {
            List<Part> parts = new List<Part>();

            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count);

                parts.Add(new Part
                {
                    Centre = Quaternion.Euler(0f, angle, 0f) * new Vector3(fernSpread, height * 0.5f, 0f),
                    Rotation = Quaternion.Euler(i % 2 == 0 ? 9f : -7f, angle, 6f),
                    Size = new Vector3(thickness, height, thickness),
                    Material = 0
                });
            }

            return parts;
        }

        static Part Box(float x, float y, float z, float w, float h, float d, int material) => new Part
        {
            Centre = new Vector3(x, y, z),
            Rotation = Quaternion.identity,
            Size = new Vector3(w, h, d),
            Material = material
        };

        static Part Turned(float x, float y, float z, float rx, float ry, float rz,
            float w, float h, float d, int material) => new Part
        {
            Centre = new Vector3(x, y, z),
            Rotation = Quaternion.Euler(rx, ry, rz),
            Size = new Vector3(w, h, d),
            Material = material
        };

        static Part Trunk(float radius, float height) => new Part
        {
            Centre = new Vector3(0f, height * 0.5f, 0f),
            Rotation = Quaternion.identity,
            Size = new Vector3(radius * 2f, height, radius * 2f),
            Material = 0
        };

        static void Plant(WorldVegetation.Plant kind, Material[] materials, float trunkRadius,
            float trunkHeight, List<Part> parts)
        {
            string name = $"Plant_{kind}";
            Mesh mesh = Combine(parts, materials.Length);
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, $"{ContentRoot}/Mesh_{name}.asset");

            GameObject root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            root.GetComponent<MeshFilter>().sharedMesh =
                AssetDatabase.LoadAssetAtPath<Mesh>($"{ContentRoot}/Mesh_{name}.asset");
            root.GetComponent<MeshRenderer>().sharedMaterials = materials;

            // Only trees stop you. A collider on every reed would cost far more than it buys
            if (trunkHeight > 0f)
            {
                CapsuleCollider trunk = root.AddComponent<CapsuleCollider>();
                trunk.radius = Mathf.Max(0.3f, trunkRadius);
                trunk.height = trunkHeight;
                trunk.center = new Vector3(0f, trunkHeight * 0.5f, 0f);
            }

            PlantLod(root);

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        // Terrain treats a tree prefab with no LODGroup as a Tree Editor tree, and Tree Editor trees
        // are Built in pipeline only, hence the Nature/Soft Occlusion warning, one per prototype
        // every time a tile assigns them. With an LODGroup the terrain draws the prefab's own
        // renderers, which is the only tree path HDRP has
        static void PlantLod(GameObject root)
        {
            LODGroup group = root.GetComponent<LODGroup>();

            if (group == null)
                group = root.AddComponent<LODGroup>();

            group.SetLODs(new[] { new LOD(0.02f, root.GetComponentsInChildren<Renderer>()) });
            group.RecalculateBounds();
        }

        // The same LODGroup, added to plant prefabs that already exist, so fixing the warning does
        // not mean regenerating, and reassigning, every greybox asset in the project
        [MenuItem("Endless Descent/Fix Plant LOD Groups")]
        public static void FixPlantLods()
        {
            // Rule 11: it rewrites prefabs, so it says which before it does
            int kinds = System.Enum.GetValues(typeof(WorldVegetation.Plant)).Length;

            if (!EditorUtility.DisplayDialog("Fix plant LOD groups",
                    $"Rewrites the {kinds} Plant_* prefabs in {PrefabRoot} in place, giving each an LODGroup. " +
                    "Their GUIDs are kept, so nothing that uses them breaks. Nothing else is touched.\n\n" +
                    "Commit first if you have unsaved work.", "Fix", "Cancel"))
                return;

            int patched = 0;

            foreach (WorldVegetation.Plant kind in System.Enum.GetValues(typeof(WorldVegetation.Plant)))
            {
                string path = $"{PrefabRoot}/Plant_{kind}.prefab";

                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogError($"Missing plant prefab {path}. Rebuild the greybox content.");
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    PlantLod(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    patched++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            Debug.Log($"Gave {patched} plant prefabs an LODGroup.");
        }

        // Boxes welded into one mesh, one submesh per material, built from Unity's own cube rather
        // than from hand written vertices. Winding decides which way a face points, a face pointing
        // the wrong way is invisible, and invisible is precisely the symptom being chased, so the
        // one thing this must not do is guess at it
        static Mesh Combine(List<Part> parts, int materialCount)
        {
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cube = probe.GetComponent<MeshFilter>().sharedMesh;

            Vector3[] cubeVertices = cube.vertices;
            Vector3[] cubeNormals = cube.normals;
            Vector2[] cubeUvs = cube.uv;
            int[] cubeTriangles = cube.triangles;

            Object.DestroyImmediate(probe);

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int>[] triangles = new List<int>[materialCount];

            for (int i = 0; i < materialCount; i++)
                triangles[i] = new List<int>();

            foreach (Part part in parts)
            {
                int baseIndex = vertices.Count;

                for (int i = 0; i < cubeVertices.Length; i++)
                {
                    vertices.Add(part.Centre + part.Rotation * Vector3.Scale(cubeVertices[i], part.Size));
                    normals.Add(part.Rotation * cubeNormals[i]);
                    uvs.Add(i < cubeUvs.Length ? cubeUvs[i] : Vector2.zero);
                }

                List<int> into = triangles[Mathf.Clamp(part.Material, 0, materialCount - 1)];

                foreach (int index in cubeTriangles)
                    into.Add(baseIndex + index);
            }

            Mesh mesh = new Mesh { subMeshCount = materialCount };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);

            for (int i = 0; i < materialCount; i++)
                mesh.SetTriangles(triangles[i], i);

            mesh.RecalculateBounds();
            return mesh;
        }

        // Traps read by silhouette, spikes underfoot, a dart slit at chest height, a blade in the
        // ceiling, a vent for gas. All four share one trap component, only the looks differ
        static void BuildTrapPrefabs()
        {
            Material iron = Surface("Mat_TrapIron", new Color(0.34f, 0.33f, 0.36f), 0.5f);
            Material warn = Surface("Mat_TrapWarn", new Color(0.55f, 0.30f, 0.22f));

            Trap(TrapKind.Spikes, "Trap_Spikes", 14, iron, warn, root =>
            {
                GameObject spikes = new GameObject("Spikes");
                spikes.transform.SetParent(root.transform, false);
                spikes.transform.localPosition = new Vector3(0f, -0.55f, 0f);

                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 0.4f;
                    AddVisual(spikes.transform, $"Spike_{i}",
                        new Vector3(Mathf.Cos(a) * 0.35f, 0.3f, Mathf.Sin(a) * 0.35f), Quaternion.identity,
                        new Vector3(0.12f, 0.7f, 0.12f), iron);
                }

                return spikes.transform;
            });

            Trap(TrapKind.Dart, "Trap_Dart", 10, iron, warn, root =>
            {
                AddVisual(root.transform, "Slit", new Vector3(0f, 1.3f, 0f), Quaternion.identity,
                    new Vector3(0.9f, 0.12f, 0.12f), iron);
                return null;
            });

            Trap(TrapKind.Blade, "Trap_Blade", 18, iron, warn, root =>
            {
                GameObject blade = AddVisual(root.transform, "Blade", new Vector3(0f, 2.4f, 0f),
                    Quaternion.identity, new Vector3(1f, 0.9f, 0.08f), iron);
                return blade.transform;
            });

            PoisonNext = true;
            Trap(TrapKind.Gas, "Trap_Gas", 8, iron, warn, root =>
            {
                AddVisual(root.transform, "Vent", new Vector3(0f, 0.06f, 0f), Quaternion.identity,
                    new Vector3(0.7f, 0.12f, 0.7f), warn);
                return null;
            });
        }

        static bool PoisonNext;

        static void Trap(TrapKind kind, string name, int damage, Material iron, Material warn,
            System.Func<GameObject, Transform> build)
        {
            GameObject root = new GameObject(name);

            // A metre square, and set to one side of a corridor by the populator, so there is always a
            // lane past it. At 1.6 m in the middle of a 4 m corridor there was barely a body's width
            AddVisual(root.transform, "Plate", new Vector3(0f, 0.03f, 0f), Quaternion.identity,
                new Vector3(1f, 0.06f, 1f), warn);

            Transform moving = build(root);

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1f, 2.4f, 1f);
            trigger.center = new Vector3(0f, 1.2f, 0f);

            EndlessDescent.World.Trap trap = root.AddComponent<EndlessDescent.World.Trap>();
            AssetAuthoring author = new AssetAuthoring(trap)
                .Enum("kind", (int)kind)
                .Int("damage", damage)
                .Float("rearmSeconds", 3f);

            if (moving != null)
                author.Ref("movingPart", moving);

            if (PoisonNext)
            {
                author.Enum("inflicts", (int)EndlessDescent.Core.AfflictionId.Poison);
                PoisonNext = false;
            }

            author.Save();

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        // A wall panel on a secret edge. Tinted while revealForTesting is on so secrets can be found
        // deliberately during testing rather than by brushing every wall in the dungeon
        static readonly (string name, SkillId skill, int upTo)[] TrainerPlans =
        {
            ("Trainer_Longsword", SkillId.Longsword, 60),
            ("Trainer_Block", SkillId.Block, 55),
            ("Trainer_Restoration", SkillId.Restoration, 55),
            ("Trainer_Destruction", SkillId.Destruction, 55),
            ("Trainer_Mercantile", SkillId.Mercantile, 50),
            ("Trainer_Stealth", SkillId.Stealth, 50)
        };

        // What you find inside a building of each trade
        static string ServiceFor(RoomPurpose purpose)
        {
            switch (purpose)
            {
                case RoomPurpose.Smithy: return "RepairBench";
                case RoomPurpose.Armorer: return "Trainer_Longsword";
                case RoomPurpose.Watchhouse: return "Trainer_Block";
                case RoomPurpose.Temple: return "Trainer_Restoration";
                case RoomPurpose.Bookseller: return "Trainer_Destruction";
                case RoomPurpose.PawnShop: return "Trainer_Mercantile";
                case RoomPurpose.Tannery: return "Trainer_Stealth";
                case RoomPurpose.Guildhouse: return "GuildHall";
                case RoomPurpose.Alchemist: return "Spellmaker";
                case RoomPurpose.Library: return "EnchantingTable";
                case RoomPurpose.Inn:
                case RoomPurpose.Tavern: return "Bed";
                default: return null;
            }
        }

        static void BuildFurnishingPrefabs()
        {
            Material cloth = Surface("Mat_Bedroll", new Color(0.44f, 0.30f, 0.22f));
            Material stone = Surface("Mat_Firepit", new Color(0.26f, 0.25f, 0.26f));
            Material iron = Surface("Mat_TrapIron", new Color(0.34f, 0.33f, 0.36f), 0.5f);

            GameObject camp = new GameObject("Camp");
            AddVisual(camp.transform, "Bedroll", new Vector3(-1f, 0.12f, 0f), Quaternion.identity,
                new Vector3(0.9f, 0.24f, 2.1f), cloth);
            AddVisual(camp.transform, "Firepit", new Vector3(0.6f, 0.15f, 0f), Quaternion.identity,
                new Vector3(1.1f, 0.3f, 1.1f), stone);
            AddVisual(camp.transform, "Embers", new Vector3(0.6f, 0.32f, 0f), Quaternion.identity,
                new Vector3(0.5f, 0.18f, 0.5f), Emissive("Mat_Embers", new Color(1f, 0.45f, 0.18f), 5f));

            BoxCollider campTrigger = camp.AddComponent<BoxCollider>();
            campTrigger.isTrigger = true;
            campTrigger.size = new Vector3(3f, 1.6f, 2.6f);
            campTrigger.center = new Vector3(-0.2f, 0.8f, 0f);

            new AssetAuthoring(camp.AddComponent<RestPoint>())
                .Bool("comfortable", false).Int("hours", 6).Save();

            PrefabUtility.SaveAsPrefabAsset(camp, $"{PrefabRoot}/Camp.prefab");
            Object.DestroyImmediate(camp);

            GameObject bench = new GameObject("RepairBench");
            AddVisual(bench.transform, "Bench", new Vector3(0f, 0.45f, 0f), Quaternion.identity,
                new Vector3(2.2f, 0.9f, 1f), cloth);
            AddVisual(bench.transform, "Anvil", new Vector3(0f, 1.1f, 0f), Quaternion.identity,
                new Vector3(1f, 0.5f, 0.5f), iron);

            BoxCollider benchTrigger = bench.AddComponent<BoxCollider>();
            benchTrigger.size = new Vector3(2.4f, 1.6f, 1.4f);
            benchTrigger.center = new Vector3(0f, 0.8f, 0f);

            bench.AddComponent<RepairBench>();

            PrefabUtility.SaveAsPrefabAsset(bench, $"{PrefabRoot}/RepairBench.prefab");
            Object.DestroyImmediate(bench);

            // A hermit who teaches. Placed in dungeons only because there are no towns yet, this is
            // a testing location, not where trainers belong
            GameObject trainer = new GameObject("Trainer");
            AddVisual(trainer.transform, "Figure", new Vector3(0f, 0.9f, 0f), Quaternion.identity,
                new Vector3(0.6f, 1.8f, 0.6f), Surface("Mat_Trainer", new Color(0.30f, 0.34f, 0.44f)));

            BoxCollider trainerTrigger = trainer.AddComponent<BoxCollider>();
            trainerTrigger.size = new Vector3(1.2f, 1.8f, 1.2f);
            trainerTrigger.center = new Vector3(0f, 0.9f, 0f);

            new AssetAuthoring(trainer.AddComponent<Trainer>())
                .Enum("skill", (int)SkillId.Longsword).Int("teachesUpTo", 50).Save();

            PrefabUtility.SaveAsPrefabAsset(trainer, $"{PrefabRoot}/Trainer.prefab");
            Object.DestroyImmediate(trainer);

            // One per trade a town has room for, so which skills you can pay for depends on where you
            // are rather than being the same everywhere
            foreach ((string name, SkillId skill, int upTo) in TrainerPlans)
            {
                GameObject teacher = new GameObject(name);
                AddVisual(teacher.transform, "Figure", new Vector3(0f, 0.9f, 0f), Quaternion.identity,
                    new Vector3(0.6f, 1.8f, 0.6f), Surface($"Mat_{name}", Color.HSVToRGB(((int)skill * 0.618f) % 1f, 0.4f, 0.6f)));

                BoxCollider stance = teacher.AddComponent<BoxCollider>();
                stance.size = new Vector3(1.2f, 1.8f, 1.2f);
                stance.center = new Vector3(0f, 0.9f, 0f);

                new AssetAuthoring(teacher.AddComponent<Trainer>())
                    .Enum("skill", (int)skill).Int("teachesUpTo", upTo).Save();

                PrefabUtility.SaveAsPrefabAsset(teacher, $"{PrefabRoot}/{name}.prefab");
                Object.DestroyImmediate(teacher);
            }

            Workbench("Constable", new Color(0.34f, 0.38f, 0.50f), go => go.AddComponent<Constable>());
            Workbench("Spellmaker", new Color(0.42f, 0.34f, 0.62f), go => go.AddComponent<Spellmaker>());
            Workbench("EnchantingTable", new Color(0.30f, 0.46f, 0.58f), go => go.AddComponent<EnchantingTable>());
        }

        static void Workbench(string name, Color colour, System.Action<GameObject> attach)
        {
            GameObject root = new GameObject(name);

            AddVisual(root.transform, "Table", new Vector3(0f, 0.5f, 0f), Quaternion.identity,
                new Vector3(2f, 1f, 1.1f), Surface($"Mat_{name}", colour));
            AddVisual(root.transform, "Focus", new Vector3(0f, 1.2f, 0f), Quaternion.identity,
                new Vector3(0.5f, 0.5f, 0.5f), Emissive($"Mat_{name}Glow", colour * 1.6f, 4f));

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.size = new Vector3(2.4f, 1.8f, 1.6f);
            trigger.center = new Vector3(0f, 0.9f, 0f);

            attach(root);

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        // The shop name is the table each merchant restocks from. Every one of them used to draw
        // from the same Loot_Shop, which is why the smith and the alchemist sold the same things
        static readonly (string name, RoomPurpose lives, Color colour, bool trades, bool talks, string shop)[]
            ResidentPlans =
        {
            ("Npc_Shopkeeper", RoomPurpose.GeneralStore, new Color(0.52f, 0.44f, 0.30f), true, true, "GeneralStore"),
            ("Npc_Armorer", RoomPurpose.Armorer, new Color(0.56f, 0.58f, 0.62f), true, true, "Armorer"),
            ("Npc_Alchemist", RoomPurpose.Alchemist, new Color(0.38f, 0.56f, 0.40f), true, true, "Alchemist"),
            ("Npc_Smith", RoomPurpose.Smithy, new Color(0.40f, 0.34f, 0.30f), true, true, "Smithy"),
            ("Npc_Innkeeper", RoomPurpose.Inn, new Color(0.54f, 0.38f, 0.24f), true, true, "Inn"),
            ("Npc_Barkeep", RoomPurpose.Tavern, new Color(0.50f, 0.34f, 0.22f), true, true, "Tavern"),
            ("Npc_Trader", RoomPurpose.Market, new Color(0.62f, 0.50f, 0.26f), true, true, "Market"),
            ("Npc_Priest", RoomPurpose.Temple, new Color(0.72f, 0.68f, 0.46f), false, true, null),
            ("Npc_Townsfolk", RoomPurpose.Townhouse, new Color(0.46f, 0.44f, 0.40f), false, true, null),
            ("Npc_Watchman", RoomPurpose.Watchhouse, new Color(0.36f, 0.40f, 0.50f), false, true, null)
        };

        // Something to be when the character creator is skipped. A generalist, enough combat to
        // survive the first crypt, enough magic to cast the three starting spells
        static void BuildDefaultClass()
        {
            new AssetAuthoring(Create<ClassDefinition>("Class_Adventurer"))
                .Str("id", "class.adventurer")
                .Str("displayName", "Adventurer")
                .Str("description", "A generalist, and the class you get when you skip the creator.")
                .Enums("primarySkills", (int)SkillId.Longsword, (int)SkillId.Block, (int)SkillId.Destruction)
                .Enums("majorSkills", (int)SkillId.Dodging, (int)SkillId.Restoration, (int)SkillId.Stealth)
                .Enums("minorSkills", (int)SkillId.ShortBlade, (int)SkillId.Archery, (int)SkillId.CriticalStrike,
                    (int)SkillId.Lockpicking, (int)SkillId.Climbing, (int)SkillId.Mercantile)
                .Int("hitPointsPerLevel", 9)
                .Float("mageryMultiplier", 1f)
                .Save();
        }

        static string[] MembersOf(string set)
        {
            foreach ((string name, string _, string[] members) in SetPlans)
                if (name == set) return members;

            return new string[0];
        }

        // Interiors are the same room modules the dungeon stack builds from, matched to the building
        // that fronts them. Streets and squares are outdoors now, so they are not interiors of
        // anything and are left out of both lists
        static void BuildSettlements()
        {
            Surface("Mat_TownGround", new Color(0.32f, 0.34f, 0.26f));
            Surface("Mat_TownStreet", new Color(0.40f, 0.38f, 0.35f));
            Surface("Mat_TownSquare", new Color(0.52f, 0.49f, 0.42f));
            Surface("Mat_TownWall", new Color(0.62f, 0.58f, 0.50f));
            Surface("Mat_TownRoof", new Color(0.35f, 0.24f, 0.20f));
            Surface("Mat_TownWater", new Color(0.16f, 0.30f, 0.44f), 0.85f);
            Surface("Mat_TownDoor", new Color(0.34f, 0.22f, 0.13f));

            foreach ((string set, bool walled, SettlementShore shore, Growth growth) in SettlementPlans)
            {
                List<Object> interiors = new List<Object>();
                List<int> trades = new List<int>();
                List<int> civic = new List<int>();

                foreach (string member in MembersOf(set))
                {
                    RoomModuleDefinition module = Load<RoomModuleDefinition>($"Module_{member}");

                    if (module == null || module.Purpose == RoomPurpose.None || module.Purpose == RoomPurpose.Square)
                        continue;

                    interiors.Add(module);
                    (IsCivic(module.Purpose) && growth.Civic > 0f ? civic : trades).Add((int)module.Purpose);
                }

                List<int> tintPurposes = new List<int>();
                List<Object> tintMaterials = new List<Object>();

                foreach (RoomModuleDefinition module in AllInteriors(interiors))
                {
                    tintPurposes.Add((int)module.Purpose);
                    tintMaterials.Add(Tint(module.Purpose));
                }

                List<int> servicePurposes = new List<int>();
                List<Object> servicePrefabs = new List<Object>();

                // Whatever this settlement actually builds decides which services it has, a hamlet with
                // no smithy has nowhere to repair, and that is the point of a market town
                foreach (RoomModuleDefinition module in AllInteriors(interiors))
                {
                    string fitting = ServiceFor(module.Purpose);
                    GameObject prefab = fitting != null ? Prefab(fitting) : null;

                    if (prefab == null || servicePurposes.Contains((int)module.Purpose))
                        continue;

                    servicePurposes.Add((int)module.Purpose);
                    servicePrefabs.Add(prefab);
                }

                List<int> residentPurposes = new List<int>();
                List<Object> residentPrefabs = new List<Object>();

                foreach ((string name, RoomPurpose lives, Color _c, bool _t, bool _d, string _s) in ResidentPlans)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{name}.prefab");
                    if (prefab == null)
                        continue;

                    residentPurposes.Add((int)lives);
                    residentPrefabs.Add(prefab);
                }

                bool big = growth.Extent.x >= 450f;

                SettlementDefinition settlement = Create<SettlementDefinition>($"Settlement_{set}");
                new AssetAuthoring(settlement)
                    .Str("displayName", DisplayNameOf(set))
                    .Vec2("extent", growth.Extent)
                    .Enum("shore", (int)shore)
                    .Bool("walled", walled)
                    .Float("relief", growth.Relief)
                    .Float("terrainScale", 140f)
                    .Float("squareRadius", growth.Square)
                    .Int("arteries", growth.Arteries)
                    .Int("arteryLength", growth.ArteryLength)
                    .Int("rings", growth.Rings)
                    .Int("lanes", growth.Lanes)
                    .Vec2("stepRange", new Vector2(24f, 36f))
                    .Float("wander", 0.4f)
                    .Float("ringGap", 30f)
                    .Float("mainStreetWidth", big ? 11f : 9f)
                    .Float("laneWidth", 6.5f)
                    .Float("alleyWidth", 3.6f)
                    .Float("civicQuarter", growth.Civic)
                    .Float("civicSpacing", 34f)
                    .Vec2Int("civicStoreys", new Vector2Int(3, 5))
                    .Vec2("buildingWidth", new Vector2(6.5f, 12f))
                    .Vec2("buildingDepth", new Vector2(7f, 11f))
                    .Vec2("gap", new Vector2(0.2f, 1.8f))
                    .Float("setback", 0.5f)
                    .Float("retry", 3f)
                    .Float("skew", 0.2f)
                    .Vec2Int("coreStoreys", new Vector2Int(2, 4))
                    .Vec2Int("edgeStoreys", new Vector2Int(1, 3))
                    .Float("storeyHeight", 3.1f)
                    .Float("jetty", 0.35f)
                    .Enums("buildingPurposes", trades.ToArray())
                    .Enums("civicPurposes", civic.ToArray())
                    .Refs("interiors", interiors.ToArray())
                    .Ref("doorPlug", AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Mod_DoorPlug.prefab"))
                    .Ref("hiddenCircle", CircleDoorFor(set))
                    .Ref("questBoard", Prefab("Prop_QuestBoard"))
                    .Enums("servicePurposes", servicePurposes.ToArray())
                    .Refs("servicePrefabs", servicePrefabs.ToArray())
                    .Enums("residentPurposes", residentPurposes.ToArray())
                    .Refs("residentPrefabs", residentPrefabs.ToArray())
                    .Enums("tintPurposes", tintPurposes.ToArray())
                    .Refs("tintMaterials", tintMaterials.ToArray())
                    .Save();
            }
        }

        static List<RoomModuleDefinition> AllInteriors(List<Object> interiors)
        {
            List<RoomModuleDefinition> found = new List<RoomModuleDefinition>();

            foreach (Object item in interiors)
                if (item is RoomModuleDefinition module) found.Add(module);

            return found;
        }

        // Hues spaced by the golden ratio so neighbouring trades never land on the same colour, which
        // is the whole point of tinting them, finding one guildhall in a city of six hundred boxes
        static Material Tint(RoomPurpose purpose)
        {
            float hue = ((int)purpose * 0.6180339887f) % 1f;
            return Surface($"Mat_Trade_{purpose}", Color.HSVToRGB(hue, 0.5f, 0.82f));
        }

        static string DisplayNameOf(string set)
        {
            foreach ((string name, string display, string[] _) in SetPlans)
                if (name == set) return display;

            return set;
        }

        // Sizes come from DungeonLayoutSettings.Scales, which the layout harness measures directly:
        // one table, so the assets and the verification cannot disagree
        static void Scale(string setName)
        {
            foreach ((string set, int _, int _2, int _3, int _4, int _5, int _6) in DungeonLayoutSettings.Scales)
            {
                if (set != setName)
                    continue;

                DungeonLayoutSettings preset = DungeonLayoutSettings.Preset(setName);

                new AssetAuthoring(Load<DungeonTypeDefinition>($"DungeonType_{setName}"))
                    .Vec2Int("mainPathRooms", new Vector2Int(preset.MinMainRooms, preset.MaxMainRooms))
                    .Vec2Int("extraBranchCount", new Vector2Int(preset.MinExtraBranches, preset.MaxExtraBranches))
                    .Vec2Int("extraBranchLength", new Vector2Int(preset.MinExtraBranchLength, preset.MaxExtraBranchLength))
                    .Vec2Int("roomsPerLevel", new Vector2Int(preset.MinRoomsPerLevel, preset.MaxRoomsPerLevel))
                    .Vec2Int("bigLevelRooms", new Vector2Int(preset.MinBigLevelRooms, preset.MaxBigLevelRooms))
                    .Float("bigLevelChance", preset.BigLevelChance)
                    .Float("loopChance", preset.LoopChance)
                    .Int("minLoopSpan", preset.MinLoopSpan)
                    .Float("deadEndReconnectChance", preset.DeadEndReconnectChance)
                    .Save();

                return;
            }

            Debug.LogError($"DungeonType_{setName} has no entry in DungeonLayoutSettings.Scales.");
        }

        static readonly string[] GivenNames =
        {
            "Halric", "Ysolde", "Hesta", "Orlan", "Marn", "Corrick", "Dalla", "Ombric",
            "Sella", "Tarn", "Wynn", "Merrow", "Edda", "Caspar", "Rowen", "Thessa",
            "Ilse", "Garrick", "Nessa", "Bram", "Oriel", "Perrin", "Alys", "Fennick"
        };

        static readonly string[] Surnames =
        {
            "Ledd", "Coss", "Ossel", "Harrow", "Venn", "Dale", "Marek", "Ferrow",
            "Gault", "Brant", "Ives", "Lyle", "Weir", "Ostry", "Renn", "Salter",
            "Coad", "Drewe", "Mallow", "Purlin", "Stagg", "Whitlock", "Aubry", "Kerrin"
        };

        // A trade and the want that comes with it. Everyone on a board is a resident who happens to
        // want something, not a quest giver
        static readonly (string trade, NpcMotive motive, RoomPurpose workplace)[] TradePlans =
        {
            ("the miller", NpcMotive.Debt, RoomPurpose.Mill),
            ("a carter", NpcMotive.Duty, RoomPurpose.Stables),
            ("the smith", NpcMotive.Ambition, RoomPurpose.Smithy),
            ("a widow of the parish", NpcMotive.Grief, RoomPurpose.Townhouse),
            ("the innkeeper", NpcMotive.Greed, RoomPurpose.Inn),
            ("a fisher", NpcMotive.Fear, RoomPurpose.Dock),
            ("the watch sergeant", NpcMotive.Duty, RoomPurpose.Watchhouse),
            ("a clerk of the Register", NpcMotive.Ambition, RoomPurpose.Courthouse),
            ("a keeper of the cycle", NpcMotive.Devotion, RoomPurpose.Temple),
            ("a carpenter", NpcMotive.Revenge, RoomPurpose.FurnitureStore),
            ("the herbalist", NpcMotive.Greed, RoomPurpose.Alchemist),
            ("a drover", NpcMotive.Fear, RoomPurpose.Stables)
        };

        static int ResidentsFor(SettlementTier tier)
        {
            switch (tier)
            {
                case SettlementTier.City:
                case SettlementTier.PortCity: return 6;
                case SettlementTier.Town:
                case SettlementTier.Port: return 5;
                case SettlementTier.Village:
                case SettlementTier.Harbour: return 4;
                default: return 3;
            }
        }

        static string Situation(NpcMotive motive, string trade, string place)
        {
            switch (motive)
            {
                case NpcMotive.Debt: return $"Is {trade} in {place}, and owes more than the season will cover.";
                case NpcMotive.Grief: return $"Is {trade} in {place}. Has not had the body back, and will not say so plainly.";
                case NpcMotive.Ambition: return $"Is {trade} in {place}, and means to hold the licence by winter.";
                case NpcMotive.Fear: return $"Is {trade} in {place}, and has not slept properly since the spring.";
                case NpcMotive.Devotion: return $"Is {trade} in {place}, and keeps the cycle whatever the parish thinks.";
                case NpcMotive.Greed: return $"Is {trade} in {place}, and is honest about wanting more than is owed.";
                case NpcMotive.Revenge: return $"Is {trade} in {place}. Knows exactly who, and lies about why.";
                default: return $"Is {trade} in {place}, and expects the same of everybody else.";
            }
        }

        static string Slug(string name)
        {
            System.Text.StringBuilder slug = new System.Text.StringBuilder();

            foreach (char c in name)
                if (char.IsLetterOrDigit(c)) slug.Append(c);

            return slug.ToString();
        }

        // Every settlement gets people who live there, want things, and know each other. A generated
        // quest is about one of them and somebody they know
        static Dictionary<string, NpcDefinition[]> BuildCasts(ScheduleDefinition schedule, FactionDefinition guild)
        {
            Dictionary<string, NpcDefinition[]> byPlace = new Dictionary<string, NpcDefinition[]>();

            for (int p = 0; p < OrdovanPlaces.All.Length; p++)
            {
                OrdovanPlaces.Place place = OrdovanPlaces.All[p];

                if (!place.IsSettlement)
                    continue;

                string slug = Slug(place.Name);

                LocationDefinition where = Create<LocationDefinition>($"Loc_{slug}");
                new AssetAuthoring(where)
                    .Str("id", $"loc.{slug.ToLowerInvariant()}").Str("displayName", place.Name)
                    .Enum("kind", (int)LocationKind.Settlement)
                    .Save();

                int count = ResidentsFor(place.Tier);
                NpcDefinition[] cast = new NpcDefinition[count];
                DeterministicRandom rng = new DeterministicRandom(p * 7919 + 13);

                for (int i = 0; i < count; i++)
                {
                    (string trade, NpcMotive motive, RoomPurpose workplace) = TradePlans[(p + i * 5) % TradePlans.Length];
                    string name = $"{GivenNames[rng.NextInt(GivenNames.Length)]} {Surnames[rng.NextInt(Surnames.Length)]}";

                    NpcDefinition npc = Create<NpcDefinition>($"Npc_{slug}_{i}");
                    new AssetAuthoring(npc)
                        .Str("id", $"npc.{slug.ToLowerInvariant()}.{i}").Str("displayName", name)
                        .Ref("faction", guild).Ref("schedule", schedule).Ref("home", where)
                        .Enums("motives", (int)motive, (int)TradePlans[(p + i * 5 + 4) % TradePlans.Length].motive)
                        .Str("situation", Situation(motive, trade, place.Name))
                        .Bool("isMerchant", i == 0)
                        .Enum("workplace", (int)workplace)
                        .Save();

                    cast[i] = Load<NpcDefinition>($"Npc_{slug}_{i}");
                }

                // Everybody knows their neighbours, so a complication always has somebody to be about
                for (int i = 0; i < count; i++)
                {
                    new AssetAuthoring(cast[i])
                        .Refs("relations", cast[(i + 1) % count], cast[(i + count - 1) % count])
                        .Save();
                }

                byPlace[place.Name] = cast;
            }

            return byPlace;
        }

        static readonly (string asset, string id, NpcMotive motive, QuestObjectiveKind objective, string title,
            string reason, string complication, int low, int high, int gold, int standing)[] TemplatePlans =
        {
            ("Template_Debt", "template.debt", NpcMotive.Debt, QuestObjectiveKind.CollectItem,
                "What {giver} owes",
                "{giver} is short before the turn, and {count} {item} covers it if it arrives in time.",
                "The debt is not {giver}'s. It is {target}'s, carried in {giver}'s name, and saying so would finish them.",
                3, 6, 60, 4),

            ("Template_Grief", "template.grief", NpcMotive.Grief, QuestObjectiveKind.CollectItem,
                "What {target} left behind",
                "{giver} wants {item} brought back. It was {target}'s, and the family has nothing else of theirs.",
                "{target} is not coming back either, and {giver} has not been told in so many words.",
                1, 2, 90, 5),

            ("Template_Ambition", "template.ambition", NpcMotive.Ambition, QuestObjectiveKind.CollectItem,
                "A matter of standing",
                "{giver} wants {count} {item} in hand before the licence is heard.",
                "It is to be held over {target}, who refused {giver} the same favour last winter.",
                2, 4, 110, 6),

            ("Template_Fear", "template.fear", NpcMotive.Fear, QuestObjectiveKind.TalkToNpc,
                "Something in the yard",
                "{giver} is certain something comes into the yard at night and wants it dealt with.",
                "There may be nothing there at all. {target} has heard it too, and thinks otherwise.",
                1, 1, 70, 3),

            ("Template_Devotion", "template.devotion", NpcMotive.Devotion, QuestObjectiveKind.DeliverItem,
                "Returned properly",
                "{giver} wants {item} returned from the right place, at the right hour, in silence.",
                "It is imperial property. {target} keeps a list, and returning it is theft.",
                1, 1, 80, 5),

            ("Template_Greed", "template.greed", NpcMotive.Greed, QuestObjectiveKind.CollectItem,
                "Worth more than it looks",
                "{giver} will pay for {count} {item} and is not pretending it is for anyone's good but theirs.",
                "{target} was offered the same work first and turned it down, which {giver} has not mentioned.",
                4, 8, 70, 2),

            ("Template_Revenge", "template.revenge", NpcMotive.Revenge, QuestObjectiveKind.KillEnemy,
                "A score of sorts",
                "{giver} wants {count} of them dead and says it is about the road being safe.",
                "It is about {target}, and {giver} will not be talked out of it by anyone who says so.",
                3, 6, 130, 6),

            ("Template_Duty", "template.duty", NpcMotive.Duty, QuestObjectiveKind.KillEnemy,
                "The parish rate",
                "{giver} is paying the parish rate for {count} of them, as the register requires.",
                "{target} was meant to have done it a month ago and drew the money for it.",
                4, 8, 100, 5)
        };

        static Object[] BuildQuestTemplates()
        {
            ItemDefinition[] carried =
            {
                Load<ItemDefinition>("Item_BoneDust"), Load<ItemDefinition>("Item_HealingPotion"),
                Load<ItemDefinition>("Item_DriedMeat"), Load<ItemDefinition>("Item_SilverGoblet"),
                Load<ItemDefinition>("Item_Dagger")
            };

            EnemyDefinition[] hunted =
            {
                Load<EnemyDefinition>("Enemy_Skeleton"), Load<EnemyDefinition>("Enemy_Bandit"),
                Load<EnemyDefinition>("Enemy_Ghoul"), Load<EnemyDefinition>("Enemy_Warhound")
            };

            List<Object> made = new List<Object>();

            foreach ((string asset, string id, NpcMotive motive, QuestObjectiveKind objective, string title,
                string reason, string complication, int low, int high, int gold, int standing) in TemplatePlans)
            {
                QuestTemplateDefinition template = Create<QuestTemplateDefinition>(asset);

                new AssetAuthoring(template)
                    .Str("id", id)
                    .Enum("servesMotive", (int)motive)
                    .Enum("objective", (int)objective)
                    .Str("titleFormat", title).Str("reasonFormat", reason).Str("complicationFormat", complication)
                    .Vec2Int("countRange", new Vector2Int(low, high))
                    .Vec2Int("goldReward", new Vector2Int(gold, gold * 2))
                    .Int("standingReward", standing)
                    .Refs("candidateItems", carried)
                    .Refs("candidateEnemies", hunted)
                    .Save();

                made.Add(Load<QuestTemplateDefinition>(asset));
            }

            return made.ToArray();
        }

        // Act I's opener is an ordinary contract that turns out not to be one. Authored rather
        // than generated, because everything touching the spine is
        static AuthoredQuestDefinition BuildUndercroftQuest(NpcDefinition giver, FactionDefinition faction,
            LocationDefinition place)
        {
            AuthoredQuestDefinition quest = Create<AuthoredQuestDefinition>("Quest_TheUndercroftJob");
            AssetAuthoring author = new AssetAuthoring(quest);

            author.Str("id", "quest.undercroft_job").Str("title", "The Undercroft Job")
                .Ref("giver", giver).Ref("faction", faction)
                .Str("reason", "Two farmhands went into the Undercroft after a noise and did not come out. " +
                               "The company posts the standard rate for a recovery and does not discuss the noise.")
                .Str("complication", "It is not a monster. Whatever is down there was dormant until they " +
                                     "disturbed it, and the company will pay out either way rather than answer questions.");

            author.Apply("stages", p =>
            {
                p.arraySize = 2;

                SerializedProperty down = p.GetArrayElementAtIndex(0);
                down.FindPropertyRelative("Title").stringValue = "Go down and find out";
                down.FindPropertyRelative("Summary").stringValue =
                    "The Undercroft is south-east of Ashmere. Whatever is in it is still there. Put it down.";

                SerializedProperty first = down.FindPropertyRelative("Objectives");
                first.arraySize = 1;
                SerializedProperty clear = first.GetArrayElementAtIndex(0);
                clear.FindPropertyRelative("Kind").enumValueIndex = (int)QuestObjectiveKind.ClearDungeon;
                clear.FindPropertyRelative("Description").stringValue = "Deal with what is in the Undercroft";
                clear.FindPropertyRelative("Location").objectReferenceValue = place;
                clear.FindPropertyRelative("Count").intValue = 1;

                SerializedProperty back = p.GetArrayElementAtIndex(1);
                back.FindPropertyRelative("Title").stringValue = "Report it";
                back.FindPropertyRelative("Summary").stringValue =
                    $"Tell {giver.DisplayName} what was down there, and see what they do with it.";

                SerializedProperty second = back.FindPropertyRelative("Objectives");
                second.arraySize = 1;
                SerializedProperty talk = second.GetArrayElementAtIndex(0);
                talk.FindPropertyRelative("Kind").enumValueIndex = (int)QuestObjectiveKind.TalkToNpc;
                talk.FindPropertyRelative("Description").stringValue = $"Report to {giver.DisplayName}";
                talk.FindPropertyRelative("Npc").objectReferenceValue = giver;
                talk.FindPropertyRelative("Count").intValue = 1;
            });

            author.Apply("onComplete", p =>
            {
                p.arraySize = 2;

                SerializedProperty paid = p.GetArrayElementAtIndex(0);
                paid.FindPropertyRelative("Kind").enumValueIndex = (int)QuestConsequenceKind.GiveGold;
                paid.FindPropertyRelative("Amount").intValue = 250;
                paid.FindPropertyRelative("Text").stringValue = "The standard rate, paid without comment.";

                SerializedProperty noted = p.GetArrayElementAtIndex(1);
                noted.FindPropertyRelative("Kind").enumValueIndex = (int)QuestConsequenceKind.FactionStanding;
                noted.FindPropertyRelative("Faction").objectReferenceValue = faction;
                noted.FindPropertyRelative("Amount").intValue = 8;
            });

            author.Save();
            return Load<AuthoredQuestDefinition>("Quest_TheUndercroftJob");
        }

        static GameObject BuildQuestBoardPrefab()
        {
            GameObject root = new GameObject("Prop_QuestBoard");

            Material timber = Surface("Mat_QuestBoard", new Color(0.38f, 0.28f, 0.18f));
            AddBox(root.transform, "Post_L", new Vector3(-0.7f, 0.9f, 0f), Quaternion.identity,
                new Vector3(0.12f, 1.8f, 0.12f), timber);
            AddBox(root.transform, "Post_R", new Vector3(0.7f, 0.9f, 0f), Quaternion.identity,
                new Vector3(0.12f, 1.8f, 0.12f), timber);
            AddBox(root.transform, "Board", new Vector3(0f, 1.5f, 0f), Quaternion.identity,
                new Vector3(1.6f, 1f, 0.1f), Surface("Mat_QuestPaper", new Color(0.78f, 0.74f, 0.62f)));

            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.size = new Vector3(1.8f, 2f, 0.6f);
            trigger.center = new Vector3(0f, 1f, 0f);

            root.AddComponent<EndlessDescent.World.QuestBoard>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/Prop_QuestBoard.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        static Object[] AllOfType<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { ContentRoot });
            Object[] found = new Object[guids.Length];

            for (int i = 0; i < guids.Length; i++)
                found[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));

            return found;
        }

        static RegionMap BuildRegionMap(Dictionary<string, NpcDefinition[]> casts)
        {
            RegionMap region = Create<RegionMap>("RegionMap");

            new AssetAuthoring(region).Float("kilometresPerDay", 28f).Apply("locations", property =>
            {
                property.arraySize = OrdovanPlaces.All.Length;

                // Every one of the seventy one, not just the seventeen that were named individually.
                // A settlement resolves by tier and a dungeon by archetype, so a new place needs
                // nothing here, it needs a row in OrdovanPlaces and no more
                for (int i = 0; i < OrdovanPlaces.All.Length; i++)
                {
                    OrdovanPlaces.Place plan = OrdovanPlaces.All[i];

                    SettlementDefinition settlement = plan.IsSettlement
                        ? Load<SettlementDefinition>(plan.Asset) : null;
                    DungeonTypeDefinition dungeon = plan.IsSettlement
                        ? null : Load<DungeonTypeDefinition>(plan.Asset);

                    if (settlement == null && dungeon == null)
                        Debug.LogError($"{plan.Name} wants {plan.Asset}, which does not exist.");

                    SerializedProperty entry = property.GetArrayElementAtIndex(i);

                    // The place's own name, not the shared asset's, twenty four dungeons share ten
                    // archetypes, and they must not all be called Crypt on the travel screen
                    entry.FindPropertyRelative("DisplayName").stringValue = plan.Name;
                    entry.FindPropertyRelative("Position").vector2Value = plan.Km;
                    entry.FindPropertyRelative("Settlement").objectReferenceValue = settlement;
                    entry.FindPropertyRelative("Dungeon").objectReferenceValue = dungeon;
                    entry.FindPropertyRelative("Scale").enumValueIndex = (int)plan.Scale;

                    SerializedProperty residents = entry.FindPropertyRelative("Residents");
                    NpcDefinition[] cast = casts != null && casts.TryGetValue(plan.Name, out NpcDefinition[] found)
                        ? found : new NpcDefinition[0];

                    residents.arraySize = cast.Length;
                    for (int r = 0; r < cast.Length; r++)
                        residents.GetArrayElementAtIndex(r).objectReferenceValue = cast[r];
                }
            }).Save();

            return Load<RegionMap>("RegionMap");
        }

        static void BuildResidentPrefabs(NpcDefinition npc, LootTableDefinition stockTable)
        {
            foreach ((string name, RoomPurpose lives, Color colour, bool trades, bool talks, string shop)
                in ResidentPlans)
            {
                GameObject root = new GameObject(name);

                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = Vector3.up;
                body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                body.GetComponent<MeshRenderer>().sharedMaterial = Surface($"Mat_{name}", colour);

                // Colour alone never told a smith from a priest at the far end of a street
                GreyboxSilhouettes.Token(root.transform, lives,
                    Surface($"Mat_{name}Token", colour * 0.6f + Color.white * 0.2f, 0.3f));

                BoxCollider trigger = root.AddComponent<BoxCollider>();
                trigger.size = new Vector3(1f, 2f, 1f);
                trigger.center = Vector3.up;
                Object.DestroyImmediate(body.GetComponent<Collider>());

                Poise poise = root.AddComponent<Poise>();
                Health health = root.AddComponent<Health>();
                new AssetAuthoring(health).Ref("poise", poise).Str("saveKey", $"health.{name}").Save();

                // A person, not a monster, hurting one in sight of another is a crime
                new AssetAuthoring(root.AddComponent<Witness>()).Ref("health", health).Save();

                NpcActor actor = root.AddComponent<NpcActor>();
                new AssetAuthoring(actor).Ref("definition", npc).Save();

                if (talks)
                    new AssetAuthoring(root.AddComponent<NpcDialogue>()).Ref("actor", actor).Save();

                if (trades)
                {
                    Inventory stock = root.AddComponent<Inventory>();
                    new AssetAuthoring(stock).Str("saveKey", $"stock.{name}").Float("carryLimit", 9999f).Save();

                    LootTableDefinition shelf = shop != null ? Load<LootTableDefinition>($"Loot_Shop_{shop}") : null;

                    new AssetAuthoring(root.AddComponent<Merchant>())
                        .Ref("actor", actor).Ref("stock", stock)
                        .Ref("restockTable", shelf != null ? shelf : stockTable)
                        .Int("gold", 400).Int("purse", 400).Int("purseRecoveryPerDay", 60)
                        .Int("restockEveryDays", 3)
                        .Save();
                }

                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{name}.prefab");
                Object.DestroyImmediate(root);
            }
        }

        static void BuildBedPrefab()
        {
            GameObject bed = new GameObject("Bed");
            AddVisual(bed.transform, "Frame", new Vector3(0f, 0.3f, 0f), Quaternion.identity,
                new Vector3(1.2f, 0.6f, 2.2f), Surface("Mat_Bed", new Color(0.42f, 0.30f, 0.22f)));

            BoxCollider trigger = bed.AddComponent<BoxCollider>();
            trigger.size = new Vector3(1.6f, 1.2f, 2.6f);
            trigger.center = new Vector3(0f, 0.6f, 0f);

            new AssetAuthoring(bed.AddComponent<RestPoint>())
                .Bool("comfortable", true).Int("hours", 8).Save();

            PrefabUtility.SaveAsPrefabAsset(bed, $"{PrefabRoot}/Bed.prefab");
            Object.DestroyImmediate(bed);
        }

        static void BuildGuildHallPrefab(FactionDefinition guild)
        {
            GameObject hall = new GameObject("GuildHall");

            AddVisual(hall.transform, "Lectern", new Vector3(0f, 0.6f, 0f), Quaternion.identity,
                new Vector3(1.2f, 1.2f, 0.8f), Surface("Mat_Guild", new Color(0.42f, 0.36f, 0.52f)));
            AddVisual(hall.transform, "Banner", new Vector3(0f, 2.2f, -0.5f), Quaternion.identity,
                new Vector3(1.4f, 2f, 0.08f), Surface("Mat_GuildBanner", new Color(0.30f, 0.24f, 0.44f)));

            BoxCollider trigger = hall.AddComponent<BoxCollider>();
            trigger.size = new Vector3(1.8f, 2.4f, 1.4f);
            trigger.center = new Vector3(0f, 1.2f, 0f);

            new AssetAuthoring(hall.AddComponent<GuildHall>()).Ref("guild", guild).Save();

            PrefabUtility.SaveAsPrefabAsset(hall, $"{PrefabRoot}/GuildHall.prefab");
            Object.DestroyImmediate(hall);
        }

        static void BuildSecretDoorPrefab()
        {
            GameObject root = new GameObject("Mod_SecretDoor");

            GameObject panel = AddVisual(root.transform, "Panel", new Vector3(0f, WallHeight * 0.5f, -WallThickness * 0.5f),
                Quaternion.identity, new Vector3(DoorWidth, WallHeight, WallThickness), Wall);

            BoxCollider block = panel.AddComponent<BoxCollider>();
            block.size = Vector3.one;

            SecretDoor secret = root.AddComponent<SecretDoor>();
            new AssetAuthoring(secret)
                .Ref("panel", panel.transform)
                .Ref("testingTint", Surface("Mat_Secret", new Color(0.34f, 0.30f, 0.24f)))
                .Bool("revealForTesting", false)
                .Save();

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/Mod_SecretDoor.prefab");
            Object.DestroyImmediate(root);
        }

        static GameObject BuildInventoryRowPrefab()
        {
            GameObject root = new GameObject("UI_InventoryRow", typeof(RectTransform));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420f, 26f);

            UnityEngine.UI.Image background = root.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.16f, 0.16f, 0.2f, 0.9f);

            UnityEngine.UI.Button button = root.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(root.transform, false);

            UnityEngine.UI.Text label = labelObject.AddComponent<UnityEngine.UI.Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 14;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = Vector2.zero;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/UI_InventoryRow.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        static GameObject BuildProjectilePrefab()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Spell_Bolt";
            root.transform.localScale = Vector3.one * 0.35f;
            root.GetComponent<MeshRenderer>().sharedMaterial =
                Emissive("Mat_Bolt", new Color(1f, 0.55f, 0.2f), 8f);

            Object.DestroyImmediate(root.GetComponent<Collider>());
            root.AddComponent<SpellProjectile>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/Spell_Bolt.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        // Hung at a connection, hinged on one jamb so it swings clear of the opening
        static GameObject BuildDoorPrefab()
        {
            GameObject root = new GameObject("Mod_Door");

            GameObject hinge = new GameObject("Hinge");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(-DoorWidth * 0.5f, 0f, -WallThickness * 0.5f);

            AddBox(hinge.transform, "Leaf", new Vector3(DoorWidth * 0.5f, DoorHeight * 0.5f, 0f),
                Quaternion.identity, new Vector3(DoorWidth, DoorHeight, 0.12f),
                Surface("Mat_Door", new Color(0.36f, 0.24f, 0.14f)));

            // Iron bands and a lock plate, off until the builder gives this door a key, so a door that
            // needs one looks like it from across the room
            Material iron = Surface("Mat_LockIron", new Color(0.20f, 0.20f, 0.22f), 0.5f);
            GameObject locked = new GameObject("Locked");
            locked.transform.SetParent(hinge.transform, false);

            AddVisual(locked.transform, "BandLow", new Vector3(DoorWidth * 0.5f, DoorHeight * 0.25f, 0f),
                Quaternion.identity, new Vector3(DoorWidth + 0.02f, 0.2f, 0.18f), iron);
            AddVisual(locked.transform, "BandHigh", new Vector3(DoorWidth * 0.5f, DoorHeight * 0.75f, 0f),
                Quaternion.identity, new Vector3(DoorWidth + 0.02f, 0.2f, 0.18f), iron);
            AddVisual(locked.transform, "Plate", new Vector3(DoorWidth - 0.4f, DoorHeight * 0.45f, 0f),
                Quaternion.identity, new Vector3(0.34f, 0.46f, 0.22f),
                Surface("Mat_LockBrass", new Color(0.66f, 0.52f, 0.22f), 0.5f));

            locked.SetActive(false);

            Door door = root.AddComponent<Door>();
            new AssetAuthoring(door).Ref("leaf", hinge.transform).Ref("lockedLook", locked).Save();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/Mod_Door.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        // Dropped onto any connector the layout never used, so an unused doorway is not a hole. Wall,
        // not a door coloured panel: a filled doorway read as a door, and could not be told from a
        // locked one. The builder repaints the frame around it to match (DungeonBuilder.Seal)
        static GameObject BuildDoorPlug()
        {
            GameObject root = new GameObject("Mod_DoorPlug");

            AddBox(root.transform, "Panel", new Vector3(0f, WallHeight * 0.5f, -WallThickness * 0.5f),
                Quaternion.identity, new Vector3(DoorWidth, WallHeight, WallThickness), Wall);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/Mod_DoorPlug.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        static GameObject BuildRoomPrefab(string name, int[] openSides, Vector2 size) =>
            BuildRoomPrefab(name, openSides, size, Floor, RoomPurpose.None);

        static GameObject BuildRoomPrefab(string name, int[] openSides, Vector2 size, Material floorMaterial,
            RoomPurpose purpose) => BuildRoomPrefab(name, openSides, size, floorMaterial, purpose, 2);

        static GameObject BuildRoomPrefab(string name, int[] openSides, Vector2 size, Material floorMaterial,
            RoomPurpose purpose, int torches)
        {
            GameObject root = new GameObject(name);

            AddBox(root.transform, "Floor", new Vector3(0f, -WallThickness * 0.5f, 0f), Quaternion.identity,
                new Vector3(size.x, WallThickness, size.y), floorMaterial);

            AddBox(root.transform, "Ceiling", new Vector3(0f, WallHeight + WallThickness * 0.5f, 0f),
                Quaternion.identity, new Vector3(size.x, WallThickness, size.y), Ceiling);

            Vector3[] outward = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

            for (int side = 0; side < 4; side++)
            {
                bool open = System.Array.IndexOf(openSides, side) >= 0;

                // Sides 0 and 2 face along Z, so they span the room's width; 1 and 3 span its depth
                float span = side % 2 == 0 ? size.x : size.y;
                float reach = (side % 2 == 0 ? size.y : size.x) * 0.5f;

                Vector3 position = outward[side] * reach;
                Quaternion rotation = Quaternion.LookRotation(outward[side]);

                GameObject wall = new GameObject($"Wall_{side}");
                wall.transform.SetParent(root.transform, false);
                wall.transform.SetLocalPositionAndRotation(position, rotation);

                if (!open)
                {
                    AddBox(wall.transform, "Panel", new Vector3(0f, WallHeight * 0.5f, -WallThickness * 0.5f),
                        Quaternion.identity, new Vector3(span, WallHeight, WallThickness), Wall);
                    continue;
                }

                BuildDoorway(wall.transform, span);

                GameObject connector = new GameObject($"Connector_{side}");
                connector.transform.SetParent(root.transform, false);
                connector.transform.SetLocalPositionAndRotation(position, rotation);
                connector.AddComponent<ModuleConnector>();
            }

            AddRoomTorches(root.transform, openSides, size, torches);
            AddProps(root.transform, purpose, size);

            return Finish(root, name,
                new Bounds(new Vector3(0f, WallHeight * 0.5f, 0f), new Vector3(size.x, WallHeight, size.y)),
                purpose);
        }

        // Two torches per room, on closed walls where there are any and tucked into corners where
        // there are not, so an all doors crossing is still lit
        static void AddRoomTorches(Transform root, int[] openSides, Vector2 size, int wanted)
        {
            Vector3[] outward = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            int placed = 0;

            for (int side = 0; side < 4 && placed < wanted; side++)
            {
                if (System.Array.IndexOf(openSides, side) >= 0)
                    continue;

                float reach = (side % 2 == 0 ? size.y : size.x) * 0.5f;

                AddTorch(root, outward[side] * (reach - 0.2f) + Vector3.up * TorchHeight,
                    Quaternion.LookRotation(-outward[side]));

                placed++;
            }

            Vector3[] corners =
            {
                new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, 1f),
                new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, -1f)
            };

            for (int i = 0; placed < wanted && i < corners.Length; i++, placed++)
            {
                Vector3 corner = new Vector3(corners[i].x * (size.x * 0.5f - 0.8f), TorchHeight,
                    corners[i].z * (size.y * 0.5f - 0.8f));

                AddTorch(root, corner, Quaternion.LookRotation(new Vector3(-corners[i].x, 0f, -corners[i].z)));
            }
        }

        // Greybox dressing, enough shape to read the room's purpose at a glance, kept a clear metre
        // off every wall so it can never block a doorway
        static void AddProps(Transform root, RoomPurpose purpose, Vector2 size)
        {
            if (purpose == RoomPurpose.None)
                return;

            Material material = Prop(purpose);
            float halfX = size.x * 0.5f - 1.6f;
            float halfZ = size.y * 0.5f - 1.6f;

            switch (purpose)
            {
                case RoomPurpose.Barracks:
                    for (int i = 0; i < 4; i++)
                    {
                        float z = Mathf.Lerp(-halfZ, halfZ, i / 3f);
                        AddBox(root, $"Bunk_L{i}", new Vector3(-halfX, 0.35f, z), Quaternion.identity,
                            new Vector3(1.8f, 0.7f, 0.9f), material);
                        AddBox(root, $"Bunk_R{i}", new Vector3(halfX, 0.35f, z), Quaternion.identity,
                            new Vector3(1.8f, 0.7f, 0.9f), material);
                    }
                    break;

                case RoomPurpose.Armoury:
                    for (int i = 0; i < 3; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, i / 2f);
                        AddBox(root, $"Rack_{i}", new Vector3(x, 1.1f, -halfZ), Quaternion.identity,
                            new Vector3(1.6f, 2.2f, 0.4f), material);
                    }
                    break;

                case RoomPurpose.Crypt:
                    for (int i = 0; i < 3; i++)
                    {
                        float z = Mathf.Lerp(-halfZ, halfZ, i / 2f);
                        AddBox(root, $"Sarcophagus_{i}", new Vector3(0f, 0.45f, z), Quaternion.identity,
                            new Vector3(1.2f, 0.9f, 2.6f), material);
                    }
                    break;

                case RoomPurpose.Chapel:
                    AddBox(root, "Altar", new Vector3(0f, 0.5f, halfZ - 0.5f), Quaternion.identity,
                        new Vector3(2.4f, 1f, 1.2f), material);
                    for (int i = 0; i < 4; i++)
                    {
                        float z = Mathf.Lerp(-halfZ, halfZ - 3f, i / 3f);
                        AddBox(root, $"Pew_{i}", new Vector3(0f, 0.3f, z), Quaternion.identity,
                            new Vector3(4.5f, 0.6f, 0.5f), material);
                    }
                    break;

                case RoomPurpose.Library:
                    for (int i = 0; i < 3; i++)
                    {
                        float z = Mathf.Lerp(-halfZ, halfZ, i / 2f);
                        AddBox(root, $"Shelf_{i}", new Vector3(-halfX, 1.4f, z), Quaternion.identity,
                            new Vector3(0.6f, 2.8f, 2.4f), material);
                    }
                    AddBox(root, "Lectern", new Vector3(halfX * 0.4f, 0.6f, 0f), Quaternion.identity,
                        new Vector3(1f, 1.2f, 1f), material);
                    break;

                case RoomPurpose.Storeroom:
                    for (int i = 0; i < 5; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, (i % 3) / 2f);
                        float z = i < 3 ? -halfZ : halfZ;
                        AddBox(root, $"Crate_{i}", new Vector3(x, 0.6f, z), Quaternion.identity,
                            new Vector3(1.2f, 1.2f, 1.2f), material);
                    }
                    break;

                case RoomPurpose.Kitchen:
                    AddBox(root, "Hearth", new Vector3(-halfX, 0.9f, 0f), Quaternion.identity,
                        new Vector3(1.2f, 1.8f, 3f), material);
                    AddBox(root, "Table", new Vector3(halfX * 0.3f, 0.45f, 0f), Quaternion.identity,
                        new Vector3(1.4f, 0.9f, 3.2f), material);
                    break;

                case RoomPurpose.Cistern:
                    AddBox(root, "Water", new Vector3(0f, 0.08f, 0f), Quaternion.identity,
                        new Vector3(size.x - 3.6f, 0.16f, size.y - 3.6f), material);
                    break;

                case RoomPurpose.Forge:
                    AddBox(root, "Furnace", new Vector3(-halfX, 1.1f, halfZ), Quaternion.identity,
                        new Vector3(2.2f, 2.2f, 2.2f), material);
                    AddBox(root, "Anvil", new Vector3(0f, 0.4f, 0f), Quaternion.identity,
                        new Vector3(1.4f, 0.8f, 0.7f), material);
                    break;

                case RoomPurpose.Cell:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, i / 3f);
                        AddBox(root, $"Bars_{i}", new Vector3(x, 1.4f, halfZ), Quaternion.identity,
                            new Vector3(0.18f, 2.8f, 0.18f), material);
                    }
                    break;

                case RoomPurpose.Guardroom:
                    AddBox(root, "Table", new Vector3(0f, 0.45f, 0f), Quaternion.identity,
                        new Vector3(2.6f, 0.9f, 1.4f), material);
                    AddBox(root, "Bench_L", new Vector3(0f, 0.25f, -1.5f), Quaternion.identity,
                        new Vector3(2.6f, 0.5f, 0.5f), material);
                    AddBox(root, "Bench_R", new Vector3(0f, 0.25f, 1.5f), Quaternion.identity,
                        new Vector3(2.6f, 0.5f, 0.5f), material);
                    break;

                case RoomPurpose.ThroneRoom:
                    AddBox(root, "Dais", new Vector3(0f, 0.25f, halfZ - 1.5f), Quaternion.identity,
                        new Vector3(6f, 0.5f, 3f), material);
                    AddBox(root, "Throne", new Vector3(0f, 1.2f, halfZ - 1.5f), Quaternion.identity,
                        new Vector3(1.6f, 1.9f, 1.4f), material);
                    goto case RoomPurpose.Hall;

                case RoomPurpose.Ossuary:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, i / 3f);
                        AddBox(root, $"BoneRack_{i}", new Vector3(x, 1.2f, -halfZ), Quaternion.identity,
                            new Vector3(1.4f, 2.4f, 0.5f), material);
                    }
                    break;

                case RoomPurpose.Barrow:
                    AddBox(root, "Mound", new Vector3(0f, 0.6f, 0f), Quaternion.identity,
                        new Vector3(4.5f, 1.2f, 4.5f), material);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * 0.5f;
                        AddBox(root, $"Standing_{i}", new Vector3(Mathf.Cos(a) * halfX, 1.3f, Mathf.Sin(a) * halfZ),
                            Quaternion.identity, new Vector3(0.7f, 2.6f, 0.5f), material);
                    }
                    break;

                case RoomPurpose.Reliquary:
                    AddBox(root, "Plinth", new Vector3(0f, 0.7f, 0f), Quaternion.identity,
                        new Vector3(1.2f, 1.4f, 1.2f), material);
                    AddBox(root, "Casket", new Vector3(0f, 1.6f, 0f), Quaternion.identity,
                        new Vector3(0.9f, 0.5f, 0.9f), material);
                    break;

                case RoomPurpose.Vault:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = i < 2 ? -halfX : halfX;
                        float z = i % 2 == 0 ? -halfZ : halfZ;
                        AddBox(root, $"Chest_{i}", new Vector3(x, 0.5f, z), Quaternion.identity,
                            new Vector3(1.6f, 1f, 1f), material);
                    }
                    break;

                case RoomPurpose.Well:
                    AddBox(root, "Rim", new Vector3(0f, 0.5f, 0f), Quaternion.identity,
                        new Vector3(2.6f, 1f, 2.6f), material);
                    AddBox(root, "Beam", new Vector3(0f, 2.6f, 0f), Quaternion.identity,
                        new Vector3(3f, 0.25f, 0.25f), material);
                    break;

                case RoomPurpose.Grotto:
                    for (int i = 0; i < 5; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, i / 4f);
                        float z = i % 2 == 0 ? -halfZ * 0.6f : halfZ * 0.6f;
                        AddBox(root, $"Stalagmite_{i}", new Vector3(x, 1f, z), Quaternion.identity,
                            new Vector3(0.7f, 2f, 0.7f), material);
                    }
                    break;

                case RoomPurpose.Mine:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, i / 3f);
                        AddBox(root, $"Prop_L{i}", new Vector3(x, WallHeight * 0.5f, -halfZ), Quaternion.identity,
                            new Vector3(0.4f, WallHeight, 0.4f), material);
                        AddBox(root, $"Prop_R{i}", new Vector3(x, WallHeight * 0.5f, halfZ), Quaternion.identity,
                            new Vector3(0.4f, WallHeight, 0.4f), material);
                    }
                    AddBox(root, "Cart", new Vector3(0f, 0.5f, 0f), Quaternion.identity,
                        new Vector3(1.4f, 1f, 2.2f), material);
                    break;

                case RoomPurpose.Alchemy:
                    AddBox(root, "Bench", new Vector3(-halfX, 0.5f, 0f), Quaternion.identity,
                        new Vector3(1.1f, 1f, 4f), material);
                    for (int i = 0; i < 3; i++)
                    {
                        float z = Mathf.Lerp(-halfZ, halfZ, i / 2f);
                        AddBox(root, $"Vat_{i}", new Vector3(halfX, 0.8f, z), Quaternion.identity,
                            new Vector3(1.3f, 1.6f, 1.3f), material);
                    }
                    break;

                case RoomPurpose.Torture:
                    AddBox(root, "Rack", new Vector3(0f, 0.5f, 0f), Quaternion.identity,
                        new Vector3(1.6f, 1f, 3f), material);
                    AddBox(root, "Brazier", new Vector3(halfX, 0.6f, halfZ), Quaternion.identity,
                        new Vector3(1f, 1.2f, 1f), material);
                    break;

                case RoomPurpose.Sanctum:
                    AddBox(root, "Circle", new Vector3(0f, 0.06f, 0f), Quaternion.identity,
                        new Vector3(7f, 0.12f, 7f), material);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        AddBox(root, $"Obelisk_{i}", new Vector3(Mathf.Cos(a) * halfX * 0.8f, 1.6f, Mathf.Sin(a) * halfZ * 0.8f),
                            Quaternion.identity, new Vector3(0.7f, 3.2f, 0.7f), material);
                    }
                    break;

                case RoomPurpose.Lair:
                    AddBox(root, "Hoard", new Vector3(0f, 0.4f, halfZ * 0.5f), Quaternion.identity,
                        new Vector3(6f, 0.8f, 4f), material);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, i / 3f);
                        AddBox(root, $"Rib_{i}", new Vector3(x, 1.8f, -halfZ), Quaternion.identity,
                            new Vector3(0.4f, 3.6f, 0.4f), material);
                    }
                    break;

                case RoomPurpose.Nave:
                    AddBox(root, "Altar", new Vector3(0f, 0.5f, halfZ - 0.5f), Quaternion.identity,
                        new Vector3(2.4f, 1f, 1.2f), material);
                    goto case RoomPurpose.Gallery;

                case RoomPurpose.Arena:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI * 0.25f;
                        AddBox(root, $"Pillar_{i}", new Vector3(Mathf.Cos(a) * halfX, WallHeight * 0.5f, Mathf.Sin(a) * halfZ),
                            Quaternion.identity, new Vector3(1f, WallHeight, 1f), material);
                    }
                    AddBox(root, "Floor_Ring", new Vector3(0f, 0.06f, 0f), Quaternion.identity,
                        new Vector3(size.x - 7f, 0.12f, size.y - 7f), material);
                    break;

                case RoomPurpose.GreatHall:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, i / 3f);
                        AddBox(root, $"Column_L{i}", new Vector3(x, WallHeight * 0.5f, -halfZ * 0.6f),
                            Quaternion.identity, new Vector3(1.1f, WallHeight, 1.1f), material);
                        AddBox(root, $"Column_R{i}", new Vector3(x, WallHeight * 0.5f, halfZ * 0.6f),
                            Quaternion.identity, new Vector3(1.1f, WallHeight, 1.1f), material);
                    }
                    break;

                case RoomPurpose.Market:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, i / 3f);
                        AddBox(root, $"Stall_{i}", new Vector3(x, 0.9f, -halfZ), Quaternion.identity,
                            new Vector3(2.4f, 1.8f, 1.4f), material);
                    }
                    break;

                case RoomPurpose.Smithy:
                    AddBox(root, "Forge", new Vector3(-halfX, 1f, 0f), Quaternion.identity,
                        new Vector3(2f, 2f, 2f), material);
                    AddBox(root, "Anvil", new Vector3(0f, 0.4f, 0f), Quaternion.identity,
                        new Vector3(1.4f, 0.8f, 0.7f), material);
                    break;

                case RoomPurpose.Inn:
                    for (int i = 0; i < 3; i++)
                    {
                        float z = Mathf.Lerp(-halfZ, halfZ, i / 2f);
                        AddBox(root, $"Table_{i}", new Vector3(halfX * 0.4f, 0.45f, z), Quaternion.identity,
                            new Vector3(1.8f, 0.9f, 1.8f), material);
                    }
                    AddBox(root, "Bar", new Vector3(-halfX, 0.55f, 0f), Quaternion.identity,
                        new Vector3(1f, 1.1f, 5f), material);
                    break;

                case RoomPurpose.Dock:
                    for (int i = 0; i < 5; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, i / 4f);
                        AddBox(root, $"Bollard_{i}", new Vector3(x, 0.4f, halfZ), Quaternion.identity,
                            new Vector3(0.4f, 0.8f, 0.4f), material);
                    }
                    AddBox(root, "Water", new Vector3(0f, 0.06f, halfZ + 1f), Quaternion.identity,
                        new Vector3(size.x - 2f, 0.12f, 2f), material);
                    break;

                case RoomPurpose.Warehouse:
                    for (int i = 0; i < 6; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, (i % 3) / 2f);
                        float z = i < 3 ? -halfZ : halfZ;
                        AddBox(root, $"Crate_{i}", new Vector3(x, 0.8f, z), Quaternion.identity,
                            new Vector3(1.6f, 1.6f, 1.6f), material);
                    }
                    break;

                case RoomPurpose.Shipyard:
                    AddBox(root, "Hull", new Vector3(0f, 1.2f, 0f), Quaternion.identity,
                        new Vector3(3f, 2.4f, 9f), material);
                    AddBox(root, "Scaffold", new Vector3(halfX, 1.6f, 0f), Quaternion.identity,
                        new Vector3(0.4f, 3.2f, 6f), material);
                    break;

                case RoomPurpose.Lighthouse:
                    AddBox(root, "Tower", new Vector3(0f, WallHeight * 0.5f, 0f), Quaternion.identity,
                        new Vector3(3f, WallHeight, 3f), material);
                    AddBox(root, "Lamp", new Vector3(0f, WallHeight - 0.4f, 0f), Quaternion.identity,
                        new Vector3(1.6f, 0.8f, 1.6f), Emissive("Mat_Lamp", new Color(1f, 0.9f, 0.6f), 8f));
                    break;

                case RoomPurpose.Temple:
                    AddBox(root, "Altar", new Vector3(0f, 0.6f, halfZ - 0.5f), Quaternion.identity,
                        new Vector3(2.6f, 1.2f, 1.2f), material);
                    for (int i = 0; i < 4; i++)
                    {
                        float z = Mathf.Lerp(-halfZ, halfZ - 3f, i / 3f);
                        AddBox(root, $"Pew_{i}", new Vector3(0f, 0.3f, z), Quaternion.identity,
                            new Vector3(5f, 0.6f, 0.5f), material);
                    }
                    break;

                case RoomPurpose.Tavern:
                    AddBox(root, "Bar", new Vector3(-halfX, 0.55f, 0f), Quaternion.identity,
                        new Vector3(1f, 1.1f, 5f), material);
                    goto case RoomPurpose.Inn;

                case RoomPurpose.Graveyard:
                    for (int i = 0; i < 8; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, (i % 4) / 3f);
                        float z = i < 4 ? -halfZ * 0.5f : halfZ * 0.5f;
                        AddBox(root, $"Stone_{i}", new Vector3(x, 0.6f, z), Quaternion.identity,
                            new Vector3(0.7f, 1.2f, 0.25f), material);
                    }
                    break;

                case RoomPurpose.Stables:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, i / 3f);
                        AddBox(root, $"Stall_{i}", new Vector3(x, 0.8f, -halfZ), Quaternion.identity,
                            new Vector3(2.2f, 1.6f, 0.2f), material);
                    }
                    break;

                case RoomPurpose.Bathhouse:
                    AddBox(root, "Pool", new Vector3(0f, 0.1f, 0f), Quaternion.identity,
                        new Vector3(size.x - 4.5f, 0.2f, size.y - 4.5f), material);
                    break;

                case RoomPurpose.Mill:
                    AddBox(root, "Wheel", new Vector3(halfX, 2f, 0f), Quaternion.identity,
                        new Vector3(0.4f, 4f, 4f), material);
                    AddBox(root, "Stone", new Vector3(0f, 0.5f, 0f), Quaternion.identity,
                        new Vector3(2.4f, 1f, 2.4f), material);
                    break;

                case RoomPurpose.Bank:
                case RoomPurpose.GemStore:
                case RoomPurpose.PawnShop:
                    AddBox(root, "Counter", new Vector3(0f, 0.55f, halfZ - 1f), Quaternion.identity,
                        new Vector3(size.x - 4f, 1.1f, 0.8f), material);
                    for (int i = 0; i < 3; i++)
                    {
                        float x = Mathf.Lerp(-halfX, halfX, i / 2f);
                        AddBox(root, $"Chest_{i}", new Vector3(x, 0.4f, -halfZ), Quaternion.identity,
                            new Vector3(1.2f, 0.8f, 0.9f), material);
                    }
                    break;

                case RoomPurpose.Armorer:
                case RoomPurpose.ClothingStore:
                case RoomPurpose.GeneralStore:
                case RoomPurpose.Bakery:
                case RoomPurpose.Alchemist:
                case RoomPurpose.Tannery:
                    AddBox(root, "Counter", new Vector3(0f, 0.55f, halfZ - 1f), Quaternion.identity,
                        new Vector3(size.x - 4f, 1.1f, 0.8f), material);
                    for (int i = 0; i < 3; i++)
                    {
                        float z = Mathf.Lerp(-halfZ, halfZ - 2.5f, i / 2f);
                        AddBox(root, $"Shelf_{i}", new Vector3(-halfX, 1.2f, z), Quaternion.identity,
                            new Vector3(0.6f, 2.4f, 1.8f), material);
                    }
                    break;

                case RoomPurpose.Bookseller:
                case RoomPurpose.FurnitureStore:
                    for (int i = 0; i < 4; i++)
                    {
                        float z = Mathf.Lerp(-halfZ, halfZ, i / 3f);
                        AddBox(root, $"Rack_{i}", new Vector3(-halfX, 1.3f, z), Quaternion.identity,
                            new Vector3(0.6f, 2.6f, 1.6f), material);
                        AddBox(root, $"Rack_R{i}", new Vector3(halfX, 1.3f, z), Quaternion.identity,
                            new Vector3(0.6f, 2.6f, 1.6f), material);
                    }
                    break;

                case RoomPurpose.Watchhouse:
                case RoomPurpose.Courthouse:
                case RoomPurpose.Palace:
                    AddBox(root, "Dais", new Vector3(0f, 0.25f, halfZ - 1.5f), Quaternion.identity,
                        new Vector3(size.x * 0.5f, 0.5f, 2.5f), material);
                    AddBox(root, "Seat", new Vector3(0f, 1.1f, halfZ - 1.5f), Quaternion.identity,
                        new Vector3(1.4f, 1.6f, 1.2f), material);
                    break;

                case RoomPurpose.Manor:
                case RoomPurpose.Guildhouse:
                case RoomPurpose.Townhouse:
                case RoomPurpose.Square:
                case RoomPurpose.Rotunda:
                case RoomPurpose.Gallery:
                case RoomPurpose.Crossing:
                case RoomPurpose.Stairwell:
                case RoomPurpose.CaveMouth:
                case RoomPurpose.Cavern:
                case RoomPurpose.Gatehouse:
                case RoomPurpose.Hall:
                    for (int i = 0; i < 2; i++)
                    {
                        float x = i == 0 ? -halfX : halfX;
                        AddBox(root, $"Pillar_A{i}", new Vector3(x, WallHeight * 0.5f, -halfZ * 0.5f),
                            Quaternion.identity, new Vector3(0.8f, WallHeight, 0.8f), material);
                        AddBox(root, $"Pillar_B{i}", new Vector3(x, WallHeight * 0.5f, halfZ * 0.5f),
                            Quaternion.identity, new Vector3(0.8f, WallHeight, 0.8f), material);
                    }
                    break;
            }
        }

        // An opening is a door sized hole, not a missing wall, two jambs and a lintel around it, so a
        // room stays enclosed whether or not anything ever attaches here
        static void BuildDoorway(Transform wall, float span)
        {
            float jamb = (span - DoorWidth) * 0.5f;

            if (jamb > 0.01f)
            {
                float offset = (DoorWidth + jamb) * 0.5f;

                AddBox(wall, "Jamb_L", new Vector3(-offset, WallHeight * 0.5f, -WallThickness * 0.5f),
                    Quaternion.identity, new Vector3(jamb, WallHeight, WallThickness), Trim);

                AddBox(wall, "Jamb_R", new Vector3(offset, WallHeight * 0.5f, -WallThickness * 0.5f),
                    Quaternion.identity, new Vector3(jamb, WallHeight, WallThickness), Trim);
            }

            float lintel = WallHeight - DoorHeight;

            if (lintel > 0.01f)
            {
                AddBox(wall, "Lintel", new Vector3(0f, DoorHeight + lintel * 0.5f, -WallThickness * 0.5f),
                    Quaternion.identity, new Vector3(DoorWidth, lintel, WallThickness), Trim);
            }
        }

        static GameObject AddBox(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return AddBox(parent, name, position, rotation, scale, null);
        }

        static GameObject AddBox(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale,
            Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.SetLocalPositionAndRotation(position, rotation);
            box.transform.localScale = scale;

            if (material != null)
                box.GetComponent<MeshRenderer>().sharedMaterial = material;

            return box;
        }

        // Surfaces are colour-coded so the greybox reads without art, floor, wall and ceiling are
        // three distinct values, doorway trim is warm so openings stand out, and stairs are the one
        // cool accent in the palette so a change of level is obvious from across a room
        internal static Material Surface(string name, Color colour, float smoothness = 0.15f)
        {
            string path = $"{MaterialRoot}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Material material = new Material(Shader.Find("HDRP/Lit"));
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);

            // The greybox is thousands of boxes over a handful of materials, which is exactly the
            // case instancing exists for
            material.enableInstancing = true;

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static Material Emissive(string name, Color colour, float intensity)
        {
            string path = $"{MaterialRoot}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Material material = new Material(Shader.Find("HDRP/Lit"));
            material.SetColor("_BaseColor", colour);
            material.SetColor("_EmissiveColor", colour * intensity);
            material.SetFloat("_UseEmissiveIntensity", 0f);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material Floor => Surface("Mat_Floor", new Color(0.44f, 0.41f, 0.36f));
        static Material Wall => Surface("Mat_Wall", new Color(0.27f, 0.27f, 0.30f));
        static Material Ceiling => Surface("Mat_Ceiling", new Color(0.15f, 0.15f, 0.17f));
        static Material Trim => Surface("Mat_Trim", new Color(0.58f, 0.46f, 0.29f));
        static Material CorridorFloor => Surface("Mat_CorridorFloor", new Color(0.34f, 0.33f, 0.33f));
        static Material StairSurface => Surface("Mat_Stair", new Color(0.27f, 0.44f, 0.55f));
        static Material Prop(RoomPurpose purpose) => Surface($"Mat_Prop_{purpose}", PropColour(purpose));

        static Color PropColour(RoomPurpose purpose)
        {
            switch (purpose)
            {
                case RoomPurpose.Gatehouse: return new Color(0.50f, 0.44f, 0.34f);
                case RoomPurpose.Stairwell: return new Color(0.42f, 0.44f, 0.48f);
                case RoomPurpose.CaveMouth: return new Color(0.36f, 0.34f, 0.30f);
                case RoomPurpose.Hall: return new Color(0.46f, 0.40f, 0.32f);
                case RoomPurpose.Crossing: return new Color(0.44f, 0.42f, 0.38f);
                case RoomPurpose.Rotunda: return new Color(0.56f, 0.52f, 0.44f);
                case RoomPurpose.Nave: return new Color(0.62f, 0.58f, 0.46f);
                case RoomPurpose.Gallery: return new Color(0.48f, 0.42f, 0.36f);
                case RoomPurpose.Cavern: return new Color(0.32f, 0.31f, 0.28f);
                case RoomPurpose.Guardroom: return new Color(0.40f, 0.30f, 0.22f);
                case RoomPurpose.Barracks: return new Color(0.55f, 0.34f, 0.28f);
                case RoomPurpose.Armoury: return new Color(0.62f, 0.62f, 0.68f);
                case RoomPurpose.Chapel: return new Color(0.72f, 0.66f, 0.44f);
                case RoomPurpose.Library: return new Color(0.45f, 0.32f, 0.20f);
                case RoomPurpose.Crypt: return new Color(0.52f, 0.52f, 0.50f);
                case RoomPurpose.Ossuary: return new Color(0.74f, 0.72f, 0.64f);
                case RoomPurpose.Barrow: return new Color(0.40f, 0.38f, 0.30f);
                case RoomPurpose.Reliquary: return new Color(0.78f, 0.66f, 0.30f);
                case RoomPurpose.Storeroom: return new Color(0.50f, 0.36f, 0.20f);
                case RoomPurpose.Vault: return new Color(0.58f, 0.54f, 0.34f);
                case RoomPurpose.Kitchen: return new Color(0.38f, 0.32f, 0.26f);
                case RoomPurpose.Cistern: return new Color(0.20f, 0.36f, 0.44f);
                case RoomPurpose.Well: return new Color(0.26f, 0.40f, 0.46f);
                case RoomPurpose.Grotto: return new Color(0.28f, 0.44f, 0.36f);
                case RoomPurpose.Forge: return new Color(0.30f, 0.26f, 0.26f);
                case RoomPurpose.Mine: return new Color(0.44f, 0.36f, 0.24f);
                case RoomPurpose.Alchemy: return new Color(0.40f, 0.52f, 0.34f);
                case RoomPurpose.Cell: return new Color(0.34f, 0.34f, 0.36f);
                case RoomPurpose.Torture: return new Color(0.44f, 0.24f, 0.22f);
                case RoomPurpose.Temple: return new Color(0.72f, 0.68f, 0.46f);
                case RoomPurpose.Tavern: return new Color(0.54f, 0.36f, 0.22f);
                case RoomPurpose.Bakery: return new Color(0.66f, 0.52f, 0.34f);
                case RoomPurpose.Alchemist: return new Color(0.38f, 0.56f, 0.40f);
                case RoomPurpose.Tannery: return new Color(0.42f, 0.30f, 0.22f);
                case RoomPurpose.Stables: return new Color(0.44f, 0.34f, 0.24f);
                case RoomPurpose.Watchhouse: return new Color(0.36f, 0.40f, 0.50f);
                case RoomPurpose.Graveyard: return new Color(0.46f, 0.46f, 0.44f);
                case RoomPurpose.Bathhouse: return new Color(0.34f, 0.52f, 0.56f);
                case RoomPurpose.Courthouse: return new Color(0.50f, 0.44f, 0.34f);
                case RoomPurpose.Bank: return new Color(0.64f, 0.56f, 0.30f);
                case RoomPurpose.Mill: return new Color(0.50f, 0.44f, 0.30f);
                case RoomPurpose.Armorer: return new Color(0.56f, 0.58f, 0.62f);
                case RoomPurpose.Bookseller: return new Color(0.44f, 0.32f, 0.22f);
                case RoomPurpose.ClothingStore: return new Color(0.58f, 0.42f, 0.52f);
                case RoomPurpose.FurnitureStore: return new Color(0.46f, 0.36f, 0.26f);
                case RoomPurpose.GemStore: return new Color(0.40f, 0.62f, 0.66f);
                case RoomPurpose.GeneralStore: return new Color(0.52f, 0.48f, 0.36f);
                case RoomPurpose.PawnShop: return new Color(0.48f, 0.40f, 0.44f);
                case RoomPurpose.Palace: return new Color(0.66f, 0.54f, 0.28f);
                case RoomPurpose.Market: return new Color(0.62f, 0.50f, 0.26f);
                case RoomPurpose.Smithy: return new Color(0.36f, 0.30f, 0.28f);
                case RoomPurpose.Inn: return new Color(0.52f, 0.38f, 0.24f);
                case RoomPurpose.Guildhouse: return new Color(0.44f, 0.36f, 0.56f);
                case RoomPurpose.Townhouse: return new Color(0.48f, 0.44f, 0.38f);
                case RoomPurpose.Square: return new Color(0.54f, 0.52f, 0.46f);
                case RoomPurpose.Arena: return new Color(0.50f, 0.30f, 0.26f);
                case RoomPurpose.GreatHall: return new Color(0.54f, 0.46f, 0.34f);
                case RoomPurpose.ThroneRoom: return new Color(0.62f, 0.48f, 0.20f);
                case RoomPurpose.Sanctum: return new Color(0.46f, 0.38f, 0.62f);
                case RoomPurpose.Lair: return new Color(0.36f, 0.28f, 0.24f);
                default: return new Color(0.45f, 0.45f, 0.45f);
            }
        }

        // A bracket and a flame, plus the point light that actually lets you see. Shadows are off,
        // a dungeon carries fifty odd of these and none of them need to cast
        static void AddTorch(Transform parent, Vector3 position, Quaternion rotation)
        {
            GameObject torch = new GameObject("Torch");
            torch.transform.SetParent(parent, false);
            torch.transform.SetLocalPositionAndRotation(position, rotation);

            AddBox(torch.transform, "Bracket", new Vector3(0f, 0f, 0.15f), Quaternion.identity,
                new Vector3(0.12f, 0.12f, 0.5f), Prop(RoomPurpose.Forge));

            AddBox(torch.transform, "Flame", new Vector3(0f, 0.18f, 0.42f), Quaternion.identity,
                new Vector3(0.22f, 0.30f, 0.22f), Emissive("Mat_Flame", new Color(1f, 0.62f, 0.26f), 6f));

            GameObject bulb = new GameObject("Light");
            bulb.transform.SetParent(torch.transform, false);
            bulb.transform.localPosition = new Vector3(0f, 0.25f, 0.5f);

            bulb.AddHDLight(LightType.Point);

            // HDAdditionalLightData.legacyLight is internal to HDRP, so the Light it requires is
            // configured directly. Punctual intensity is in lumen here (HDRP defaults to 600)
            Light light = bulb.GetComponent<Light>();
            light.color = new Color(1f, 0.76f, 0.48f);
            light.intensity = TorchLumens;
            light.range = TorchRange;
            light.shadows = LightShadows.None;
        }

        static GameObject Finish(GameObject root, string name, Bounds localBounds) =>
            Finish(root, name, localBounds, RoomPurpose.None);

        static GameObject Finish(GameObject root, string name, Bounds localBounds, RoomPurpose purpose)
        {
            RoomModule module = root.AddComponent<RoomModule>();
            new AssetAuthoring(module)
                .Bounds("localBounds", localBounds)
                .Enum("purpose", (int)purpose)
                .Refs("connectors", root.GetComponentsInChildren<ModuleConnector>(true))
                .Save();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{name}.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        static GameObject BuildPickupPrefab()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Pickup_Item";
            root.transform.localScale = Vector3.one * 0.35f;
            root.GetComponent<Collider>().isTrigger = false;
            root.AddComponent<ItemPickup>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/Pickup_Item.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        static GameObject BuildKeyPrefab()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Pickup_Key";
            root.transform.localScale = Vector3.one * 0.4f;
            root.AddComponent<DungeonKeyPickup>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/Pickup_Key.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        // Greybox bestiary, a capsule sized and tinted per creature. Shape carries the read, squat
        // and wide for a rat, tall and thin for a wraith, so they are distinguishable before art
        static readonly (string name, string id, string display, float height, float radius, Color colour,
            int health, int armour, int damage, float speed, float aggro, int xp)[] EnemyPlans =
        {
            // General: found in any dungeon
            ("Enemy_Rat", "enemy.rat", "Giant Rat", 0.7f, 0.3f, new Color(0.42f, 0.32f, 0.24f), 18, 0, 5, 3.2f, 10f, 4),
            ("Enemy_Spider", "enemy.spider", "Cave Spider", 0.9f, 0.45f, new Color(0.20f, 0.18f, 0.24f), 26, 0, 8, 3.6f, 12f, 7),
            ("Enemy_Bandit", "enemy.bandit", "Bandit", 1.9f, 0.35f, new Color(0.45f, 0.32f, 0.22f), 55, 2, 11, 2.6f, 14f, 12),
            ("Enemy_Skeleton", "enemy.skeleton", "Skeleton", 1.9f, 0.35f, new Color(0.80f, 0.78f, 0.70f), 55, 1, 9, 2.3f, 14f, 10),

            // Undead: crypts, barrows, ossuaries
            ("Enemy_Zombie", "enemy.zombie", "Zombie", 1.9f, 0.42f, new Color(0.36f, 0.44f, 0.32f), 80, 1, 13, 1.5f, 11f, 14),
            ("Enemy_Ghoul", "enemy.ghoul", "Ghoul", 1.8f, 0.34f, new Color(0.55f, 0.52f, 0.40f), 65, 1, 15, 3.1f, 15f, 16),
            ("Enemy_Wraith", "enemy.wraith", "Wraith", 2.2f, 0.28f, new Color(0.45f, 0.55f, 0.72f), 70, 4, 18, 2.8f, 17f, 24),

            // Garrison: barracks, keeps, gaols
            ("Enemy_Guard", "enemy.guard", "Corrupt Guard", 2.0f, 0.38f, new Color(0.40f, 0.42f, 0.50f), 90, 5, 16, 2.4f, 15f, 20),
            ("Enemy_Warhound", "enemy.warhound", "Warhound", 1.0f, 0.35f, new Color(0.32f, 0.26f, 0.22f), 40, 1, 12, 4.2f, 16f, 11),

            // Arcane: monasteries, sanctums.
            ("Enemy_Cultist", "enemy.cultist", "Cultist", 1.9f, 0.34f, new Color(0.42f, 0.28f, 0.52f), 50, 1, 14, 2.7f, 16f, 15),
            ("Enemy_Imp", "enemy.imp", "Imp", 1.1f, 0.30f, new Color(0.62f, 0.28f, 0.24f), 34, 0, 12, 3.8f, 15f, 13),

            // Depths: mines, waterworks, undercrofts
            ("Enemy_Kobold", "enemy.kobold", "Kobold", 1.3f, 0.32f, new Color(0.50f, 0.42f, 0.24f), 35, 1, 9, 3.0f, 13f, 8),
            ("Enemy_Slime", "enemy.slime", "Cave Slime", 0.9f, 0.55f, new Color(0.30f, 0.58f, 0.44f), 60, 3, 10, 1.2f, 8f, 12),
            ("Enemy_Bat", "enemy.bat", "Blood Bat", 0.6f, 0.28f, new Color(0.28f, 0.22f, 0.30f), 20, 0, 7, 4.6f, 14f, 6),

            // Bosses
            ("Enemy_BoneLord", "enemy.bonelord", "Bone Lord", 2.6f, 0.50f, new Color(0.86f, 0.82f, 0.62f), 260, 6, 30, 2.2f, 22f, 120),
            ("Enemy_Warlord", "enemy.warlord", "Warlord", 2.5f, 0.48f, new Color(0.52f, 0.36f, 0.30f), 300, 8, 34, 2.4f, 22f, 140),
            ("Enemy_Archmage", "enemy.archmage", "Archmage", 2.3f, 0.40f, new Color(0.48f, 0.36f, 0.70f), 220, 4, 38, 2.6f, 24f, 150)
        };

        // Reloaded by name rather than handing back the instances the dictionary holds, for the
        // same reason everything else here is, an import destroys them and the nulls are silent
        static EnemyDefinition[] AllOf(Dictionary<string, EnemyDefinition> bestiary)
        {
            List<EnemyDefinition> all = new List<EnemyDefinition>();

            foreach (string name in bestiary.Keys)
            {
                EnemyDefinition definition = Load<EnemyDefinition>(name);
                if (definition != null) all.Add(definition);
            }

            return all.ToArray();
        }

        // Everything authored under the content root, so the database never quietly misses an item
        static ItemDefinition[] AllItems()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { ContentRoot });
            ItemDefinition[] items = new ItemDefinition[guids.Length];

            for (int i = 0; i < guids.Length; i++)
                items[i] = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));

            return items;
        }

        // What a place's leavings run to. Every table holds every item, the profile decides what is
        // common there, and an item's value decides how deep you have to be before it appears at all
        static readonly (string set, float weapon, float armour, float consumable, float valuable, float ingredient)[] LootProfiles =
        {
            ("Crypt", 0.7f, 0.7f, 0.6f, 1.6f, 1.2f),
            ("Barracks", 1.6f, 1.5f, 1.1f, 0.6f, 0.5f),
            ("Undercroft", 0.8f, 0.8f, 1.6f, 1.1f, 1.1f),
            ("Monastery", 0.5f, 0.6f, 1.0f, 1.5f, 1.4f),
            ("Gaol", 1.2f, 1.0f, 0.9f, 0.8f, 0.7f),
            ("Mine", 0.6f, 0.7f, 1.0f, 1.3f, 1.8f),
            ("Sanctum", 0.5f, 0.6f, 0.9f, 1.7f, 1.5f),
            ("Barrow", 0.8f, 0.9f, 0.5f, 1.7f, 1.0f),
            ("Keep", 1.5f, 1.6f, 1.0f, 1.0f, 0.6f),
            ("Waterworks", 0.7f, 0.7f, 1.2f, 1.0f, 1.5f)
        };

        static void BuildLootTables()
        {
            foreach ((string set, float weapon, float armour, float consumable, float valuable, float ingredient) in LootProfiles)
            {
                LootTable($"Loot_{set}", weapon, armour, consumable, valuable, ingredient, false);
                LootTable($"Loot_{set}_Boss", weapon, armour, consumable, valuable, ingredient, true);
            }

            // What a body carries, rather than what a room holds
            LootTable("Loot_Kill", 1.1f, 0.9f, 1.4f, 0.7f, 0.9f, false);

            // What a shop has on its shelves. Merchants restock at shallow depth, so the deep tiers
            // are things you find rather than things you buy
            LootTable("Loot_Shop", 0.8f, 0.8f, 2f, 1f, 1.2f, false);

            // A taste of zero keeps the category off the shelf entirely, which is the whole point:
            // every merchant used to restock from the one table above, so the smith, the alchemist
            // and the general store sold the same things
            foreach ((string trade, float weapon, float armour, float consumable, float valuable, float ingredient)
                in ShopProfiles)
                LootTable($"Loot_Shop_{trade}", weapon, armour, consumable, valuable, ingredient, false);
        }

        static readonly (string trade, float weapon, float armour, float consumable, float valuable, float ingredient)[]
            ShopProfiles =
        {
            ("Smithy", 3.2f, 1.0f, 0f, 0f, 0.4f),
            ("Armorer", 0.6f, 3.4f, 0f, 0f, 0f),
            ("Alchemist", 0f, 0f, 2.6f, 0f, 3.2f),
            ("GeneralStore", 0.4f, 0.4f, 2.4f, 0.6f, 1.0f),
            ("Market", 0.8f, 0.8f, 2.0f, 1.4f, 1.6f),
            ("Inn", 0f, 0f, 3.0f, 0f, 0f),
            ("Tavern", 0f, 0f, 3.0f, 0f, 0f),
            ("PawnShop", 1.0f, 1.0f, 0.4f, 3.0f, 0.4f)
        };

        static void LootTable(string asset, float weapon, float armour, float consumable, float valuable,
            float ingredient, bool boss)
        {
            LootTableDefinition table = Create<LootTableDefinition>(asset);
            AssetAuthoring author = new AssetAuthoring(table);

            author.Int("minRolls", boss ? 2 : 1).Int("maxRolls", boss ? 4 : 2)
                .Int("minGold", boss ? 80 : 3).Int("maxGold", boss ? 400 : 30)
                .Float("enchantChance", boss ? 0.5f : 0.06f)
                .Int("minEnchantPower", 4).Int("maxEnchantPower", boss ? 18 : 9);

            author.Apply("entries", p =>
            {
                List<ItemDefinition> pool = new List<ItemDefinition>();

                foreach (ItemDefinition item in AllItems())
                {
                    // A boss is worth the walk, nothing cheap on its table at all
                    if (item == null || (boss && item.BaseValue < 300))
                        continue;

                    if (Taste(item, weapon, armour, consumable, valuable, ingredient) > 0f)
                        pool.Add(item);
                }

                p.arraySize = pool.Count;

                for (int i = 0; i < pool.Count; i++)
                {
                    ItemDefinition item = pool[i];
                    int value = Mathf.Max(1, item.BaseValue);

                    float taste = Taste(item, weapon, armour, consumable, valuable, ingredient);

                    float common = boss ? value / 120f : 400f / value;

                    SerializedProperty entry = p.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("Item").objectReferenceValue = item;
                    entry.FindPropertyRelative("Weight").intValue = Mathf.Clamp(Mathf.RoundToInt(common * taste), 1, 60);
                    entry.FindPropertyRelative("MinCount").intValue = 1;
                    entry.FindPropertyRelative("MaxCount").intValue = value < 30 ? 3 : 1;
                    entry.FindPropertyRelative("MinDepth").floatValue =
                        value < 120 ? 0f : value < 400 ? 0.3f : value < 900 ? 0.55f : 0.75f;
                }
            });

            author.Save();
        }

        static float Taste(ItemDefinition item, float weapon, float armour, float consumable, float valuable,
            float ingredient)
        {
            switch (item.Category)
            {
                case ItemCategory.Weapon: return weapon;
                case ItemCategory.Armour: return armour;
                case ItemCategory.Consumable: return consumable;
                case ItemCategory.Valuable: return valuable;
                case ItemCategory.Ingredient: return ingredient;
                default: return 1f;
            }
        }

        // Every creature's sheet, the same eight attributes the player has, four skill bands, and hit
        // points a level tuned so the derived health lands where the hand set numbers were
        static readonly (string id, int level, int hp, int str, int intel, int wil, int agi, int spd, int end,
            int per, int luck, int weapon, int defence, int magic, int stealth)[] CreaturePlans =
        {
            ("enemy.rat", 1, 15, 25, 10, 15, 45, 55, 30, 10, 40, 15, 10, 5, 45),
            ("enemy.spider", 2, 10, 30, 10, 20, 55, 50, 30, 10, 40, 22, 12, 5, 55),
            ("enemy.bandit", 3, 15, 45, 30, 30, 45, 45, 40, 35, 45, 35, 30, 5, 35),
            ("enemy.skeleton", 3, 15, 45, 10, 40, 35, 35, 40, 10, 35, 30, 25, 5, 25),
            ("enemy.zombie", 4, 15, 55, 5, 40, 20, 20, 55, 5, 30, 28, 15, 5, 10),
            ("enemy.ghoul", 4, 12, 50, 15, 35, 45, 45, 45, 10, 35, 38, 22, 5, 40),
            ("enemy.wraith", 5, 10, 35, 45, 60, 50, 45, 40, 20, 45, 42, 30, 45, 60),
            ("enemy.guard", 5, 13, 55, 30, 35, 45, 40, 55, 30, 40, 45, 45, 5, 25),
            ("enemy.warhound", 3, 9, 40, 10, 25, 55, 65, 40, 10, 40, 32, 18, 5, 45),
            ("enemy.cultist", 4, 9, 35, 50, 50, 40, 40, 35, 40, 45, 30, 20, 50, 35),
            ("enemy.imp", 3, 8, 25, 45, 40, 60, 60, 30, 25, 50, 30, 20, 45, 55),
            ("enemy.kobold", 2, 13, 35, 25, 25, 45, 45, 40, 20, 40, 26, 20, 5, 45),
            ("enemy.slime", 3, 14, 45, 5, 30, 15, 15, 60, 5, 30, 25, 30, 5, 15),
            ("enemy.bat", 1, 18, 20, 10, 15, 60, 70, 25, 5, 45, 20, 12, 5, 55),
            ("enemy.bonelord", 10, 20, 65, 55, 70, 45, 40, 60, 30, 50, 70, 60, 65, 30),
            ("enemy.warlord", 11, 21, 75, 40, 55, 55, 45, 65, 45, 50, 80, 70, 10, 30),
            ("enemy.archmage", 11, 16, 40, 80, 75, 50, 45, 45, 55, 55, 55, 45, 85, 40)
        };

        // Anything that fights with its hands fights with HandToHand; the drilled ones carry steel.
        static SkillId CreatureWeaponSkill(string id) =>
            id == "enemy.bandit" || id == "enemy.guard" || id == "enemy.warlord" || id == "enemy.cultist"
                ? SkillId.Longsword
                : SkillId.HandToHand;

        static Dictionary<string, EnemyDefinition> BuildEnemies()
        {
            Dictionary<string, EnemyDefinition> byName = new Dictionary<string, EnemyDefinition>();

            foreach ((string name, string id, string display, float height, float radius, Color colour,
                int health, int armour, int damage, float speed, float aggro, int xp) in EnemyPlans)
            {
                // The definition is created first and handed to the prefab, so the prefab is saved
                // once with the reference already on it. Writing it afterwards needs a second
                // PrefabUtility save, and that import discards every definition still dirty in
                // memory, which is everything, since SaveAssets only runs at the end
                Create<EnemyDefinition>(name);
                BuildEnemyPrefab(name, height, radius, colour, Load<EnemyDefinition>(name));

                EnemyDefinition definition = Load<EnemyDefinition>(name);

                new AssetAuthoring(definition)
                    .Str("id", id).Str("displayName", display)
                    .Ref("prefab", Prefab(name)).Ref("loot", Load<LootTableDefinition>("Loot_Kill"))
                    .Int("maxHealth", health).Int("armour", armour).Int("damage", damage)
                    .Float("reach", radius + 1.4f)
                    .Float("windup", 0.55f).Float("active", 0.15f).Float("recovery", 0.7f)
                    .Float("moveSpeed", speed).Float("aggroRange", aggro)
                    .Int("experience", xp)
                    .Enum("inflicts", (int)Infliction(id))
                    .Float("inflictChance", 0.2f)
                    .Enum("weaponSkill", (int)CreatureWeaponSkill(id))
                    .Apply("stats", p =>
                    {
                        foreach ((string plan, int level, int hp, int str, int intel, int wil, int agi, int spd,
                            int end, int per, int luck, int weapon, int defence, int magic, int stealth) in CreaturePlans)
                        {
                            if (plan != id)
                                continue;

                            p.FindPropertyRelative("Level").intValue = level;
                            p.FindPropertyRelative("HitPointsPerLevel").intValue = hp;
                            p.FindPropertyRelative("Strength").intValue = str;
                            p.FindPropertyRelative("Intelligence").intValue = intel;
                            p.FindPropertyRelative("Willpower").intValue = wil;
                            p.FindPropertyRelative("Agility").intValue = agi;
                            p.FindPropertyRelative("Speed").intValue = spd;
                            p.FindPropertyRelative("Endurance").intValue = end;
                            p.FindPropertyRelative("Personality").intValue = per;
                            p.FindPropertyRelative("Luck").intValue = luck;
                            p.FindPropertyRelative("Weapon").intValue = weapon;
                            p.FindPropertyRelative("Defence").intValue = defence;
                            p.FindPropertyRelative("Magic").intValue = magic;
                            p.FindPropertyRelative("Stealth").intValue = stealth;
                            break;
                        }
                    })
                    .Save();

                byName[name] = definition;
            }

            return byName;
        }

        // Rotting and crawling things carry illness, soldiers and constructs do not
        static EndlessDescent.Core.AfflictionId Infliction(string id)
        {
            switch (id)
            {
                case "enemy.rat": return EndlessDescent.Core.AfflictionId.Plague;
                case "enemy.spider": return EndlessDescent.Core.AfflictionId.Poison;
                case "enemy.slime": return EndlessDescent.Core.AfflictionId.Poison;
                case "enemy.zombie": return EndlessDescent.Core.AfflictionId.Witheringrot;
                case "enemy.ghoul": return EndlessDescent.Core.AfflictionId.SwampFever;
                case "enemy.bat": return EndlessDescent.Core.AfflictionId.SwampFever;
                default: return EndlessDescent.Core.AfflictionId.None;
            }
        }

        static GameObject BuildEnemyPrefab(string name, float height, float radius, Color colour,
            EnemyDefinition definition)
        {
            GameObject root = new GameObject(name);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * (height * 0.5f);
            body.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            Material skin = Surface($"Mat_{name}", colour);
            Material trim = Surface($"Mat_{name}Trim", colour * 0.55f + Color.white * 0.14f, 0.35f);

            body.GetComponent<MeshRenderer>().sharedMaterial = skin;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            GreyboxSilhouettes.Shape(root.transform, body.transform,
                GreyboxSilhouettes.BuildOf(name), height, radius, skin, trim);

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = radius;
            controller.center = Vector3.up * (height * 0.5f);

            Poise poise = root.AddComponent<Poise>();
            Health health = root.AddComponent<Health>();
            MeleeAttacker attacker = root.AddComponent<MeleeAttacker>();
            EnemySenses senses = root.AddComponent<EnemySenses>();
            EnemyBrain brain = root.AddComponent<EnemyBrain>();
            // Without this a kill is only a corpse, nothing tells the journal it happened
            root.AddComponent<EnemyDeathReporter>();

            LootDropper dropper = root.AddComponent<LootDropper>();
            HitResponse response = root.AddComponent<HitResponse>();

            // Something that moves when it attacks. A capsule with no limb gives the player no
            // windup to read, and the whole combat rests on reading the windup
            GameObject arm = new GameObject("Arm");
            arm.transform.SetParent(root.transform, false);
            arm.transform.localPosition = new Vector3(radius * 0.9f, height * 0.62f, radius * 0.4f);

            AddVisual(arm.transform, "Limb", new Vector3(0f, 0f, 0.34f), Quaternion.identity,
                new Vector3(0.16f, 0.16f, 0.72f), skin);

            AddVisual(arm.transform, "Weapon", new Vector3(0f, 0f, 0.9f), Quaternion.identity,
                new Vector3(0.1f, 0.34f, 0.62f), Surface("Mat_Steel", new Color(0.66f, 0.68f, 0.72f), 0.55f));

            SwingArm swing = root.AddComponent<SwingArm>();

            new AssetAuthoring(health).Ref("poise", poise).Str("saveKey", $"health.{name}").Save();
            new AssetAuthoring(senses).Ref("definition", definition).Ref("eyes", body.transform).Save();
            new AssetAuthoring(response)
                .Ref("health", health).Ref("body", body.GetComponent<Renderer>()).Save();

            new AssetAuthoring(swing).Ref("attacker", attacker).Ref("arm", arm.transform).Save();

            // Without a definition EnemyBrain.Update returns on its first line and Awake never
            // configures health or the attacker, the enemy is an inert capsule
            new AssetAuthoring(brain)
                .Ref("definition", definition)
                .Ref("health", health).Ref("poise", poise).Ref("attacker", attacker).Ref("senses", senses)
                .Save();
            new AssetAuthoring(dropper).Ref("health", health).Ref("brain", brain).Save();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{name}.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        // Three material tiers across weapons and armour, plus the consumables and valuables a shop
        // needs to have anything on its shelves. Flat by depth, as decided
        static readonly (string asset, string display, int damage, float reach, float stamina, float weight, int value, SkillId skill)[] WeaponPlans =
        {
            ("Item_Dagger", "Dagger", 8, 1.6f, 9f, 1.5f, 30, SkillId.ShortBlade),
            ("Item_Shortsword", "Shortsword", 14, 2.2f, 16f, 3f, 90, SkillId.ShortBlade),
            ("Item_Longsword", "Longsword", 20, 2.6f, 22f, 5f, 220, SkillId.Longsword),
            ("Item_SteelLongsword", "Steel Longsword", 27, 2.6f, 22f, 5.5f, 480, SkillId.Longsword),
            ("Item_Battleaxe", "Battleaxe", 30, 2.4f, 30f, 8f, 420, SkillId.Axe),
            ("Item_Warhammer", "Warhammer", 33, 2.2f, 34f, 9f, 460, SkillId.BluntWeapon),
            ("Item_Mace", "Mace", 18, 2f, 20f, 5f, 160, SkillId.BluntWeapon),
            ("Item_SilverSword", "Silver Longsword", 24, 2.6f, 20f, 4.5f, 900, SkillId.Longsword),

            // The tier below the deepest floors, and the reason to reach them
            ("Item_RunedLongsword", "Runed Longsword", 34, 2.7f, 21f, 5f, 1600, SkillId.Longsword),
            ("Item_RunedAxe", "Runed Axe", 38, 2.5f, 28f, 7.5f, 1500, SkillId.Axe)
        };

        static readonly (string asset, string display, int armour, EquipSlot slot, float weight, int value,
            ArmourWeight load)[] ArmourPlans =
        {
            ("Item_LeatherCap", "Leather Cap", 2, EquipSlot.Head, 1.5f, 40, ArmourWeight.Light),
            ("Item_Jerkin", "Leather Jerkin", 4, EquipSlot.Chest, 6f, 70, ArmourWeight.Light),
            ("Item_LeatherGreaves", "Leather Greaves", 3, EquipSlot.Legs, 4f, 55, ArmourWeight.Light),
            ("Item_LeatherBoots", "Leather Boots", 2, EquipSlot.Feet, 2f, 35, ArmourWeight.Light),
            ("Item_LeatherGloves", "Leather Gloves", 1, EquipSlot.Hands, 1f, 25, ArmourWeight.Light),
            ("Item_IronHelm", "Iron Helm", 4, EquipSlot.Head, 3.5f, 110, ArmourWeight.Medium),
            ("Item_IronCuirass", "Iron Cuirass", 8, EquipSlot.Chest, 12f, 260, ArmourWeight.Heavy),
            ("Item_IronGreaves", "Iron Greaves", 6, EquipSlot.Legs, 8f, 190, ArmourWeight.Medium),
            ("Item_SteelHelm", "Steel Helm", 6, EquipSlot.Head, 3f, 240, ArmourWeight.Medium),
            ("Item_SteelCuirass", "Steel Cuirass", 12, EquipSlot.Chest, 11f, 620, ArmourWeight.Heavy),

            // The deepest tier is worth wearing partly because it is lighter than what it beats
            ("Item_RunedCuirass", "Runed Cuirass", 15, EquipSlot.Chest, 10f, 1500, ArmourWeight.Medium),
            ("Item_RunedHelm", "Runed Helm", 8, EquipSlot.Head, 2.8f, 700, ArmourWeight.Light)
        };

        // A plank, a banded shield and a steel tower. What each soaks, what holding it costs and how
        // long the parry window stays open are what tell them apart now, they used to differ only
        // by an armour rating that blocking never read
        static readonly (string asset, string display, int armour, float weight, int value,
            ArmourWeight load, float block, float stamina, float parry)[] ShieldPlans =
        {
            ("Item_Shield", "Oak Shield", 2, 4f, 60, ArmourWeight.Light, 0.42f, 9f, 0.30f),
            ("Item_IronShield", "Iron Shield", 5, 7f, 180, ArmourWeight.Medium, 0.58f, 13f, 0.24f),
            ("Item_SteelShield", "Steel Shield", 8, 6.5f, 400, ArmourWeight.Heavy, 0.72f, 17f, 0.18f)
        };

        static readonly (string asset, string display, float hunger, int heal, float weight, int value)[] ConsumablePlans =
        {
            ("Item_Bread", "Stale Bread", 30f, 5, 0.4f, 5),
            ("Item_Cheese", "Cheese", 25f, 4, 0.3f, 8),
            ("Item_DriedMeat", "Dried Meat", 40f, 8, 0.5f, 14),
            ("Item_Ale", "Ale", 10f, 3, 0.8f, 6),
            ("Item_HealingPotion", "Healing Potion", 0f, 60, 0.5f, 120),
            ("Item_StrongPotion", "Strong Healing Potion", 0f, 140, 0.6f, 340)
        };

        static readonly (string asset, string display, ItemCategory category, float weight, int value, int stack)[] SundryPlans =
        {
            ("Item_BoneDust", "Bone Dust", ItemCategory.Ingredient, 0.2f, 6, 20),
            ("Item_Nightshade", "Nightshade", ItemCategory.Ingredient, 0.1f, 18, 20),
            ("Item_IronOre", "Iron Ore", ItemCategory.Ingredient, 2f, 12, 10),
            ("Item_Emerald", "Emerald", ItemCategory.Valuable, 0.1f, 260, 5),
            ("Item_SilverGoblet", "Silver Goblet", ItemCategory.Valuable, 1.2f, 140, 5),
            ("Item_GoldRing", "Gold Ring", ItemCategory.Valuable, 0.1f, 320, 5)
        };

        static void BuildItems()
        {
            foreach ((string asset, string display, int damage, float reach, float stamina, float weight, int value,
                SkillId skill) in WeaponPlans)
            {
                WeaponDefinition weapon = Item<WeaponDefinition>(asset, display, ItemCategory.Weapon, weight, value, 1);
                new AssetAuthoring(weapon)
                    .Int("damage", damage).Float("reach", reach).Float("staminaCost", stamina)
                    .Float("windup", 0.26f).Float("active", 0.12f).Float("recovery", 0.34f)
                    .Enum("skill", (int)skill)
                    .Save();
            }

            foreach ((string asset, string display, int armour, EquipSlot slot, float weight, int value,
                ArmourWeight load) in ArmourPlans)
            {
                ArmourDefinition piece = Item<ArmourDefinition>(asset, display, ItemCategory.Armour, weight, value, 1);
                new AssetAuthoring(piece)
                    .Int("armour", armour).Enum("slot", (int)slot).Enum("weightClass", (int)load)
                    .Save();
            }

            foreach ((string asset, string display, int armour, float weight, int value, ArmourWeight load,
                float block, float stamina, float parry) in ShieldPlans)
            {
                ShieldDefinition shield = Item<ShieldDefinition>(asset, display, ItemCategory.Armour, weight, value, 1);
                new AssetAuthoring(shield)
                    .Int("armour", armour).Enum("slot", (int)EquipSlot.OffHand).Enum("weightClass", (int)load)
                    .Float("blockFraction", block).Float("blockStaminaCost", stamina).Float("parryWindow", parry)
                    .Save();
            }

            foreach ((string asset, string display, float hunger, int heal, float weight, int value) in ConsumablePlans)
            {
                ConsumableDefinition food = Item<ConsumableDefinition>(asset, display, ItemCategory.Consumable, weight, value, 10);
                new AssetAuthoring(food).Float("reduceHunger", hunger).Int("restoreHealth", heal).Save();
            }

            foreach ((string asset, string display, ItemCategory category, float weight, int value, int stack) in SundryPlans)
                Item<ItemDefinition>(asset, display, category, weight, value, stack);
        }

        static T Item<T>(string asset, string display, ItemCategory category, float weight, int value, int stack)
            where T : ItemDefinition
        {
            T item = Create<T>(asset);
            new AssetAuthoring(item)
                .Str("id", $"item.{asset.ToLowerInvariant()}")
                .Str("displayName", display)
                .Enum("category", (int)category)
                .Float("weight", weight)
                .Int("baseValue", value)
                .Int("maxStack", stack)
                .Save();

            return item;
        }

        internal static T Create<T>(string assetName) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, $"{ContentRoot}/{assetName}.asset");
            return asset;
        }

        internal static GameObject Prefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{name}.prefab");

        internal static T Load<T>(string assetName) where T : ScriptableObject =>
            AssetDatabase.LoadAssetAtPath<T>($"{ContentRoot}/{assetName}.asset");

        static void Reset(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder);

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
