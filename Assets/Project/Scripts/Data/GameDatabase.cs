using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    // One lookup for every definition kind. Saves store string ids, so restoring anything at all
    // depends on this being the single place ids resolve
    [CreateAssetMenu(menuName = "Endless Descent/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        // Bumped by the content builder whenever the shape of what it writes changes. A scene built
        // against older content fails on a null or, worse, on a field that silently took its C#
        // default, which is how a port city came out the size of a village
        [SerializeField] int contentVersion;

        public int ContentVersion => contentVersion;

        // For what is built at runtime and so cannot be wired to the asset, a shopkeeper's shelf
        // restoring its stock from a save among them
        public static GameDatabase Loaded { get; private set; }

        void OnEnable() => Loaded = this;

        [SerializeField] ItemDefinition[] items;
        [SerializeField] EnemyDefinition[] enemies;
        [SerializeField] NpcDefinition[] npcs;
        [SerializeField] FactionDefinition[] factions;
        [SerializeField] LocationDefinition[] locations;
        [SerializeField] RecipeDefinition[] recipes;
        [SerializeField] PropertyDefinition[] properties;
        [SerializeField] AuthoredQuestDefinition[] quests;
        [SerializeField] QuestTemplateDefinition[] questTemplates;
        [SerializeField] ClassDefinition[] classes;
        [SerializeField] BackgroundDefinition[] backgrounds;
        [SerializeField] DialogueDefinition[] dialogues;

        Dictionary<string, ItemDefinition> itemsById;
        Dictionary<string, EnemyDefinition> enemiesById;
        Dictionary<string, NpcDefinition> npcsById;
        Dictionary<string, FactionDefinition> factionsById;
        Dictionary<string, LocationDefinition> locationsById;
        Dictionary<string, RecipeDefinition> recipesById;
        Dictionary<string, PropertyDefinition> propertiesById;
        Dictionary<string, AuthoredQuestDefinition> questsById;
        Dictionary<string, ClassDefinition> classesById;
        Dictionary<string, BackgroundDefinition> backgroundsById;
        Dictionary<string, DialogueDefinition> dialoguesById;

        public IReadOnlyList<ItemDefinition> Items => items;
        public IReadOnlyList<EnemyDefinition> Enemies => enemies;
        public IReadOnlyList<NpcDefinition> Npcs => npcs;
        public IReadOnlyList<FactionDefinition> Factions => factions;
        public IReadOnlyList<LocationDefinition> Locations => locations;
        public IReadOnlyList<RecipeDefinition> Recipes => recipes;
        public IReadOnlyList<PropertyDefinition> Properties => properties;
        public IReadOnlyList<AuthoredQuestDefinition> Quests => quests;
        public IReadOnlyList<QuestTemplateDefinition> QuestTemplates => questTemplates;
        public IReadOnlyList<ClassDefinition> Classes => classes;
        public IReadOnlyList<BackgroundDefinition> Backgrounds => backgrounds;
        public IReadOnlyList<DialogueDefinition> Dialogues => dialogues;

        public ItemDefinition Item(string id) => Lookup(ref itemsById, items, d => d.Id, id);
        public EnemyDefinition Enemy(string id) => Lookup(ref enemiesById, enemies, d => d.Id, id);
        public NpcDefinition Npc(string id) => Lookup(ref npcsById, npcs, d => d.Id, id);
        public FactionDefinition Faction(string id) => Lookup(ref factionsById, factions, d => d.Id, id);
        public LocationDefinition Location(string id) => Lookup(ref locationsById, locations, d => d.Id, id);
        public RecipeDefinition Recipe(string id) => Lookup(ref recipesById, recipes, d => d.Id, id);
        public PropertyDefinition Property(string id) => Lookup(ref propertiesById, properties, d => d.Id, id);
        public AuthoredQuestDefinition Quest(string id) => Lookup(ref questsById, quests, d => d.Id, id);
        public ClassDefinition Class(string id) => Lookup(ref classesById, classes, d => d.Id, id);
        public BackgroundDefinition Background(string id) => Lookup(ref backgroundsById, backgrounds, d => d.Id, id);
        public DialogueDefinition Dialogue(string id) => Lookup(ref dialoguesById, dialogues, d => d.Id, id);

        static T Lookup<T>(ref Dictionary<string, T> cache, T[] source, System.Func<T, string> idOf, string id)
            where T : Object
        {
            if (string.IsNullOrEmpty(id))
                return null;

            if (cache == null)
            {
                cache = new Dictionary<string, T>();
                if (source != null)
                {
                    foreach (T entry in source)
                    {
                        if (entry != null)
                            cache[idOf(entry)] = entry;
                    }
                }
            }

            return cache.TryGetValue(id, out T found) ? found : null;
        }
    }
}
