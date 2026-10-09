using System.Collections;
using UnityEngine;

namespace EndlessDescent.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    // Runs before PlayerLocomotion so a mantle consumes the jump press instead of also jumping
    [DefaultExecutionOrder(-10)]
    public class PlayerMantle : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] PlayerLocomotion locomotion;

        [Header("Detection")]
        [SerializeField] float chestHeight = 1.1f;
        [SerializeField] float reach = 0.75f;
        [SerializeField] float minLedgeHeight = 0.6f;
        [SerializeField] float maxLedgeHeight = 1.9f;
        [SerializeField] float maxSurfaceAngle = 40f;
        [SerializeField] float forwardInset = 0.35f;
        [SerializeField] LayerMask obstacleMask = ~0;

        [Header("Motion")]
        [SerializeField] float duration = 0.34f;
        [SerializeField] float staminaCost = 18f;

        CharacterController controller;
        CharacterSheet sheet;

        public bool IsMantling { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            sheet = GetComponent<CharacterSheet>();
        }

        void Update()
        {
            if (IsMantling || !input.JumpPressed)
                return;

            if (!TryFindLedge(out Vector3 target))
                return;

            if (!stamina.TrySpend(staminaCost))
                return;

            sheet?.Use(EndlessDescent.Data.SkillId.Climbing);
            StartCoroutine(Climb(target));
        }

        bool TryFindLedge(out Vector3 target)
        {
            target = default;

            Vector3 chest = transform.position + Vector3.up * chestHeight;
            if (!Physics.Raycast(chest, transform.forward, out RaycastHit wall, reach, obstacleMask, QueryTriggerInteraction.Ignore))
                return false;

            Vector3 flatNormal = new Vector3(wall.normal.x, 0f, wall.normal.z);
            if (flatNormal.sqrMagnitude < 0.0001f)
                return false;

            Vector3 forward = -flatNormal.normalized;
            float probeHeight = maxLedgeHeight + 0.5f;
            Vector3 probeStart = transform.position + Vector3.up * probeHeight + forward * (reach + forwardInset);

            if (!Physics.Raycast(probeStart, Vector3.down, out RaycastHit top, probeHeight, obstacleMask, QueryTriggerInteraction.Ignore))
                return false;

            if (Vector3.Angle(top.normal, Vector3.up) > maxSurfaceAngle)
                return false;

            float rise = top.point.y - transform.position.y;
            if (rise < minLedgeHeight || rise > maxLedgeHeight)
                return false;

            if (!Fits(top.point))
                return false;

            target = top.point;
            return true;
        }

        bool Fits(Vector3 footPosition)
        {
            float radius = controller.radius * 0.95f;
            float height = Mathf.Max(controller.height, radius * 2f);
            Vector3 bottom = footPosition + Vector3.up * (radius + controller.skinWidth);
            Vector3 top = footPosition + Vector3.up * (height - radius);

            return !Physics.CheckCapsule(bottom, top, radius, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        IEnumerator Climb(Vector3 target)
        {
            IsMantling = true;
            locomotion.enabled = false;
            controller.enabled = false;

            Vector3 start = transform.position;
            Vector3 apex = new Vector3(start.x, target.y + 0.05f, start.z);

            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Clamp01(t + Time.deltaTime / duration);

                // Rise clear of the ledge face first, then move in, so the capsule never clips it
                Vector3 rising = Vector3.Lerp(start, apex, Mathf.Clamp01(t * 2f));
                transform.position = Vector3.Lerp(rising, target, Mathf.Clamp01(t * 2f - 1f));

                yield return null;
            }

            transform.position = target;

            controller.enabled = true;
            locomotion.enabled = true;
            IsMantling = false;
        }
    }
}
