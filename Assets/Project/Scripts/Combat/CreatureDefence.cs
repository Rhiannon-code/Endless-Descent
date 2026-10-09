using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Combat
{
    // A creature's armour, which until now was a number on its definition that nothing read
    [DisallowMultipleComponent]
    public class CreatureDefence : DamageMitigation
    {
        [SerializeField] int armour;
        [SerializeField] CreatureSheet sheet;

        public void Configure(int worn, CreatureSheet from)
        {
            armour = worn;
            sheet = from;
        }

        public override DamageInfo Modify(DamageInfo info)
        {
            int worn = CharacterMaths.Armour(sheet, armour);
            info.Amount = Mathf.Max(1, info.Amount - Mathf.RoundToInt(worn * (1f - Mathf.Clamp01(info.Penetration))))
                          + Mathf.Max(0, info.Unstoppable);
            return info;
        }
    }
}
