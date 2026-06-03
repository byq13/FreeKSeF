using System.Windows.Controls;
using FreeKSeF.App.ViewModels;

namespace FreeKSeF.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        // PasswordBox nie wspiera bindowania - token przekazujemy do ViewModelu recznie.
        TokenBox.PasswordChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
                vm.NowyToken = TokenBox.Password;
        };
    }
}
