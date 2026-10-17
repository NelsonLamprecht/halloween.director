using halloween.director.remote.ViewModel;

namespace halloween.director.remote.Views
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

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await ViewModel.GetServers();
        }
    }
}
