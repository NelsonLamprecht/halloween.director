using Meadow;

namespace meadow_scarecrow.Controllers.LEDController
{
    public interface ILEDDeviceController : IOnOrOffController
    {
        void SetColor(Color color);

        void Stop();

        void StartBlink(Color color);
    }
}
