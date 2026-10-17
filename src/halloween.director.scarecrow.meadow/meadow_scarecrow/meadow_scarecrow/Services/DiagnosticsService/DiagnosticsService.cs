using Meadow;
using Meadow.Hardware;
using Meadow.Logging;

namespace meadow_scarecrow.Services.DiagnosticsService
{
    public class DiagnosticsService : BaseService
    {
        private const string outputFooter = "=======================================================================";

        private readonly IMeadowDevice device;

        public DiagnosticsService(Logger logger, IMeadowDevice device) : base(logger)
        {
            this.device = device;
        }

        public void OutputMeadowOSInfo()
        {
            Logger.Info($"=========================OutputMeadowOSInfo============================");
            Logger.Info($"OS version: {MeadowOS.SystemInformation.OSVersion}");
            Logger.Info($"Runtime version: {MeadowOS.SystemInformation.RuntimeVersion}");
            Logger.Info($"Build date: {MeadowOS.SystemInformation.OSBuildDate}");
            Logger.Info(outputFooter);
        }

        public void OutputDeviceInfo()
        {
            Logger.Info($"=========================OutputDeviceInfo==============================");
            Logger.Info($"Device name: {device.Information.DeviceName}");
            Logger.Info($"Processor serial number: {device.Information.ProcessorSerialNumber}");
            Logger.Info($"Processor ID: {device.Information.UniqueID}");
            Logger.Info($"Model: {device.Information.Model}");
            Logger.Info($"Processor type: {device.Information.ProcessorType}");
            Logger.Info($"Product: {device.Information.Model}");
            Logger.Info($"Coprocessor type: {device.Information.CoprocessorType}");
            Logger.Info($"Coprocessor firmware version: {device.Information.CoprocessorOSVersion}");
            Logger.Info(outputFooter);
        }

        public void OutputNtpInfo()
        {
            Logger.Info($"=========================OutputMeadowOSInfo============================");
            Logger.Info($"NTP Client Enabled: {device.PlatformOS.NtpClient.Enabled}");
            Logger.Info(outputFooter);
        }

        public void OutputDeviceWifiInfo(INetworkAdapter sender)
        {
            Logger.Info($"====================OutputDeviceWifiInfo===========================");
            if (sender is IWiFiNetworkAdapter wifi)
            {
                Logger.Info($"Ssid: {wifi.Ssid}");
            }

            Logger.Info($"MacAddress: {sender.MacAddress}");
            Logger.Info($"IpAddress: {sender.IpAddress}");
            Logger.Info(outputFooter);
            }
        }
    }
