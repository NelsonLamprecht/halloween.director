using System.Collections.ObjectModel;
using System.Collections.Specialized;

using Halloween.Director.Remote.ViewModel;

namespace Halloween.Director.Remote.Views
{
    /// <summary>
    /// A plain Grid of sound buttons, five to a row, rebuilt whenever <see cref="Sounds"/> changes. The
    /// sounds come from the monsterbox's GET /sounds, so how many there are isn't known when the page is written.
    /// (The same Grid-of-Buttons structure the page always used, instead of a FlexLayout with a bound
    /// ItemTemplate, which drew no buttons on Windows.)
    /// </summary>
    public class SoundGrid : Grid
    {
        public const int Columns = 5;

        public static readonly BindableProperty SoundsProperty = BindableProperty.Create(
            nameof(Sounds), typeof(ObservableCollection<SoundItem>), typeof(SoundGrid), null,
            propertyChanged: (bindable, oldValue, newValue) => ((SoundGrid)bindable).OnSoundsChanged(oldValue as ObservableCollection<SoundItem>, newValue as ObservableCollection<SoundItem>));

        public ObservableCollection<SoundItem>? Sounds
        {
            get => (ObservableCollection<SoundItem>?)GetValue(SoundsProperty);
            set => SetValue(SoundsProperty, value);
        }

        /// <summary>Style applied to every button (for example the FX or the creature style).</summary>
        public Style? ButtonStyle { get; set; }

        public SoundGrid()
        {
            for (int column = 0; column < Columns; column++)
            {
                ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }
        }

        private void OnSoundsChanged(ObservableCollection<SoundItem>? oldValue, ObservableCollection<SoundItem>? newValue)
        {
            if (oldValue != null)
            {
                oldValue.CollectionChanged -= OnCollectionChanged;
            }

            if (newValue != null)
            {
                newValue.CollectionChanged += OnCollectionChanged;
            }

            Rebuild();
        }

        private bool _rebuildQueued;

        // ApplySounds adds the sounds one at a time; queue a single rebuild for the whole batch instead of
        // one per Add (which rebuilt every button each time and made the pane slow to appear).
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_rebuildQueued)
            {
                return;
            }

            _rebuildQueued = true;
            Dispatcher.Dispatch(() =>
            {
                _rebuildQueued = false;
                Rebuild();
            });
        }

        // call on the main thread (the view model raises collection changes there)
        internal void Rebuild()
        {
            Children.Clear();
            RowDefinitions.Clear();

            var sounds = Sounds;
            if (sounds == null)
            {
                return;
            }

            for (int index = 0; index < sounds.Count; index++)
            {
                var sound = sounds[index];
                int row = index / Columns;

                if (row >= RowDefinitions.Count)
                {
                    RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                }

                var button = new Button
                {
                    Text = sound.Name,
                    Command = sound.PlayCommand,
                    HorizontalOptions = LayoutOptions.Fill,
                };

                if (ButtonStyle != null)
                {
                    button.Style = ButtonStyle;
                }

                button.SetValue(RowProperty, row);
                button.SetValue(ColumnProperty, index % Columns);
                Children.Add(button);
            }
        }
    }
}
