using ExpenseTracker.Services;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views;

public partial class EmployeePage : ContentPage
{
    public EmployeePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is EmployeeViewModel vm)
            await vm.LoadExpensesCommand.ExecuteAsync(null);
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        AuthService.Logout();
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
