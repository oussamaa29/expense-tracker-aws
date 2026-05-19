using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Views;

[QueryProperty(nameof(ExpenseId), "expenseId")]
public partial class ExpenseDetailPage : ContentPage
{
    private readonly ExpenseService  _expenseService  = new();
    private readonly ReceiptService  _receiptService  = new();

    private ExpenseReport? _expense;

    public string? ExpenseId { get; set; }

    public ExpenseDetailPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await LoadExpenseAsync();
    }

    private async Task LoadExpenseAsync()
    {
        if (string.IsNullOrEmpty(ExpenseId)) return;

        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ContentCard.IsVisible = false;

        try
        {
            _expense = await _expenseService.GetExpenseAsync(ExpenseId);
            if (_expense == null)
            {
                await DisplayAlert("Erreur", "Dépense introuvable.", "OK");
                return;
            }

            PopulateUI(_expense);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", ex.Message, "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            ContentCard.IsVisible = true;
        }
    }

    private void PopulateUI(ExpenseReport expense)
    {
        AmountLabel.Text      = expense.FormattedAmount;
        CategoryLabel.Text    = expense.Category.Length > 0
            ? char.ToUpper(expense.Category[0]) + expense.Category[1..]
            : expense.Category;
        DescriptionLabel.Text = expense.Description;
        DateLabel.Text        = expense.FormattedDate;
        EmployeeLabel.Text    = expense.EmployeeId;
        StatusLabel.Text      = expense.Status;
        StatusBadge.BackgroundColor = expense.StatusColor;

        bool isEmployee = AuthService.UserRole == "employee";
        bool isFinance  = AuthService.UserRole == "finance";

        // Reject comment
        if (expense.IsRejected && !string.IsNullOrEmpty(expense.ReviewComment))
        {
            RejectCommentBox.IsVisible  = true;
            RejectCommentLabel.Text     = expense.ReviewComment;
        }
        else
        {
            RejectCommentBox.IsVisible = false;
        }

        // Submit Draft button (employee only, draft status)
        SubmitDraftButton.IsVisible = isEmployee && expense.Status == "Draft";

        // Resubmit button (employee only, rejected or resubmitted)
        ResubmitButton.IsVisible = isEmployee && expense.IsRejected;

        // Finance action bar (finance only, submitted or resubmitted)
        FinanceActionBar.IsVisible = isFinance &&
            (expense.Status == "Submitted" || expense.Status == "Resubmitted");

        // Receipt
        NoReceiptLabel.IsVisible      = !expense.HasReceipt;
        UploadReceiptButton.IsVisible = isEmployee && !expense.HasReceipt;

        if (expense.HasReceipt)
            _ = LoadReceiptImageAsync(expense);
    }

    private async Task LoadReceiptImageAsync(ExpenseReport expense)
    {
        ReceiptLoading.IsVisible = true;
        ReceiptLoading.IsRunning = true;
        try
        {
            var fileName  = expense.ReceiptKey?.Split('/').LastOrDefault() ?? "receipt.jpg";
            var presigned = await _receiptService.GetDownloadUrlAsync(expense.ExpenseId, fileName);
            if (presigned == null) return;

            var bytes = await _receiptService.DownloadFileAsync(presigned.Url);
            if (bytes == null) return;

            ReceiptImage.Source  = ImageSource.FromStream(() => new MemoryStream(bytes));
            ReceiptImage.IsVisible = true;
        }
        catch { /* silent — image just won't show */ }
        finally
        {
            ReceiptLoading.IsVisible = false;
            ReceiptLoading.IsRunning = false;
        }
    }

    private async void OnSubmitDraftClicked(object sender, EventArgs e)
    {
        if (_expense == null) return;
        bool confirm = await DisplayAlert("Soumettre", "Soumettre cette dépense pour approbation ?", "Oui", "Annuler");
        if (!confirm) return;
        try
        {
            await _expenseService.PatchExpenseAsync(_expense.ExpenseId, "submit");
            await DisplayAlert("Succès", "Dépense soumise.", "OK");
            await LoadExpenseAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", ex.Message, "OK");
        }
    }

    private async void OnUploadReceiptClicked(object sender, EventArgs e)
    {
        if (_expense == null) return;
        try
        {
            var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Choisir un reçu"
            });
            if (result == null) return;

            ReceiptLoading.IsVisible = true;
            ReceiptLoading.IsRunning = true;
            UploadReceiptButton.IsEnabled = false;

            var stream    = await result.OpenReadAsync();
            var presigned = await _receiptService.GetUploadUrlAsync(_expense.ExpenseId, result.FileName);
            if (presigned == null) throw new Exception("Impossible d'obtenir l'URL d'upload.");

            var success = await _receiptService.UploadFileAsync(presigned.Url, stream, "image/jpeg");
            if (!success) throw new Exception("L'upload a échoué.");

            await DisplayAlert("Succès", "Reçu uploadé avec succès.", "OK");
            UploadReceiptButton.IsVisible = false;
            NoReceiptLabel.IsVisible = false;
            await LoadExpenseAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", ex.Message, "OK");
        }
        finally
        {
            ReceiptLoading.IsVisible = false;
            ReceiptLoading.IsRunning = false;
            UploadReceiptButton.IsEnabled = true;
        }
    }

    private async void OnResubmitClicked(object sender, EventArgs e)
    {
        if (_expense == null) return;
        try
        {
            await _expenseService.PatchExpenseAsync(_expense.ExpenseId, "resubmit");
            await DisplayAlert("Succès", "Dépense resoumise.", "OK");
            await LoadExpenseAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", ex.Message, "OK");
        }
    }

    private async void OnApproveClicked(object sender, EventArgs e)
    {
        if (_expense == null) return;
        bool confirm = await DisplayAlert("Approuver", "Approuver cette dépense ?", "Oui", "Annuler");
        if (!confirm) return;
        try
        {
            await _expenseService.PatchExpenseAsync(_expense.ExpenseId, "approve", "Approved");
            await DisplayAlert("Succès", "Dépense approuvée.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", ex.Message, "OK");
        }
    }

    private async void OnRejectClicked(object sender, EventArgs e)
    {
        if (_expense == null) return;
        var comment = await DisplayPromptAsync(
            "Rejeter",
            "Commentaire obligatoire :",
            placeholder: "Ex : Reçu manquant...",
            maxLength: 500);
        if (string.IsNullOrWhiteSpace(comment)) return;
        try
        {
            await _expenseService.PatchExpenseAsync(_expense.ExpenseId, "reject", comment);
            await DisplayAlert("Succès", "Dépense rejetée.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", ex.Message, "OK");
        }
    }
}
