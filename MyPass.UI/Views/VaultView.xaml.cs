using System.ComponentModel;
using System.Windows.Controls;
using MyPass.UI.ViewModels;

namespace MyPass.UI.Views;

/// <summary>
/// Interaction logic for VaultView.xaml.
/// The masked PasswordBox for the vault entry password field is wired here in code-behind
/// because WPF's PasswordBox family intentionally withholds a bindable Password property.
/// Syncing is done by:
///   • UI → VM : PasswordChanged pushes the typed value into the ViewModel's EditPassword.
///   • VM → UI : PropertyChanged on the ViewModel updates the PasswordBox when EditPassword
///               is set programmatically (entry selection, generate, clear, lock).
/// This approach is explicitly permitted by the project coding rules.
/// </summary>
public partial class VaultView : UserControl
{
    public VaultView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not VaultViewModel vm)
        {
            return;
        }

        // ── UI → VM ──────────────────────────────────────────────────────────
        // Push the typed password into the ViewModel on every keystroke.
        EditPasswordBox.PasswordChanged += (_, _) => vm.EditPassword = EditPasswordBox.Password;

        // ── VM → UI ──────────────────────────────────────────────────────────
        // Keep the PasswordBox in sync when the ViewModel updates EditPassword programmatically
        // (entry selection/decryption, password generation, field clearing, vault lock).
        vm.PropertyChanged += OnViewModelPropertyChanged;

        await vm.LoadEntriesCommand.ExecuteAsync(null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not VaultViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(VaultViewModel.EditPassword))
        {
            // Only update the PasswordBox when it doesn't already display the same value,
            // and only when it's not the user currently typing (which would be a round-trip).
            if (EditPasswordBox.Password != vm.EditPassword)
            {
                EditPasswordBox.Password = vm.EditPassword;
            }
        }
    }
}
