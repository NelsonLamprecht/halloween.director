using System.Reflection;

namespace Halloween.Director.Remote
{
    /// <summary>The version and build stamp from src\Directory.Build.props, for display ("v1.0.0 (build 261017.1742)").</summary>
    public static class AppVersion
    {
        public static string Text { get; } = Read();

        private static string Read()
        {
            var version = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(version))
            {
                return string.Empty;
            }

            // the SDK can append "+<source revision>"
            var plus = version.IndexOf('+');
            return "v" + (plus < 0 ? version : version.Substring(0, plus));
        }
    }
}
