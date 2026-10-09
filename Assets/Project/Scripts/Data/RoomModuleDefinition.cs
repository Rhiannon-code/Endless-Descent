using System;
using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/Dungeons/Room Module", fileName = "RoomModule")]
    public class RoomModuleDefinition : ScriptableObject
    {
        [SerializeField] GameObject prefab;
        [SerializeField] RoomRole[] roles = { RoomRole.Junction };
        [SerializeField, Min(1)] int weight = 1;
        [SerializeField] RoomPurpose purpose = RoomPurpose.None;

        public GameObject Prefab => prefab;
        public int Weight => Mathf.Max(1, weight);
        public RoomPurpose Purpose => purpose;

        public bool CanServe(RoomRole role) => roles != null && Array.IndexOf(roles, role) >= 0;
    }
}
