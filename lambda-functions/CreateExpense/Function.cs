using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CreateExpense
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
                var userEmail = request.RequestContext.Authorizer.Claims["email"];

                var body = JsonSerializer.Deserialize<CreateExpenseBody>(request.Body);
                if (body == null || body.Amount <= 0 || string.IsNullOrEmpty(body.Category))
                    return Response(400, "Invalid request: amount must be > 0 and category is required.");

                var validCategories = new[] { "travel", "meals", "equipment", "other" };
                if (!validCategories.Contains(body.Category.ToLower()))
                    return Response(400, $"Invalid category. Must be one of: {string.Join(", ", validCategories)}");

                var status = string.IsNullOrEmpty(body.Status) ? "Draft" : body.Status;
                if (status != "Draft" && status != "Submitted")
                    return Response(400, "Initial status must be Draft or Submitted.");

                var expenseId = Ulid.NewUlid().ToString();
                var now = DateTime.UtcNow.ToString("o");

                var item = new Dictionary<string, AttributeValue>
                {
                    ["PK"]          = new AttributeValue($"USER#{userId}"),
                    ["SK"]          = new AttributeValue($"EXPENSE#{expenseId}"),
                    ["ExpenseId"]   = new AttributeValue(expenseId),
                    ["UserId"]      = new AttributeValue(userId),
                    ["UserEmail"]   = new AttributeValue(userEmail),
                    ["Amount"]      = new AttributeValue { N = body.Amount.ToString("F2") },
                    ["Category"]    = new AttributeValue(body.Category.ToLower()),
                    ["Description"] = new AttributeValue(body.Description ?? ""),
                    ["Status"]      = new AttributeValue(status),
                    ["CreatedAt"]   = new AttributeValue(now),
                    ["UpdatedAt"]   = new AttributeValue(now),
                };

                await _dynamoDb.PutItemAsync(new PutItemRequest
                {
                    TableName = _tableName,
                    Item = item
                });

                return Response(201, new
                {
                    expenseId,
                    userId,
                    userEmail,
                    amount = body.Amount,
                    category = body.Category.ToLower(),
                    description = body.Description,
                    status,
                    createdAt = now
                });
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error creating expense: {ex.Message}");
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

    public class CreateExpenseBody
    {
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Status { get; set; }
    }

    public static class Ulid
    {
        private static readonly Random _random = new();
        private static readonly char[] _encoding = "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();

        public static string NewUlid()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var chars = new char[26];

            for (int i = 9; i >= 0; i--)
            {
                chars[i] = _encoding[timestamp & 0x1F];
                timestamp >>= 5;
            }

            var bytes = new byte[10];
            _random.NextBytes(bytes);
            int bitIndex = 0;
            for (int i = 10; i < 26; i++)
            {
                int byteIdx = bitIndex / 8;
                int bitOffset = bitIndex % 8;
                int value;
                if (bitOffset <= 3)
                    value = (bytes[byteIdx] >> (3 - bitOffset)) & 0x1F;
                else
                {
                    value = (bytes[byteIdx] << (bitOffset - 3)) & 0x1F;
                    if (byteIdx + 1 < bytes.Length)
                        value |= bytes[byteIdx + 1] >> (11 - bitOffset);
                }
                chars[i] = _encoding[value & 0x1F];
                bitIndex += 5;
            }

            return new string(chars);
        }
    }
}
