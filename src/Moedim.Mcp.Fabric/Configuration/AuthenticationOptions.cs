namespace Moedim.Mcp.Fabric.Configuration;

/// <summary>
/// Configuration options for JWT Bearer authentication.
/// Used to validate incoming tokens from Microsoft Foundry Agent or other OAuth clients.
/// </summary>
public sealed class AuthenticationOptions
{
    /// <summary>
    /// Gets or sets whether JWT validation is enabled.
    /// When false, authentication middleware is not registered.
    /// Default: false
    /// </summary>
    public bool EnableValidation { get; set; } = false;

    /// <summary>
    /// Gets or sets the Application (client) ID from Azure App Registration.
    /// This is used as the valid audience for token validation.
    /// Required when EnableValidation is true.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the list of allowed tenant IDs (GUIDs) for cross-tenant authentication.
    /// Tokens from tenants not in this list will be rejected.
    /// Required when EnableValidation is true.
    /// </summary>
    public string[] AllowedTenants { get; set; } = [];

    /// <summary>
    /// Gets or sets the OpenID Connect metadata endpoint URL.
    /// Used for automatic signing key rotation.
    /// Default: https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration
    /// </summary>
    public string MetadataAddress { get; set; } = "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration";
}
