using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using System.Collections.ObjectModel;
using Microsoft.Maui.Storage;

namespace ExpenseTracker.ViewModels;

public partial class EmployeeViewModel : ObservableObject
{
    private readonly ExpenseService _expenseService = new();
    private readonly ReceiptService _receiptService = new();

    private FileResult? _pendingReceipt;

    // Mapping affichage français → valeur API anglais
    private static readonly Dictionary<string, string> _categoryMap = new()
    {
        { "Voyage",     "travel"    },
        { "Repas",      "meals"     },
        { "Équipement", "equipment" },
        { "Autre",      "other"     }
    };

    [ObservableProperty]
    private ObservableCollection<ExpenseReport> expenses = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isCreating;

    [ObservableProperty]
    private string newAmount = string.Empty;

    [ObservableProperty]
    private string newCategory = "Repas";

    [ObservableProperty]
    private string newDescription = string.Empty;

    [ObservableProperty]
    private string receiptFileName = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public List<string> Categories { get; } = new() { "Voyage", "Repas", "Équipement", "Autre" };

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
    private async Task PickReceiptAsync()
    {
        var picked = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choisir un justificatif",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI,   new[] { ".jpg", ".jpeg", ".png", ".pdf" } },
                { DevicePlatform.iOS,     new[] { "public.image", "com.adobe.pdf" } },
                { DevicePlatform.Android, new[] { "image/*", "application/pdf" } }
            })
        });
        if (picked == null) return;
        _pendingReceipt  = picked;
        ReceiptFileName  = picked.FileName;
    }

    [RelayCommand]
    private async Task SubmitExpenseAsync()
        => await CreateAsync("Submitted");

    [RelayCommand]
    private async Task SaveDraftAsync()
        => await CreateAsync("Draft");

    private async Task CreateAsync(string status)
    {
        if (!decimal.TryParse(NewAmount, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0)
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
            var apiCategory = _categoryMap.TryGetValue(NewCategory, out var eng) ? eng : NewCategory;
            var created = await _expenseService.CreateExpenseAsync(amount, apiCategory, NewDescription, status);
            if (created == null) return;

            // Upload justificatif si sélectionné
            if (_pendingReceipt != null)
            {
                var stream      = await _pendingReceipt.OpenReadAsync();
                var contentType = _pendingReceipt.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? "application/pdf" : "image/jpeg";
                var presigned = await _receiptService.GetUploadUrlAsync(created.ExpenseId, _pendingReceipt.FileName);
                if (presigned != null)
                    await _receiptService.UploadFileAsync(presigned.Url, stream, contentType);
            }

            Expenses.Insert(0, created);
            IsCreating = false;
            ResetForm();
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
    private async Task UploadReceiptAsync(ExpenseReport expense)
    {
        try
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choisir un justificatif",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI,   new[] { ".jpg", ".jpeg", ".png", ".pdf" } },
                    { DevicePlatform.iOS,     new[] { "public.image", "com.adobe.pdf" } },
                    { DevicePlatform.Android, new[] { "image/*", "application/pdf" } }
                })
            });
            if (picked == null) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            var stream      = await picked.OpenReadAsync();
            var contentType = picked.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf" : "image/jpeg";

            var presigned = await _receiptService.GetUploadUrlAsync(expense.ExpenseId, picked.FileName);
            if (presigned == null) throw new Exception("Impossible d'obtenir l'URL d'upload.");

            var success = await _receiptService.UploadFileAsync(presigned.Url, stream, contentType);
            if (!success) throw new Exception("L'upload a échoué.");

            await Shell.Current.DisplayAlert("Succès", "Justificatif envoyé avec succès.", "OK");
            await LoadExpensesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur upload : {ex.Message}";
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
        NewAmount       = string.Empty;
        NewCategory     = "Repas";
        NewDescription  = string.Empty;
        ReceiptFileName = string.Empty;
        ErrorMessage    = string.Empty;
        _pendingReceipt = null;
    }
}
