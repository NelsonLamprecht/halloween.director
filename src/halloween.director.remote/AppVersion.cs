using System.Reflection;

namespace Halloween.Director.Remote
{
    /// <summary>The version and build stamp from src\Directory.Build.props, for display ("v1.0.0 (build 261017.1742 a3f9c1)").</summary>
    public static class AppVersion
    {
        public static string Text { get; } = Read();

        private static string Read()
        {
            var version = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return string.IsNullOrEmpty(version) ? string.Empty : "v" + version;
        }
    }
}
