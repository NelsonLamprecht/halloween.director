using System;

namespace Halloween.Director.Scarecrow.Meadow.Controllers
{
    public class LiftOptions
    {
        /// <summary>
        /// Longest the lift stays up before it lowers itself. Override with
        /// <c>App.LiftMaxUpSeconds</c> in app.config.yaml; 0 means no limit.
        /// </summary>
        public const int DefaultMaxUpSeconds = 30;

        public TimeSpan MaxUp { get; set; } = TimeSpan.FromSeconds(DefaultMaxUpSeconds);
    }
}
