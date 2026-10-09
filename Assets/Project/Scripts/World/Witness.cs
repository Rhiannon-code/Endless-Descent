using EndlessDescent.Combat;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.World
{
    // Put on anything that is a person rather than a monster. If it takes a hit from the player and
    // something can see it happen, that is an assault on the record
    [DisallowMultipleComponent]
    public class Witness : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] float witnessRadius = 20f;

        readonly Collider[] nearby = new Collider[24];

        void OnEnable()
        {
            if (health == null)
                health = GetComponent<Health>();

            if (health != null)
                health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (health != null)
                health.Damaged -= OnDamaged;
        }

        // The record lives with the other systems, not on the player, so it was never found on the
        // attacker and nothing was ever reported
        void OnDamaged(DamageInfo info)
        {
            if (info.Source == null || !info.Source.CompareTag("Player"))
                return;

            // Only a crime if someone is around to see it. An empty corridor is an empty corridor
            if (!Seen(info.Source))
                return;

            CrimeRecord record = FindFirstObjectByType<CrimeRecord>();
            record?.Report(health != null && !health.IsAlive ? CrimeKind.Murder : CrimeKind.Assault);
        }

        bool Seen(GameObject attacker)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, witnessRadius, nearby,
                ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Witness other = nearby[i].GetComponentInParent<Witness>();
                if (other != null && other != this && Sees(other, attacker))
                    return true;
            }

            return false;
        }

        // From their eyes to the victim. Anyone within twenty metres used to count, through walls
        // and from inside houses. The attacker standing in the way is not a wall
        bool Sees(Witness other, GameObject attacker)
        {
            Vector3 eyes = other.transform.position + Vector3.up * 1.6f;
            Vector3 victim = transform.position + Vector3.up * 1.2f;

            if (!Physics.Linecast(eyes, victim, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                return true;

            Transform struck = hit.collider.transform;
            return struck.IsChildOf(transform) || struck.IsChildOf(other.transform) ||
                   (attacker != null && struck.IsChildOf(attacker.transform));
        }
    }
}
