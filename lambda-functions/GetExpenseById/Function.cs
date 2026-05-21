// ============================================================
// GetExpenseById/Function.cs
// GET /expenses/{expenseId}
// Returns a single expense. Employee can only see own. Finance can see all.
// ============================================================

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace GetExpenseById
{
    public class Function
    {
        private readonly AmazonDynamoDBClient _dynamoDb;
        private readonly string _tableName;

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
                    ? request.RequestContext.Authorizer.Claims["cognito:groups"].Split(',').ToList()
                    : new List<string>();
                var isFinance = groups.Contains("finance");

                var expenseId = request.PathParameters["expenseId"];

                // We need to find this expense — but we may not know the owner's userId
                // Strategy: if employee, query with their PK. If finance, scan by ExpenseId.
                Dictionary<string, AttributeValue>? item = null;

                if (!isFinance)
                {
                    // Employee: can only access own expenses
                    var result = await _dynamoDb.GetItemAsync(new GetItemRequest
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["PK"] = new AttributeValue($"USER#{userId}"),
                            ["SK"] = new AttributeValue($"EXPENSE#{expenseId}")
                        }
                    });
                    item = result.Item?.Count > 0 ? result.Item : null;
                }
                else
                {
                    // Finance: query across all users — use a scan with filter
                    // In production, consider a GSI on ExpenseId for O(1) lookup
                    var scanRequest = new ScanRequest
                    {
                        TableName = _tableName,
                        FilterExpression = "ExpenseId = :eid",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":eid"] = new AttributeValue(expenseId)
                        }
                    };
                    var result = await _dynamoDb.ScanAsync(scanRequest);
                    item = result.Items.FirstOrDefault();
                }

                if (item == null)
                {
                    return Response(404, "Expense not found.");
                }

                var expense = new
                {
                    expenseId = item.GetValueOrDefault("ExpenseId")?.S,
                    userId = item.GetValueOrDefault("UserId")?.S,
                    userEmail = item.GetValueOrDefault("UserEmail")?.S,
                    amount = decimal.TryParse(item.GetValueOrDefault("Amount")?.N, out var a) ? a : 0,
                    category = item.GetValueOrDefault("Category")?.S,
                    description = item.GetValueOrDefault("Description")?.S,
                    status = item.GetValueOrDefault("Status")?.S,
                    receiptKey = item.GetValueOrDefault("ReceiptKey")?.S,
                    reviewerComment = item.GetValueOrDefault("ReviewerComment")?.S,
                    reviewerId = item.GetValueOrDefault("ReviewerId")?.S,
                    createdAt = item.GetValueOrDefault("CreatedAt")?.S,
                    updatedAt = item.GetValueOrDefault("UpdatedAt")?.S,
                };

                return Response(200, expense);
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
}
