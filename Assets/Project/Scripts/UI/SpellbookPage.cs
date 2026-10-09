using System.Collections.Generic;
using EndlessDescent.Data;
using EndlessDescent.Player;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    [DisallowMultipleComponent]
    public class SpellbookPage : MonoBehaviour, IMenuPage
    {
        [SerializeField] Spellcasting spellcasting;
        [SerializeField] PlayerStance stance;
        [SerializeField] Mana mana;
        [SerializeField] RectTransform root;

        readonly List<GameObject> rows = new List<GameObject>();

        Text header;
        Text footer;

        public string Title => "Spells";
        public RectTransform Root => root;

        void Awake()
        {
            if (root == null)
                return;

            header = MenuWidgets.Label(root, new Vector2(20f, -14f), 760f, string.Empty, 16);
            header.color = new Color(0.95f, 0.85f, 0.5f);
            footer = MenuWidgets.Label(root, new Vector2(20f, -470f), 760f, string.Empty);
            footer.color = new Color(0.72f, 0.78f, 0.9f);
        }

        public void Refresh()
        {
            if (spellcasting == null || root == null)
                return;

            foreach (GameObject row in rows)
                Destroy(row);

            rows.Clear();

            IReadOnlyList<SpellDefinition> known = spellcasting.Known;
            header.text = $"SPELLBOOK: {known.Count} known: " +
                          $"Magicka {(mana != null ? Mathf.RoundToInt(mana.Current) : 0)}";

            float y = -50f;

            for (int i = 0; i < known.Count; i++)
            {
                SpellDefinition spell = known[i];
                if (spell == null)
                    continue;

                int captured = i;
                Text label = MenuWidgets.Button(root, new Vector2(20f, y), new Vector2(500f, 24f), () => Select(captured));

                bool selected = spellcasting.Selected == spell;
                label.text = $"{(selected ? "> " : "  ")}{spell.DisplayName,-18} {spell.Kind,-11} " +
                             $"{spell.ManaCost,3} magicka   power {spell.Power}";
                label.color = selected ? new Color(0.95f, 0.85f, 0.5f) : Color.white;

                rows.Add(label.transform.parent.gameObject);
                y -= 28f;
            }

            if (known.Count == 0)
                footer.text = "You know no spells.";
            else
                footer.text = "Click a spell to ready it.  F cycles.  R draws the focus.  " +
                              "Left mouse casts while the focus is out.";
        }

        // Readying a spell is also a statement that you mean to cast it, so it draws the focus
        void Select(int index)
        {
            spellcasting.Select(index);
            stance?.Set(CombatStance.Spell);
            Refresh();
        }
    }
}
