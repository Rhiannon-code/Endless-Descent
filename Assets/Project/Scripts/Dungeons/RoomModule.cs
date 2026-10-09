using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Dungeons
{
    [DisallowMultipleComponent]
    public class RoomModule : MonoBehaviour
    {
        [SerializeField] ModuleConnector[] connectors;
        [SerializeField] Bounds localBounds = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(8f, 4f, 8f));
        [SerializeField] EndlessDescent.Data.RoomPurpose purpose = EndlessDescent.Data.RoomPurpose.None;

        public IReadOnlyList<ModuleConnector> Connectors => connectors;
        public Bounds LocalBounds => localBounds;
        public EndlessDescent.Data.RoomPurpose Purpose => purpose;

        public Vector3 LocalPositionOf(ModuleConnector connector)
        {
            return transform.InverseTransformPoint(connector.transform.position);
        }

        public Quaternion LocalRotationOf(ModuleConnector connector)
        {
            return Quaternion.Inverse(transform.rotation) * connector.transform.rotation;
        }

        void Reset() => connectors = GetComponentsInChildren<ModuleConnector>(true);

        void OnValidate()
        {
            if (connectors == null || connectors.Length == 0)
                connectors = GetComponentsInChildren<ModuleConnector>(true);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localBounds.center, localBounds.size);
        }
    }
}
