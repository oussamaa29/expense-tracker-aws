namespace ExpenseTracker.Services;

// Valeurs propres à chaque déploiement : à recopier depuis les sorties de la stack
// CloudFormation (voir « Configurer l'application » dans le README).
//   aws cloudformation describe-stacks --stack-name expense-tracker --query "Stacks[0].Outputs"
public static class ApiConfig
{
    public const string ApiBaseUrl        = "https://VOTRE-API.execute-api.eu-west-1.amazonaws.com/prod"; // Output ApiUrl
    public const string CognitoUserPoolId = "eu-west-1_XXXXXXXXX";                                     // Output UserPoolId
    public const string CognitoClientId   = "VOTRE_CLIENT_ID";                                         // Output UserPoolClientId
    public const string CognitoRegion     = "eu-west-1";
}
