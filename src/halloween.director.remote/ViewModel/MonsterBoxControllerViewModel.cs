using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

using Meadow.Foundation.Web.Maple;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;

namespace Halloween.Director.Remote.ViewModel
{
    public class MonsterBoxControllerViewModel : BaseViewModel
    {
        // Buttons flash this state briefly (see ButtonStyle's IsCommandFailed
        // trigger in AppStyles.xaml) so a failed command is visible to whoever's
        // running the show, instead of looking identical to a successful one.
        private const int CommandFailedFlashDurationMs = 700;

        int _beginIterations = 25;
        public int BeginIterations
        {
            get => _beginIterations;
            set { _beginIterations = value; OnPropertyChanged(nameof(BeginIterations)); }
        }

        int _endIterations = 50;
        public int EndIterations
        {
            get => _endIterations;
            set { _endIterations = value; OnPropertyChanged(nameof(EndIterations)); }
        }

        int _beginDelay = 50;
        public int BeginDelay
        {
            get => _beginDelay;
            set { _beginDelay = value; OnPropertyChanged(nameof(BeginDelay)); }
        }

        int _endDelay = 75;
        public int EndDelay
        {
            get => _endDelay;
            set { _endDelay = value; OnPropertyChanged(nameof(EndDelay)); }
        }

        ServerModel? _monsterBoxDevice;
        public ServerModel? MonsterBoxDevice
        {
            get => _monsterBoxDevice;
            set
            {
                _monsterBoxDevice = value;
                OnPropertyChanged(nameof(MonsterBoxDevice));

                // the sound buttons come from whichever monsterbox is selected
                _ = LoadSoundsAsync(value?.IpAddress);
            }
        }

        // Sound buttons, built from the monsterbox's GET /sounds (the SoundCatalog in its
        // app.config.yaml). Until it answers, or if it can't be reached, the built-in list is shown.
        public ObservableCollection<SoundItem> FxSounds { get; } = new ObservableCollection<SoundItem>();

        public ObservableCollection<SoundItem> CreatureSounds { get; } = new ObservableCollection<SoundItem>();

        ServerModel? _scareCrowDevice;
        public ServerModel? ScareCrowDevice
        {
            get => _scareCrowDevice;
            set { _scareCrowDevice = value; OnPropertyChanged(nameof(ScareCrowDevice)); }
        }

        bool _isCommandFailed;
        public bool IsCommandFailed
        {
            get => _isCommandFailed;
            private set { _isCommandFailed = value; OnPropertyChanged(nameof(IsCommandFailed)); }
        }

        string? _manualMonsterBoxAddress;
        public string? ManualMonsterBoxAddress
        {
            get => _manualMonsterBoxAddress;
            set { _manualMonsterBoxAddress = value; OnPropertyChanged(nameof(ManualMonsterBoxAddress)); }
        }

        string? _manualScarecrowAddress;
        public string? ManualScarecrowAddress
        {
            get => _manualScarecrowAddress;
            set { _manualScarecrowAddress = value; OnPropertyChanged(nameof(ManualScarecrowAddress)); }
        }

        public Command SendMonsterBoxCommand { set; get; }

        public Command SendScarecrowCommand { set; get; }

        public Command AddManualMonsterBoxDeviceCommand { get; }

        public Command AddManualScarecrowDeviceCommand { get; }

        public MonsterBoxControllerViewModel() : base()
        {
            IsBusy = false;
            SendMonsterBoxCommand = new Command(async (obj) => await SendMeadowCommand(MonsterBoxDevice?.IpAddress, obj as string));
            SendScarecrowCommand = new Command(async (obj) => await SendMeadowCommand(ScareCrowDevice?.IpAddress, obj as string));

            ApplySounds(SoundCatalogClient.Defaults);

            AddManualMonsterBoxDeviceCommand = new Command(() =>
            {
                var device = AddOrGetManualDevice(ManualMonsterBoxAddress);
                if (device != null)
                {
                    MonsterBoxDevice = device;
                    ManualMonsterBoxAddress = string.Empty;
                }
            });
            AddManualScarecrowDeviceCommand = new Command(() =>
            {
                var device = AddOrGetManualDevice(ManualScarecrowAddress);
                if (device != null)
                {
                    ScareCrowDevice = device;
                    ManualScarecrowAddress = string.Empty;
                }
            });
        }

