using UnityEngine;

namespace EndlessDescent.Combat
{
    // What you have done with a working, kept because somebody is going to ask
    // The Lamplighters admit anyone who has never killed with a spell, and they check, this is what
    // they check. It is deliberately not a score and not shown anywhere, the player is
    // never told this is being counted, and finds out when they are asked
    [DisallowMultipleComponent]
    public class SpellRecord : MonoBehaviour
    {
        [SerializeField] int killsBySpell;

        public int KillsBySpell => killsBySpell;
        public bool HasKilledWithMagic => killsBySpell > 0;

        public void RecordKill() => killsBySpell++;
    }
}
