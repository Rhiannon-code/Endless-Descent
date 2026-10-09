using EndlessDescent.Core;
using UnityEngine;

namespace EndlessDescent.Dungeons
{
    [DisallowMultipleComponent]
    public class Door : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform leaf;
        [SerializeField] DungeonGenerator generator;
        [SerializeField] int keyId = -1;
        [SerializeField] GameObject lockedLook;
        [SerializeField] float openAngle = 100f;
        [SerializeField] float degreesPerSecond = 260f;

        bool open;
        float angle;

        public bool Locked => keyId >= 0 && (generator == null || !generator.HasKey(keyId));

        public string Prompt => Locked ? "Locked: needs a key" : open ? "Close door" : "Open door";

        public void SetKey(int lockedBy)
        {
            keyId = lockedBy;

            if (lockedLook != null)
                lockedLook.SetActive(lockedBy >= 0);
        }

        public void BindGenerator(DungeonGenerator owner) => generator = owner;

        public bool CanInteract(GameObject actor) => true;

        public void Interact(GameObject actor)
        {
            if (!Locked)
                open = !open;
        }

        void Update()
        {
            if (leaf == null)
                return;

            float target = open ? openAngle : 0f;
            if (Mathf.Approximately(angle, target))
                return;

            angle = Mathf.MoveTowards(angle, target, degreesPerSecond * Time.deltaTime);
            leaf.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
