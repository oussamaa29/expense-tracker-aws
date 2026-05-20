using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels;

public partial class EmployeeViewModel : ObservableObject
{
    private readonly ExpenseService _expenseService = new();

    [ObservableProperty]
    private ObservableCollection<ExpenseReport> expenses = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isCreating;

    // New expense form fields
    [ObservableProperty]
    private string newAmount = string.Empty;

    [ObservableProperty]
    private string newCategory = "meals";

    [ObservableProperty]
    private string newDescription = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public List<string> Categories { get; } = new() { "travel", "meals", "equipment", "other" };

    [RelayCommand]
    public async Task LoadExpensesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _expenseService.GetExpensesAsync();
            Expenses.Clear();
            foreach (var e in list)
                Expenses.Add(e);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Impossible de charger les dépenses : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ShowCreateForm() => IsCreating = true;

    [RelayCommand]
    private void CancelCreate()
    {
        IsCreating = false;
        ResetForm();
    }

    [RelayCommand]
    private async Task SubmitExpenseAsync()
        => await CreateAsync("Submitted");

    [RelayCommand]
    private async Task SaveDraftAsync()
        => await CreateAsync("Draft");

    private async Task CreateAsync(string status)
    {
        if (!decimal.TryParse(NewAmount, out var amount) || amount <= 0)
        {
            ErrorMessage = "Montant invalide.";
            return;
        }
        if (string.IsNullOrWhiteSpace(NewDescription))
        {
            ErrorMessage = "La description est obligatoire.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var created = await _expenseService.CreateExpenseAsync(amount, NewCategory, NewDescription, status);
            if (created != null)
            {
                Expenses.Insert(0, created);
                IsCreating = false;
                ResetForm();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur lors de la création : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResubmitAsync(ExpenseReport expense)
    {
        IsBusy = true;
        try
        {
            await _expenseService.PatchExpenseAsync(expense.ExpenseId, "resubmit");
            await LoadExpensesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenDetailAsync(ExpenseReport? expense)
    {
        if (expense == null) return;
        await Shell.Current.GoToAsync($"ExpenseDetailPage?expenseId={expense.ExpenseId}");
    }

    private void ResetForm()
    {
        NewAmount      = string.Empty;
        NewCategory    = "meals";
        NewDescription = string.Empty;
        ErrorMessage   = string.Empty;
    }
}
