using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace GetExpenses
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
                var groups = GetUserGroups(request);
                var isFinance = groups.Contains("finance");

                List<Dictionary<string, AttributeValue>> items;

                if (isFinance)
                {
                    var statusFilter = request.QueryStringParameters?.ContainsKey("status") == true
                        ? request.QueryStringParameters["status"]
                        : "Submitted";

                    var queryRequest = new QueryRequest
                    {
                        TableName = _tableName,
                        IndexName = "StatusIndex",
                        KeyConditionExpression = "#status = :status",
                        ExpressionAttributeNames = new Dictionary<string, string>
                        {
                            ["#status"] = "Status"
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":status"] = new AttributeValue(statusFilter)
                        },
                        ScanIndexForward = false
                    };

                    var result = await _dynamoDb.QueryAsync(queryRequest);
                    items = result.Items;
                }
                else
                {
                    var queryRequest = new QueryRequest
                    {
                        TableName = _tableName,
                        KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":pk"]       = new AttributeValue($"USER#{userId}"),
                            [":skPrefix"] = new AttributeValue("EXPENSE#")
                        },
                        ScanIndexForward = false
                    };

                    var result = await _dynamoDb.QueryAsync(queryRequest);
                    items = result.Items;
                }

                var expenses = items.Select(item => new
                {
                    expenseId       = item.GetValueOrDefault("ExpenseId")?.S,
                    userId          = item.GetValueOrDefault("UserId")?.S,
                    userEmail       = item.GetValueOrDefault("UserEmail")?.S,
                    amount          = decimal.TryParse(item.GetValueOrDefault("Amount")?.N, out var a) ? a : 0,
                    category        = item.GetValueOrDefault("Category")?.S,
                    description     = item.GetValueOrDefault("Description")?.S,
                    status          = item.GetValueOrDefault("Status")?.S,
                    receiptKey      = item.GetValueOrDefault("ReceiptKey")?.S,
                    reviewerComment = item.GetValueOrDefault("ReviewerComment")?.S,
                    createdAt       = item.GetValueOrDefault("CreatedAt")?.S,
                    updatedAt       = item.GetValueOrDefault("UpdatedAt")?.S,
                }).ToList();

                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, string>
                    {
                        ["Content-Type"] = "application/json",
                        ["Access-Control-Allow-Origin"] = "*"
                    },
                    Body = JsonSerializer.Serialize(new { statusCode = 200, data = expenses })
                };
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error: {ex.Message}");
                return new APIGatewayProxyResponse
                {
                    StatusCode = 500,
                    Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                    Body = JsonSerializer.Serialize(new { statusCode = 500, data = "Internal server error." })
                };
            }
        }

        private List<string> GetUserGroups(APIGatewayProxyRequest request)
        {
            var claims = request.RequestContext.Authorizer.Claims;
            if (claims.ContainsKey("cognito:groups"))
                return claims["cognito:groups"].Split(',').Select(g => g.Trim()).ToList();
            return new List<string>();
        }
    }
}
