using System;

using Halloween.Director.Scarecrow.Meadow.Controllers;
using Halloween.Director.Shared;

namespace Halloween.Director.Scarecrow.Meadow
{
    public class MeadowApp : MeadowBase
    {
        protected override string DefaultDeviceName => "Scarecrow-v1";

        protected override string DeviceType => "scarecrow";

        // MeadowBase.Initialize() runs the shared boot sequence and calls this for the
        // scarecrow's own hardware
        protected override void InitializeProp()
        {
            // the controller logs its own section from Initialize()
            var relayController = Services.Create<RelayController>();
            relayController.Initialize(Device.Pins.D05);

            Logger.StartupSection("LiftController", () =>
            {
                var options = new LiftOptions();
                if (Settings.TryGetValue("App.LiftMaxUpSeconds", out var configured))
                {
                    if (int.TryParse(configured, out var seconds) && seconds >= 0)
                    {
                        options.MaxUp = TimeSpan.FromSeconds(seconds);
                    }
                    else
                    {
                        Logger.Warn($"App.LiftMaxUpSeconds '{configured}' is not a number of seconds >= 0; using {LiftOptions.DefaultMaxUpSeconds}.");
                    }
                }

                Services.Add(options);
                Services.Create<LiftController>();
                Logger.Info(options.MaxUp > TimeSpan.Zero
                    ? $"Lift lowers itself after {options.MaxUp.TotalSeconds}s up."
                    : "Lift has no time limit.");
            });
        }

        protected override void DisposeServices()
        {
            // the lift's pending auto-lower first, then the relay (which closes the valve
            // before its pin is released)
            DisposeIfRegistered<LiftController>();
            DisposeIfRegistered<RelayController>();
        }
    }
}
