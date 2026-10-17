using System;

namespace Halloween.Director.Remote.ViewModel
{
    /// <summary>
    /// Holds Android's WiFi multicast lock while a scan runs. Without it the WiFi hardware drops the
    /// UDP broadcast the props advertise with, so discovery finds nothing (typing an address still works).
    /// Does nothing on other platforms.
    /// </summary>
    public sealed class MulticastLockScope : IDisposable
    {
#if ANDROID
        private readonly Android.Net.Wifi.WifiManager.MulticastLock? _lock;

        public MulticastLockScope()
        {
            try
            {
                var wifi = (Android.Net.Wifi.WifiManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.WifiService);
                _lock = wifi?.CreateMulticastLock("halloween.director.discovery");
                _lock?.SetReferenceCounted(false);
                _lock?.Acquire();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Couldn't acquire the multicast lock: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                if (_lock?.IsHeld == true)
                {
                    _lock.Release();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Couldn't release the multicast lock: {ex.Message}");
            }
        }
#else
        public void Dispose()
        {
        }
#endif
    }
}
