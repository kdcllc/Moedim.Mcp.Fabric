using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Moedim.Mcp.Fabric.Services;

/// <summary>
/// Token provider that extracts bearer tokens from HTTP request headers.
/// Falls back to DefaultCredentialTokenProvider when no bearer token is present.
/// When JWT authentication is enabled, leverages the validated ClaimsPrincipal for logging.
/// </summary>
public sealed class HttpContextTokenProvider : ITokenProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly DefaultCredentialTokenProvider _fallbackProvider;
    private readonly ILogger<HttpContextTokenProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the HttpContextTokenProvider.
    /// </summary>
    /// <param name="httpContextAccessor">HTTP context accessor for request headers.</param>
    /// <param name="fallbackProvider">Fallback provider when no bearer token is present.</param>
    /// <param name="logger">Logger for authentication diagnostics.</param>
    public HttpContextTokenProvider(
        IHttpContextAccessor httpContextAccessor,
        DefaultCredentialTokenProvider fallbackProvider,
        ILogger<HttpContextTokenProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _fallbackProvider = fallbackProvider ?? throw new ArgumentNullException(nameof(fallbackProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is not null)
        {
            var authorizationHeader = httpContext.Request.Headers.Authorization.ToString();

            if (!string.IsNullOrEmpty(authorizationHeader) &&
                authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authorizationHeader["Bearer ".Length..].Trim();

                if (!string.IsNullOrEmpty(token))
                {
                    return token;
                }

                _logger.LogDebug("Bearer token in header was empty after trimming");
            }
            else
            {
                _logger.LogDebug("No valid Bearer token found in Authorization header");
            }
        }
        else
        {
            _logger.LogDebug("No HTTP context available");
        }

        // Fall back to DefaultAzureCredential when no bearer token is present
        _logger.LogDebug("Falling back to DefaultAzureCredential for token acquisition");
        var fallbackToken = await _fallbackProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        return fallbackToken;
    }
}
