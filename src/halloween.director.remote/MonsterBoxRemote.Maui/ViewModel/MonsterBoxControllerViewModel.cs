using Meadow.Foundation.Web.Maple;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;

namespace MonsterBoxRemote.Maui.ViewModel
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
            set { _monsterBoxDevice = value; OnPropertyChanged(nameof(MonsterBoxDevice)); }
        }

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
            if (IsBusy || string.IsNullOrEmpty(hostAddress) || string.IsNullOrEmpty(command))
            {
                return;
            }

            IsBusy = true;
            bool succeeded;

            try
            {
                bool response = false;
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
                            response = await PostHttpDataWithCommand(hostAddress,complexCommand);
                            break;
                        }
                    case MonsterBoxCommands.Werewolf:
                    case MonsterBoxCommands.Laugh:
                    case MonsterBoxCommands.Chains:
                    case MonsterBoxCommands.Heartbeat:
                    case MonsterBoxCommands.DragonGrowl:
                    case MonsterBoxCommands.DoorCreek:
                    case MonsterBoxCommands.MetalHit:
                    case MonsterBoxCommands.Raven:
                    case MonsterBoxCommands.Creature1:
                    case MonsterBoxCommands.Creature2:
                    case MonsterBoxCommands.Creature3:
                    case MonsterBoxCommands.Creature4:
                    case MonsterBoxCommands.Creature5:
                        {
                            Dictionary<string, string>? query = ResolveSoundParameters(command);
                            if (query != null)
                            {
                                var complexCommand = RequestUriUtil.GetUriWithQueryString("sound", query).ToLower();
                                response = await PostHttpDataWithCommand(hostAddress,complexCommand);
                            }
                            else
                            {
                                Debug.WriteLine("Unknown sound file.");
                            }
                            break;
                        }
                    default:
                        {
                            response = await PostHttpDataWithCommand(hostAddress,command);
                            break;
                        }
                }

                succeeded = response;
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

        private static Dictionary<string, string>? ResolveSoundParameters(string command)
        {
            switch (command)
            {
                case MonsterBoxCommands.Werewolf:
                    {
                        return BuildSoundQuery(1);
                    }
                case MonsterBoxCommands.Laugh:
                    {
                        return BuildSoundQuery(2);
                    }
                case MonsterBoxCommands.Chains:
                    {
                        return BuildSoundQuery(3);
                    }
                case MonsterBoxCommands.Heartbeat:
                    {
                        return BuildSoundQuery(4);
                    }
                case MonsterBoxCommands.DragonGrowl:
                    {
                        return BuildSoundQuery(5);
                    }
                case MonsterBoxCommands.DoorCreek:
                    {
                        return BuildSoundQuery(6);
                    }
                case MonsterBoxCommands.Creature1:
                    {
                        return BuildSoundQuery(7);
                    }
                case MonsterBoxCommands.Creature2:
                    {
                        return BuildSoundQuery(8);
                    }
                case MonsterBoxCommands.Creature3:
                    {
                        return BuildSoundQuery(9);
                    }
                case MonsterBoxCommands.Creature4:
                    {
                        return BuildSoundQuery(10);
                    }
                case MonsterBoxCommands.Creature5:
                    {
                        return BuildSoundQuery(11);
                    }
                case MonsterBoxCommands.MetalHit:
                    {
                        return BuildSoundQuery(12);
                    }
                case MonsterBoxCommands.Raven:
                    {
                        return BuildSoundQuery(13);
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        private async Task<bool> PostHttpDataWithCommand(string hostAddress, string command)
        {
            bool response;

            response = await client.PostAsync(hostAddress, ServerPort, command, string.Empty);

            return response;
        }
    }
}
