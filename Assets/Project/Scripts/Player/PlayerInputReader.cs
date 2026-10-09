using UnityEngine;
using UnityEngine.InputSystem;

namespace EndlessDescent.Player
{
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;
        [SerializeField] string actionMapName = "Player";

        InputActionMap map;
        InputAction moveAction;
        InputAction lookAction;
        InputAction jumpAction;
        InputAction sprintAction;
        InputAction crouchAction;
        InputAction attackAction;
        InputAction interactAction;
        InputAction stanceAction;
        InputAction dodgeAction;
        InputAction blockAction;
        InputAction nextSpellAction;
        InputAction inventoryAction;
        InputAction mapAction;
        InputAction characterAction;
        InputAction journalAction;
        InputAction quickSaveAction;
        InputAction quickLoadAction;

        public Vector2 Move => moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 Look => lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool JumpPressed => jumpAction != null && jumpAction.WasPressedThisFrame();
        public bool SprintHeld => sprintAction != null && sprintAction.IsPressed();
        public bool CrouchHeld => crouchAction != null && crouchAction.IsPressed();
        public bool AttackPressed => attackAction != null && attackAction.WasPressedThisFrame();
        public bool InteractPressed => interactAction != null && interactAction.WasPressedThisFrame();
        public bool StancePressed => stanceAction != null && stanceAction.WasPressedThisFrame();
        public bool DodgePressed => dodgeAction != null && dodgeAction.WasPressedThisFrame();
        public bool BlockHeld => blockAction != null && blockAction.IsPressed();
        public bool NextSpellPressed => nextSpellAction != null && nextSpellAction.WasPressedThisFrame();
        public bool InventoryPressed => inventoryAction != null && inventoryAction.WasPressedThisFrame();
        public bool MapPressed => mapAction != null && mapAction.WasPressedThisFrame();
        public bool CharacterPressed => characterAction != null && characterAction.WasPressedThisFrame();
        public bool JournalPressed => journalAction != null && journalAction.WasPressedThisFrame();
        public bool QuickSavePressed => quickSaveAction != null && quickSaveAction.WasPressedThisFrame();
        public bool QuickLoadPressed => quickLoadAction != null && quickLoadAction.WasPressedThisFrame();

        void Awake()
        {
            if (actions == null)
            {
                Debug.LogError($"{nameof(PlayerInputReader)} has no {nameof(InputActionAsset)} assigned.", this);
                enabled = false;
                return;
            }

            map = actions.FindActionMap(actionMapName);
            if (map == null)
            {
                Debug.LogError($"Action map '{actionMapName}' not found in '{actions.name}'.", this);
                enabled = false;
                return;
            }

            moveAction = map.FindAction("Move");
            lookAction = map.FindAction("Look");
            jumpAction = map.FindAction("Jump");
            sprintAction = map.FindAction("Sprint");
            crouchAction = map.FindAction("Crouch");
            attackAction = map.FindAction("Attack");
            interactAction = map.FindAction("Interact");
            stanceAction = map.FindAction("Stance");
            dodgeAction = map.FindAction("Dodge");
            blockAction = map.FindAction("Block");
            nextSpellAction = map.FindAction("NextSpell");
            inventoryAction = map.FindAction("Inventory");
            mapAction = map.FindAction("Map");
            characterAction = map.FindAction("Character");
            journalAction = map.FindAction("Journal");
            quickSaveAction = map.FindAction("QuickSave");
            quickLoadAction = map.FindAction("QuickLoad");
        }

        void OnEnable() => map?.Enable();

        void OnDisable() => map?.Disable();
    }
}
