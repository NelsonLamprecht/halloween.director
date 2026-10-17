using System;

using MonsterBoxRemote.Maui.ViewModel;

namespace MonsterBoxRemote.Maui.Views
{
    public partial class MonsterBoxOptionsPage : ContentPage
    {
        private MonsterBoxControllerViewModel ViewModel { get; }

        public MonsterBoxOptionsPage(MonsterBoxControllerViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            BindingContext = ViewModel;
        }

        // Let pressing Enter/Done on the keyboard submit the manual device
        // address, same as tapping Add - Entry has no Command/CommandParameter
        // of its own to bind Completed to directly.
        private void ManualMonsterBoxEntry_Completed(object? sender, EventArgs e)
        {
            if (ViewModel.AddManualMonsterBoxDeviceCommand.CanExecute(null))
            {
                ViewModel.AddManualMonsterBoxDeviceCommand.Execute(null);
            }
        }

        private void ManualScarecrowEntry_Completed(object? sender, EventArgs e)
        {
            if (ViewModel.AddManualScarecrowDeviceCommand.CanExecute(null))
            {
                ViewModel.AddManualScarecrowDeviceCommand.Execute(null);
            }
        }
    }
}
