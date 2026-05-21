using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public class ExpenseService
{
    private static readonly HttpClient _http = new();
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private void SetAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AuthService.IdToken);
    }

    public async Task<List<ExpenseReport>> GetExpensesAsync(string? status = null)
    {
        SetAuthHeader();
        var url = string.IsNullOrEmpty(status)
            ? $"{ApiConfig.ApiBaseUrl}/expenses"
            : $"{ApiConfig.ApiBaseUrl}/expenses?status={status}";
        var response = await _http.GetStringAsync(url);
        var result = JsonSerializer.Deserialize<ApiResponse<List<ExpenseReport>>>(response, _jsonOpts);
        return result?.Data ?? new List<ExpenseReport>();
    }

    public async Task<ExpenseReport?> GetExpenseAsync(string expenseId)
    {
        SetAuthHeader();
        var response = await _http.GetStringAsync($"{ApiConfig.ApiBaseUrl}/expenses/{expenseId}");
        var result = JsonSerializer.Deserialize<ApiResponse<ExpenseReport>>(response, _jsonOpts);
        return result?.Data;
    }

    public async Task<ExpenseReport?> CreateExpenseAsync(decimal amount, string category, string description, string status)
    {
        SetAuthHeader();
        var payload = JsonSerializer.Serialize(new { Amount = amount, Category = category, Description = description, Status = status });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{ApiConfig.ApiBaseUrl}/expenses", content);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<ExpenseReport>>(body, _jsonOpts);
        return result?.Data;
    }

    public async Task<PatchResponse?> PatchExpenseAsync(string expenseId, string action, string? comment = null)
    {
        SetAuthHeader();
        object payload = comment != null
            ? new { Action = action, Comment = comment }
            : new { Action = action };
        var json    = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"{ApiConfig.ApiBaseUrl}/expenses/{expenseId}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<PatchResponse>>(body, _jsonOpts);
        return result?.Data;
    }
}
