using System.Text.Json.Serialization;

namespace ExpenseTracker.Models;

public class ExpenseReport
{
    [JsonPropertyName("expenseId")]
    public string ExpenseId { get; set; } = string.Empty;

    [JsonPropertyName("userEmail")]
    public string EmployeeId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("reviewerComment")]
    public string? ReviewComment { get; set; }

    [JsonPropertyName("receiptKey")]
    public string? ReceiptKey { get; set; }

    // UI helpers (not from JSON)
    [JsonIgnore]
    public Color StatusColor => Status switch
    {
        "Draft"       => Color.FromArgb("#9E9E9E"),
        "Submitted"   => Color.FromArgb("#2196F3"),
        "Approved"    => Color.FromArgb("#4CAF50"),
        "Rejected"    => Color.FromArgb("#F44336"),
        "Resubmitted" => Color.FromArgb("#FF9800"),
        _             => Color.FromArgb("#9E9E9E")
    };

    [JsonIgnore]
    public string CategoryLabel => Category switch
    {
        "travel"    => "Voyage",
        "meals"     => "Repas",
        "equipment" => "Équipement",
        "other"     => "Autre",
        _           => Category
    };

    [JsonIgnore]
    public string StatusLabel => Status switch
    {
        "Draft"       => "Brouillon",
        "Submitted"   => "Soumis",
        "Approved"    => "Approuvé",
        "Rejected"    => "Rejeté",
        "Resubmitted" => "Resoumis",
        _             => Status
    };

    [JsonIgnore]
    public bool IsRejected => Status == "Rejected";

    [JsonIgnore]
    public bool HasReceipt => !string.IsNullOrEmpty(ReceiptKey);

    [JsonIgnore]
    public string FormattedAmount => $"{Amount:F2} €";

    [JsonIgnore]
    public string FormattedDate
    {
        get
        {
            if (DateTime.TryParse(CreatedAt, out var dt))
                return dt.ToString("MMM dd, yyyy");
            return CreatedAt;
        }
    }
}
