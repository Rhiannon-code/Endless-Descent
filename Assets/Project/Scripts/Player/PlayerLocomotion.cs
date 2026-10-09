using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerLocomotion : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] Transform cameraPivot;
        [SerializeField] CharacterSheet sheet;
        [SerializeField] GearLoad gear;

        [Header("Speed")]
        [SerializeField] float walkSpeed = 4.2f;
        [SerializeField] float sprintSpeed = 7f;
        [SerializeField] float crouchSpeed = 2f;
        [SerializeField] float groundAcceleration = 60f;
        [SerializeField] float airAcceleration = 12f;

        [Header("Jump and gravity")]
        [SerializeField] float jumpHeight = 1.15f;
        [SerializeField] float gravity = -22f;
        [SerializeField] float coyoteTime = 0.12f;
        [SerializeField] float jumpBufferTime = 0.12f;

        [Header("Crouch")]
        [SerializeField] float standHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.1f;
        [SerializeField] float heightLerpSpeed = 12f;
        [SerializeField] float eyeDropFromTop = 0.15f;
        [SerializeField] LayerMask obstacleMask = ~0;

        [Header("Stamina cost")]
        [SerializeField] float sprintDrainPerSecond = 12f;
        [SerializeField] float jumpCost = 12f;

        CharacterController controller;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float targetHeight;
        float lastGroundedTime = -99f;
        float lastJumpPressedTime = -99f;

        public bool IsGrounded => controller.isGrounded;
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public Vector3 HorizontalVelocity => horizontalVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (sheet == null)
                sheet = GetComponent<CharacterSheet>();

            targetHeight = standHeight;
            ApplyHeight(standHeight);
        }

        void Update()
        {
            UpdateCrouch();
            UpdateHorizontal();
            UpdateVertical();

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        void UpdateCrouch()
        {
            bool wantsCrouch = input.CrouchHeld;
            IsCrouching = wantsCrouch || !CanStand();
            targetHeight = IsCrouching ? crouchHeight : standHeight;

            if (!Mathf.Approximately(controller.height, targetHeight))
                ApplyHeight(Mathf.Lerp(controller.height, targetHeight, heightLerpSpeed * Time.deltaTime));
        }

        void UpdateHorizontal()
        {
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);
            Vector3 direction = transform.right * move.x + transform.forward * move.y;

            IsSprinting = input.SprintHeld
                          && !IsCrouching
                          && move.y > 0.1f
                          && stamina.TrySpend(sprintDrainPerSecond * Time.deltaTime);

            float targetSpeed = IsCrouching ? CharacterMaths.MoveSpeed(sheet, crouchSpeed)
                : IsSprinting ? CharacterMaths.SprintSpeed(sheet, sprintSpeed)
                : CharacterMaths.MoveSpeed(sheet, walkSpeed);

            if (gear != null)
                targetSpeed *= gear.MoveScale;
            float acceleration = controller.isGrounded ? groundAcceleration : airAcceleration;

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                direction * targetSpeed,
                acceleration * Time.deltaTime);
        }

        void UpdateVertical()
        {
            if (controller.isGrounded)
            {
                lastGroundedTime = Time.time;

                if (verticalVelocity < 0f)
                    verticalVelocity = -2f;
            }

            if (input.JumpPressed)
                lastJumpPressedTime = Time.time;

            bool buffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
            bool coyote = Time.time - lastGroundedTime <= coyoteTime;

            if (buffered && coyote && !IsCrouching && stamina.TrySpend(jumpCost))
            {
                verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
                lastJumpPressedTime = -99f;
                lastGroundedTime = -99f;
            }

            verticalVelocity += gravity * Time.deltaTime;
        }

        bool CanStand()
        {
            float gap = standHeight - controller.height;
            if (gap <= 0.01f)
                return true;

            float radius = controller.radius * 0.95f;
            Vector3 top = transform.position + Vector3.up * (controller.height - radius);

            return !Physics.SphereCast(top, radius, Vector3.up, out _, gap, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        void ApplyHeight(float height)
        {
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);

            if (cameraPivot != null)
                cameraPivot.localPosition = new Vector3(0f, height - eyeDropFromTop, 0f);
        }
    }
}
