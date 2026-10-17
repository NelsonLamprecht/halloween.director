using Meadow.Foundation.Web.Maple;

namespace Halloween.Director.Shared.Services.MapleService
{
    /// <summary>
    /// Per-app settings for <see cref="MapleService"/>. Each prop picks its own port so
    /// several of them can share one network and be told apart by the remote.
    /// </summary>
    public class MapleServiceOptions
    {
        public MapleServiceOptions(int port)
        {
            Port = port;
        }

        public int Port { get; }

        public int AdvertiseIntervalMs { get; set; } = 5000;

        // Parallel by default so a long-running request can't hold up the others
        public RequestProcessMode ProcessMode { get; set; } = RequestProcessMode.Parallel;
    }
}
