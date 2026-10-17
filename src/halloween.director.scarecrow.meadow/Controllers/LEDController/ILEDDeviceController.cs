using Meadow;

namespace Halloween.Director.Scarecrow.Meadow.Controllers.LEDController
{
    public interface ILEDDeviceController : IOnOrOffController
    {
        void SetColor(Color color);

        void Stop();

        void StartBlink(Color color);
    }
}
