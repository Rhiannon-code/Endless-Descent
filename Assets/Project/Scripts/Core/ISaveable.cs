namespace EndlessDescent.Core
{
    public interface ISaveable
    {
        string SaveKey { get; }
        string CaptureJson();
        void RestoreJson(string json);
    }
}
