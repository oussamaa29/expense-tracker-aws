// ============================================================
// GeneratePresignedUrl/Function.cs
// POST /presigned-url
// Generates pre-signed S3 URLs for receipt upload or download
// Body: { "expenseId": "...", "fileName": "receipt.jpg", "operation": "upload|download" }
// ============================================================

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace GeneratePresignedUrl
{
    public class Function
    {
        private readonly AmazonS3Client _s3;
        private readonly AmazonDynamoDBClient _dynamoDb;
        private readonly string _bucketName;
        private readonly string _tableName;

        public Function()
        {
            _s3 = new AmazonS3Client();
            _dynamoDb = new AmazonDynamoDBClient();
            _bucketName = Environment.GetEnvironmentVariable("BUCKET_NAME") ?? "expense-tracker-receipts";
            _tableName = Environment.GetEnvironmentVariable("TABLE_NAME") ?? "expense-tracker-expenses";
        }

        public async Task<APIGatewayProxyResponse> FunctionHandler(
            APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                var userId = request.RequestContext.Authorizer.Claims["sub"];

                var body = JsonSerializer.Deserialize<PresignedUrlBody>(request.Body);
                if (body == null || string.IsNullOrEmpty(body.ExpenseId))
                {
                    return Response(400, "Missing required field: expenseId");
                }

                var operation = (body.Operation ?? "upload").ToLower();
                if (operation != "upload" && operation != "download")
                {
                    return Response(400, "Operation must be 'upload' or 'download'.");
                }

                // Build the S3 object key: receipts/<userId>/<expenseId>/<fileName>
                var fileName = string.IsNullOrEmpty(body.FileName) ? "receipt.jpg" : body.FileName;
                var objectKey = $"receipts/{userId}/{body.ExpenseId}/{fileName}";

                string presignedUrl;

                if (operation == "upload")
                {
                    // Generate PUT pre-signed URL (15 min expiry)
                    var putRequest = new GetPreSignedUrlRequest
                    {
                        BucketName = _bucketName,
                        Key = objectKey,
                        Verb = HttpVerb.PUT,
                        Expires = DateTime.UtcNow.AddMinutes(15),
                        ContentType = GetContentType(fileName)
                    };

                    presignedUrl = await _s3.GetPreSignedURLAsync(putRequest);

                    // Update the expense record with the receipt key
                    // Find the expense first to get PK/SK
                    await UpdateReceiptKey(userId, body.ExpenseId, objectKey, context);
                }
                else
                {
                    // Generate GET pre-signed URL (60 min expiry for viewing)
                    var getRequest = new GetPreSignedUrlRequest
                    {
                        BucketName = _bucketName,
                        Key = objectKey,
                        Verb = HttpVerb.GET,
                        Expires = DateTime.UtcNow.AddHours(1)
                    };

                    presignedUrl = await _s3.GetPreSignedURLAsync(getRequest);
                }

                context.Logger.LogInformation($"Generated {operation} pre-signed URL for {objectKey}");

                return Response(200, new
                {
                    url = presignedUrl,
                    objectKey,
                    operation,
                    expiresIn = operation == "upload" ? "15 minutes" : "60 minutes"
                });
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error: {ex.Message}");
                return Response(500, "Internal server error.");
            }
        }

        private async Task UpdateReceiptKey(string userId, string expenseId, string receiptKey, ILambdaContext context)
        {
            try
            {
                await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = _tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = new AttributeValue($"USER#{userId}"),
                        ["SK"] = new AttributeValue($"EXPENSE#{expenseId}")
                    },
                    UpdateExpression = "SET ReceiptKey = :rk, UpdatedAt = :now",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":rk"] = new AttributeValue(receiptKey),
                        [":now"] = new AttributeValue(DateTime.UtcNow.ToString("o"))
                    }
                });
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning($"Could not update receipt key: {ex.Message}");
            }
        }

        private string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".pdf" => "application/pdf",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
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

    public class PresignedUrlBody
    {
        public string ExpenseId { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? Operation { get; set; }
    }
}
