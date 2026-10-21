using System.Globalization;
using System.Text;

namespace Halloween.Director.Shared.Controllers
{
    /// <summary>
    /// What a prop says about itself on GET /info (see <see cref="InfoRequestHandler"/>). The remote asks this
    /// to find props on the network without relying on the UDP advertisement. Example:
    /// <c>{"name":"Monsterbox-v1","type":"monsterbox","version":"1.0.0 (build ...)","ip":"192.168.1.42","uptimeSeconds":3725,"health":"healthy"}</c>
    /// </summary>
    public static class DeviceInfo
    {
        /// <summary>The kind of prop, such as "monsterbox" or "scarecrow"; set by MeadowBase at startup.</summary>
        public static string Type { get; internal set; } = "unknown";

        /// <summary>The build's version string (see <see cref="BuildInfo"/>); set by MeadowBase at startup.</summary>
        public static string Version { get; internal set; } = "unknown version";

        // Hand-built because the device's JsonResult uses reflection (see agents.md). ip is null
        // when there isn't one; health is "healthy" or what the heartbeat health check found wrong.
        public static string ToJson(string name, string ip, long uptimeSeconds, string health)
        {
            return "{\"name\":" + Quote(name)
                + ",\"type\":" + Quote(Type)
                + ",\"version\":" + Quote(Version)
                + ",\"ip\":" + (ip == null ? "null" : Quote(ip))
                + ",\"uptimeSeconds\":" + uptimeSeconds.ToString(CultureInfo.InvariantCulture)
                + ",\"health\":" + Quote(health)
                + "}";
        }

        private static string Quote(string value)
        {
            var text = new StringBuilder("\"");
            foreach (var c in value ?? string.Empty)
            {
                if (c == '"' || c == '\\')
                {
                    text.Append('\\').Append(c);
                }
                else if (c < ' ')
                {
                    text.Append(' ');
                }
                else
                {
                    text.Append(c);
                }
            }

            return text.Append('"').ToString();
        }
    }
}
