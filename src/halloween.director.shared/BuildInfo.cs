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
            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var version = attribute?.InformationalVersion;
            if (string.IsNullOrEmpty(version))
            {
                return "unknown version";
            }

            // the SDK can append "+<source revision>"; the build number already says it
            var plus = version.IndexOf('+');
            return plus < 0 ? version : version.Substring(0, plus);
        }
    }
}
