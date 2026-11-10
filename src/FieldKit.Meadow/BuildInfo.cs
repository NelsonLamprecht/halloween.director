using System.Reflection;

namespace FieldKit.Meadow
{
    /// <summary>
    /// The assembly's informational version (for example "1.0.0 (build 261017.1742 a3f9c1)"), so the
    /// boot log shows which firmware is running.
    /// </summary>
    public static class BuildInfo
    {
        public static string Describe(Assembly assembly)
        {
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return string.IsNullOrEmpty(version) ? "unknown version" : version;
        }
    }
}
