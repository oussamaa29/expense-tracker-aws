using System.Text.Json.Serialization;

namespace ExpenseTracker.Shared
{
    public class ExpenseReport
    {
        public string PK { get; set; } = string.Empty;
        public string SK { get; set; } = string.Empty;
        public string ExpenseId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Draft";
        public string? ReceiptKey { get; set; }
        public string? ReviewerComment { get; set; }
        public string? ReviewerId { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }

    public class CreateExpenseRequest
    {
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "Draft";
    }

    public class UpdateStatusRequest
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }
    }

    public class PresignedUrlRequest
    {
        [JsonPropertyName("expenseId")]
        public string ExpenseId { get; set; } = string.Empty;

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("operation")]
        public string Operation { get; set; } = "upload";
    }

    public class ApiResponse
    {
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public object? Data { get; set; }
    }

    public static class StateMachine
    {
        private static readonly Dictionary<(string Status, string Action), string> Transitions = new()
        {
            { ("Draft",       "submit"),   "Submitted"   },
            { ("Submitted",   "approve"),  "Approved"    },
            { ("Submitted",   "reject"),   "Rejected"    },
            { ("Rejected",    "resubmit"), "Resubmitted" },
            { ("Resubmitted", "submit"),   "Submitted"   },
        };

        private static readonly HashSet<string> FinanceOnlyActions = new() { "approve", "reject" };
        private static readonly HashSet<string> OwnerOnlyActions   = new() { "submit", "resubmit" };

        public static bool IsFinanceOnlyAction(string action) => FinanceOnlyActions.Contains(action);
        public static bool IsOwnerOnlyAction(string action)   => OwnerOnlyActions.Contains(action);

        public static (bool IsValid, string? NewStatus) TryTransition(string currentStatus, string action)
        {
            if (Transitions.TryGetValue((currentStatus, action), out var newStatus))
                return (true, newStatus);
            return (false, null);
        }
    }

    public static class JwtHelper
    {
        public static string GetUserId(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest request)
            => request.RequestContext.Authorizer.Claims["sub"];

        public static string GetUserEmail(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest request)
            => request.RequestContext.Authorizer.Claims["email"];

        public static List<string> GetUserGroups(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest request)
        {
            var groupsClaim = request.RequestContext.Authorizer.Claims.ContainsKey("cognito:groups")
                ? request.RequestContext.Authorizer.Claims["cognito:groups"]
                : string.Empty;

            if (string.IsNullOrEmpty(groupsClaim)) return new List<string>();
            return groupsClaim.Split(',').Select(g => g.Trim()).ToList();
        }

        public static bool IsFinance(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest request)
            => GetUserGroups(request).Contains("finance");
    }
}
