using Microsoft.Maui.ApplicationModel;

namespace Halloween.Director.Remote
{
    /// <summary>The version and build number stamped by src\Directory.Build.props, for display.</summary>
    public static class AppVersion
    {
        public static string Text => $"v{AppInfo.Current.VersionString} (build {AppInfo.Current.BuildString})";
    }
}
