using System;
using System.Threading.Tasks;

using Meadow.Logging;

namespace Halloween.Director.Shared
{
    /// <summary>
    /// Optional startup detail: titled banners and extra lines describing what each startup
    /// step did. On by default; set <c>App.StartupDetails: false</c> in app.config.yaml for a
    /// quiet production build. The work inside a section always runs either way.
    /// </summary>
    public static class LoggerExtensions
    {
        public const int SectionWidth = 71;

        /// <summary>Whether startup banners and detail lines are logged.</summary>
        public static bool StartupDetails { get; set; } = true;

        /// <summary>
        /// Runs <paramref name="writeBody"/> between a centered "====Title:Begin====" header and a
        /// matching "====Title:End====" footer of the same width. The banners are skipped when
        /// <see cref="StartupDetails"/> is off; the body always runs.
        /// </summary>
        public static void StartupSection(this Logger logger, string title, Action writeBody)
        {
            if (StartupDetails)
            {
                logger.Info(Banner($"{title}:Begin"));
            }

            writeBody();

            if (StartupDetails)
            {
                logger.Info(Banner($"{title}:End"));
            }
        }

        /// <summary>Like <see cref="StartupSection"/>, for a body that awaits.</summary>
        public static async Task StartupSectionAsync(this Logger logger, string title, Func<Task> writeBody)
        {
            if (StartupDetails)
            {
                logger.Info(Banner($"{title}:Begin"));
            }

            await writeBody();

            if (StartupDetails)
            {
                logger.Info(Banner($"{title}:End"));
            }
        }

        /// <summary>An extra line describing what a startup step did; skipped when <see cref="StartupDetails"/> is off.</summary>
        public static void StartupDetail(this Logger logger, string message)
        {
            if (StartupDetails)
            {
                logger.Info(message);
            }
        }

        private static string Banner(string text)
        {
            var padding = Math.Max(0, SectionWidth - text.Length);
            var left = padding / 2;
            return new string('=', left) + text + new string('=', padding - left);
        }
    }
}
