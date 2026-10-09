namespace EndlessDescent.Core
{
    // Implemented on the player so enemy senses can read how detectable they currently are without
    // Game.Combat having to know anything about Game.Player
    public interface IStealthProfile
    {
        // How far the noise of moving carries, in metres. Zero means silent
        float NoiseRadius { get; }

        // Scales how far away this actor can be seen. Below 1 is harder to see
        float VisibilityScale { get; }

        bool IsSneaking { get; }
    }
}
