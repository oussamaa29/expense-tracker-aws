# Expense Report Tracker

[![CI](https://github.com/oussamaa29/expense-tracker-aws/actions/workflows/ci.yml/badge.svg)](https://github.com/oussamaa29/expense-tracker-aws/actions/workflows/ci.yml)

Gestion de notes de frais **serverless sur AWS** : un employé crée une dépense et joint son reçu, le service finance l'approuve ou la rejette avec un motif. Client desktop .NET MAUI, API sur API Gateway + Lambda, données dans DynamoDB, reçus dans S3, authentification Cognito avec deux rôles.

Projet du module 5ENTAPP / E5WMD (Enterprise Software Engineering on AWS), Master, semestre 2, ESTIAM.

## Stack technique

- **Client** : .NET MAUI (Windows), architecture MVVM
- **API** : Amazon API Gateway (REST) avec un autorisateur JWT Cognito
- **Backend** : 5 fonctions AWS Lambda en C# (.NET 10)
- **Base de données** : Amazon DynamoDB (à la demande)
- **Fichiers** : Amazon S3, envoi direct des reçus par URL pré-signée
- **Auth** : Amazon Cognito, groupes `employees` et `finance`
- **Infrastructure** : CloudFormation (tout le backend est décrit dans `scripts/cloudformation.yaml`)

## Architecture

```
.NET MAUI (Windows)
        │  JWT Cognito
        ▼
Amazon API Gateway  ←── autorisateur Cognito
        │
        ▼
AWS Lambda (.NET 10)
   ├── CreateExpense         POST  /expenses
   ├── GetExpenses           GET   /expenses
   ├── GetExpenseById        GET   /expenses/{id}
   ├── UpdateExpenseStatus   PATCH /expenses/{id}
   └── GeneratePresignedUrl  POST  /presigned-url
        │
        ├──▶ DynamoDB  (dépenses)
        └──▶ S3        (reçus, accès public bloqué)
```

### Modèle DynamoDB

| Accès | Clé |
|---|---|
| Dépenses d'un employé | table : `PK = USER#<id>`, `SK = EXPENSE#<id>` |
| Dépenses par statut (vue finance) | index `StatusIndex` : `Status` + `CreatedAt` |
| Dépenses par catégorie | index `CategoryIndex` : `Category` + `CreatedAt` |
| Une dépense par son identifiant (vue finance) | index `ExpenseIdIndex` : `ExpenseId` |

Toutes les lectures sont des `GetItem` ou des `Query` : aucune fonction ne parcourt la table entière, et le rôle IAM des Lambdas n'a d'ailleurs pas le droit `dynamodb:Scan`.

## Machine à états

```
Draft ──► Submitted ──► Approved
               │
               ▼
           Rejected ──► Resubmitted ──► Approved / Rejected
```

Les règles sont appliquées côté serveur, dans `ExpenseStateMachine` (Lambda `UpdateExpenseStatus`) :
- seul le groupe `finance` peut approuver ou rejeter, et un rejet exige un motif ;
- seul le propriétaire d'une dépense peut la soumettre ou la resoumettre ;
- une transition non prévue renvoie 409 ;
- la mise à jour est **conditionnelle** (`ConditionExpression` sur le statut lu). Si deux validateurs agissent en même temps, le second reçoit un 409 au lieu d'écraser le premier.

## Sécurité

- Inscription fermée : les comptes sont créés par un administrateur (`AllowAdminCreateUserOnly`), car l'identifiant client Cognito est public par nature ;
- les reçus ne transitent pas par l'API : le client les envoie directement à S3 avec une URL pré-signée (15 minutes pour l'envoi, 1 heure pour la lecture) ;
- bucket S3 privé (accès public entièrement bloqué) ;
- rôle IAM des Lambdas limité aux actions utilisées, sur la table et le bucket du projet uniquement.

## Prérequis

- .NET 10 SDK (Lambdas, tests) et .NET 9 SDK avec le workload MAUI pour le client : `dotnet workload install maui-windows`
- AWS CLI configuré : `aws configure`
- Outils Lambda : `dotnet tool install -g Amazon.Lambda.Tools`

## Déploiement

```powershell
# 1. Packager chaque Lambda et envoyer les zips dans un bucket de déploiement
foreach ($f in "CreateExpense","GetExpenses","GetExpenseById","UpdateExpenseStatus","GeneratePresignedUrl") {
  dotnet lambda package --project-location lambda-functions/$f --output-package deploy/$f.zip
  aws s3 cp deploy/$f.zip s3://MON-BUCKET-DE-DEPLOIEMENT/
}

# 2. Créer ou mettre à jour la stack
aws cloudformation deploy `
  --template-file scripts/cloudformation.yaml `
  --stack-name expense-tracker `
  --parameter-overrides DeployBucket=MON-BUCKET-DE-DEPLOIEMENT `
  --capabilities CAPABILITY_IAM CAPABILITY_NAMED_IAM `
  --region eu-west-1
```

### Créer les utilisateurs

```powershell
$pool = aws cloudformation describe-stacks --stack-name expense-tracker `
  --query "Stacks[0].Outputs[?OutputKey=='UserPoolId'].OutputValue" --output text

aws cognito-idp admin-create-user --user-pool-id $pool --username employe@exemple.fr
aws cognito-idp admin-add-user-to-group --user-pool-id $pool --username employe@exemple.fr --group-name employees
```

### Configurer l'application

Recopier les sorties de la stack (`ApiUrl`, `UserPoolId`, `UserPoolClientId`) dans `maui-app/Services/ApiConfig.cs` :

```powershell
aws cloudformation describe-stacks --stack-name expense-tracker --query "Stacks[0].Outputs"
```

## Lancer l'application

```powershell
cd maui-app
dotnet run --framework net9.0-windows10.0.19041.0
```

## Tests

```bash
dotnet test tests/ExpenseTracker.Tests
```

Tests xUnit de la machine à états : transitions autorisées et interdites, droits par rôle, motif de rejet obligatoire, action inconnue. La CI GitHub Actions compile les 5 Lambdas, lance les tests et vérifie le template avec `cfn-lint`.

## Licence

MIT
