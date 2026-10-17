using System;
using System.Threading.Tasks;

using Meadow.Logging;

namespace Halloween.Director.Shared
{
    public static class LoggerExtensions
    {
        public const int SectionWidth = 71;

        /// <summary>
        /// Wraps a block of work in a titled banner that is only logged at Trace: a centered "====Title====" header, whatever
        /// <paramref name="writeBody"/> logs, then a "=====" footer of the same width.
        /// </summary>
        public static void TraceSection(this Logger logger, string title, Action writeBody)
        {
            logger.Trace(SectionHeader(title));
            writeBody();
            logger.Trace(new string('=', SectionWidth));
        }

        /// <summary>Like <see cref="TraceSection"/>, for a body that awaits.</summary>
        public static async Task TraceSectionAsync(this Logger logger, string title, Func<Task> writeBody)
        {
            logger.Trace(SectionHeader(title));
            await writeBody();
            logger.Trace(new string('=', SectionWidth));
        }

        private static string SectionHeader(string title)
        {
            var padding = Math.Max(0, SectionWidth - title.Length);
            var left = padding / 2;
            return new string('=', left) + title + new string('=', padding - left);
        }
    }
}
