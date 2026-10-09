using UnityEngine;

namespace EndlessDescent.Data
{
    public enum SpellKind
    {
        Projectile,
        Heal,
        Ward
    }

    [CreateAssetMenu(menuName = "Endless Descent/Spell", fileName = "Spell")]
    public class SpellDefinition : ScriptableObject
    {
        [SerializeField] string id = "spell.unnamed";
        [SerializeField] string displayName = "Spell";
        [SerializeField] SpellKind kind = SpellKind.Projectile;
        [SerializeField, Min(0)] int manaCost = 10;
        [SerializeField, Min(0f)] float cooldown = 1f;
        [SerializeField, Min(0)] int power = 20;
        [SerializeField, Min(0f)] float duration = 8f;
        [SerializeField, Min(0f)] float projectileSpeed = 18f;
        [SerializeField] Color colour = new Color(1f, 0.55f, 0.2f);

        [Header("Where it comes from")]
        [SerializeField] SkillId school = SkillId.Destruction;

        // The circle whose tradition this working belongs to. Left empty for the handful of common
        // workings that predate the Ban and survived in books anybody can read
        [SerializeField] FactionDefinition tradition;

        public string Id => id;
        public string DisplayName => displayName;
        public SpellKind Kind => kind;
        public int ManaCost => manaCost;
        public float Cooldown => cooldown;
        public int Power => power;
        public float Duration => duration;
        public float ProjectileSpeed => projectileSpeed;
        public Color Colour => colour;
        public SkillId School => school;
        public FactionDefinition Tradition => tradition;
        public bool IsCommon => tradition == null;

        // Built by the spellmaker at runtime rather than authored as an asset
        public void Configure(string newId, string newName, SpellKind newKind, SkillId newSchool, int cost,
            float newCooldown, int newPower, float newDuration, Color newColour)
        {
            id = newId;
            school = newSchool;
            displayName = newName;
            kind = newKind;
            manaCost = cost;
            cooldown = newCooldown;
            power = newPower;
            duration = newDuration;
            projectileSpeed = newKind == SpellKind.Projectile ? 22f : 0f;
            colour = newColour;
        }
    }
}
