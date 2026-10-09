using System.Collections.Generic;
using EndlessDescent.Combat;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Dungeons;
using EndlessDescent.Items;
using EndlessDescent.Player;
using EndlessDescent.Quests;
using EndlessDescent.Settlements;
using EndlessDescent.Simulation;
using EndlessDescent.UI;
using EndlessDescent.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace EndlessDescent.EditorTools
{
    public static class PlaytestSceneBuilder
    {
        const string ContentRoot = "Assets/Project/Content/Greybox";
        const string PrefabRoot = "Assets/Project/Prefabs/Greybox";
        const string SceneFolder = "Assets/Project/Scenes/Test";
        const string WorldSceneFolder = "Assets/Project/Scenes/World";
        const string WorldScenePath = "Assets/Project/Scenes/World/Ordovan.unity";

        // What the scene is for. A dungeon test and a settlement test want different starting
        // places, and neither wants to sit behind the character creator while it is being iterated on
        public enum Playtest { Dungeon, Settlement, CharacterCreation, Wilderness, World }

        [MenuItem("Endless Descent/Build Playtest Scene/Settlement")]
        public static void BuildSettlementScene() => Ask(Playtest.Settlement);

        [MenuItem("Endless Descent/Build Playtest Scene/Dungeon")]
        public static void BuildDungeonScene() => Ask(Playtest.Dungeon);

        [MenuItem("Endless Descent/Build Playtest Scene/Character Creation")]
        public static void BuildCreationScene() => Ask(Playtest.CharacterCreation);

        [MenuItem("Endless Descent/Build Playtest Scene/Wilderness")]
        public static void BuildWildernessScene() => Ask(Playtest.Wilderness);

        // Not a playtest scene, and deliberately not filed with them: this is the world itself
        [MenuItem("Endless Descent/Build World Scene")]
        public static void BuildWorldScene()
        {
            if (!ContentIsCurrent())
                return;

            WorldGeography geography = WorldGeography.Ordovan();
            Rect area = geography.Extent();
            int tiles = Mathf.CeilToInt(area.width / 16f) * Mathf.CeilToInt(area.height / 16f);

            bool proceed = EditorUtility.DisplayDialog("Build the world scene",
                $"Creates {WorldScenePath}.\n\n" +
                $"Bakes {tiles} coarse terrains covering the whole empire ({area.width:0} x {area.height:0} km) " +
                $"into Assets/Project/Content/Generated/Terrain/Overview, deleting whatever is there now, " +
                $"and places all {OrdovanPlaces.All.Length} places.\n\n" +
                "This takes a couple of minutes. The four playtest scenes are untouched. " +
                "The open scene is replaced, so save it first.",
                "Build", "Cancel");

            if (!proceed)
                return;

            EditorApplication.delayCall += () => BuildNow(Playtest.World);
        }

        static void Ask(Playtest kind)
        {
            if (!ContentIsCurrent())
                return;

            bool proceed = EditorUtility.DisplayDialog($"Build {Describe(kind)} playtest scene",
                $"Creates a new scene under {SceneFolder}/ with a fresh seed.\n\n" +
                $"{Explain(kind)}\n\n" +
                "Existing playtest scenes are kept. The open scene is replaced, so save it first.",
                "Build", "Cancel");

            if (!proceed)
                return;

            EditorApplication.delayCall += () => BuildNow(kind);
        }

        static string Describe(Playtest kind)
        {
            switch (kind)
            {
                case Playtest.Dungeon: return "dungeon";
                case Playtest.Settlement: return "settlement";
                case Playtest.Wilderness: return "wilderness";
                case Playtest.World: return "world";
                default: return "character creation";
            }
        }

        static string Explain(Playtest kind)
        {
            switch (kind)
            {
                case Playtest.Dungeon:
                    return "You start in the Crypt, as an Adventurer. The character creator is skipped.";
                case Playtest.Settlement:
                    return "You start on the square in Ashmere, as an Adventurer. The creator is skipped.";
                case Playtest.Wilderness:
                    return "You start on open ground where Ashmere stands, and the land streams in around " +
                           "you as you walk. No town and no dungeon is generated.";
                case Playtest.World:
                    return "The whole empire: a coarse terrain of every province to look at and edit, all " +
                           "seventy-one places marked on it, and the streamer laying real ground under you " +
                           "wherever you stand. The coarse bake stands down when you press Play.";
                default:
                    return "Opens on the character creator. Choosing BEGIN drops you into Ashmere.";
            }
        }

        // The scene builder reads content the content builder writes, so a stale Greybox folder shows
        // up here as null references in the built scene rather than as an error anyone can act on
        static bool ContentIsCurrent()
        {
            if (LoadDatabase() == null)
            {
                Report("There is no GameDatabase.", "Run 'Endless Descent > Build Greybox Content' first.");
                return false;
            }

            int version = LoadDatabase().ContentVersion;

            if (version != GreyboxContentBuilder.ContentVersion)
            {
                Report($"The Greybox content is version {version}, this build expects " +
                       $"{GreyboxContentBuilder.ContentVersion}.",
                    "Fields added since that content was written would silently take their C# " +
                    "defaults rather than the values the builder means to give them.\n\n" +
                    "Run 'Endless Descent > Build Greybox Content' again, then build the scene.");
                return false;
            }

            if (Content<RegionMap>("RegionMap") == null || Content<ClassDefinition>("Class_Adventurer") == null ||
                Material("Mat_TownWall") == null)
            {
                Report("The Greybox content is incomplete.",
                    "It has no RegionMap, no Adventurer class or no town materials.\n\n" +
                    "Run 'Endless Descent > Build Greybox Content' again, then build the scene.");
                return false;
            }

            return true;
        }

        static void Report(string headline, string what)
        {
            string message = $"{headline}\n\n{what}";

            if (InteractiveMode)
                EditorUtility.DisplayDialog("Content out of date", message, "OK");

            Debug.LogError($"{headline} {what.Replace("\n\n", " ")}");
        }

        static bool InteractiveMode => !Application.isBatchMode;

        // Also the -executeMethod entry point, no dialog, no delayCall
        public static void BuildNow() => BuildNow(Playtest.Settlement);

        public static void BuildNow(Playtest kind)
        {
            GameDatabase database = LoadDatabase();
            if (database == null)
            {
                Debug.LogError($"No GameDatabase at {ContentRoot}/GameDatabase.asset. " +
                               "Run 'Endless Descent > Build Greybox Content' first.");
                return;
            }

            try
            {
                Generate(database, Random.Range(1, int.MaxValue), kind);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Playtest scene build failed: {e}");
            }
        }

        static GameDatabase LoadDatabase() =>
            AssetDatabase.LoadAssetAtPath<GameDatabase>($"{ContentRoot}/GameDatabase.asset");

        static void Generate(GameDatabase database, int seed, Playtest kind)
        {
            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // NewScene unloads assets the new scene does not reference, which invalidates anything
            // loaded before it. Every reference taken from a stale database serialises as a silent
            // null, that is how the dungeon ended up with no enemies at all
            database = LoadDatabase();

            GameObject clockObject = new GameObject("WorldClock");
            clockObject.AddComponent<WorldClock>();

            GameObject systems = new GameObject("Systems");
            NpcDirectory npcs = systems.AddComponent<NpcDirectory>();
            FactionRegistry factions = systems.AddComponent<FactionRegistry>();
            EconomySystem economy = systems.AddComponent<EconomySystem>();
            PropertyRegistry properties = systems.AddComponent<PropertyRegistry>();
            systems.AddComponent<CraftingSystem>();
            WorldSimulation simulation = systems.AddComponent<WorldSimulation>();
            QuestJournal journal = systems.AddComponent<QuestJournal>();
            GuildMembership guilds = systems.AddComponent<GuildMembership>();
            systems.AddComponent<CrimeRecord>();
            SaveCoordinator saves = systems.AddComponent<SaveCoordinator>();

            new AssetAuthoring(factions).Ref("database", database).Save();
            new AssetAuthoring(economy).Ref("factions", factions).Save();
            new AssetAuthoring(guilds).Ref("factions", factions).Save();

            new AssetAuthoring(properties).Ref("database", database).Save();
            new AssetAuthoring(simulation).Ref("npcs", npcs).Ref("properties", properties).Save();

            GameObject player = BuildPlayer(database, factions, npcs, journal);

            new AssetAuthoring(systems.AddComponent<SaveHotkeys>())
                .Ref("input", player.GetComponent<PlayerInputReader>())
                .Ref("coordinator", saves)
                .Save();

            new AssetAuthoring(journal)
                .Ref("database", database).Ref("factions", factions).Ref("npcs", npcs)
                .Ref("inventory", player.GetComponent<Inventory>())
                .Ref("wallet", player.GetComponent<Wallet>())
                .Save();

            BuildLighting();
            BuildPlace(database, seed, player, kind);
            BuildHud(player, kind);
            BuildEventSystem();

            if (kind == Playtest.World)
                BakeWorldOverview();

            string folder = kind == Playtest.World ? WorldSceneFolder : SceneFolder;
            string scenePath = kind == Playtest.World
                ? WorldScenePath
                : $"{SceneFolder}/Playtest_{kind}_{seed}.unity";

            System.IO.Directory.CreateDirectory(folder);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            VerifyScene(player);

            Debug.Log($"Playtest scene built at {scenePath} (seed {seed}). Press Play. {Explain(kind)} " +
                      "Tab, C, M and J open the menu. Enemies use direct steering and will walk into " +
                      "walls (ADR 0009).");
        }

        // Without this nothing on any canvas is clickable, a GraphicRaycaster finds the button, but
        // there is no EventSystem to dispatch to it. NewSceneSetup.DefaultGameObjects does not make
        // one, which is why character creation looked frozen rather than broken
        static void BuildEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            GameObject events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();

            // Left unassigned on purpose, the module assigns the package's own default UI actions in
            // OnEnable when it has none, which covers Point, Click, Navigate, Submit and Cancel
            events.AddComponent<InputSystemUIInputModule>();
        }

        // Interiors are lit by torches and nothing else, so the exposure floor is what decides
        // whether a corridor reads as dim or as black
        static void BuildLighting()
        {
            GameObject sun = GameObject.Find("Directional Light");
            if (sun != null)
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            System.IO.Directory.CreateDirectory(SceneFolder);
            AssetDatabase.CreateAsset(profile, $"{SceneFolder}/Playtest_Exposure.asset");

            Exposure exposure = profile.Add<Exposure>();
            exposure.mode.overrideState = true;
            exposure.mode.value = ExposureMode.Automatic;
            exposure.compensation.overrideState = true;
            exposure.compensation.value = 1.6f;
            exposure.limitMin.overrideState = true;
            exposure.limitMin.value = -6f;

            EditorUtility.SetDirty(profile);

            GameObject volumeObject = new GameObject("Exposure");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        static GameObject BuildPlayer(GameDatabase database, FactionRegistry factions, NpcDirectory npcs, QuestJournal journal)
        {
            GameObject player = new GameObject("Player") { tag = "Player" };
            player.transform.position = new Vector3(0f, 1f, 0f);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            GameObject pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(player.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.65f, 0f);

            GameObject cameraObject = GameObject.Find("Main Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                cameraObject.AddComponent<Camera>();
            }

            cameraObject.transform.SetParent(pivot.transform, false);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;

            // Unity's default far plane is 1000 m, which is less than the terrain ring is wide: the
            // world was there and simply could not be seen. The near plane comes up with it, because
            // depth precision is the ratio between the two and 0.3 m against 6 km is a lot to ask
            if (cameraObject.GetComponent<ViewSettings>() == null)
                cameraObject.AddComponent<ViewSettings>();

            Camera view = cameraObject.GetComponent<Camera>();
            // Far enough to see the distant ring, which is what puts mountains on the horizon. The
            // near plane comes up with it, depth precision is the ratio between the two
            view.farClipPlane = 45000f;
            view.nearClipPlane = 0.3f;

            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Project/Settings/InputSystem_Actions.inputactions");

            PlayerInputReader input = player.AddComponent<PlayerInputReader>();
            new AssetAuthoring(input).Ref("actions", actions).Save();

            PlayerStamina stamina = player.AddComponent<PlayerStamina>();
            Poise poise = player.AddComponent<Poise>();
            Inventory inventory = player.AddComponent<Inventory>();
            Wallet wallet = player.AddComponent<Wallet>();
            Equipment equipment = player.AddComponent<Equipment>();
            SurvivalNeeds needs = player.AddComponent<SurvivalNeeds>();
            Health health = player.AddComponent<Health>();
            MeleeAttacker attacker = player.AddComponent<MeleeAttacker>();
            PlayerCombat combat = player.AddComponent<PlayerCombat>();
            PlayerDefence defence = player.AddComponent<PlayerDefence>();

            CharacterSheet sheet = player.AddComponent<CharacterSheet>();
            // The sheet reads worn gear for attributes and rebuilds its class from a blueprint on
            // load, so it needs both the equipment and the database it resolves ids against
            new AssetAuthoring(sheet)
                .Ref("characterClass", Content<ClassDefinition>("Class_Adventurer"))
                .Ref("database", database)
                .Ref("equipment", equipment)
                .Save();
            PlayerStealth stealth = player.AddComponent<PlayerStealth>();
            AfflictionTracker afflictions = player.AddComponent<AfflictionTracker>();
            Mana mana = player.AddComponent<Mana>();
            Spellcasting spellcasting = player.AddComponent<Spellcasting>();
            player.AddComponent<SpellRecord>();
            PlayerStance stance = player.AddComponent<PlayerStance>();
            GearLoad gear = player.AddComponent<GearLoad>();

            PlayerLook look = player.AddComponent<PlayerLook>();
            PlayerLocomotion locomotion = player.AddComponent<PlayerLocomotion>();
            PlayerMantle mantle = player.AddComponent<PlayerMantle>();
            PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
            PlayerNeedsEffects needsEffects = player.AddComponent<PlayerNeedsEffects>();

            new AssetAuthoring(gear).Ref("equipment", equipment).Save();
            new AssetAuthoring(stamina).Ref("gear", gear).Save();
            new AssetAuthoring(stealth).Ref("gear", gear).Save();

            new AssetAuthoring(inventory).Ref("database", database).Save();
            new AssetAuthoring(equipment).Ref("database", database).Ref("inventory", inventory).Save();
            new AssetAuthoring(health).Ref("poise", poise).Ref("mitigation", defence).Str("saveKey", "health.player").Save();
            new AssetAuthoring(attacker).Ref("origin", pivot.transform).Save();
            new AssetAuthoring(player.AddComponent<CharacterSetup>())
                .Ref("sheet", sheet).Ref("inventory", inventory).Ref("equipment", equipment)
                .Ref("wallet", wallet).Ref("database", database).Ref("factions", factions)
                .Save();

            new AssetAuthoring(combat)
                .Ref("input", input).Ref("stamina", stamina).Ref("equipment", equipment)
                .Ref("attacker", attacker).Ref("stance", stance).Save();

            new AssetAuthoring(stance).Ref("input", input).Ref("spellcasting", spellcasting).Save();
            new AssetAuthoring(defence)
                .Ref("combat", combat).Ref("equipment", equipment)
                .Ref("input", input).Ref("stamina", stamina).Ref("spellcasting", spellcasting)
                .Save();

            new AssetAuthoring(spellcasting)
                .Ref("input", input).Ref("stance", stance).Ref("mana", mana).Ref("health", health)
                .Ref("circles", Object.FindFirstObjectByType<GuildMembership>())
                .Ref("castOrigin", pivot.transform)
                .Ref("projectilePrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Spell_Bolt.prefab"))
                .Refs("spells", Content<SpellDefinition>("Spell_Firebolt"), Content<SpellDefinition>("Spell_Mend"),
                    Content<SpellDefinition>("Spell_Ward"))
                .Save();

            new AssetAuthoring(stealth)
                .Ref("input", input).Ref("controller", controller).Ref("sheet", sheet)
                .Save();

            new AssetAuthoring(afflictions).Ref("health", health).Ref("sheet", sheet).Save();

            new AssetAuthoring(player.AddComponent<PlayerDeath>())
                .Ref("health", health).Ref("wallet", wallet).Ref("controller", controller)
                .Save();

            new AssetAuthoring(player.AddComponent<CharacterStatsBinder>())
                .Ref("sheet", sheet).Ref("health", health).Ref("mana", mana)
                .Ref("stamina", stamina).Ref("inventory", inventory)
                .Save();

            new AssetAuthoring(player.AddComponent<SkillProgression>())
                .Ref("sheet", sheet).Ref("attacker", attacker).Ref("defence", defence)
                .Ref("combat", combat).Ref("spellcasting", spellcasting).Ref("equipment", equipment)
                .Ref("locomotion", locomotion)
                .Save();

            StartingKit(player);
            BuildViewmodel(cameraObject.transform, player);
            new AssetAuthoring(look).Ref("input", input).Ref("cameraPivot", pivot.transform).Save();
            new AssetAuthoring(locomotion)
                .Ref("input", input).Ref("stamina", stamina).Ref("cameraPivot", pivot.transform)
                .Ref("gear", gear).Save();
            new AssetAuthoring(mantle)
                .Ref("input", input).Ref("stamina", stamina).Ref("locomotion", locomotion).Save();
            new AssetAuthoring(interactor).Ref("input", input).Ref("rayOrigin", pivot.transform).Save();
            new AssetAuthoring(needsEffects).Ref("needs", needs).Ref("stamina", stamina).Save();

            return player;
        }

        // Something in hand on the first frame: the greybox has no shop or starting chest yet, so a
        // sword, a shield and a jerkin are put straight into the pack and worn.
        // Parented to the camera so it reads as first person, and driven purely off combat state
        static void BuildViewmodel(Transform camera, GameObject player)
        {
            GameObject root = new GameObject("Viewmodel");
            root.transform.SetParent(camera, false);

            Transform sword = Spawn(root.transform, "Vm_Sword");
            Transform shield = Spawn(root.transform, "Vm_Shield");
            Transform focus = Spawn(root.transform, "Vm_Focus");

            PlayerViewmodel viewmodel = root.AddComponent<PlayerViewmodel>();

            new AssetAuthoring(viewmodel)
                .Ref("weapon", sword).Ref("shield", shield).Ref("focus", focus)
                .Refs("weaponShapes", WeaponShapes(sword))
                .Ref("attacker", player.GetComponent<MeleeAttacker>())
                .Ref("defence", player.GetComponent<PlayerDefence>())
                .Ref("spellcasting", player.GetComponent<Spellcasting>())
                .Ref("equipment", player.GetComponent<Equipment>())
                .Ref("stance", player.GetComponent<PlayerStance>())
                .Save();
        }

        // In WeaponClass order, taken from the enum rather than from the child order: the viewmodel
        // indexes this array by the class, so a shape missing from the prefab must come back as a
        // null in its own slot and not shift every shape after it
        static Object[] WeaponShapes(Transform sword)
        {
            System.Array classes = System.Enum.GetValues(typeof(WeaponClass));
            Object[] shapes = new Object[classes.Length];

            if (sword == null)
                return shapes;

            for (int i = 0; i < classes.Length; i++)
            {
                Transform shape = sword.Find($"Shape_{(WeaponClass)classes.GetValue(i)}");
                shapes[i] = shape;

                if (shape == null)
                    Debug.LogError($"Vm_Sword has no Shape_{(WeaponClass)classes.GetValue(i)}. Rebuild the greybox content.");
            }

            return shapes;
        }

        static Transform Spawn(Transform parent, string prefabName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{prefabName}.prefab");
            if (prefab == null)
                return null;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            return instance.transform;
        }

        // Reads the built scene back rather than trusting that the authoring ran: a reference that
        // silently serialised as null is the failure mode this project keeps hitting
        static void VerifyScene(GameObject player)
        {
            int problems = 0;

            problems += Require(player.GetComponent<Inventory>(), "database", "Inventory");
            problems += Require(player.GetComponent<Equipment>(), "database", "Equipment");

            DungeonPopulator populator = Object.FindFirstObjectByType<DungeonPopulator>();
            if (populator != null)
            {
                problems += Require(populator, "bossEnemy", "DungeonPopulator");
                problems += RequireArray(populator, "enemies", "DungeonPopulator");
            }

            QuestJournal journal = Object.FindFirstObjectByType<QuestJournal>();
            if (journal != null)
                problems += Require(journal, "database", "QuestJournal");

            PlaceLoader place = Object.FindFirstObjectByType<PlaceLoader>();
            if (place != null)
            {
                problems += Require(place, "region", "PlaceLoader");
                problems += Require(place, "dungeons", "PlaceLoader");
                problems += Require(place, "settlements", "PlaceLoader");
            }

            SettlementGenerator settlement = Object.FindFirstObjectByType<SettlementGenerator>();
            if (settlement != null)
                problems += Require(settlement, "wallMaterial", "SettlementGenerator");

            GameMenu menu = Object.FindFirstObjectByType<GameMenu>();
            if (menu != null)
                problems += RequireArray(menu, "pages", "GameMenu");

            if (problems == 0)
                Debug.Log("Playtest scene verified: database, region, both generators and the menu all resolve.");
        }

        static int Require(Object target, string field, string label)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(field);

            if (property != null && property.objectReferenceValue != null)
                return 0;

            Debug.LogError($"{label}.{field} is null in the built scene.");
            return 1;
        }

        static int RequireArray(Object target, string field, string label)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(field);

            if (property != null && property.arraySize > 0 &&
                property.GetArrayElementAtIndex(0).objectReferenceValue != null)
                return 0;

            Debug.LogError($"{label}.{field} is empty or null in the built scene.");
            return 1;
        }

        static T Content<T>(string assetName) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>($"{ContentRoot}/{assetName}.asset");

        static void StartingKit(GameObject player)
        {
            StartingKitApplier applier = player.AddComponent<StartingKitApplier>();

            new AssetAuthoring(applier)
                .Ref("inventory", player.GetComponent<Inventory>())
                .Ref("equipment", player.GetComponent<Equipment>())
                .Refs("worn", Content<ItemDefinition>("Item_Shortsword"), Content<ItemDefinition>("Item_Shield"),
                    Content<ItemDefinition>("Item_Jerkin"))
                .Refs("carried", Content<ItemDefinition>("Item_Bread"),
                    Content<ItemDefinition>("Item_HealingPotion"))
                .Save();
        }

        // Both kinds of place exist in the scene from the start and only one of them is built at a
        // time, PlaceLoader decides which, so travel is one call rather than two code paths
        static void BuildPlace(GameDatabase database, int seed, GameObject player, Playtest kind)
        {
            GameObject dungeonObject = new GameObject("Dungeon");
            DungeonGenerator generator = dungeonObject.AddComponent<DungeonGenerator>();
            DungeonPopulator populator = dungeonObject.AddComponent<DungeonPopulator>();
            DungeonVisibility visibility = dungeonObject.AddComponent<DungeonVisibility>();

            new AssetAuthoring(generator).Int("seed", seed).Bool("generateOnStart", false).Save();

            new AssetAuthoring(populator)
                .Ref("generator", generator)
                .Refs("enemies", database.Enemy("enemy.skeleton"))
                .Ref("bossEnemy", database.Enemy("enemy.skeleton"))
                .Ref("treasureLoot", AssetDatabase.LoadAssetAtPath<LootTableDefinition>($"{ContentRoot}/Loot_Crypt.asset"))
                .Ref("pickupPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Pickup_Item.prefab"))
                .Ref("keyPickupPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Pickup_Key.prefab"))
                .Save();

            new AssetAuthoring(visibility)
                .Ref("generator", generator)
                .Ref("viewer", player.transform)
                .Save();

            new AssetAuthoring(dungeonObject.AddComponent<AiActivation>())
                .Ref("viewer", player.transform)
                .Save();

            GameObject settlementObject = new GameObject("Settlement");
            SettlementGenerator settlement = settlementObject.AddComponent<SettlementGenerator>();

            new AssetAuthoring(settlementObject.AddComponent<SettlementVisibility>())
                .Ref("generator", settlement)
                .Ref("viewer", player.transform)
                .Save();

            new AssetAuthoring(settlement)
                .Int("seed", seed)
                .Ref("groundMaterial", Material("Mat_TownGround"))
                .Ref("streetMaterial", Material("Mat_TownStreet"))
                .Ref("squareMaterial", Material("Mat_TownSquare"))
                .Ref("wallMaterial", Material("Mat_TownWall"))
                .Ref("roofMaterial", Material("Mat_TownRoof"))
                .Ref("waterMaterial", Material("Mat_TownWater"))
                .Ref("doorMaterial", Material("Mat_TownDoor"))
                .Refs("townsfolk",
                    AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Npc_Townsfolk.prefab"),
                    AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Npc_Trader.prefab"),
                    AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Npc_Watchman.prefab"))
                .Save();

            RegionMap region = Content<RegionMap>("RegionMap");

            GameObject placeObject = new GameObject("Place");
            PlaceLoader place = placeObject.AddComponent<PlaceLoader>();

            new AssetAuthoring(place)
                .Ref("region", region)
                .Ref("dungeons", generator)
                .Ref("settlements", settlement)
                .Ref("populator", populator)
                .Ref("player", player.transform)
                .Ref("sun", Sun())
                .Ref("entrancePrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Mod_DungeonEntrance.prefab"))
                .Ref("exitPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Mod_DungeonExit.prefab"))
                .Int("startAt", StartingPlace(region, kind))
                // The wilderness is deliberately empty country. The world scene starts you inside
                // Ashmere, a settlement now stands on the world at its own coordinates, so
                // entering one no longer clears the ground the streamer is holding up
                .Bool("enterOnStart", kind != Playtest.Wilderness)
                .Save();

            if (kind == Playtest.Wilderness)
                BuildWilderness(seed, player);

            if (kind == Playtest.World)
                BuildWorld(player);
        }

        // Taken from the enum, in the enum's order, because that order is the contract, a scatter
        // names its kind by prototype index and nothing re-maps it afterwards. A hand written list
        // here would silently reseat every biome the first time a plant was added
        static Object[] PlantPrefabs()
        {
            System.Array kinds = System.Enum.GetValues(typeof(WorldVegetation.Plant));
            Object[] prefabs = new Object[kinds.Length];

            for (int i = 0; i < kinds.Length; i++)
            {
                string path = $"{PrefabRoot}/Plant_{(WorldVegetation.Plant)kinds.GetValue(i)}.prefab";
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefabs[i] == null)
                    Debug.LogError($"Missing plant prefab {path}. Rebuild the greybox content.");
            }

            return prefabs;
        }

        // The wilderness plus everything in it, every place stands where the map says and is built as
        // the player approaches, with the whole empire coarsely baked for editing
        static void BuildWorld(GameObject player)
        {
            WorldGeography geography = WorldGeography.Ordovan();
            WorldSurface surface = new WorldSurface(geography, WorldSeed);

            Vector2 start = OrdovanPlaces.KmOf("Ashmere");
            player.transform.position = new Vector3(
                start.x * WorldSurface.MetresPerKm,
                surface.Height(start) + 2f,
                start.y * WorldSurface.MetresPerKm);

            GameObject worldObject = new GameObject("World");
            WorldTerrainStreamer streamer = worldObject.AddComponent<WorldTerrainStreamer>();

            new AssetAuthoring(worldObject.AddComponent<Journey>())
                .Ref("viewer", player.transform)
                .Ref("world", streamer)
                .Refs("suspendWhileTravelling",
                    player.GetComponent<PlayerLook>(), player.GetComponent<PlayerLocomotion>(),
                    player.GetComponent<PlayerCombat>(), player.GetComponent<PlayerInteractor>())
                .Save();

            // PlaceLoader has to know the world is there, or travel builds a town at the origin and
            // drops the player under the terrain of somewhere else
            new AssetAuthoring(Object.FindFirstObjectByType<PlaceLoader>())
                .Ref("world", streamer)
                .Save();

            new AssetAuthoring(worldObject.AddComponent<PlaceStreamer>())
                .Ref("place", Object.FindFirstObjectByType<PlaceLoader>())
                .Ref("player", player.transform)
                .Ref("silhouetteMaterial", Material("Mat_TownWall"))
                .Save();

            new AssetAuthoring(streamer)
                .Ref("viewer", player.transform)
                .Int("seed", WorldSeed)
                .Int("ringRadius", 3)
                .Int("heightmapResolution", 129)
                .Int("alphamapResolution", 64)
                // PlaceLoader puts the player in Ashmere, the streamer must not then move them
                .Bool("dropViewerOnFirstTile", false)
                .Ref("water", Water(worldObject))
                .Refs("plantPrefabs", PlantPrefabs())
                .Save();

            ShaderWarmup shaders = worldObject.AddComponent<ShaderWarmup>();

            new AssetAuthoring(worldObject.AddComponent<WorldPrewarm>())
                .Ref("place", Object.FindFirstObjectByType<PlaceLoader>())
                .Ref("shaders", shaders)
                .Ref("saves", Object.FindFirstObjectByType<SaveCoordinator>())
                .Ref("character", player.GetComponent<CharacterSetup>())
                .Save();

            WorldBaker.Markers(surface);
        }

        // The overview bake writes assets and refreshes the database, and a refresh part way through
        // a build is how references taken earlier turn into silent nulls. So it runs at the very end,
        // once everything that had to be wired is wired
        static void BakeWorldOverview()
        {
            WorldGeography geography = WorldGeography.Ordovan();

            WorldBaker.BakeOverviewInto(geography, new WorldSurface(geography, WorldSeed));
        }

        // The world has one seed, not a fresh one per build, it is the same empire every time
        const int WorldSeed = 20260907;

        // The wilderness has no place to arrive at, PlaceLoader stands down and the streamer owns the
        // ground instead. Both cannot run, because entering a place clears the scene the streamer
        // just filled
        static void BuildWilderness(int seed, GameObject player)
        {
            Vector2 start = OrdovanPlaces.KmOf("Ashmere");

            player.transform.position = new Vector3(
                start.x * WorldSurface.MetresPerKm, 0f, start.y * WorldSurface.MetresPerKm);

            GameObject worldObject = new GameObject("World");

            new AssetAuthoring(worldObject.AddComponent<WorldTerrainStreamer>())
                .Ref("viewer", player.transform)
                .Int("seed", seed)
                .Int("ringRadius", 2)
                .Int("heightmapResolution", 129)
                .Int("alphamapResolution", 64)
                .Ref("water", Water(worldObject))
                .Refs("plantPrefabs", PlantPrefabs())
                .Save();
        }

        // The sea, the lakes and the rivers. The river material is the one part of the
        // water that is an ordinary transparent mesh rather than an HDRP water surface
        static WorldWater Water(GameObject worldObject)
        {
            WorldWater water = worldObject.AddComponent<WorldWater>();

            new AssetAuthoring(water)
                .Ref("riverMaterial", WaterSetup.River())
                .Save();

            return water;
        }

        // A dungeon test should not have to walk out of a town first, and a settlement test should
        // not open in a crypt. Both can still travel anywhere from the world map
        static int StartingPlace(RegionMap region, Playtest kind)
        {
            if (region == null)
                return 0;

            bool wantSettlement = kind != Playtest.Dungeon;

            for (int i = 0; i < region.Locations.Count; i++)
            {
                RegionLocation location = region.Locations[i];

                if (wantSettlement && location.DisplayName.StartsWith("Ashmere"))
                    return i;

                if (!wantSettlement && location.Dungeon != null && location.DisplayName == "Crypt")
                    return i;
            }

            // Nothing matched by name, so fall back to the first place of the right kind
            for (int i = 0; i < region.Locations.Count; i++)
                if (region.Locations[i].IsSettlement == wantSettlement) return i;

            return 0;
        }

        static Light Sun()
        {
            GameObject sun = GameObject.Find("Directional Light");
            return sun != null ? sun.GetComponent<Light>() : null;
        }

        static Material Material(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{ContentRoot}/Materials/{name}.mat");

        static void BuildHud(GameObject player, Playtest kind)
        {
            GameObject canvasObject = new GameObject("HUD");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();

            // A bare CanvasScaler defaults to ConstantPixelSize, which sizes the HUD in raw pixels,
            // identical on screen at 4K as at 1080p, so half the size relative to everything else.
            // Every panel in this project is laid out against 1920x1080, so that is the reference
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            PlayerHud hud = canvasObject.AddComponent<PlayerHud>();

            GameObject flashObject = new GameObject("DamageFlash", typeof(RectTransform));
            flashObject.transform.SetParent(canvasObject.transform, false);

            RectTransform flashRect = flashObject.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;

            Image damageFlash = flashObject.AddComponent<Image>();
            damageFlash.color = new Color(0.7f, 0.05f, 0.05f, 0f);
            damageFlash.raycastTarget = false;
            damageFlash.enabled = false;

            Slider healthBar = Bar(canvasObject.transform, "Health", new Vector2(20f, -20f), Color.red);
            Slider staminaBar = Bar(canvasObject.transform, "Stamina", new Vector2(20f, -50f), Color.green);
            Slider manaBar = Bar(canvasObject.transform, "Mana", new Vector2(20f, -80f), new Color(0.35f, 0.55f, 1f));
            Slider hungerBar = Bar(canvasObject.transform, "Hunger", new Vector2(20f, -110f), new Color(0.8f, 0.6f, 0.2f));
            Slider fatigueBar = Bar(canvasObject.transform, "Fatigue", new Vector2(20f, -140f), new Color(0.4f, 0.5f, 0.9f));

            Text clockLabel = Label(canvasObject.transform, "Clock", new Vector2(20f, -175f));
            Text goldLabel = Label(canvasObject.transform, "Gold", new Vector2(20f, -200f));
            Text spellLabel = Label(canvasObject.transform, "Spell", new Vector2(20f, -225f));
            Text afflictionLabel = Label(canvasObject.transform, "Afflictions", new Vector2(20f, -250f));
            Text promptLabel = Label(canvasObject.transform, "Prompt", new Vector2(20f, -275f));

            new AssetAuthoring(hud)
                .Ref("health", player.GetComponent<Health>())
                .Ref("stamina", player.GetComponent<PlayerStamina>())
                .Ref("needs", player.GetComponent<SurvivalNeeds>())
                .Ref("wallet", player.GetComponent<Wallet>())
                .Ref("interactor", player.GetComponent<PlayerInteractor>())
                .Ref("mana", player.GetComponent<Mana>())
                .Ref("spellcasting", player.GetComponent<Spellcasting>())
                .Ref("healthBar", healthBar).Ref("staminaBar", staminaBar).Ref("manaBar", manaBar)
                .Ref("hungerBar", hungerBar).Ref("fatigueBar", fatigueBar)
                .Ref("clockLabel", clockLabel).Ref("goldLabel", goldLabel)
                .Ref("spellLabel", spellLabel).Ref("promptLabel", promptLabel)
                .Ref("afflictionLabel", afflictionLabel)
                .Ref("afflictions", player.GetComponent<AfflictionTracker>())
                .Ref("stance", player.GetComponent<PlayerStance>())
                .Ref("damageFlash", damageFlash)
                .Save();

            BuildMenu(canvasObject.transform, player, kind);

            if (kind == Playtest.CharacterCreation)
                BuildCreationScreen(canvasObject.transform, player);
        }

        // Six pages behind one panel. Every page is a component on the canvas rather than on its own
        // panel: a component on the panel is disabled with it, taking the only way of reopening it
        // down as well
        static void BuildMenu(Transform canvas, GameObject player, Playtest kind)
        {
            GameObject panel = new GameObject("Menu", typeof(RectTransform));
            panel.transform.SetParent(canvas, false);

            RectTransform root = panel.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(840f, 560f);

            panel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.96f);

            RectTransform tabBar = MenuWidgets.Panel(root, "Tabs", new Vector2(20f, -8f), new Vector2(800f, 26f));
            RectTransform pages = MenuWidgets.Panel(root, "Pages", new Vector2(0f, -38f), new Vector2(840f, 520f));

            PlaceLoader place = Object.FindFirstObjectByType<PlaceLoader>();

            CharacterSheetPage character = canvas.gameObject.AddComponent<CharacterSheetPage>();
            new AssetAuthoring(character)
                .Ref("sheet", player.GetComponent<CharacterSheet>())
                .Ref("health", player.GetComponent<Health>())
                .Ref("mana", player.GetComponent<Mana>())
                .Ref("stamina", player.GetComponent<PlayerStamina>())
                .Ref("inventory", player.GetComponent<Inventory>())
                .Ref("equipment", player.GetComponent<Equipment>())
                .Ref("root", Page(pages, "Character"))
                .Save();

            InventoryScreen inventory = canvas.gameObject.AddComponent<InventoryScreen>();
            new AssetAuthoring(inventory)
                .Ref("inventory", player.GetComponent<Inventory>())
                .Ref("equipment", player.GetComponent<Equipment>())
                .Ref("wallet", player.GetComponent<Wallet>())
                .Ref("sheet", player.GetComponent<CharacterSheet>())
                .Ref("needs", player.GetComponent<SurvivalNeeds>())
                .Ref("health", player.GetComponent<Health>())
                .Ref("root", Page(pages, "Inventory"))
                .Save();

            SpellbookPage spells = canvas.gameObject.AddComponent<SpellbookPage>();
            new AssetAuthoring(spells)
                .Ref("spellcasting", player.GetComponent<Spellcasting>())
                .Ref("stance", player.GetComponent<PlayerStance>())
                .Ref("mana", player.GetComponent<Mana>())
                .Ref("root", Page(pages, "Spells"))
                .Save();

            LocalMapPage local = canvas.gameObject.AddComponent<LocalMapPage>();
            new AssetAuthoring(local)
                .Ref("place", place)
                .Ref("player", player.transform)
                .Ref("root", Page(pages, "LocalMap"))
                .Save();

            WorldMapPage world = canvas.gameObject.AddComponent<WorldMapPage>();
            QuestJournalScreen journal = canvas.gameObject.AddComponent<QuestJournalScreen>();

            RectTransform journalPage = Page(pages, "Journal");
            Text body = Label(journalPage, "Body", new Vector2(20f, -14f));
            body.rectTransform.sizeDelta = new Vector2(780f, 460f);
            body.alignment = TextAnchor.UpperLeft;
            body.fontSize = 13;

            new AssetAuthoring(journal)
                .Ref("journal", Object.FindFirstObjectByType<QuestJournal>())
                .Ref("body", body)
                .Ref("root", journalPage)
                .Save();

            SystemPage system = canvas.gameObject.AddComponent<SystemPage>();
            new AssetAuthoring(system)
                .Ref("saves", Object.FindFirstObjectByType<SaveCoordinator>())
                .Ref("root", Page(pages, "System"))
                .Str("bootScene", "Boot")
                .Save();

            GameMenu menu = canvas.gameObject.AddComponent<GameMenu>();

            new AssetAuthoring(world)
                .Ref("place", place)
                .Ref("menu", menu)
                .Ref("journey", Object.FindFirstObjectByType<Journey>())
                .Ref("wallet", player.GetComponent<Wallet>())
                .Ref("mapImage", AssetDatabase.LoadAssetAtPath<Sprite>(WorldMapExporter.OutputPath))
                .Ref("root", Page(pages, "WorldMap"))
                .Save();

            if (AssetDatabase.LoadAssetAtPath<Sprite>(WorldMapExporter.OutputPath) == null)
                Debug.LogWarning("No world map picture yet, so the map page will draw markers on a plain " +
                                 "backdrop. Run 'Endless Descent > Export World Map', then build this scene again.");

            new AssetAuthoring(menu)
                .Ref("input", player.GetComponent<PlayerInputReader>())
                .Ref("root", root)
                .Ref("tabBar", tabBar)
                .Bool("captureCursorOnStart", kind != Playtest.CharacterCreation)
                .Refs("pages", character, inventory, spells, local, world, journal, system)
                .Refs("suspendWhileOpen", player.GetComponent<PlayerLook>(), player.GetComponent<PlayerLocomotion>(),
                    player.GetComponent<PlayerCombat>(), player.GetComponent<PlayerInteractor>(),
                    player.GetComponent<Spellcasting>(), player.GetComponent<PlayerStance>())
                .Save();

            panel.SetActive(false);
        }

        static RectTransform Page(RectTransform parent, string name)
        {
            RectTransform page = MenuWidgets.Panel(parent, name, Vector2.zero, new Vector2(840f, 520f));
            page.gameObject.SetActive(false);
            return page;
        }

        // Shown first, the run does not start until a class exists, which is what makes the sheet
        // the source of health, magicka, stamina and carry weight rather than a set of defaults
        static void BuildCreationScreen(Transform canvas, GameObject player)
        {
            GameObject panel = new GameObject("CharacterCreation", typeof(RectTransform));
            panel.transform.SetParent(canvas, false);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image backdrop = panel.AddComponent<Image>();
            backdrop.color = new Color(0.04f, 0.04f, 0.06f, 0.97f);

            CharacterCreationScreen screen = panel.AddComponent<CharacterCreationScreen>();

            new AssetAuthoring(screen)
                .Ref("sheet", player.GetComponent<CharacterSheet>())
                .Ref("root", rect)
                .Refs("suspendWhileOpen", player.GetComponent<PlayerLook>(), player.GetComponent<PlayerLocomotion>(),
                    player.GetComponent<PlayerCombat>(), player.GetComponent<PlayerInteractor>(),
                    player.GetComponent<Spellcasting>(), player.GetComponent<PlayerStance>(),
                    Object.FindFirstObjectByType<GameMenu>())
                .Save();
        }

        static Slider Bar(Transform parent, string name, Vector2 position, Color colour)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            Anchor(rect, position, new Vector2(220f, 22f));

            GameObject fillArea = new GameObject("Fill", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Image fillImage = fillArea.AddComponent<Image>();
            fillImage.color = colour;

            RectTransform fillRect = fillArea.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Text caption = new GameObject("Caption", typeof(RectTransform)).AddComponent<Text>();
            caption.transform.SetParent(root.transform, false);
            caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize = 13;
            caption.color = Color.white;
            caption.alignment = TextAnchor.MiddleLeft;
            caption.text = name;

            RectTransform captionRect = caption.GetComponent<RectTransform>();
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(6f, 0f);
            captionRect.offsetMax = Vector2.zero;

            Slider slider = root.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImage;
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        static Text Label(Transform parent, string name, Vector2 position)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Anchor(root.GetComponent<RectTransform>(), position, new Vector2(500f, 24f));

            Text text = root.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = Color.white;
            text.text = string.Empty;
            return text;
        }

        static void Anchor(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
