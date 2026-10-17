using Meadow;

namespace halloween.director.scarecrow.meadow.Controllers.LEDController
{
    public interface ILEDDeviceController : IOnOrOffController
    {
        void SetColor(Color color);

        void Stop();

        void StartBlink(Color color);
    }
}
