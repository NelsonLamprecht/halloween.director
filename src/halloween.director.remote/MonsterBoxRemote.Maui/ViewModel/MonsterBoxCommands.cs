namespace MonsterBoxRemote.Maui.ViewModel
{
    // Centralizes the command-name strings shared between button
    // CommandParameter values in XAML and the switch statements in
    // MonsterBoxControllerViewModel, so a typo in either place is a compile
    // error instead of a silently-ignored default-case fallthrough.
    public static class MonsterBoxCommands
    {
        public const string Shake = "shake";
        public const string Werewolf = "werewolf";
        public const string Laugh = "laugh";
        public const string Chains = "chains";
        public const string Heartbeat = "heartbeat";
        public const string DragonGrowl = "dragongrowl";
        public const string DoorCreek = "doorcreek";
        public const string MetalHit = "metalhit";
        public const string Raven = "raven";
        public const string Creature1 = "creature1";
        public const string Creature2 = "creature2";
        public const string Creature3 = "creature3";
        public const string Creature4 = "creature4";
        public const string Creature5 = "creature5";
        public const string Up = "up";
        public const string Down = "down";
    }
}
