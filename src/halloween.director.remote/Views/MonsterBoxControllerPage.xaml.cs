using Halloween.Director.Remote.ViewModel;

namespace Halloween.Director.Remote.Views
{
    public partial class MonsterBoxControllerPage : ContentPage
    {
        private MonsterBoxControllerViewModel ViewModel { get; }

        public MonsterBoxControllerPage(MonsterBoxControllerViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            BindingContext = ViewModel;
        }
    }
}
