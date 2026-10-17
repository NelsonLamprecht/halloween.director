using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Meadow.Foundation.Web.Maple;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Halloween.Director.Remote.ViewModel
{
    public class BaseViewModel : INotifyPropertyChanged, IDisposable
    {
        // Requests (shake can run several seconds) get the longer timeout.
        private const int MapleClientListenTimeout = 10000;

        // How long a scan listens for advertisements. The props broadcast every 2 seconds, so this hears
        // each of them a couple of times.
        private const int ScanListenTimeout = 5000;

        // Addresses added via AddOrGetManualDevice, so GetServers() can leave
        // them in HostList across rescans instead of wiping them out along with
        // the auto-discovered entries.
        private readonly HashSet<string> _manualDeviceAddresses = new(StringComparer.OrdinalIgnoreCase);

        public MapleClient client { get; private set; }

        // Scans use their own client: the listen timeout also bounds a MapleClient's HTTP requests, and
        // shortening the scan must not shorten how long a /shake may take.
        private MapleClient scanner { get; }

        int _serverPort;
        public int ServerPort
        {
            get => _serverPort;
            set { _serverPort = value; OnPropertyChanged(nameof(ServerPort)); }
        }

        bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(nameof(IsBusy)); }
        }

        public ObservableCollection<ServerModel> HostList { get; set; }

        public BaseViewModel()
        {
            HostList = new ObservableCollection<ServerModel>();

            //HostList.Add(new ServerModel() { Name="Meadow (192.168.1.73)", IpAddress="192.168.1.73" });
            //HostList.Add(new ServerModel() { Name = "Meadow (192.168.1.74)", IpAddress = "192.168.1.74" });

            ServerPort = 5417;

            // MapleClient.ListenTimeout has a protected setter as of Maple.Client 0.96.0,
            // so the timeout must be supplied via the constructor instead of an initializer.
            client = new MapleClient(listenTimeout: TimeSpan.FromMilliseconds(MapleClientListenTimeout));
            scanner = new MapleClient(listenTimeout: TimeSpan.FromMilliseconds(ScanListenTimeout));
            scanner.Servers.CollectionChanged += ServersCollectionChanged;
        }

        // Adds (or reuses, if already present) a ServerModel for a manually-typed
        // IP address to HostList, so callers can assign it straight to a specific
        // device slot (MonsterBoxDevice/ScareCrowDevice) instead of just dropping
        // it in the shared pool and making the user re-select it from a Picker.
        // Returns null for empty/malformed input.
        protected ServerModel? AddOrGetManualDevice(string? rawAddress)
        {
            var address = rawAddress?.Trim();
            if (string.IsNullOrEmpty(address) || Uri.CheckHostName(address) == UriHostNameType.Unknown)
            {
                Debug.WriteLine($"Ignoring invalid manual device address: '{address}'");
                return null;
            }

            var existing = HostList.FirstOrDefault(server => string.Equals(server.IpAddress, address, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _manualDeviceAddresses.Add(address);
                return existing;
            }

            var device = new ServerModel { Name = $"Manual ({address})", IpAddress = address };
            HostList.Add(device);
            _manualDeviceAddresses.Add(address);
            return device;
        }

        // Whether a HostList entry should survive GetServers()'s per-scan cleanup.
        // Base case: manually-typed devices. MonsterBoxControllerViewModel extends
        // this to also cover whatever's currently assigned to a device slot.
        protected virtual bool ShouldPreserveHostListEntry(ServerModel server)
        {
            return _manualDeviceAddresses.Contains(server.IpAddress);
        }

        public Command ScanForServersCommand => new Command(async () => await GetServers());

        public async Task GetServers()
        {
            if (IsBusy)
            {
                return;
            }
            IsBusy = true;

            try
            {
                // Only entries nobody still cares about are cleared, so manually-
                // added devices and whatever's currently assigned to a device slot
                // (MonsterBoxDevice/ScareCrowDevice - see the override in
                // MonsterBoxControllerViewModel) survive repeat scans - e.g. every
                // time the Options tab reappears - instead of a fresh batch of
                // announcements wiping out a selection made from an earlier scan.
                for (int i = HostList.Count - 1; i >= 0; i--)
                {
                    if (!ShouldPreserveHostListEntry(HostList[i]))
                    {
                        HostList.RemoveAt(i);
                    }
                }

                // MapleClient only reports a server it hasn't already got in Servers, so forget the earlier
                // scan's or a rescan would never put those devices back in HostList.
                scanner.Servers.Clear();

                using (new MulticastLockScope())
                {
                    await scanner.StartScanningForAdvertisingServers();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            }
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion

        private void ServersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when e.NewItems != null:
                    foreach (ServerModel server in e.NewItems)
                    {
                        Debug.WriteLine($"'{server.Name}' @ ip:[{server.IpAddress}]");

                        var discovered = new ServerModel { Name = $"{server.Name} ({server.IpAddress})", IpAddress = server.IpAddress };

                        // MapleClient raises this from its UDP listener thread, not the UI
                        // thread. Android/iOS tolerate an off-thread ObservableCollection
                        // mutation; WinUI3 hard-crashes on it (native 0xc000027b in
                        // Microsoft.UI.Xaml.dll), so this must be marshaled back to the
                        // main thread before touching HostList - the duplicate check has
                        // to happen there too, atomically with the add, rather than
                        // reading HostList from this thread.
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (!HostList.Any(h => string.Equals(h.IpAddress, discovered.IpAddress, StringComparison.OrdinalIgnoreCase)))
                            {
                                HostList.Add(discovered);
                            }
                        });
                    }
                    break;
            }
        }

        public void Dispose()
        {
            scanner.Servers.CollectionChanged -= ServersCollectionChanged;
            GC.SuppressFinalize(this);
        }
    }
}
