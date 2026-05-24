using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels;

public partial class FinanceViewModel : ObservableObject
{
    private readonly ExpenseService _expenseService = new();

    [ObservableProperty]
    private ObservableCollection<ExpenseReport> expenses = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [RelayCommand]
    public async Task LoadExpensesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var submitted    = await _expenseService.GetExpensesAsync("Submitted");
            var resubmitted  = await _expenseService.GetExpensesAsync("Resubmitted");
            var list = submitted.Concat(resubmitted)
                                .OrderByDescending(e => e.CreatedAt)
                                .ToList();
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
    private async Task ApproveAsync(ExpenseReport expense)
    {
        IsBusy = true;
        try
        {
            await _expenseService.PatchExpenseAsync(expense.ExpenseId, "approve", "Approved");
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
    private async Task RejectAsync(ExpenseReport expense)
    {
        var comment = await Shell.Current.DisplayPromptAsync(
            "Rejeter la dépense",
            "Commentaire obligatoire :",
            placeholder: "Ex : Reçu manquant...",
            maxLength: 500);

        if (string.IsNullOrWhiteSpace(comment)) return;

        IsBusy = true;
        try
        {
            await _expenseService.PatchExpenseAsync(expense.ExpenseId, "reject", comment);
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
    private async Task OpenDetailAsync(ExpenseReport expense)
    {
        await Shell.Current.GoToAsync($"ExpenseDetailPage?expenseId={expense.ExpenseId}");
    }
}
