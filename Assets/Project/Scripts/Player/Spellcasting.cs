using System;
using EndlessDescent.Combat;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Player
{
    [Serializable]
    public struct SpellbookState
    {
        public int[] MadeEffects;
        public int[] MadePowers;
        public int Selected;
    }

    [DisallowMultipleComponent]
    public class Spellcasting : MonoBehaviour, ISaveable
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerStance stance;
        [SerializeField] Mana mana;
        [SerializeField] Health health;
        [SerializeField] EndlessDescent.Simulation.GuildMembership circles;
        [SerializeField] Transform castOrigin;
        [SerializeField] GameObject projectilePrefab;
        [SerializeField] SpellDefinition[] spells;
        [SerializeField] CharacterSheet sheet;
        [SerializeField] string saveKey = "spells.player";

        readonly System.Collections.Generic.List<SpellDefinition> learned =
            new System.Collections.Generic.List<SpellDefinition>();

        int selected;
        float ready;
        float wardUntil;
        int wardPower;

        public event Action<SpellDefinition> SpellCast;

        public System.Collections.Generic.IReadOnlyList<SpellDefinition> Known
        {
            get
            {
                if (learned.Count == 0 && spells != null)
                    learned.AddRange(spells);

                return learned;
            }
        }

        public SpellDefinition Selected =>
            Known.Count > 0 ? Known[Mathf.Clamp(selected, 0, Known.Count - 1)] : null;

        public void Select(int index)
        {
            if (index >= 0 && index < Known.Count)
                selected = index;
        }

        // Nine circles, nine traditions, and they do not transfer. A working belongs to whoever
        // taught it, and the only way to hold two traditions is to have been let into two circles
        public bool CanLearn(SpellDefinition spell)
        {
            if (spell == null)
                return false;

            return spell.IsCommon || (circles != null && circles.IsMember(spell.Tradition));
        }

        // Spells made at a workbench join the same list the starting three live in
        public bool Learn(SpellDefinition spell)
        {
            if (!CanLearn(spell))
                return false;

            _ = Known;

            // A remade spell replaces the one it improves on. Compared by id, each one made at a bench
            // is a new object, so comparing objects never found the copy already known
            int known = learned.FindIndex(existing => existing.Id == spell.Id);

            if (known >= 0)
            {
                learned[known] = spell;
                selected = known;
                return true;
            }

            learned.Add(spell);
            selected = learned.Count - 1;
            return true;
        }

        public int WardArmour => Time.time < wardUntil ? wardPower : 0;

        void Update()
        {
            if (input == null || Known.Count == 0)
                return;

            if (input.NextSpellPressed && Known.Count > 0)
                selected = (selected + 1) % Known.Count;

            // Attack and cast share a button, the stance says which of the two it means
            if (input.AttackPressed && stance != null && stance.IsCasting)
                Cast(Selected);
        }

        void Awake()
        {
            if (sheet == null)
                sheet = GetComponent<CharacterSheet>();
        }

        void Cast(SpellDefinition spell)
        {
            if (spell == null || Time.time < ready)
                return;

            if (mana == null || !mana.Spend(CharacterMaths.SpellCost(sheet, spell.School, spell.ManaCost)))
                return;

            ready = Time.time + spell.Cooldown;
            SpellCast?.Invoke(spell);

            switch (spell.Kind)
            {
                case SpellKind.Projectile:
                    Launch(spell);
                    break;

                case SpellKind.Heal:
                    health?.Heal(CharacterMaths.SpellPower(sheet, spell.School, spell.Power));
                    break;

                case SpellKind.Ward:
                    wardPower = CharacterMaths.SpellPower(sheet, spell.School, spell.Power);
                    wardUntil = Time.time + spell.Duration;
                    break;
            }
        }

        void Launch(SpellDefinition spell)
        {
            if (projectilePrefab == null)
                return;

            Transform origin = castOrigin != null ? castOrigin : transform;

            // Spawned clear of the caster's own capsule so the sweep does not start inside it
            GameObject bolt = Instantiate(projectilePrefab, origin.position + origin.forward * 0.8f,
                Quaternion.LookRotation(origin.forward));

            // Scaled by the school like a heal or a ward is. The bolt used to carry the spell's raw number,
            // so Destruction made no difference to what it hit for
            int power = CharacterMaths.SpellPower(sheet, spell.School, spell.Power);
            bolt.GetComponent<SpellProjectile>()?.Launch(gameObject, power, spell.ProjectileSpeed, spell.Colour);
        }

        public string SaveKey => saveKey;

        // The starting spells come back from the component itself, so only what was made at a bench
        // is kept, as the effect and strength it was made from
        public string CaptureJson()
        {
            System.Collections.Generic.List<int> effects = new System.Collections.Generic.List<int>();
            System.Collections.Generic.List<int> powers = new System.Collections.Generic.List<int>();

            foreach (SpellDefinition spell in Known)
            {
                if (!SpellEffects.IsCustom(spell.Id, out SpellEffectId effect))
                    continue;

                effects.Add((int)effect);
                powers.Add(spell.Power);
            }

            return JsonUtility.ToJson(new SpellbookState
            {
                MadeEffects = effects.ToArray(), MadePowers = powers.ToArray(), Selected = selected
            });
        }

        public void RestoreJson(string json)
        {
            SpellbookState state = JsonUtility.FromJson<SpellbookState>(json);

            learned.Clear();

            if (spells != null)
                learned.AddRange(spells);

            if (state.MadeEffects != null && state.MadePowers != null)
            {
                for (int i = 0; i < Mathf.Min(state.MadeEffects.Length, state.MadePowers.Length); i++)
                    learned.Add(SpellEffects.Custom((SpellEffectId)state.MadeEffects[i], state.MadePowers[i]));
            }

            selected = Mathf.Clamp(state.Selected, 0, Mathf.Max(0, learned.Count - 1));
        }
    }
}
