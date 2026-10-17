using System.Windows.Input;

namespace Halloween.Director.Remote.ViewModel
{
    /// <summary>One sound button: its label, its file number on the monsterbox's SD card, and what pressing it does.</summary>
    public class SoundItem
    {
        public SoundItem(string name, int number, ICommand playCommand)
        {
            Name = name;
            Number = number;
            PlayCommand = playCommand;
        }

        public string Name { get; }

        public int Number { get; }

        public ICommand PlayCommand { get; }
    }
}
