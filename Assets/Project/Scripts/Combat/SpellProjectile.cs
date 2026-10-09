using UnityEngine;

namespace EndlessDescent.Combat
{
    [DisallowMultipleComponent]
    public class SpellProjectile : MonoBehaviour
    {
        [SerializeField, Min(0f)] float speed = 18f;
        [SerializeField, Min(0f)] float lifetime = 5f;
        [SerializeField, Min(0f)] float radius = 0.25f;
        [SerializeField] int damage = 20;

        static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        GameObject owner;
        float age;

        public void Launch(GameObject source, int amount, float launchSpeed, Color colour)
        {
            owner = source;
            damage = amount;
            speed = launchSpeed;

            // A property block, not renderer.material, which leaves a material copy behind every bolt
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColour, colour);

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                renderer.SetPropertyBlock(block);
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            float step = speed * Time.deltaTime;

            // Swept rather than a trigger, at this speed a frame can step clean through a wall
            if (Physics.SphereCast(transform.position, radius, transform.forward, out RaycastHit hit, step,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                bool hitTheCaster = owner != null && hit.transform.IsChildOf(owner.transform);

                if (!hitTheCaster)
                {
                    IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

                    if (target != null && target.IsAlive)
                    {
                        target.TakeDamage(new DamageInfo
                        {
                            Amount = damage,
                            PoiseDamage = damage * 0.5f,
                            Source = owner,
                            Direction = transform.forward,
                            Parryable = false,
                            FromSpell = true
                        });

                        // Recorded on the caster, because it is the caster who will be asked
                        if (!target.IsAlive && owner != null)
                            owner.GetComponentInParent<SpellRecord>()?.RecordKill();
                    }

                    Destroy(gameObject);
                    return;
                }
            }

            transform.position += transform.forward * step;
        }
    }
}
