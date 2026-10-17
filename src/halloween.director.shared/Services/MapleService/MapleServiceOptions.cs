using Meadow.Foundation.Web.Maple;

namespace Halloween.Director.Shared.Services.MapleService
{
    /// <summary>
    /// Per-app settings for <see cref="MapleService"/>. Every prop listens on Maple's
    /// default port; props run on separate devices (separate IPs), so they don't clash.
    /// </summary>
    public class MapleServiceOptions
    {
        // Maple's own default; the remote app sends to this port
        public const int DefaultPort = 5417;

        public int Port { get; set; } = DefaultPort;

        public int AdvertiseIntervalMs { get; set; } = 2000;

        // Parallel by default so a long-running request can't hold up the others
        public RequestProcessMode ProcessMode { get; set; } = RequestProcessMode.Parallel;
    }
}
