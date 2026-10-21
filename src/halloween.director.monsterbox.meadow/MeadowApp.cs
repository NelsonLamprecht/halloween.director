using Halloween.Director.Monsterbox.Meadow.Controllers;
using Halloween.Director.Shared;

namespace Halloween.Director.Monsterbox.Meadow
{
    public class MeadowApp : MeadowBase
    {
        protected override string DefaultDeviceName => "Monsterbox-v1";

        protected override string DeviceType => "monsterbox";

        // MeadowBase.Initialize() runs the shared boot sequence and calls this for the
        // monsterbox's own hardware
        protected override void InitializeProp()
        {
            // each controller logs its own section from Initialize()
            var relayController = Services.Create<RelayController>();
            relayController.Initialize(Device.Pins.D05, Device.Pins.D06);

            var mp3Controller = Services.Create<MP3Controller>();
            mp3Controller.Initialize();

            Logger.StartupSection("Sounds", () =>
            {
                var soundCatalog = SoundCatalog.FromSettings(Settings, Logger);
                Services.Add(soundCatalog);
                Logger.StartupDetail($"{soundCatalog.Count} sounds configured for GET /sounds.");
            });

            Logger.StartupSection("Cylinders", () =>
            {
                Services.Create<CylindersController>();
            });
        }

        protected override void DisposeServices()
        {
            DisposeIfRegistered<MP3Controller>();

            // before RelayController, so an in-flight shake stops before its relays go away
            DisposeIfRegistered<CylindersController>();
            DisposeIfRegistered<RelayController>();
        }
    }
}
