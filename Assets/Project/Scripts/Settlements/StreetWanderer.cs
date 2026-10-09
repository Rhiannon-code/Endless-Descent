using UnityEngine;

namespace EndlessDescent.Settlements
{
    // Someone walking somewhere. Not a schedule and not an errand, a town with nobody in the
    // streets reads as abandoned, and that is the gap this closes
    [DisallowMultipleComponent]
    public class StreetWanderer : MonoBehaviour
    {
        [SerializeField] SettlementGenerator settlement;
        [SerializeField] float speed = 1.3f;
        [SerializeField] float turnSpeed = 4f;
        [SerializeField] float arriveWithin = 1.4f;
        [SerializeField] Vector2 pauseFor = new Vector2(1.5f, 6f);

        Vector2 target;
        float waitUntil;
        bool hasTarget;

        public void Bind(SettlementGenerator owner) => settlement = owner;

        void Update()
        {
            if (settlement == null || settlement.Plan == null)
                return;

            if (Time.time < waitUntil)
                return;

            if (!hasTarget && !ChooseTarget())
                return;

            Vector3 here = transform.localPosition;
            Vector2 flat = new Vector2(here.x, here.z);
            Vector2 toTarget = target - flat;

            if (toTarget.magnitude <= arriveWithin)
            {
                hasTarget = false;
                waitUntil = Time.time + Random.Range(pauseFor.x, pauseFor.y);
                return;
            }

            Vector2 step = toTarget.normalized * (speed * Time.deltaTime);
            Vector2 next = flat + step;

            // Walked over the ground rather than through it, there is no navmesh in a town that is
            // rebuilt every time you travel. In the plan's own space, which is the settlement's: the
            // town stands wherever the world put it
            transform.localPosition = new Vector3(next.x, settlement.Plan.Height(next), next.y);

            Quaternion look = Quaternion.LookRotation(new Vector3(step.x, 0f, step.y).normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        bool ChooseTarget()
        {
            SettlementPlan plan = settlement.Plan;

            if (plan.Streets.Count == 0)
                return false;

            // Somewhere along a street, so people keep to the roads instead of walking through walls
            Street street = plan.Streets[Random.Range(0, plan.Streets.Count)];
            target = Vector2.Lerp(street.A, street.B, Random.value);

            hasTarget = true;
            return true;
        }
    }
}
