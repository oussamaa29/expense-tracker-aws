using System.Text.Json.Serialization;

namespace ExpenseTracker.Models;

public class ApiResponse<T>
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class PatchResponse
{
    [JsonPropertyName("previousStatus")]
    public string PreviousStatus { get; set; } = string.Empty;

    [JsonPropertyName("newStatus")]
    public string NewStatus { get; set; } = string.Empty;
}

public class PresignedUrlResponse
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("objectKey")]
    public string ObjectKey { get; set; } = string.Empty;
}
