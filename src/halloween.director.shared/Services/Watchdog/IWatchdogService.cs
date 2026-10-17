namespace Halloween.Director.Shared.Services.Watchdog
{
    public interface IWatchdogService: IRunableService
    {
        // device will reset inSeconds if not petted
        void Enable(int inSeconds);

        void Pet(int inSeconds);

        // Enable() with DefaultTimeoutSeconds, then Pet() every DefaultPetIntervalSeconds.
        void Start();
    }
}
