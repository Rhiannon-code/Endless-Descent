using UnityEngine;

namespace EndlessDescent.Combat
{
    // An enemy is a capsule. It swings on the same timing the player's viewmodel does, and without
    // something that moves there is no way to read a windup, which is the whole basis of the combat
    [DisallowMultipleComponent]
    public class SwingArm : MonoBehaviour
    {
        [SerializeField] MeleeAttacker attacker;
        [SerializeField] Transform arm;
        [SerializeField] float followSpeed = 14f;

        static readonly Pose Rest = new Pose(new Vector3(0.42f, 0.05f, 0.18f), Quaternion.Euler(18f, 0f, 8f));
        static readonly Pose Windup = new Pose(new Vector3(0.52f, 0.42f, -0.16f), Quaternion.Euler(-64f, 22f, 20f));
        static readonly Pose Active = new Pose(new Vector3(-0.12f, -0.05f, 0.62f), Quaternion.Euler(46f, -34f, -40f));
        static readonly Pose Recover = new Pose(new Vector3(0.18f, -0.18f, 0.44f), Quaternion.Euler(30f, -14f, -14f));

        void Awake()
        {
            if (attacker == null)
                attacker = GetComponent<MeleeAttacker>();
        }

        void LateUpdate()
        {
            if (arm == null || attacker == null)
                return;

            Pose want = Pick();
            float step = followSpeed * Time.deltaTime;

            // The windup is snapped to rather than eased into, a telegraph the player cannot read in
            // time is not a telegraph
            if (attacker.Phase == AttackPhase.Windup)
                step = 1f;

            arm.localPosition = Vector3.Lerp(arm.localPosition, want.position, step);
            arm.localRotation = Quaternion.Slerp(arm.localRotation, want.rotation, step);
        }

        Pose Pick()
        {
            switch (attacker.Phase)
            {
                case AttackPhase.Windup: return Windup;
                case AttackPhase.Active: return Active;
                case AttackPhase.Recovery: return Recover;
                default: return Rest;
            }
        }
    }
}
