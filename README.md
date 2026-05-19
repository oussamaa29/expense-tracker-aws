# Expense Report Tracker — Serverless AWS

Module 5ENTAPP / E5WMD — Enterprise Software Engineering on AWS  
Supervisor: Dr. Abdelhak TOUITI — Master Semester 2

## Architecture

```
.NET MAUI (Windows)
        │
        ▼
Amazon API Gateway  ←── Cognito Authorizer (JWT)
        │
        ▼
AWS Lambda (.NET 8)
   ├── CreateExpense
   ├── GetExpenses
   ├── GetExpenseById
   ├── UpdateExpenseStatus  ← state machine + RBAC
   └── GeneratePresignedUrl
        │
        ├──▶ Amazon DynamoDB  (expense storage)
        └──▶ Amazon S3        (receipts via pre-signed URLs)

Amazon Cognito — groups: employees / finance
AWS IAM        — least-privilege Lambda role
```

## State Machine

```
Draft ──► Submitted ──► Approved
               │
               ▼
           Rejected ──► Resubmitted ──► Submitted
```

Transitions enforced server-side in UpdateExpenseStatus Lambda.

## DynamoDB Model

| Attribute | Type | Description |
|-----------|------|-------------|
| PK | String | USER#<userId> |
| SK | String | EXPENSE#<ulid> |
| Status | String | Draft/Submitted/Approved/Rejected/Resubmitted |
| Amount | Number | Expense amount |
| Category | String | travel/meals/equipment/other |

GSIs: StatusIndex (Finance queue), CategoryIndex

## Prerequisites

- .NET 9 SDK + MAUI workload: `dotnet workload install maui-windows`
- AWS CLI: `aws configure`
- Lambda tools: `dotnet tool install -g Amazon.Lambda.Tools`

## Deploy

```powershell
# Package Lambdas (repeat for each function)
cd lambda-functions/CreateExpense
dotnet lambda package --output-package ../../deploy/CreateExpense.zip

# Upload & deploy
aws s3 mb s3://expense-tracker-deploy-YOURNAME --region eu-west-1
aws s3 cp deploy/ s3://expense-tracker-deploy-YOURNAME/ --recursive
aws cloudformation deploy \
  --template-file scripts/cloudformation.yaml \
  --stack-name expense-tracker \
  --parameter-overrides DeployBucket=expense-tracker-deploy-YOURNAME \
  --capabilities CAPABILITY_IAM CAPABILITY_NAMED_IAM \
  --region eu-west-1
```

## Run

```powershell
cd maui-app
dotnet run --framework net9.0-windows10.0.19041.0
```

## Test accounts

| Role | Email | Password |
|------|-------|----------|
| Employee | employe@test.com | Employe@1234 |
| Finance | finance@test.com | Finance@1234 |
