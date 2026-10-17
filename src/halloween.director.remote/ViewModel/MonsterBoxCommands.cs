namespace Halloween.Director.Remote.ViewModel
{
    // Centralizes the command-name strings shared between button
    // CommandParameter values in XAML and the switch statements in
    // MonsterBoxControllerViewModel, so a typo in either place is a compile
    // error instead of a silently-ignored default-case fallthrough.
    public static class MonsterBoxCommands
    {
        public const string Shake = "shake";
        public const string Up = "up";
        public const string Down = "down";
    }
}
