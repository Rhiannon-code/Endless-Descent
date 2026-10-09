using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Dungeons
{
    // Reads as wall until you search it. revealForTesting tints it and labels it [secret], for when a
    // secret has to be found on purpose, off, it is only findable by looking for the seam
    [DisallowMultipleComponent]
    public class SecretDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform panel;
        [SerializeField] Material testingTint;
        [SerializeField] bool revealForTesting;
        [SerializeField] float slide = 2.6f;
        [SerializeField] float speed = 3.5f;

        Vector3 closed;
        bool open;

        public string Prompt => revealForTesting
            ? (open ? "Secret passage (open)" : "Search the wall  [secret]")
            : open ? string.Empty : "Search the wall";

        void Awake()
        {
            if (panel == null)
                panel = transform;

            closed = panel.localPosition;

            if (revealForTesting && testingTint != null)
            {
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = testingTint;
            }
        }

        public bool CanInteract(GameObject actor) => !open;

        public void Interact(GameObject actor) => open = true;

        void Update()
        {
            if (panel == null)
                return;

            Vector3 target = open ? closed + Vector3.down * slide : closed;

            if (panel.localPosition == target)
                return;

            panel.localPosition = Vector3.MoveTowards(panel.localPosition, target, speed * Time.deltaTime);
        }
    }
}
