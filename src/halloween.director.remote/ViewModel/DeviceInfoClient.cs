using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Halloween.Director.Remote.ViewModel
{
    /// <summary>What a prop reports on GET /info.</summary>
    public readonly record struct DeviceInfo(string Name, string Type, string Version);

    /// <summary>Asks a prop who it is (GET /info, served by the shared firmware library).</summary>
    public static class DeviceInfoClient
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

        /// <summary>The prop's info, or null if it doesn't answer or isn't one of ours.</summary>
        public static async Task<DeviceInfo?> FetchAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            try
            {
                using var http = new HttpClient { Timeout = RequestTimeout };
                var json = await http.GetStringAsync($"http://{host}:{port}/info", cancellationToken);
                return Parse(json);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>Parses {"name":"Monsterbox-v1","type":"monsterbox","version":"..."}; null without a name.</summary>
        public static DeviceInfo? Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(name.GetString()))
            {
                return null;
            }

            return new DeviceInfo(
                name.GetString()!,
                root.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String ? type.GetString()! : "unknown",
                root.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.String ? version.GetString()! : string.Empty);
        }
    }
}
