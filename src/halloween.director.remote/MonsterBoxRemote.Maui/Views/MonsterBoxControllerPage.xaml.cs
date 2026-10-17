using MonsterBoxRemote.Maui.ViewModel;

namespace MonsterBoxRemote.Maui.Views
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
