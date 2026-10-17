using System;

namespace halloween.director.monsterbox.meadow.Controllers
{
    public class ShakeConfiguration
    {
        // System.Random isn't thread-safe and this instance is shared by every request
        private static readonly Random _random = new Random();
        private static readonly object _randomLock = new object();

        public int BeginIterations { get; set; } = 25;

        public int EndIterations { get; set; } = 50;

        public int GetIterations()
        {
            lock (_randomLock)
            {
                return _random.Next(BeginIterations, EndIterations);
            }
        }

        public int BeginDelay { get; set; } = 50;

        public int EndDelay { get; set; } = 75;

        public int GetDelay()
        {
            lock (_randomLock)
            {
                return _random.Next(BeginDelay, EndDelay);
            }
        }

        // Limits keep a bad query string from wedging a shake: a negative delay of -1
        // is Task.Delay's "wait forever" (relay stuck on, shake gate never released),
        // and Random.Next throws when min > max.
        public const int MaxIterations = 50;
        public const int MaxDelayMs = 1000;

        public bool TryValidate(out string error)
        {
            if (BeginIterations < 0 || EndIterations > MaxIterations || BeginIterations > EndIterations)
            {
                error = $"iterations must satisfy 0 <= bi <= ei <= {MaxIterations}.";
                return false;
            }

            if (BeginDelay < 1 || EndDelay > MaxDelayMs || BeginDelay > EndDelay)
            {
                error = $"delays must satisfy 1 <= bd <= ed <= {MaxDelayMs} ms.";
                return false;
            }

            error = null;
            return true;
        }
    }
}