        protected override bool ShouldPreserveHostListEntry(ServerModel server)
        {
            return base.ShouldPreserveHostListEntry(server)
                || string.Equals(server.IpAddress, MonsterBoxDevice?.IpAddress, StringComparison.OrdinalIgnoreCase)
                || string.Equals(server.IpAddress, ScareCrowDevice?.IpAddress, StringComparison.OrdinalIgnoreCase);
        }

        async Task SendMeadowCommand(string? hostAddress, string? command)
        {
            if (string.IsNullOrEmpty(command))
            {
                return;
            }

            await RunCommandAsync(hostAddress, async () =>
            {
                switch (command.ToLower())
                {
                    case MonsterBoxCommands.Shake:
                        {
                            var query = new Dictionary<string, string>()
                            {
                                ["bi"] = BeginIterations.ToString(),
                                ["ei"] = EndIterations.ToString(),
                                ["bd"] = BeginDelay.ToString(),
                                ["ed"] = EndDelay.ToString(),
                            };
                            var complexCommand = RequestUriUtil.GetUriWithQueryString(command, query).ToLower();
                            return await PostHttpDataWithCommand(hostAddress!, complexCommand);
                        }
                    default:
                        {
                            return await PostHttpDataWithCommand(hostAddress!, command);
                        }
                }
            });
        }

        async Task SendSoundAsync(string? hostAddress, int fileNumber)
        {
            await RunCommandAsync(hostAddress, async () =>
            {
                var complexCommand = RequestUriUtil.GetUriWithQueryString("sound", BuildSoundQuery(fileNumber)).ToLower();
                return await PostHttpDataWithCommand(hostAddress!, complexCommand);
            });
        }

        // Shared by every command: ignores presses while one is in flight, shows the busy overlay,
        // and flashes the buttons if the monsterbox/scarecrow didn't accept it.
        private async Task RunCommandAsync(string? hostAddress, Func<Task<bool>> send)
        {
            if (IsBusy || string.IsNullOrEmpty(hostAddress))
            {
                return;
            }

            IsBusy = true;
            bool succeeded;

            try
            {
                succeeded = await send();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                succeeded = false;
            }
            finally
            {
                // Reset before the failure flash below, so the flash isn't
                // fighting the IsBusy trigger for the button's BackgroundColor.
                IsBusy = false;
            }

            if (!succeeded)
            {
                await SignalCommandFailedAsync();
            }
        }

        // Asks the selected monsterbox for its sounds. If it can't be reached the buttons stay as they are.
        private async Task LoadSoundsAsync(string? hostAddress)
        {
            if (string.IsNullOrEmpty(hostAddress))
            {
                return;
            }

            var sounds = await SoundCatalogClient.FetchAsync(hostAddress, ServerPort);
            if (sounds == null)
            {
                Debug.WriteLine($"No sound list from {hostAddress}; keeping the current buttons.");
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() => ApplySounds(sounds));
        }

        // call on the main thread
        private void ApplySounds(IReadOnlyList<SoundEntry> sounds)
        {
            FxSounds.Clear();
            CreatureSounds.Clear();

            foreach (var sound in sounds)
            {
                var number = sound.Number;
                var item = new SoundItem(sound.Name, number, new Command(async () => await SendSoundAsync(MonsterBoxDevice?.IpAddress, number)));

                if (sound.Name.StartsWith(SoundCatalogClient.CreaturePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    CreatureSounds.Add(item);
                }
                else
                {
                    FxSounds.Add(item);
                }
            }
        }

        private async Task SignalCommandFailedAsync()
        {
            try
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            }
            catch (FeatureNotSupportedException)
            {
                // No haptic hardware/support on this platform (e.g. Windows) -
                // the button's visual flash below still signals the failure.
            }

            IsCommandFailed = true;
            await Task.Delay(CommandFailedFlashDurationMs);
            await MainThread.InvokeOnMainThreadAsync(() => IsCommandFailed = false);
        }

        private static Dictionary<string, string> BuildSoundQuery(int fileNumber)
        {
            return new Dictionary<string, string>()
            {
                ["filenumber"] = fileNumber.ToString()
            };
        }

        private async Task<bool> PostHttpDataWithCommand(string hostAddress, string command)
        {
            bool response;

            response = await client.PostAsync(hostAddress, ServerPort, command, string.Empty);

            return response;
        }
    }
}
