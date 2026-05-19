# Prompt pour Claude Code — Build the .NET MAUI App

## Context

Tu dois créer l'application .NET MAUI pour un projet scolaire "Expense Report Tracker".
Le backend (Lambda C# + DynamoDB + S3 + Cognito + API Gateway) est déjà codé et prêt à déployer.
Tu dois construire le client MAUI qui consomme cette API.

## Architecture backend (déjà faite)

### API Endpoints

```
BASE_URL = https://<api-id>.execute-api.<region>.amazonaws.com/prod

POST   /expenses               → Créer une expense
GET    /expenses               → Lister (employee=les siennes, finance=toutes en Submitted)
GET    /expenses/{expenseId}   → Détail d'une expense
PATCH  /expenses/{expenseId}   → Changer le statut (state machine)
POST   /presigned-url          → Obtenir URL S3 pour upload/download reçu
```

Toutes les requêtes nécessitent un header `Authorization: Bearer <JWT_TOKEN>` (Cognito).

### Request/Response formats

**POST /expenses**
```json
// Request
{ "amount": 340.00, "category": "meals", "description": "Client lunch", "status": "Submitted" }
// Response
{ "statusCode": 201, "data": { "expenseId": "...", "status": "Submitted", ... } }
```

**GET /expenses** → `{ "statusCode": 200, "data": [ { expense }, ... ] }`

**PATCH /expenses/{id}**
```json
// Request
{ "action": "approve", "comment": "Looks good" }
// or
{ "action": "reject", "comment": "Missing receipt" }
// or
{ "action": "resubmit" }
// Response
{ "statusCode": 200, "data": { "previousStatus": "Submitted", "newStatus": "Approved" } }
```

**POST /presigned-url**
```json
// Request
{ "expenseId": "...", "fileName": "receipt.jpg", "operation": "upload" }
// Response
{ "statusCode": 200, "data": { "url": "https://s3...", "objectKey": "receipts/..." } }
```

### Cognito Config
- User Pool avec deux groupes: `employees` et `finance`
- Auth flow: USER_PASSWORD_AUTH
- Le JWT contient `cognito:groups` pour le RBAC

### State Machine (enforced server-side)
```
Draft → Submitted (employee)
Submitted → Approved (finance)
Submitted → Rejected (finance, comment obligatoire)
Rejected → Resubmitted (employee, owner only)
Resubmitted → Submitted (employee)
```

## Ce que tu dois construire

### 1. Projet .NET MAUI

Crée un projet .NET 8 MAUI avec cette structure :

```
maui-app/
├── ExpenseTracker.csproj
├── MauiProgram.cs
├── App.xaml / App.xaml.cs
├── AppShell.xaml / AppShell.xaml.cs
├── Models/
│   ├── ExpenseReport.cs
│   ├── LoginRequest.cs
│   └── ApiResponse.cs
├── Services/
│   ├── ApiConfig.cs          ← URLs et IDs Cognito (à remplir par l'étudiant)
│   ├── AuthService.cs        ← Login Cognito, stockage JWT, détection du rôle
│   ├── ExpenseService.cs     ← CRUD expenses via API
│   └── ReceiptService.cs     ← Upload/download reçu via pre-signed URL
├── ViewModels/
│   ├── LoginViewModel.cs
│   ├── EmployeeViewModel.cs
│   └── FinanceViewModel.cs
├── Views/
│   ├── LoginPage.xaml
│   ├── EmployeePage.xaml      ← Liste + création d'expenses
│   ├── ExpenseDetailPage.xaml ← Détail + upload reçu
│   └── FinancePage.xaml       ← Queue de review + approve/reject
└── Resources/
    └── (icons, images, styles)
```

### 2. Fonctionnalités

#### Login Page
- Email + password
- Appel Cognito `InitiateAuth` (USER_PASSWORD_AUTH)
- Stocker le JWT (AccessToken + IdToken)
- Détecter le groupe (`cognito:groups` dans le IdToken décodé)
- Router vers EmployeePage ou FinancePage selon le rôle

#### Employee Page
- Liste des expenses de l'employé (GET /expenses)
- Chaque item affiche: montant, catégorie, statut (avec couleur), date
- Bouton "+" pour créer une nouvelle expense
- Formulaire: montant (numérique), catégorie (picker: travel/meals/equipment/other), description (texte)
- Option de soumettre directement (status="Submitted") ou sauvegarder en brouillon (status="Draft")
- Sur une expense rejetée: bouton "Resoumettre"

#### Expense Detail Page
- Affiche tous les détails de l'expense
- Si pas de reçu: bouton "Ajouter un reçu" → ouvre la caméra/galerie, upload via pre-signed URL
- Si reçu existe: affiche l'image du reçu (download via pre-signed URL GET)
- Si rejetée: affiche le commentaire du reviewer + bouton resoumettre
- Statut avec code couleur (Draft=gris, Submitted=bleu, Approved=vert, Rejected=rouge)

#### Finance Page
- Liste des expenses en attente (GET /expenses?status=Submitted)
- Chaque item: employé (email), montant, catégorie, date
- Tap → détail avec image du reçu
- Deux boutons: "Approuver" (vert) et "Rejeter" (rouge)
- Rejeter → popup pour saisir un commentaire obligatoire

### 3. Configuration

Crée un fichier `ApiConfig.cs` avec des placeholders clairs :

```csharp
public static class ApiConfig
{
    // ═══ À REMPLIR APRÈS DÉPLOIEMENT AWS ═══
    public const string ApiBaseUrl = "https://YOUR_API_ID.execute-api.YOUR_REGION.amazonaws.com/prod";
    public const string CognitoUserPoolId = "YOUR_REGION_YOUR_POOL_ID";
    public const string CognitoClientId = "YOUR_CLIENT_ID";
    public const string CognitoRegion = "eu-west-1";
}
```

### 4. NuGet Packages nécessaires

```xml
<PackageReference Include="Amazon.Extensions.CognitoAuthentication" Version="2.*" />
<PackageReference Include="AWSSDK.CognitoIdentityProvider" Version="3.*" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.*" />
```

### 5. Style

- Material Design feel
- Couleurs de statut: Draft=#9E9E9E, Submitted=#2196F3, Approved=#4CAF50, Rejected=#F44336, Resubmitted=#FF9800
- Police système
- Navigation: Shell avec tabs (Employee) ou simple stack (Finance)

### 6. Important

- Le RBAC est enforced côté serveur (Lambda), mais le client doit aussi cacher les boutons non-pertinents (UX)
- Les pre-signed URLs expirent (15min upload, 60min download)
- Utilise HttpClient pour les appels API
- Gère les erreurs gracieusement (toast/alert pour les erreurs réseau)
- Le projet doit compiler et tourner sur Windows au minimum (pour la démo)
