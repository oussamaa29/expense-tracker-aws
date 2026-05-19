using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public class ReceiptService
{
    private static readonly HttpClient _http = new();
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private void SetAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AuthService.AccessToken);
    }

    public async Task<PresignedUrlResponse?> GetUploadUrlAsync(string expenseId, string fileName)
    {
        SetAuthHeader();
        var payload = JsonSerializer.Serialize(new { expenseId, fileName, operation = "upload" });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{ApiConfig.ApiBaseUrl}/presigned-url", content);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<PresignedUrlResponse>>(body, _jsonOpts);
        return result?.Data;
    }

    public async Task<PresignedUrlResponse?> GetDownloadUrlAsync(string expenseId, string fileName)
    {
        SetAuthHeader();
        var payload = JsonSerializer.Serialize(new { expenseId, fileName, operation = "download" });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{ApiConfig.ApiBaseUrl}/presigned-url", content);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<PresignedUrlResponse>>(body, _jsonOpts);
        return result?.Data;
    }

    public async Task<bool> UploadFileAsync(string presignedUrl, Stream fileStream, string contentType)
    {
        var content = new StreamContent(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var response = await _http.PutAsync(presignedUrl, content);
        return response.IsSuccessStatusCode;
    }

    public async Task<byte[]?> DownloadFileAsync(string presignedUrl)
    {
        var response = await _http.GetAsync(presignedUrl);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsByteArrayAsync();
    }
}
