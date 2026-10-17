namespace halloween.director.monsterbox.meadow.Services.Watchdog
{
    internal interface IWatchdogService: IRunableService
    {
        // device will reset inSeconds if not petted
        void Enable(int inSeconds);

        void Pet(int inSeconds);
    }
}
