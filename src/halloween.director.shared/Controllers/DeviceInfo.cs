using System.Text;

namespace Halloween.Director.Shared.Controllers
{
    /// <summary>
    /// What a prop says about itself on GET /info (see <see cref="InfoRequestHandler"/>). The remote asks this
    /// to find props on the network without relying on the UDP advertisement.
    /// </summary>
    public static class DeviceInfo
    {
        /// <summary>The kind of prop, such as "monsterbox" or "scarecrow"; set by MeadowBase at startup.</summary>
        public static string Type { get; internal set; } = "unknown";

        /// <summary>The build's version string (see <see cref="BuildInfo"/>); set by MeadowBase at startup.</summary>
        public static string Version { get; internal set; } = "unknown version";

        // Hand-built because the device's JsonResult uses reflection (see agents.md).
        public static string ToJson(string name)
        {
            return "{\"name\":" + Quote(name) + ",\"type\":" + Quote(Type) + ",\"version\":" + Quote(Version) + "}";
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
