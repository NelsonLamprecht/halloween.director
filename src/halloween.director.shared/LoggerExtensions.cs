using System;

using Meadow.Logging;

namespace Halloween.Director.Shared
{
    public static class LoggerExtensions
    {
        public const int SectionWidth = 71;

        /// <summary>
        /// Logs a titled block at Information: a centered "====Title====" header, whatever
        /// <paramref name="writeBody"/> logs, then a "=====" footer of the same width.
        /// </summary>
        public static void InfoSection(this Logger logger, string title, Action writeBody)
        {
            logger.Info(SectionHeader(title));
            writeBody();
            logger.Info(new string('=', SectionWidth));
        }

        private static string SectionHeader(string title)
        {
            var padding = Math.Max(0, SectionWidth - title.Length);
            var left = padding / 2;
            return new string('=', left) + title + new string('=', padding - left);
        }
    }
}
