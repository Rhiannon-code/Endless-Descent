using EndlessDescent.Combat;
using UnityEngine;

namespace EndlessDescent.World
{
    public enum TrapKind { Spikes, Dart, Blade, Gas }

    // One component for every trap, the prefabs differ in looks, not behaviour. Armed traps fire on
    // anything with health that enters, then re-arm, so a corridor stays dangerous on the way back
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class Trap : MonoBehaviour
    {
        [SerializeField] TrapKind kind = TrapKind.Spikes;
        [SerializeField, Min(1)] int damage = 12;
        [SerializeField, Min(0f)] float rearmSeconds = 3f;
        [SerializeField] Transform movingPart;
        [SerializeField] float strikeDistance = 0.6f;
        [SerializeField] EndlessDescent.Core.AfflictionId inflicts = EndlessDescent.Core.AfflictionId.None;

        float armedAt;
        float strikeUntil;

        public TrapKind Kind => kind;

        void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (Time.time < armedAt)
                return;

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive)
                return;

            target.TakeDamage(new DamageInfo
            {
                Amount = damage,
                PoiseDamage = damage,
                Source = gameObject,
                Direction = Vector3.up,
                Parryable = false
            });

            if (inflicts != EndlessDescent.Core.AfflictionId.None)
                other.GetComponentInParent<EndlessDescent.Core.IAfflictable>()?.Contract(inflicts);

            armedAt = Time.time + rearmSeconds;
            strikeUntil = Time.time + 0.2f;
        }

        void Update()
        {
            if (movingPart == null)
                return;

            float offset = Time.time < strikeUntil ? strikeDistance : 0f;
            Vector3 target = new Vector3(0f, offset, 0f);

            if (movingPart.localPosition == target)
                return;

            movingPart.localPosition = Vector3.MoveTowards(movingPart.localPosition, target, 6f * Time.deltaTime);
        }
    }
}
