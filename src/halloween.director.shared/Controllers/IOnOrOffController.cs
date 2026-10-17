namespace Halloween.Director.Shared.Controllers
{
    /// <summary>
    /// Anything that is switched on and off: a relay, an LED, ... "On" means active
    /// (energized, lit), whatever the electrical polarity behind it is.
    /// </summary>
    public interface IOnOrOffController
    {
        void TurnOn();

        void TurnOff();
    }
}
