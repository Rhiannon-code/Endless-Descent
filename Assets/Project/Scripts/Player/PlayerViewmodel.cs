using EndlessDescent.Combat;
using EndlessDescent.Data;
using EndlessDescent.Items;
using UnityEngine;

namespace EndlessDescent.Player
{
    // Greybox first person hands. Everything is driven off state the combat scripts already expose,
    // so nothing here decides anything, it only shows what is happening
    [DisallowMultipleComponent]
    public class PlayerViewmodel : MonoBehaviour
    {
        [SerializeField] Transform weapon;

        // One shape per WeaponClass, in the enum's order, the way the plant prototypes are keyed:
        // the index IS the contract, so the builder derives this list from the enum rather than
        // writing it out
        [SerializeField] Transform[] weaponShapes;
        [SerializeField] Transform shield;
        [SerializeField] Transform focus;

        [SerializeField] MeleeAttacker attacker;
        [SerializeField] PlayerDefence defence;
        [SerializeField] Spellcasting spellcasting;
        [SerializeField] Equipment equipment;
        [SerializeField] PlayerStance stance;

        [SerializeField] float followSpeed = 16f;

        static readonly Pose WeaponRest = new Pose(new Vector3(0.30f, -0.26f, 0.52f), Quaternion.Euler(14f, -10f, 10f));
        static readonly Pose WeaponWindup = new Pose(new Vector3(0.38f, -0.10f, 0.36f), Quaternion.Euler(-52f, 24f, 26f));
        static readonly Pose WeaponActive = new Pose(new Vector3(-0.10f, -0.30f, 0.62f), Quaternion.Euler(38f, -34f, -46f));
        static readonly Pose WeaponRecover = new Pose(new Vector3(0.10f, -0.34f, 0.52f), Quaternion.Euler(26f, -20f, -18f));

        static readonly Pose ShieldRest = new Pose(new Vector3(-0.30f, -0.28f, 0.50f), Quaternion.Euler(6f, 22f, -14f));
        static readonly Pose ShieldUp = new Pose(new Vector3(-0.14f, -0.10f, 0.40f), Quaternion.Euler(0f, 6f, -2f));
        static readonly Pose ShieldParry = new Pose(new Vector3(-0.06f, -0.06f, 0.34f), Quaternion.Euler(-10f, -8f, 6f));

        Renderer[] focusRenderers;
        float flashUntil;
        Color flashColour = Color.white;
        WeaponDefinition shown;

        void Awake()
        {
            focusRenderers = focus != null ? focus.GetComponentsInChildren<Renderer>() : new Renderer[0];
            ShowFocus(false);
        }

        void OnEnable()
        {
            if (spellcasting != null)
                spellcasting.SpellCast += OnCast;
        }

        void OnDisable()
        {
            if (spellcasting != null)
                spellcasting.SpellCast -= OnCast;
        }

        void OnCast(SpellDefinition spell)
        {
            flashUntil = Time.time + 0.35f;
            flashColour = spell != null ? spell.Colour : Color.white;
        }

        void LateUpdate()
        {
            float step = followSpeed * Time.deltaTime;

            bool casting = stance != null && stance.IsCasting;

            if (weapon != null)
            {
                weapon.gameObject.SetActive(!casting && (equipment == null || equipment.Weapon != null));
                ShowShapeFor(equipment != null ? equipment.Weapon : null);
                Drive(weapon, WeaponPose(), step);
            }

            if (shield != null)
            {
                bool hasShield = equipment == null || equipment.Get(EquipSlot.OffHand) != null;
                shield.gameObject.SetActive(hasShield);
                Drive(shield, ShieldPose(), step);
            }

            DriveFocus();
        }

        // A dagger, a mace and a battleaxe used to be the same box in your hand. Which shape is
        // shown follows the weapon's class, and its colour follows what it is worth
        void ShowShapeFor(WeaponDefinition held)
        {
            if (weaponShapes == null || weaponShapes.Length == 0 || held == shown)
                return;

            shown = held;
            int wanted = (int)WeaponClasses.Of(held);

            for (int i = 0; i < weaponShapes.Length; i++)
            {
                if (weaponShapes[i] == null)
                    continue;

                bool active = i == wanted;
                weaponShapes[i].gameObject.SetActive(active);

                if (!active || held == null)
                    continue;

                foreach (Renderer renderer in weaponShapes[i].GetComponentsInChildren<Renderer>())
                    renderer.material.color = Tier(held.BaseValue);
            }
        }

        // Leather, iron, steel, runed. Read off value so a weapon added later is coloured without
        // anything being told about it
        static Color Tier(int value)
        {
            if (value >= 300) return new Color(0.72f, 0.62f, 0.86f);
            if (value >= 140) return new Color(0.78f, 0.80f, 0.84f);
            if (value >= 60) return new Color(0.58f, 0.60f, 0.62f);
            return new Color(0.52f, 0.42f, 0.30f);
        }

        Pose WeaponPose()
        {
            if (attacker == null)
                return WeaponRest;

            switch (attacker.Phase)
            {
                case AttackPhase.Windup: return WeaponWindup;
                case AttackPhase.Active: return WeaponActive;
                case AttackPhase.Recovery: return WeaponRecover;
                default: return WeaponRest;
            }
        }

        Pose ShieldPose()
        {
            if (defence != null && defence.IsParrying)
                return ShieldParry;

            return defence != null && defence.IsBlocking ? ShieldUp : ShieldRest;
        }

        void Drive(Transform target, Pose pose, float step)
        {
            target.localPosition = Vector3.Lerp(target.localPosition, pose.position, step);
            target.localRotation = Quaternion.Slerp(target.localRotation, pose.rotation, step);
        }

        void DriveFocus()
        {
            if (focus == null)
                return;

            float remaining = flashUntil - Time.time;
            bool casting = stance != null && stance.IsCasting;

            if (remaining <= 0f && !casting)
            {
                ShowFocus(false);
                return;
            }

            ShowFocus(true);
            focus.localScale = Vector3.one * Mathf.Max(casting ? 0.45f : 0f, Mathf.Clamp01(remaining / 0.35f));

            foreach (Renderer renderer in focusRenderers)
                renderer.material.color = flashColour;
        }

        void ShowFocus(bool visible)
        {
            if (focus != null && focus.gameObject.activeSelf != visible)
                focus.gameObject.SetActive(visible);
        }
    }
}
