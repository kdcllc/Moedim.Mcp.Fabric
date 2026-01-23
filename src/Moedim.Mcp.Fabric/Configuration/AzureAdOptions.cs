using System.ComponentModel.DataAnnotations;

namespace Moedim.Mcp.Fabric.Configuration;

/// <summary>
/// Represents configuration options for Azure Active Directory authentication.
/// </summary>
public class AzureAdOptions
{
    /// <summary>
    /// Gets or sets the Application (client) ID from Azure App Registration.
    /// This is used as the valid audience for token validation.
    /// Required when EnableValidation is true.
    /// </summary>
    [Required(ErrorMessage = "AzureAdOptions:ClientId is required")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret for the Azure App Registration.
    /// This is used for confidential client authentication.
    /// </summary>
    [Required(ErrorMessage = "AzureAdOptions:ClientSecret is required")]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure Active Directory tenant ID.
    /// </summary>
    [Required(ErrorMessage = "AzureAdOptions:TenantId is required")]
    public string TenantId { get; set; } = string.Empty;
}