using System;
using System.Collections.Generic;
using EndlessDescent.Combat;
using EndlessDescent.Data;
using EndlessDescent.Items;
using EndlessDescent.Player;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // Where the eight attributes and twenty skills live. They were on the HUD, which meant the one
    // screen you read between fights was also the one you could not get away from
    [DisallowMultipleComponent]
    public class CharacterSheetPage : MonoBehaviour, IMenuPage
    {
        [SerializeField] CharacterSheet sheet;
        [SerializeField] Health health;
        [SerializeField] Mana mana;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] Inventory inventory;
        [SerializeField] Equipment equipment;
        [SerializeField] RectTransform root;

        readonly Dictionary<CharacterAttribute, Text> attributeLabels = new Dictionary<CharacterAttribute, Text>();
        readonly Dictionary<SkillId, Text> skillLabels = new Dictionary<SkillId, Text>();

        Text header;
        Text derived;

        public string Title => "Character";
        public RectTransform Root => root;

        void Awake() => Build();

        void Build()
        {
            if (root == null)
                return;

            header = MenuWidgets.Label(root, new Vector2(20f, -14f), 760f, string.Empty, 16);
            header.color = new Color(0.95f, 0.85f, 0.5f);

            MenuWidgets.Label(root, new Vector2(20f, -46f), 300f, "ATTRIBUTES").color = new Color(0.8f, 0.8f, 0.9f);

            float y = -70f;
            foreach (CharacterAttribute attribute in Enum.GetValues(typeof(CharacterAttribute)))
            {
                attributeLabels[attribute] = MenuWidgets.Label(root, new Vector2(20f, y), 280f, string.Empty);
                y -= 22f;
            }

            derived = MenuWidgets.Label(root, new Vector2(20f, y - 16f), 300f, string.Empty);
            derived.color = new Color(0.72f, 0.78f, 0.9f);

            MenuWidgets.Label(root, new Vector2(340f, -46f), 300f, "SKILLS").color = new Color(0.8f, 0.8f, 0.9f);

            y = -70f;
            int column = 0;

            foreach (SkillId skill in Enum.GetValues(typeof(SkillId)))
            {
                skillLabels[skill] = MenuWidgets.Label(root, new Vector2(340f + column * 240f, y), 230f, string.Empty);
                y -= 22f;

                // Twenty skills do not fit one column at a readable size, so they run in two
                if (y < -290f)
                {
                    y = -70f;
                    column++;
                }
            }
        }

        public void Refresh()
        {
            if (sheet == null || header == null)
                return;

            string className = sheet.Class != null ? sheet.Class.DisplayName : "Unclassed";
            header.text = $"{className}: Level {sheet.Level}: named under " +
                          $"{Birthsigns.NamedIn(sheet.Birthsign)}, at {Birthsigns.Describe(sheet.Birthsign)}";

            foreach (KeyValuePair<CharacterAttribute, Text> pair in attributeLabels)
                pair.Value.text = $"{pair.Key,-14}{sheet.Get(pair.Key),3}";

            foreach (KeyValuePair<SkillId, Text> pair in skillLabels)
            {
                int value = sheet.Skill(pair.Key);
                pair.Value.text = $"{pair.Key,-16}{value,3}   {Skills.Group(pair.Key)}";
                pair.Value.color = value >= 50 ? new Color(0.7f, 0.95f, 0.75f)
                    : value >= 25 ? Color.white : new Color(0.68f, 0.68f, 0.68f);
            }

            derived.text =
                $"Health   {(health != null ? health.Current : 0)} / {sheet.MaxHealth}\n" +
                $"Magicka  {(mana != null ? Mathf.RoundToInt(mana.Current) : 0)} / {sheet.MaxMagicka}\n" +
                $"Stamina  {(stamina != null ? Mathf.RoundToInt(stamina.Current) : 0)} / {Mathf.RoundToInt(sheet.MaxStamina)}\n" +
                $"Armour   {(equipment != null ? equipment.ArmourRating : 0)}\n" +
                $"Carrying {(inventory != null ? inventory.CarriedWeight : 0f):F1} / {sheet.CarryWeight:F0} kg";
        }
    }
}
