namespace EndlessDescent.Core
{
    // Lives in Core so combat can hand out an illness without depending on whatever tracks it
    public enum AfflictionId
    {
        None,
        Poison,
        SwampFever,
        Plague,
        Witheringrot,
        Vampirism
    }

    public interface IAfflictable
    {
        bool Contract(AfflictionId affliction);
    }
}
