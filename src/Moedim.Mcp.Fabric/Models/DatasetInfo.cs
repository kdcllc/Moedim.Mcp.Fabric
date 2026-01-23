namespace Moedim.Mcp.Fabric.Models;

/// <summary>
/// Represents a dataset in the workspace.
/// </summary>
public class DatasetInfo
{
    /// <summary>
    /// Gets or sets the unique identifier of the dataset.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the dataset.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the web URL of the dataset.
    /// </summary>
    public string? WebUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the Push API is enabled for this dataset.
    /// </summary>
    public bool? AddRowsAPIEnabled { get; set; }

    /// <summary>
    /// Gets or sets the user who configured this dataset.
    /// </summary>
    public string? ConfiguredBy { get; set; }

    /// <summary>
    /// Gets or sets whether the dataset is refreshable.
    /// </summary>
    public bool? IsRefreshable { get; set; }

    /// <summary>
    /// Gets or sets whether effective identity is required.
    /// </summary>
    public bool? IsEffectiveIdentityRequired { get; set; }

    /// <summary>
    /// Gets or sets whether effective identity roles are required.
    /// </summary>
    public bool? IsEffectiveIdentityRolesRequired { get; set; }

    /// <summary>
    /// Gets or sets whether an on-premises gateway is required.
    /// </summary>
    public bool? IsOnPremGatewayRequired { get; set; }

    /// <summary>
    /// Gets or sets the target storage mode (e.g., "PremiumFiles").
    /// </summary>
    public string? TargetStorageMode { get; set; }

    /// <summary>
    /// Gets or sets the date when the dataset was created.
    /// </summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the embed URL for creating reports.
    /// </summary>
    public string? CreateReportEmbedURL { get; set; }

    /// <summary>
    /// Gets or sets the Q and A embed URL.
    /// </summary>
    public string? QnaEmbedURL { get; set; }

    /// <summary>
    /// Gets or sets the query scale-out settings.
    /// </summary>
    public QueryScaleOutSettings? QueryScaleOutSettings { get; set; }
}

/// <summary>
/// Represents query scale-out settings for a dataset.
/// </summary>
public class QueryScaleOutSettings
{
    /// <summary>
    /// Gets or sets whether to auto-sync read-only replicas.
    /// </summary>
    public bool AutoSyncReadOnlyReplicas { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of read-only replicas.
    /// </summary>
    public int MaxReadOnlyReplicas { get; set; }
}
