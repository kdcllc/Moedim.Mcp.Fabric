using System.ComponentModel.DataAnnotations;

namespace Moedim.Mcp.Fabric.Configuration;

/// <summary>
/// Strongly typed configuration for connecting to Microsoft Fabric workspaces and datasets.
/// </summary>
public sealed class FabricOptions
{
    /// <summary>
    /// Gets or sets the Fabric workspace ID.
    /// Required to identify the workspace containing semantic models.
    /// </summary>
    [Required(ErrorMessage = "Fabric:WorkspaceId is required")]
    public required string WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the default dataset (semantic model) ID.
    /// Optional; can be overridden per query.
    /// </summary>
    public string? DefaultDatasetId { get; set; }

    /// <summary>
    /// Gets or sets the base URL for the Fabric REST API.
    /// Default: https://api.powerbi.com/v1.0/myorg
    /// </summary>
    [Url(ErrorMessage = "Fabric:ApiBaseUrl must be a valid URL")]
    public string ApiBaseUrl { get; set; } = "https://api.powerbi.com/v1.0/myorg";

    /// <summary>
    /// Gets or sets the HTTP timeout in seconds for API requests.
    /// Default: 30 seconds
    /// </summary>
    [Range(1, 300, ErrorMessage = "Fabric:HttpTimeoutSeconds must be between 1 and 300")]
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to exclude Visual Studio and Visual Studio Code credentials
    /// from the DefaultAzureCredential authentication chain.
    /// Default: false (IDE credentials are included)
    /// </summary>
    public bool ExcludeIdeCredentials { get; set; } = false;

    /// <summary>
    /// Gets or sets the authentication scopes used for acquiring tokens to access the Fabric REST API.
    /// Default: []
    /// </summary>
    public string[] AuthenticationScopes { get; set; } = Array.Empty<string>();
}
