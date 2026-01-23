using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Moedim.Mcp.Fabric.Configuration;

namespace Moedim.Mcp.Fabric.Services;

/// <summary>
/// Provides methods to acquire Microsoft Fabric delegated access tokens using the On-Behalf-Of flow.
/// </summary>
public class FabricOnBehalfOfTokenService : IOnBehalfOfTokenService
{
    private readonly IConfidentialClientApplication _cca;
    private readonly string[] _fabricScopes;
    private readonly ILogger<FabricOnBehalfOfTokenService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FabricOnBehalfOfTokenService"/> class using Azure AD configuration.
    /// </summary>
    /// <param name="azureAdOptions">The application configuration containing Azure AD settings.</param>
    /// <param name="fabricOptions">The application configuration containing Fabric settings.</param>
    /// <param name="logger">The logger instance for logging.</param>
    public FabricOnBehalfOfTokenService(IOptions<AzureAdOptions> azureAdOptions, IOptions<FabricOptions> fabricOptions, ILogger<FabricOnBehalfOfTokenService> logger)
    {
        var clientId = azureAdOptions.Value.ClientId;          // API app registration client id
        var clientSecret = azureAdOptions.Value.ClientSecret;  // API secret
        var tenantId = azureAdOptions.Value.TenantId;          // or derive from tid claim at runtime

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cca = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithAuthority($"https://login.microsoftonline.com/{tenantId}/v2.0")
            .WithClientSecret(clientSecret)
            .Build();

        _fabricScopes = fabricOptions.Value.AuthenticationScopes;
    }

    /// <summary>
    /// Acquires a delegated access token for Microsoft Fabric/Power BI on behalf of a user.
    /// </summary>
    /// <param name="incomingAccessToken">The user's access token to exchange.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The delegated access token for Fabric/Power BI.</returns>
    public async Task<string> GetTokenOnBehalfOfAsync(string incomingAccessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var userAssertion = new UserAssertion(incomingAccessToken);

            var result = await _cca
                .AcquireTokenOnBehalfOf(_fabricScopes, userAssertion)
                .ExecuteAsync(cancellationToken);

            return result.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring Fabric on-behalf-of token");
            throw;
        }
    }
}