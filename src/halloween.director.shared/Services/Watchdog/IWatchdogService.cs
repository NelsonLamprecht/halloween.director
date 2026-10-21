using System;

namespace Halloween.Director.Shared.Services.Watchdog
{
    public interface IWatchdogService: IRunableService
    {
        // raised on a thread-pool thread after each pet, so a slow handler can't delay petting
        event Action Petted;

        // device will reset inSeconds if not petted
        void Enable(int inSeconds);

        void Pet(int inSeconds);

        // Enable() with DefaultTimeoutSeconds, then Pet() every DefaultPetIntervalSeconds.
        void Start();
    }
}
