# Expense Report Tracker

Module 5ENTAPP / E5WMD — Enterprise Software Engineering on AWS  
Superviseur : Dr. Abdelhak TOUITI — Master Semestre 2  
Étudiant : Oussama CHAGHIL

## Stack technique

- **Frontend** : .NET MAUI (Windows)
- **API** : Amazon API Gateway + Cognito (JWT)
- **Backend** : AWS Lambda (.NET 8)
- **Base de données** : Amazon DynamoDB
- **Stockage** : Amazon S3 (reçus via pre-signed URLs)
- **Auth** : Amazon Cognito (groupes : employees / finance)

## Architecture

```
.NET MAUI (Windows)
        │
        ▼
Amazon API Gateway  ←── Cognito JWT Authorizer
        │
        ▼
AWS Lambda (.NET 8)
   ├── CreateExpense
   ├── GetExpenses
   ├── GetExpenseById
   ├── UpdateExpenseStatus
   └── GeneratePresignedUrl
        │
        ├──▶ DynamoDB  (stockage des dépenses)
        └──▶ S3        (reçus)
```

## Machine à états

```
Draft ──► Submitted ──► Approved
               │
               ▼
           Rejected ──► Resubmitted ──► Submitted
```

Les transitions sont vérifiées côté serveur (Lambda `UpdateExpenseStatus`).

## Prérequis

- .NET 9 SDK + workload MAUI : `dotnet workload install maui-windows`
- AWS CLI configuré : `aws configure`
- Lambda tools : `dotnet tool install -g Amazon.Lambda.Tools`

## Déploiement

```powershell
# Packager chaque Lambda
cd lambda-functions/CreateExpense
dotnet lambda package --output-package ../../deploy/CreateExpense.zip

# Déployer via CloudFormation
aws cloudformation deploy `
  --template-file scripts/cloudformation.yaml `
  --stack-name expense-tracker `
  --capabilities CAPABILITY_IAM CAPABILITY_NAMED_IAM `
  --region eu-west-1
```

## Lancement de l'application

```powershell
cd maui-app
dotnet run --framework net9.0-windows10.0.19041.0
```

## Comptes de test

| Rôle | Email | Mot de passe |
|------|-------|--------------|
| Employé | employe@test.com | Employe@1234 |
| Finance | finance@test.com | Finance@1234 |
