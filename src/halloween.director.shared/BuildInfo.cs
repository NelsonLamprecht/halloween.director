using System.Reflection;

namespace Halloween.Director.Shared
{
    /// <summary>
    /// The version stamped on a build by src\Directory.Build.props ("1.0.0 (build 26101714)"), so the
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
