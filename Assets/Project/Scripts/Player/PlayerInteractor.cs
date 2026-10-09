using System;
using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Player
{
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] Transform rayOrigin;
        [SerializeField] float range = 3f;
        [SerializeField] LayerMask mask = ~0;

        IInteractable current;
        string shown;
        float nextRead;

        public event Action<string> PromptChanged;

        public IInteractable Current => current;

        // The prompt is read again a few times a second and straight after using the thing, not only
        // when looking at something new. A trainer still showed the price of the point just bought
        void Update()
        {
            IInteractable found = Probe();

            if (!ReferenceEquals(found, current) || Time.time >= nextRead)
            {
                current = found;
                nextRead = Time.time + 0.25f;

                string prompt = current != null && current.CanInteract(gameObject) ? current.Prompt : string.Empty;

                if (prompt != shown)
                {
                    shown = prompt;
                    PromptChanged?.Invoke(prompt);
                }
            }

            if (input != null && input.InteractPressed && current != null && current.CanInteract(gameObject))
            {
                current.Interact(gameObject);
                nextRead = 0f;
            }
        }

        IInteractable Probe()
        {
            Transform origin = rayOrigin != null ? rayOrigin : transform;

            if (!Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, range, mask, QueryTriggerInteraction.Collide))
                return null;

            return hit.collider.GetComponentInParent<IInteractable>();
        }
    }
}
