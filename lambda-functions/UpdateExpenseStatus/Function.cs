// ============================================================
// UpdateExpenseStatus/Function.cs
// PATCH /expenses/{expenseId}
// Server-side state machine with RBAC enforcement
// Body: { "action": "approve|reject|submit|resubmit", "comment": "..." }
// ============================================================

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace UpdateExpenseStatus
{
    public class Function
    {
        private readonly AmazonDynamoDBClient _dynamoDb;
        private readonly string _tableName;

        // ── State Machine Definition ──
        // Maps (currentStatus, action) → newStatus
        private static readonly Dictionary<(string, string), string> ValidTransitions = new()
        {
            { ("Draft", "submit"), "Submitted" },
            { ("Submitted", "approve"), "Approved" },
            { ("Submitted", "reject"), "Rejected" },
            { ("Rejected", "resubmit"), "Resubmitted" },
            { ("Resubmitted", "submit"), "Submitted" },
        };

        // Actions only finance managers can perform
        private static readonly HashSet<string> FinanceActions = new() { "approve", "reject" };

        // Actions only the expense owner can perform
        private static readonly HashSet<string> OwnerActions = new() { "submit", "resubmit" };

        public Function()
        {
            _dynamoDb = new AmazonDynamoDBClient();
            _tableName = Environment.GetEnvironmentVariable("TABLE_NAME") ?? "expense-tracker-expenses";
        }

        public async Task<APIGatewayProxyResponse> FunctionHandler(
            APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                // 1. Extract user info from JWT
                var userId = request.RequestContext.Authorizer.Claims["sub"];
                var groups = request.RequestContext.Authorizer.Claims.ContainsKey("cognito:groups")
                    ? request.RequestContext.Authorizer.Claims["cognito:groups"].Split(',').Select(g => g.Trim()).ToList()
                    : new List<string>();
                var isFinance = groups.Contains("finance");

                // 2. Parse request
                var expenseId = request.PathParameters["expenseId"];
                var body = JsonSerializer.Deserialize<UpdateStatusBody>(request.Body);
                if (body == null || string.IsNullOrEmpty(body.Action))
                {
                    return Response(400, "Missing required field: action");
                }

                var action = body.Action.ToLower();

                // 3. RBAC check — before even hitting the database
                if (FinanceActions.Contains(action) && !isFinance)
                {
                    return Response(403, "Only finance managers can approve or reject expenses.");
                }

                if (OwnerActions.Contains(action) && isFinance)
                {
                    return Response(403, "Finance managers cannot submit or resubmit expenses.");
                }

                // 4. Require comment for reject
                if (action == "reject" && string.IsNullOrWhiteSpace(body.Comment))
                {
                    return Response(400, "A comment is required when rejecting an expense.");
                }

                // 5. Fetch current expense from DynamoDB
                // For finance: we need to find the item regardless of owner
                Dictionary<string, AttributeValue>? item = null;
                string pk, sk;

                if (!isFinance)
                {
                    // Employee: direct key lookup
                    pk = $"USER#{userId}";
                    sk = $"EXPENSE#{expenseId}";
                    var getResult = await _dynamoDb.GetItemAsync(new GetItemRequest
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["PK"] = new AttributeValue(pk),
                            ["SK"] = new AttributeValue(sk)
                        }
                    });
                    item = getResult.Item?.Count > 0 ? getResult.Item : null;
                }
                else
                {
                    // Finance: scan by ExpenseId
                    var scanResult = await _dynamoDb.ScanAsync(new ScanRequest
                    {
                        TableName = _tableName,
                        FilterExpression = "ExpenseId = :eid",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":eid"] = new AttributeValue(expenseId)
                        },
                        Limit = 1
                    });
                    item = scanResult.Items.FirstOrDefault();
                }

                if (item == null)
                {
                    return Response(404, "Expense not found.");
                }

                pk = item["PK"].S;
                sk = item["SK"].S;

                // 6. Ownership check for employee actions
                if (OwnerActions.Contains(action))
                {
                    var ownerId = item.GetValueOrDefault("UserId")?.S;
                    if (ownerId != userId)
                    {
                        return Response(403, "You can only modify your own expenses.");
                    }
                }

                // 7. State machine validation
                var currentStatus = item.GetValueOrDefault("Status")?.S ?? "Draft";
                if (!ValidTransitions.TryGetValue((currentStatus, action), out var newStatus))
                {
                    return Response(409, $"Invalid transition: cannot '{action}' an expense with status '{currentStatus}'.");
                }

                // 8. Update DynamoDB
                var now = DateTime.UtcNow.ToString("o");
                var updateExpr = "SET #status = :newStatus, UpdatedAt = :now";
                var exprNames = new Dictionary<string, string> { ["#status"] = "Status" };
                var exprValues = new Dictionary<string, AttributeValue>
                {
                    [":newStatus"] = new AttributeValue(newStatus),
                    [":now"] = new AttributeValue(now)
                };

                // Add reviewer info for approve/reject
                if (action == "approve" || action == "reject")
                {
                    updateExpr += ", ReviewerId = :reviewerId";
                    exprValues[":reviewerId"] = new AttributeValue(userId);

                    if (!string.IsNullOrEmpty(body.Comment))
                    {
                        updateExpr += ", ReviewerComment = :comment";
                        exprValues[":comment"] = new AttributeValue(body.Comment);
                    }
                }

                // Add currentStatus for optimistic lock condition
                exprValues[":currentStatus"] = new AttributeValue(currentStatus);

                await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = _tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = new AttributeValue(pk),
                        ["SK"] = new AttributeValue(sk)
                    },
                    UpdateExpression = updateExpr,
                    ExpressionAttributeNames = exprNames,
                    ExpressionAttributeValues = exprValues,
                    // Optimistic lock: ensure status hasn't changed between read and write
                    ConditionExpression = "#status = :currentStatus",
                });

                context.Logger.LogInformation(
                    $"Expense {expenseId}: {currentStatus} → {newStatus} by {userId} (action: {action})");

                return Response(200, new
                {
                    expenseId,
                    previousStatus = currentStatus,
                    newStatus,
                    action,
                    updatedAt = now
                });
            }
            catch (ConditionalCheckFailedException)
            {
                return Response(409, "Conflict: the expense status was modified by another request. Please retry.");
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error: {ex.Message}");
                return Response(500, "Internal server error.");
            }
        }

        private APIGatewayProxyResponse Response(int statusCode, object body)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = statusCode,
                Headers = new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/json",
                    ["Access-Control-Allow-Origin"] = "*"
                },
                Body = JsonSerializer.Serialize(new { statusCode, data = body })
            };
        }
    }

    public class UpdateStatusBody
    {
        public string Action { get; set; } = string.Empty;
        public string? Comment { get; set; }
    }
}
