using System;
using System.Collections.Generic;
using System.Text;

using Meadow.Logging;

namespace Halloween.Director.Monsterbox.Meadow.Controllers
{
    /// <summary>
    /// The sounds on the MP3 module's SD card, as named in app.config.yaml's "Sounds" section
    /// (one "Name: number" line each). The module can only report counts, never file names, so
    /// this list is what the remote asks for (GET /sounds) to build its buttons. A number is the
    /// value sent as /sound?filenumber=, i.e. the file copied to the card at position number + 1.
    /// </summary>
    public class SoundCatalog
    {
        private const string SettingsPrefix = "Sounds.";

        private readonly List<KeyValuePair<int, string>> _sounds;

        private SoundCatalog(List<KeyValuePair<int, string>> sounds)
        {
            _sounds = sounds;
        }

        public int Count => _sounds.Count;

        /// <summary>Reads every "Sounds.&lt;Name&gt;" setting; entries that aren't a valid file number are logged and skipped.</summary>
        public static SoundCatalog FromSettings(IDictionary<string, string> settings, Logger logger)
        {
            var sounds = new List<KeyValuePair<int, string>>();
            var used = new Dictionary<int, string>();

            foreach (var setting in settings)
            {
                if (!setting.Key.StartsWith(SettingsPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var name = setting.Key.Substring(SettingsPrefix.Length);

                if (!int.TryParse(setting.Value, out var number) || number < 0 || number > MP3Controller.MaxFileNumber)
                {
                    logger.Warn($"Sound '{name}' has an invalid file number '{setting.Value}' (0-{MP3Controller.MaxFileNumber}); skipping.");
                    continue;
                }

                if (used.TryGetValue(number, out var existing))
                {
                    logger.Warn($"Sound '{name}' reuses file number {number} already used by '{existing}'; skipping.");
                    continue;
                }

                used[number] = name;
                sounds.Add(new KeyValuePair<int, string>(number, name));
            }

            sounds.Sort((a, b) => a.Key.CompareTo(b.Key));
            return new SoundCatalog(sounds);
        }

        /// <summary>[{"number":1,"name":"Werewolf"},...] ordered by number.</summary>
        public string ToJson()
        {
            var json = new StringBuilder("[");

            for (var i = 0; i < _sounds.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append("{\"number\":").Append(_sounds[i].Key).Append(",\"name\":\"").Append(Escape(_sounds[i].Value)).Append("\"}");
            }

            return json.Append(']').ToString();
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
