using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace ExpenseTracker.Services;

public class AuthService
{
    private static string? _accessToken;
    private static string? _idToken;
    private static string? _userRole;
    private static string? _userEmail;

    public static string? AccessToken => _accessToken;
    public static string? IdToken     => _idToken;
    public static string? UserRole    => _userRole;
    public static string? UserEmail   => _userEmail;
    public static bool    IsLoggedIn  => !string.IsNullOrEmpty(_accessToken);

    public async Task<(bool success, string error)> LoginAsync(string email, string password)
    {
        try
        {
            var credentials = new AnonymousAWSCredentials();
            var client = new AmazonCognitoIdentityProviderClient(
                credentials,
                Amazon.RegionEndpoint.GetBySystemName(ApiConfig.CognitoRegion));

            var request = new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                ClientId = ApiConfig.CognitoClientId,
                AuthParameters = new Dictionary<string, string>
                {
                    { "USERNAME", email },
                    { "PASSWORD", password }
                }
            };

            var response = await client.InitiateAuthAsync(request);

            _accessToken = response.AuthenticationResult.AccessToken;
            _idToken     = response.AuthenticationResult.IdToken;
            _userEmail   = email;
            _userRole    = ExtractRole(_idToken);

            return (true, string.Empty);
        }
        catch (NotAuthorizedException)
        {
            return (false, "Email ou mot de passe incorrect.");
        }
        catch (UserNotFoundException)
        {
            return (false, "Utilisateur introuvable.");
        }
        catch (Exception ex)
        {
            return (false, $"Erreur de connexion : {ex.Message}");
        }
    }

    private static string ExtractRole(string idToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(idToken);

        var groupsClaim = jwt.Claims
            .FirstOrDefault(c => c.Type == "cognito:groups");

        if (groupsClaim?.Value?.Contains("finance") == true)
            return "finance";

        return "employee";
    }

    public static void Logout()
    {
        _accessToken = null;
        _idToken     = null;
        _userRole    = null;
        _userEmail   = null;
    }
}
