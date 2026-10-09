using UnityEngine;

namespace EndlessDescent.Dungeons
{
    // Authored on the donor module prefab. Forward points OUT through the doorway
    [DisallowMultipleComponent]
    public class ModuleConnector : MonoBehaviour
    {
        [SerializeField] float gizmoSize = 0.6f;

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, gizmoSize * 0.25f);
            Gizmos.DrawRay(transform.position, transform.forward * gizmoSize);
        }
    }
}
