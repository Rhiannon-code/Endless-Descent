using EndlessDescent.Combat;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Player
{
    // Turns how you are moving into how detectable you are, and credits the Stealth skill for time
    // spent unseen near something that could see you, so the skill trains by sneaking, not by a
    // button press
    [DisallowMultipleComponent]
    public class PlayerStealth : MonoBehaviour, IStealthProfile
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] CharacterController controller;
        [SerializeField] CharacterSheet sheet;
        [SerializeField] GearLoad gear;

        [SerializeField] float sprintNoise = 18f;
        [SerializeField] float walkNoise = 10f;
        [SerializeField] float crouchNoise = 3f;
        [SerializeField] float stillNoise = 0.5f;
        [SerializeField] float creditInterval = 2f;
        [SerializeField] float watcherRange = 18f;

        readonly Collider[] nearby = new Collider[32];
        float nextCredit;

        public bool IsSneaking => input != null && input.CrouchHeld;

        public float NoiseRadius
        {
            get
            {
                float speed = controller != null ? new Vector2(controller.velocity.x, controller.velocity.z).magnitude : 0f;

                float baseNoise = speed < 0.2f ? stillNoise
                    : IsSneaking ? crouchNoise
                    : input != null && input.SprintHeld ? sprintNoise
                    : walkNoise;

                return baseNoise * Quiet * (gear != null ? gear.NoiseScale : 1f);
            }
        }

        public float VisibilityScale => (IsSneaking ? 0.55f : 1f) * Quiet;

        // A trained sneak is both quieter and harder to see, 100 Stealth roughly halves both
        float Quiet => sheet == null ? 1f : Mathf.Lerp(1f, 0.5f, sheet.Skill(SkillId.Stealth) / 100f);

        void Update()
        {
            if (sheet == null || !IsSneaking || Time.time < nextCredit)
                return;

            nextCredit = Time.time + creditInterval;

            int count = Physics.OverlapSphereNonAlloc(transform.position, watcherRange, nearby,
                ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                EnemySenses senses = nearby[i].GetComponentInParent<EnemySenses>();

                if (senses != null && !senses.IsAware)
                {
                    sheet.Use(SkillId.Stealth);
                    return;
                }
            }
        }
    }
}
