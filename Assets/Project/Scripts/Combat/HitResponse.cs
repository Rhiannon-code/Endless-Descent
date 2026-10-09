using UnityEngine;

namespace EndlessDescent.Combat
{
    // Something visible has to happen when a blow lands, or the player cannot tell a hit from a miss
    // Driven entirely off Health's own events, so it reports damage rather than deciding any
    [DisallowMultipleComponent]
    public class HitResponse : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] Renderer body;
        [SerializeField] Color hitColour = new Color(1f, 0.35f, 0.3f);
        [SerializeField] Color deathColour = new Color(0.25f, 0.22f, 0.24f);
        [SerializeField] float flashSeconds = 0.16f;
        [SerializeField] float recoilDistance = 0.9f;

        MaterialPropertyBlock block;
        CharacterController controller;
        Color baseColour;
        float flashUntil;
        Vector3 recoil;
        bool dead;
        bool flashing;

        void Awake()
        {
            if (health == null)
                health = GetComponent<Health>();

            controller = GetComponent<CharacterController>();

            if (body == null)
                body = GetComponentInChildren<Renderer>();

            if (body == null)
                return;

            block = new MaterialPropertyBlock();
            baseColour = body.sharedMaterial != null ? body.sharedMaterial.GetColor(BaseColour) : Color.white;
        }

        void OnEnable()
        {
            if (health == null)
                return;

            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            if (health == null)
                return;

            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        void OnDamaged(DamageInfo info)
        {
            flashUntil = Time.time + flashSeconds;

            // Knocked back along the blow, so a hit reads as an impact and not just a colour change
            Vector3 push = info.Direction;
            push.y = 0f;
            recoil = push.sqrMagnitude > 0.001f ? push.normalized * recoilDistance : Vector3.zero;
        }

        void OnDied()
        {
            dead = true;
            Tint(deathColour);

            // Falls over where it stood. There is no death animation to play yet
            transform.rotation *= Quaternion.Euler(84f, 0f, 0f);
        }

        void LateUpdate()
        {
            if (body == null || dead)
                return;

            if (recoil.sqrMagnitude > 0.0001f)
            {
                // Assigning transform.position on something with a CharacterController does nothing,
                // the controller writes its own position back on its next move. It has to be pushed
                Vector3 step = recoil * (Time.deltaTime * 6f);

                if (controller != null && controller.enabled)
                    controller.Move(step);
                else
                    transform.position += step;

                recoil = Vector3.Lerp(recoil, Vector3.zero, Time.deltaTime * 12f);
            }

            // Only written when it changes, a property block set every frame on every enemy is a
            // per frame cost for a colour that is the same as it was last frame
            bool wants = Time.time < flashUntil;

            if (wants == flashing)
                return;

            flashing = wants;
            Tint(wants ? hitColour : baseColour);
        }

        void Tint(Color colour)
        {
            if (body == null)
                return;

            block.SetColor(BaseColour, colour);
            body.SetPropertyBlock(block);
        }
    }
}
