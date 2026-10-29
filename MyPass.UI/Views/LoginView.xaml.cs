using System.ComponentModel;
using System.Windows.Controls;
using MyPass.UI.ViewModels;

namespace MyPass.UI.Views;

/// <summary>
/// Interaction logic for LoginView.xaml.
/// Password fields are wired in code-behind — the WPF PasswordBox family intentionally
/// does not expose a bindable Password property (security design). Syncing is done by:
///   • UI → VM : subscribing to PasswordChanged and pushing the value into the ViewModel.
///   • VM → UI : watching PropertyChanged on the ViewModel and clearing the control when
///               the ViewModel resets the property to string.Empty (e.g. after login/logout).
/// This approach is explicitly permitted by the project coding rules.
/// </summary>
public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not LoginViewModel vm)
        {
            return;
        }

        // ── UI → VM ──────────────────────────────────────────────────────────
        // Push typed characters into the ViewModel on every keystroke.
        MasterPasswordBox.PasswordChanged  += (_, _) => vm.MasterPassword         = MasterPasswordBox.Password;
        ConfirmPasswordBox.PasswordChanged += (_, _) => vm.ConfirmMasterPassword  = ConfirmPasswordBox.Password;

        // ── VM → UI ──────────────────────────────────────────────────────────
        // Clear the PasswordBox controls whenever the ViewModel resets the backing
        // properties to empty (e.g. after successful login or switching database).
        vm.PropertyChanged += OnViewModelPropertyChanged;

        await vm.InitializeCommand.ExecuteAsync(null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LoginViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(LoginViewModel.MasterPassword) &&
            string.IsNullOrEmpty(vm.MasterPassword))
        {
            MasterPasswordBox.Password = string.Empty;
        }

        if (e.PropertyName == nameof(LoginViewModel.ConfirmMasterPassword) &&
            string.IsNullOrEmpty(vm.ConfirmMasterPassword))
        {
            ConfirmPasswordBox.Password = string.Empty;
        }
    }
}
