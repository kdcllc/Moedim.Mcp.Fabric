using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Moedim.Mcp.Fabric.Configuration;

namespace Moedim.Mcp.Fabric.Services;

/// <summary>
/// Token provider that uses DefaultAzureCredential for authentication.
/// Registered as singleton for cross-request token caching.
/// </summary>
public sealed class DefaultCredentialTokenProvider : ITokenProvider
{
    private static readonly string[] FabricScopes = ["https://analysis.windows.net/powerbi/api/.default"];

    private readonly TokenCredential _tokenCredential;
    private readonly object _lock = new();
    private string? _cachedAccessToken;
    private DateTimeOffset _tokenExpiryTime = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of the DefaultCredentialTokenProvider with configuration options.
    /// </summary>
    /// <param name="options">The Fabric configuration options.</param>
    public DefaultCredentialTokenProvider(IOptions<FabricOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var credentialOptions = new DefaultAzureCredentialOptions
        {
            ExcludeVisualStudioCredential = options.Value.ExcludeIdeCredentials,
            ExcludeVisualStudioCodeCredential = options.Value.ExcludeIdeCredentials
        };

        _tokenCredential = new DefaultAzureCredential(credentialOptions);
    }

    /// <summary>
    /// Initializes a new instance of the DefaultCredentialTokenProvider with default settings.
    /// </summary>
    public DefaultCredentialTokenProvider()
    {
        _tokenCredential = new DefaultAzureCredential();
    }

    /// <summary>
    /// Initializes a new instance with a custom token credential (for testing).
    /// </summary>
    /// <param name="tokenCredential">The token credential to use.</param>
    internal DefaultCredentialTokenProvider(TokenCredential tokenCredential)
    {
        _tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));
    }

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Check cache first (with buffer for token refresh)
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTimeOffset.UtcNow.AddMinutes(5) < _tokenExpiryTime)
            {
                return _cachedAccessToken;
            }
        }

        // Acquire new token
        var token = await _tokenCredential.GetTokenAsync(
            new TokenRequestContext(FabricScopes),
            cancellationToken).ConfigureAwait(false);

        lock (_lock)
        {
            _cachedAccessToken = token.Token;
            _tokenExpiryTime = token.ExpiresOn;
        }

        return token.Token;
    }
}
