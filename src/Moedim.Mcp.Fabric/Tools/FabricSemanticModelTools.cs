using ModelContextProtocol.Server;
using Moedim.Mcp.Fabric.Services;
using System.ComponentModel;

namespace Moedim.Mcp.Fabric.Tools;

/// <summary>
/// Tools for accessing Microsoft Fabric Semantic Models via DAX queries.
/// </summary>
[McpServerToolType]
public class FabricSemanticModelTools(IFabricSemanticModelService fabricService)
{
    private readonly IFabricSemanticModelService _fabricService = fabricService ?? throw new ArgumentNullException(nameof(fabricService));

    /// <summary>
    /// Executes a DAX query against a semantic model.
    /// </summary>
    [McpServerTool(Name = "query_semantic_model")]
    [Description("Executes a DAX query against a Microsoft Fabric semantic model and returns the results")]
    public async Task<string> QuerySemanticModel(
        [Description("The DAX query to execute against the semantic model")] string daxQuery,
        [Description("Optional dataset ID. If not provided, uses the default dataset")] string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _fabricService.ExecuteDAXQueryAsync(daxQuery, datasetId, cancellationToken).ConfigureAwait(false);
        return result.Success
            ? (result.FormattedText ?? "Query executed successfully")
            : $"Error: {result.Error}";
    }

    /// <summary>
    /// Lists all semantic models (datasets) in the configured Fabric workspace.
    /// </summary>
    [McpServerTool(Name = "list_semantic_models")]
    [Description("Lists all available semantic models (datasets) in the Microsoft Fabric workspace")]
    public async Task<string> ListSemanticModels(CancellationToken cancellationToken = default)
    {
        var result = await _fabricService.ListDatasetsAsync(cancellationToken).ConfigureAwait(false);
        return result.Success
            ? (result.FormattedText ?? "No datasets found")
            : $"Error: {result.Error}";
    }

    /// <summary>
    /// Gets the metadata (tables and columns) for a semantic model.
    /// </summary>
    [McpServerTool(Name = "get_semantic_model_metadata")]
    [Description("Retrieves table/column schema for a Microsoft Fabric semantic model (use when user asks for fields; requires Push API datasets)")]
    public async Task<string> GetSemanticModelMetadata(
        [Description("Optional dataset ID. If not provided, uses the default dataset")] string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _fabricService.GetSemanticModelMetadataAsync(datasetId, cancellationToken).ConfigureAwait(false);
        return result.Success
            ? (result.FormattedText ?? "No metadata found")
            : $"Error: {result.Error}";
    }

    /// <summary>
    /// Gets detailed information about a specific dataset.
    /// </summary>
    [McpServerTool(Name = "get_dataset_details")]
    [Description("Retrieves full dataset info (name, URL, owner, created, storage, refresh, security, scale-out); use when asked for all info about a dataset")]
    public async Task<string> GetDatasetDetails(
        [Description("Optional dataset ID. If not provided, uses the default dataset")] string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _fabricService.GetDatasetDetailsAsync(datasetId, cancellationToken).ConfigureAwait(false);
        return result.Success
            ? (result.FormattedText ?? "No details found")
            : $"Error: {result.Error}";
    }

    /// <summary>
    /// Gets datasources configured for a dataset.
    /// </summary>
    [McpServerTool(Name = "get_dataset_datasources")]
    [Description("Lists all datasources configured for a Microsoft Fabric dataset")]
    public async Task<string> GetDatasetDatasources(
        [Description("Optional dataset ID. If not provided, uses the default dataset")] string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _fabricService.GetDatasourcesAsync(datasetId, cancellationToken).ConfigureAwait(false);
        return result.Success
            ? (result.FormattedText ?? "No datasources found")
            : $"Error: {result.Error}";
    }

    /// <summary>
    /// Gets parameters defined for a dataset.
    /// </summary>
    [McpServerTool(Name = "get_dataset_parameters")]
    [Description("Lists mashup parameters defined for a Microsoft Fabric dataset")]
    public async Task<string> GetDatasetParameters(
        [Description("Optional dataset ID. If not provided, uses the default dataset")] string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _fabricService.GetDatasetParametersAsync(datasetId, cancellationToken).ConfigureAwait(false);
        return result.Success
            ? (result.FormattedText ?? "No parameters found")
            : $"Error: {result.Error}";
    }

    /// <summary>
    /// Gets refresh history entries for a dataset.
    /// </summary>
    [McpServerTool(Name = "get_dataset_refresh_history")]
    [Description("Retrieves refresh history entries for a Microsoft Fabric dataset")]
    public async Task<string> GetDatasetRefreshHistory(
        [Description("Optional dataset ID. If not provided, uses the default dataset")] string? datasetId = null,
        [Description("Optional number of entries to return (default API limit applies)")] int? top = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _fabricService.GetRefreshHistoryAsync(datasetId, top, cancellationToken).ConfigureAwait(false);
        return result.Success
            ? (result.FormattedText ?? "No refresh history found")
            : $"Error: {result.Error}";
    }

    /// <summary>
    /// Gets users and principals who have access to a dataset.
    /// </summary>
    [McpServerTool(Name = "get_dataset_users")]
    [Description("Lists principals that have access to a Microsoft Fabric dataset")]
    public async Task<string> GetDatasetUsers(
        [Description("Optional dataset ID. If not provided, uses the default dataset")] string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _fabricService.GetDatasetUsersAsync(datasetId, cancellationToken).ConfigureAwait(false);
        return result.Success
            ? (result.FormattedText ?? "No users found")
            : $"Error: {result.Error}";
    }

    /// <summary>
    /// Performs an aggregation operation on a column.
    /// </summary>
    [McpServerTool(Name = "aggregate_data")]
    [Description("Performs an aggregation operation (SUM, AVG, COUNT, MIN, MAX) on a column in a semantic model table")]
    public async Task<string> AggregateData(
        [Description("The name of the table containing the column")] string tableName,
        [Description("The name of the column to aggregate")] string columnName,
        [Description("The aggregation function to apply (SUM, AVG, COUNT, MIN, MAX)")] string aggregationFunction,
        [Description("Optional dataset ID. If not provided, uses the default dataset")] string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _fabricService.AggregateDataAsync(
            tableName,
            columnName,
            aggregationFunction,
            datasetId,
            cancellationToken).ConfigureAwait(false);
        return result.Success
            ? (result.FormattedText ?? "Aggregation completed")
            : $"Error: {result.Error}";
    }

    /// <summary>
    /// Gets distinct values from a column.
    /// </summary>
    [McpServerTool(Name = "get_distinct_values")]
    [Description("Retrieves all distinct (unique) values from a specified column in a semantic model table")]
    public async Task<string> GetDistinctValues(
        [Description("The name of the table containing the column")] string tableName,
        [Description("The name of the column to get distinct values from")] string columnName,
        [Description("Optional dataset ID. If not provided, uses the default dataset")] string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _fabricService.GetDistinctValuesAsync(
            tableName,
            columnName,
            datasetId,
            cancellationToken).ConfigureAwait(false);
        return result.Success
            ? (result.FormattedText ?? "No distinct values found")
            : $"Error: {result.Error}";
    }
}
