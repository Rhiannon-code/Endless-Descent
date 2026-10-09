using EndlessDescent.Data;
using EndlessDescent.Items;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.Player
{
    // Turns what the creator decided into a player standing in the world: the sheet, the background's
    // kit, its purse and whoever it left on good terms with. The creator itself knows none of this,
    // it runs in a scene with no player in it
    [DisallowMultipleComponent]
    public class CharacterSetup : MonoBehaviour
    {
        [SerializeField] CharacterSheet sheet;
        [SerializeField] Inventory inventory;
        [SerializeField] Equipment equipment;
        [SerializeField] Wallet wallet;
        [SerializeField] GameDatabase database;
        [SerializeField] FactionRegistry factions;

        public bool Applied { get; private set; }

        void Awake()
        {
            if (sheet == null) sheet = GetComponent<CharacterSheet>();
            if (inventory == null) inventory = GetComponent<Inventory>();
            if (equipment == null) equipment = GetComponent<Equipment>();
            if (wallet == null) wallet = GetComponent<Wallet>();
        }

        // The creator may be in this scene, in which case it hands the blueprint over the moment
        // BEGIN is pressed, or in the boot scene, in which case whatever loaded the world hands it
        // over instead. Both end up here
        void OnEnable() => GameStart.Created += Apply;
        void OnDisable() => GameStart.Created -= Apply;

        public void Apply(CharacterBlueprint blueprint)
        {
            if (Applied)
                return;

            Applied = true;
            sheet?.Apply(blueprint);

            BackgroundDefinition background = database != null ? database.Background(blueprint.BackgroundId) : null;
            if (background == null)
                return;

            if (factions != null && background.Faction != null && background.Standing != 0)
                factions.Modify(background.Faction, background.Standing);

            wallet?.Add(background.StartingGold);

            if (background.TrainedSkills != null && sheet != null)
            {
                foreach (SkillId skill in background.TrainedSkills)
                    sheet.Train(skill, background.TrainedBonus);
            }

            if (inventory == null)
                return;

            foreach (ItemDefinition item in background.Worn)
            {
                if (item == null)
                    continue;

                inventory.Add(item, 1);
                equipment?.Equip(item);
            }

            foreach (ItemDefinition item in background.Carried)
                if (item != null) inventory.Add(item, 1);
        }
    }
}
