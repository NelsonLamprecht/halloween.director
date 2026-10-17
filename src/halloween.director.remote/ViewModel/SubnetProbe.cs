using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Halloween.Director.Remote.ViewModel
{
    /// <summary>
    /// Finds props without the UDP advertisement: tries a TCP connection to the Maple port on every
    /// address of this device's own /24 subnet. Some tablets (a Lenovo running ZUI, for one) drop the
    /// broadcast packets even while the app holds the multicast lock; ordinary TCP always gets through.
    /// </summary>
    public static class SubnetProbe
    {
        private const int ConnectTimeoutMs = 600;
        private const int MaxParallel = 64;

        /// <summary>Addresses on the local /24 that accept a connection on <paramref name="port"/>.</summary>
        public static async Task<IReadOnlyList<string>> FindOpenAsync(int port, CancellationToken cancellationToken = default)
        {
            var local = GetLocalAddress();
            if (local == null)
            {
                return Array.Empty<string>();
            }

            var prefix = local.GetAddressBytes();
            var found = new List<string>();
            using var gate = new SemaphoreSlim(MaxParallel);

            var probes = Enumerable.Range(1, 254)
                .Where(host => host != prefix[3])
                .Select(async host =>
                {
                    var address = new IPAddress(new[] { prefix[0], prefix[1], prefix[2], (byte)host });
                    await gate.WaitAsync(cancellationToken);
                    try
                    {
                        if (await CanConnectAsync(address, port, cancellationToken))
                        {
                            lock (found)
                            {
                                found.Add(address.ToString());
                            }
                        }
                    }
                    finally
                    {
                        gate.Release();
                    }
                })
                .ToList();

            try
            {
                await Task.WhenAll(probes);
            }
            catch (OperationCanceledException)
            {
            }

            return found.OrderBy(a => a, StringComparer.Ordinal).ToList();
        }

        private static async Task<bool> CanConnectAsync(IPAddress address, int port, CancellationToken cancellationToken)
        {
            try
            {
                using var client = new TcpClient();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(ConnectTimeoutMs);
                await client.ConnectAsync(address, port, timeout.Token);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // this device's WiFi IPv4 address (first up, non-loopback, private-range one)
        private static IPAddress? GetLocalAddress()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Select(a => a.Address)
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && IsPrivate(a));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Couldn't read the local address: {ex.Message}");
                return null;
            }
        }

        private static bool IsPrivate(IPAddress address)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
        }
    }
}
