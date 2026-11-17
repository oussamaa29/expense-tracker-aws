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

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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
                var userId = request.RequestContext.Authorizer.Claims["sub"];
                var groups = request.RequestContext.Authorizer.Claims.ContainsKey("cognito:groups")
                    ? request.RequestContext.Authorizer.Claims["cognito:groups"].Split(',').Select(g => g.Trim()).ToList()
                    : new List<string>();
                var isFinance = groups.Contains("finance");

                var expenseId = request.PathParameters["expenseId"];
                UpdateStatusBody? body;
                try
                {
                    body = string.IsNullOrEmpty(request.Body)
                        ? null
                        : JsonSerializer.Deserialize<UpdateStatusBody>(request.Body, JsonOptions);
                }
                catch (JsonException)
                {
                    return Response(400, "Invalid JSON body.");
                }
                if (body == null || string.IsNullOrEmpty(body.Action))
                    return Response(400, "Missing required field: action");

                var action = body.Action.ToLower();

                var error = ExpenseStateMachine.Validate(action, isFinance, body.Comment);
                if (error is { } e)
                    return Response(e.StatusCode, e.Message);

                Dictionary<string, AttributeValue>? item = null;
                string pk, sk;

                if (!isFinance)
                {
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
                    // Index secondaire sur ExpenseId : une seule lecture ciblée. Un Scan lisait
                    // toute la table et, au-delà de 1 Mo, pouvait renvoyer une page sans la dépense.
                    var queryResult = await _dynamoDb.QueryAsync(new QueryRequest
                    {
                        TableName = _tableName,
                        IndexName = "ExpenseIdIndex",
                        KeyConditionExpression = "ExpenseId = :eid",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":eid"] = new AttributeValue(expenseId)
                        },
                        Limit = 1
                    });
                    item = queryResult.Items.FirstOrDefault();
                }

                if (item == null)
                    return Response(404, "Expense not found.");

                pk = item["PK"].S;
                sk = item["SK"].S;

                if (ExpenseStateMachine.OwnerActions.Contains(action))
                {
                    var ownerId = item.GetValueOrDefault("UserId")?.S;
                    if (ownerId != userId)
                        return Response(403, "You can only modify your own expenses.");
                }

                var currentStatus = item.GetValueOrDefault("Status")?.S ?? "Draft";
                if (!ExpenseStateMachine.TryTransition(currentStatus, action, out var newStatus))
                    return Response(409, $"Invalid transition: cannot '{action}' an expense with status '{currentStatus}'.");

                var now = DateTime.UtcNow.ToString("o");
                var updateExpr = "SET #status = :newStatus, UpdatedAt = :now";
                var exprNames  = new Dictionary<string, string> { ["#status"] = "Status" };
                var exprValues = new Dictionary<string, AttributeValue>
                {
                    [":newStatus"]     = new AttributeValue(newStatus),
                    [":now"]           = new AttributeValue(now),
                    [":currentStatus"] = new AttributeValue(currentStatus)
                };

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

                await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = _tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = new AttributeValue(pk),
                        ["SK"] = new AttributeValue(sk)
                    },
                    UpdateExpression      = updateExpr,
                    ExpressionAttributeNames  = exprNames,
                    ExpressionAttributeValues = exprValues,
                    ConditionExpression   = "#status = :currentStatus",
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
