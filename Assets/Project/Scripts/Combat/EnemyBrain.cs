using System.Collections.Generic;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Combat
{
    // Direct steering rather than a NavMeshAgent, dungeons have no baked NavMesh yet, and this lets
    // enemies be playtested in a greybox scene today. Swap to an agent when runtime baking lands
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class EnemyBrain : MonoBehaviour
    {
        [SerializeField] EnemyDefinition definition;
        [SerializeField] Health health;
        [SerializeField] Poise poise;
        [SerializeField] MeleeAttacker attacker;
        [SerializeField] Transform target;
        [SerializeField] string targetTag = "Player";
        [SerializeField] float gravity = -18f;
        [SerializeField] float turnSpeed = 8f;
        [SerializeField] float stopDistanceMargin = 0.4f;
        [SerializeField] EnemySenses senses;

        CharacterController controller;
        float nextAttackTime;
        float verticalVelocity;
        Behaviour[] gated;
        bool thinking = true;

        public EnemyDefinition Definition => definition;

        // Registered on Awake and cleared on destroy, never on enable, AiActivation switches this
        // component off, and a registry keyed on enabled state could never switch it back on
        static readonly List<EnemyBrain> alive = new List<EnemyBrain>();
        public static IReadOnlyList<EnemyBrain> Alive => alive;

        public void SetThinking(bool value)
        {
            if (value == thinking)
                return;

            thinking = value;

            foreach (Behaviour behaviour in gated)
                if (behaviour != null) behaviour.enabled = value;
        }

        void OnDestroy() => alive.Remove(this);

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            gated = new Behaviour[] { this, senses, attacker, poise, GetComponent<HitResponse>() };
            alive.Add(this);

            if (definition == null)
                return;

            CreatureSheet sheet = GetComponent<CreatureSheet>();
            if (sheet == null)
                sheet = gameObject.AddComponent<CreatureSheet>();

            sheet.Adopt(definition.Stats);

            if (health != null)
                health.Configure(definition.MaxHealth);

            // Armour was a number on the definition that nothing read until now
            CreatureDefence defence = GetComponent<CreatureDefence>();
            if (defence == null)
                defence = gameObject.AddComponent<CreatureDefence>();

            defence.Configure(definition.Armour, sheet);

            if (attacker != null)
            {
                attacker.Configure(definition.Damage, definition.Reach, definition.MaxHealth * 0.25f,
                    definition.Windup, definition.Active, definition.Recovery);
                attacker.Wield(sheet, definition.WeaponSkill);
            }
        }

        void Start()
        {
            if (senses == null)
                senses = GetComponent<EnemySenses>();

            if (target == null && !string.IsNullOrEmpty(targetTag))
            {
                GameObject found = GameObject.FindGameObjectWithTag(targetTag);
                if (found != null)
                    target = found.transform;

                senses?.Bind(target);
            }
        }

        void Update()
        {
            if (definition == null || health == null || !health.IsAlive)
                return;

            ApplyGravity();

            if (target == null || (poise != null && poise.IsStaggered) || (attacker != null && attacker.IsBusy))
                return;

            if (senses != null && !senses.IsAware)
                return;

            Vector3 chase = senses != null ? senses.LastKnownPosition : target.position;

            Vector3 toTarget = chase - transform.position;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            if (senses == null && distance > definition.AggroRange)
                return;

            Face(toTarget);

            TryOpenDoorAhead();

            float stopDistance = definition.Reach - stopDistanceMargin;
            if (distance > stopDistance)
                controller.Move(toTarget.normalized *
                    (EndlessDescent.Data.CharacterMaths.MoveSpeed(GetComponent<CreatureSheet>(), definition.MoveSpeed) * Time.deltaTime));
            else if (Time.time >= nextAttackTime && attacker != null && attacker.TryStartAttack())
            {
                nextAttackTime = Time.time + definition.AttackCooldown;
                TryInflict();
            }
        }

        // Rolled on the swing rather than on the hit, the attacker does not know whether it landed,
        // and a near miss from a plague bearer being harmless is a fair simplification for greybox
        void TryInflict()
        {
            if (definition.Inflicts == EndlessDescent.Core.AfflictionId.None || target == null)
                return;

            if (Random.value > definition.InflictChance)
                return;

            target.GetComponentInParent<EndlessDescent.Core.IAfflictable>()?.Contract(definition.Inflicts);
        }

        // Doors closed the dungeon off to everything that was not the player. An enemy that has seen
        // you will now open one that is in its way
        void TryOpenDoorAhead()
        {
            if (Time.time < nextDoorTry)
                return;

            nextDoorTry = Time.time + 0.5f;

            if (!Physics.Raycast(transform.position + Vector3.up, transform.forward, out RaycastHit hit, 2.2f,
                    ~0, QueryTriggerInteraction.Ignore))
                return;

            EndlessDescent.Dungeons.Door door = hit.collider.GetComponentInParent<EndlessDescent.Dungeons.Door>();

            if (door != null && door.CanInteract(gameObject))
                door.Interact(gameObject);
        }

        float nextDoorTry;

        void ApplyGravity()
        {
            verticalVelocity = controller.isGrounded ? -2f : verticalVelocity + gravity * Time.deltaTime;
            controller.Move(Vector3.up * (verticalVelocity * Time.deltaTime));
        }

        void Face(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return;

            Quaternion wanted = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, turnSpeed * Time.deltaTime);
        }
    }
}
