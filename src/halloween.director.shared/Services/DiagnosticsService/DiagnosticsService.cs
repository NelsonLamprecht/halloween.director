using Meadow;
using Meadow.Hardware;
using Meadow.Logging;

namespace Halloween.Director.Shared.Services.DiagnosticsService
{
    public class DiagnosticsService : BaseService
    {
        private readonly IMeadowDevice device;

        public DiagnosticsService(Logger logger, IMeadowDevice device) : base(logger)
        {
            this.device = device;
        }

        public void OutputMeadowOSInfo()
        {
            Logger.StartupSection("OutputMeadowOSInfo", () =>
            {
                Logger.Info($"OS version: {MeadowOS.SystemInformation.OSVersion}");
                Logger.Info($"Runtime version: {MeadowOS.SystemInformation.RuntimeVersion}");
                Logger.Info($"Build date: {MeadowOS.SystemInformation.OSBuildDate}");
            });
        }

        public void OutputDeviceInfo()
        {
            Logger.StartupSection("OutputDeviceInfo", () =>
            {
                Logger.Info($"Device name: {device.Information.DeviceName}");
                Logger.Info($"Processor serial number: {device.Information.ProcessorSerialNumber}");
                Logger.Info($"Processor ID: {device.Information.UniqueID}");
                Logger.Info($"Model: {device.Information.Model}");
                Logger.Info($"Processor type: {device.Information.ProcessorType}");
                Logger.Info($"Coprocessor type: {device.Information.CoprocessorType}");
                Logger.Info($"Coprocessor firmware version: {device.Information.CoprocessorOSVersion}");
            });
        }

        public void OutputNtpInfo()
        {
            Logger.StartupSection("OutputNtpInfo", () =>
            {
                Logger.Info($"NTP Client Enabled: {device.PlatformOS.NtpClient.Enabled}");
            });
        }

        public void OutputDeviceWifiInfo(INetworkAdapter sender)
        {
            Logger.StartupSection("OutputDeviceWifiInfo", () =>
            {
                if (sender is IWiFiNetworkAdapter wifi)
                {
                    Logger.Info($"Ssid: {wifi.Ssid}");
                }

                Logger.Info($"MacAddress: {sender.MacAddress}");
                Logger.Info($"IpAddress: {sender.IpAddress}");
            });
        }
    }
}
