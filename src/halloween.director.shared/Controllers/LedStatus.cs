namespace Halloween.Director.Shared.Controllers
{
    /// <summary>
    /// Boot and network stages shown on the onboard LED. See <see cref="LedController"/>
    /// for the color and pattern of each one.
    /// </summary>
    public enum LedStatus
    {
        /// <summary>Solid red: the app is initializing hardware.</summary>
        Initializing,

        /// <summary>Slow blue blink: waiting for the first WiFi connection.</summary>
        WaitingForWifi,

        /// <summary>Fast orange blink: WiFi failed to connect or dropped, and Meadow OS is retrying.</summary>
        WifiRetrying,

        /// <summary>Solid cyan: WiFi is connected and the Maple server is starting.</summary>
        StartingServer,

        /// <summary>Fast purple blink: the Maple server failed to start; the next WiFi connect retries.</summary>
        ServerFailed,

        /// <summary>Solid green: ready for commands.</summary>
        Ready,
    }
}
