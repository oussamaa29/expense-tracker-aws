using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Services;
using ExpenseTracker.Views;

namespace ExpenseTracker.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _authService = new();

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Veuillez saisir votre email et mot de passe.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var (success, error) = await _authService.LoginAsync(Email, Password);

            if (!success)
            {
                ErrorMessage = error;
                return;
            }

            if (AuthService.UserRole == "finance")
                await Shell.Current.GoToAsync("FinancePage");
            else
                await Shell.Current.GoToAsync("EmployeePage");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur inattendue : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
