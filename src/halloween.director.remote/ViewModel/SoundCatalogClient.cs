using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Halloween.Director.Remote.ViewModel
{
    /// <summary>A sound on the monsterbox's SD card, as listed by its GET /sounds.</summary>
    public readonly record struct SoundEntry(int Number, string Name);

    /// <summary>
    /// Asks the monsterbox which sounds it has (GET /sounds, built from the Sounds section of its
    /// app.config.yaml) so the remote doesn't have to hard-code them. The monsterbox's MP3 module can't
    /// report file names, so that list is the only source of names.
    /// </summary>
    public static class SoundCatalogClient
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

        /// <summary>
        /// The sounds shown only if the monsterbox can't be reached: the list this app shipped with, which
        /// matches the monsterbox's default Sounds section (tools\Copy-SoundsToCard.ps1 prints the lines to paste here when the list changes).
        /// </summary>
        public static IReadOnlyList<SoundEntry> Defaults { get; } = new[]
        {
            new SoundEntry(0, "Werewolf"),
            new SoundEntry(1, "Laugh"),
            new SoundEntry(2, "Chains"),
            new SoundEntry(3, "Heartbeat"),
            new SoundEntry(4, "Dragon Growl"),
            new SoundEntry(5, "Door creek"),
            new SoundEntry(6, "Metal Hit"),
            new SoundEntry(7, "Raven"),
            new SoundEntry(8, "Creature 1"),
            new SoundEntry(9, "Creature 2"),
            new SoundEntry(10, "Creature 3"),
            new SoundEntry(11, "Creature 4"),
            new SoundEntry(12, "Creature 5"),
        };

        /// <summary>Names starting with this go in the CREATURES section; everything else is FX.</summary>
        public const string CreaturePrefix = "Creature";

        /// <summary>Returns the monsterbox's sounds, or null if it can't be reached or sends something unusable.</summary>
        public static async Task<IReadOnlyList<SoundEntry>?> FetchAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            try
            {
                using var http = new HttpClient { Timeout = RequestTimeout };
                var json = await http.GetStringAsync($"http://{host}:{port}/sounds", cancellationToken);
                return Parse(json);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>Parses [{"number":1,"name":"Werewolf"},...]; entries without a name or a 0-254 number are dropped. Null if no sounds.</summary>
        public static IReadOnlyList<SoundEntry>? Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var sounds = new List<SoundEntry>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("number", out var number) && number.TryGetInt32(out var n) && n >= 0 && n <= 254
                    && item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(name.GetString()))
                {
                    sounds.Add(new SoundEntry(n, name.GetString()!));
                }
            }

            return sounds.Count == 0 ? null : sounds.OrderBy(s => s.Number).ToList();
        }
    }
}
