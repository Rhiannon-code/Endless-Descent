using UnityEngine;

namespace EndlessDescent.Combat
{
    public abstract class DamageMitigation : MonoBehaviour
    {
        public abstract DamageInfo Modify(DamageInfo info);
    }
}
