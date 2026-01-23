using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moedim.Mcp.Fabric.Configuration;
using Moedim.Mcp.Fabric.Models;
using System.Text.Json;

namespace Moedim.Mcp.Fabric.Services;

/// <summary>
/// Service for querying Fabric Semantic Models via REST API.
/// Handles DAX query execution, metadata retrieval, and data aggregation.
/// </summary>
public class FabricSemanticModelService : IFabricSemanticModelService
{
    private readonly string _workspaceId;
    private readonly string? _defaultDatasetId;
    private readonly string _apiBaseUrl;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<FabricSemanticModelService> _logger;

    /// <summary>
    /// Initializes a new instance of the FabricSemanticModelService.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients</param>
    /// <param name="options">Fabric configuration options</param>
    /// <param name="tokenProvider">Token provider for Fabric API authentication</param>
    /// <param name="logger">Logger for service diagnostics</param>
    public FabricSemanticModelService(
        IHttpClientFactory httpClientFactory,
        IOptions<FabricOptions> options,
        ITokenProvider tokenProvider,
        ILogger<FabricSemanticModelService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var fabricOptions = options.Value;
        _workspaceId = fabricOptions.WorkspaceId;
        _defaultDatasetId = fabricOptions.DefaultDatasetId;
        _apiBaseUrl = fabricOptions.ApiBaseUrl;

        _logger.LogDebug(
            "FabricSemanticModelService initialized. WorkspaceId: {WorkspaceId}, DefaultDatasetId: {DefaultDatasetId}, ApiBaseUrl: {ApiBaseUrl}",
            _workspaceId,
            _defaultDatasetId ?? "(not set)",
            _apiBaseUrl);
    }

    /// <summary>
    /// Executes a DAX query against a semantic model.
    /// </summary>
    public async Task<FabricResponse<QueryResult>> ExecuteDAXQueryAsync(
        string daxQuery,
        string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var id = datasetId ?? _defaultDatasetId;
            if (string.IsNullOrEmpty(id))
                return new FabricResponse<QueryResult>
                {
                    Success = false,
                    Error = "Dataset ID not provided and no default dataset configured"
                };

            var requestBody = new
            {
                queries = new[]
                {
                    new { query = daxQuery }
                },
                serializerSettings = new
                {
                    includeNulls = false
                }
            };
            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var url = $"{_apiBaseUrl}/groups/{_workspaceId}/datasets/{id}/executeQueries";
            var response = await PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new FabricResponse<QueryResult>
                {
                    Success = false,
                    Error = $"Query execution failed: {response.StatusCode} - {error}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = ParseQueryResult(responseBody);

            return new FabricResponse<QueryResult>
            {
                Success = true,
                Data = result,
                FormattedText = FormatQueryResult(result)
            };
        }
        catch (Exception ex)
        {
            return new FabricResponse<QueryResult>
            {
                Success = false,
                Error = $"Exception executing DAX query: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets metadata for a semantic model.
    /// </summary>
    public async Task<FabricResponse<List<TableMetadata>>> GetSemanticModelMetadataAsync(
        string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var id = datasetId ?? _defaultDatasetId;
            if (string.IsNullOrEmpty(id))
                return new FabricResponse<List<TableMetadata>>
                {
                    Success = false,
                    Error = "Dataset ID not provided and no default dataset configured"
                };

            var url = $"{_apiBaseUrl}/groups/{_workspaceId}/datasets/{id}/tables";
            var response = await GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new FabricResponse<List<TableMetadata>>
                {
                    Success = false,
                    Error = $"Failed to get metadata: {response.StatusCode} - {error}\nURL: {url}\nDataset ID: {id}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var tables = ParseTableMetadata(responseBody);

            return new FabricResponse<List<TableMetadata>>
            {
                Success = true,
                Data = tables,
                FormattedText = FormatTableMetadata(tables)
            };
        }
        catch (Exception ex)
        {
            return new FabricResponse<List<TableMetadata>>
            {
                Success = false,
                Error = $"Exception getting metadata: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Lists all datasets in the workspace.
    /// </summary>
    public async Task<FabricResponse<List<DatasetInfo>>> ListDatasetsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_apiBaseUrl}/groups/{_workspaceId}/datasets";
            var response = await GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new FabricResponse<List<DatasetInfo>>
                {
                    Success = false,
                    Error = $"Failed to list datasets: {response.StatusCode} - {error}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var datasets = ParseDatasets(responseBody);

            return new FabricResponse<List<DatasetInfo>>
            {
                Success = true,
                Data = datasets,
                FormattedText = FormatDatasets(datasets)
            };
        }
        catch (Exception ex)
        {
            return new FabricResponse<List<DatasetInfo>>
            {
                Success = false,
                Error = $"Exception listing datasets: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets detailed information about a specific dataset.
    /// </summary>
    public async Task<FabricResponse<DatasetInfo>> GetDatasetDetailsAsync(
        string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var id = datasetId ?? _defaultDatasetId;
            if (string.IsNullOrEmpty(id))
                return new FabricResponse<DatasetInfo>
                {
                    Success = false,
                    Error = "Dataset ID not provided and no default dataset configured"
                };

            var url = $"{_apiBaseUrl}/groups/{_workspaceId}/datasets/{id}";
            var response = await GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new FabricResponse<DatasetInfo>
                {
                    Success = false,
                    Error = $"Failed to get dataset details: {response.StatusCode} - {error}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var dataset = ParseDatasetDetails(responseBody);

            return new FabricResponse<DatasetInfo>
            {
                Success = true,
                Data = dataset,
                FormattedText = FormatDatasetDetails(dataset)
            };
        }
        catch (Exception ex)
        {
            return new FabricResponse<DatasetInfo>
            {
                Success = false,
                Error = $"Exception getting dataset details: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets datasources configured for a dataset.
    /// </summary>
    public async Task<FabricResponse<List<DataSourceInfo>>> GetDatasourcesAsync(
        string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var id = datasetId ?? _defaultDatasetId;
            if (string.IsNullOrEmpty(id))
                return new FabricResponse<List<DataSourceInfo>>
                {
                    Success = false,
                    Error = "Dataset ID not provided and no default dataset configured"
                };

            var url = $"{_apiBaseUrl}/groups/{_workspaceId}/datasets/{id}/datasources";
            var response = await GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new FabricResponse<List<DataSourceInfo>>
                {
                    Success = false,
                    Error = $"Failed to get datasources: {response.StatusCode} - {error}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var datasources = ParseDatasources(responseBody);

            return new FabricResponse<List<DataSourceInfo>>
            {
                Success = true,
                Data = datasources,
                FormattedText = FormatDatasources(datasources)
            };
        }
        catch (Exception ex)
        {
            return new FabricResponse<List<DataSourceInfo>>
            {
                Success = false,
                Error = $"Exception getting datasources: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets parameters defined for a dataset.
    /// </summary>
    public async Task<FabricResponse<List<DatasetParameter>>> GetDatasetParametersAsync(
        string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var id = datasetId ?? _defaultDatasetId;
            if (string.IsNullOrEmpty(id))
                return new FabricResponse<List<DatasetParameter>>
                {
                    Success = false,
                    Error = "Dataset ID not provided and no default dataset configured"
                };

            var url = $"{_apiBaseUrl}/groups/{_workspaceId}/datasets/{id}/parameters";
            var response = await GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new FabricResponse<List<DatasetParameter>>
                {
                    Success = false,
                    Error = $"Failed to get parameters: {response.StatusCode} - {error}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var parameters = ParseDatasetParameters(responseBody);

            return new FabricResponse<List<DatasetParameter>>
            {
                Success = true,
                Data = parameters,
                FormattedText = FormatDatasetParameters(parameters)
            };
        }
        catch (Exception ex)
        {
            return new FabricResponse<List<DatasetParameter>>
            {
                Success = false,
                Error = $"Exception getting parameters: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets refresh history entries for a dataset.
    /// </summary>
    public async Task<FabricResponse<List<DatasetRefresh>>> GetRefreshHistoryAsync(
        string? datasetId = null,
        int? top = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var id = datasetId ?? _defaultDatasetId;
            if (string.IsNullOrEmpty(id))
                return new FabricResponse<List<DatasetRefresh>>
                {
                    Success = false,
                    Error = "Dataset ID not provided and no default dataset configured"
                };

            var url = $"{_apiBaseUrl}/groups/{_workspaceId}/datasets/{id}/refreshes";
            if (top.HasValue)
            {
                url += $"?$top={top.Value}";
            }

            var response = await GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new FabricResponse<List<DatasetRefresh>>
                {
                    Success = false,
                    Error = $"Failed to get refresh history: {response.StatusCode} - {error}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var refreshes = ParseRefreshHistory(responseBody);

            return new FabricResponse<List<DatasetRefresh>>
            {
                Success = true,
                Data = refreshes,
                FormattedText = FormatRefreshHistory(refreshes)
            };
        }
        catch (Exception ex)
        {
            return new FabricResponse<List<DatasetRefresh>>
            {
                Success = false,
                Error = $"Exception getting refresh history: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets users and principals who have access to a dataset.
    /// </summary>
    public async Task<FabricResponse<List<DatasetUserAccess>>> GetDatasetUsersAsync(
        string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var id = datasetId ?? _defaultDatasetId;
            if (string.IsNullOrEmpty(id))
                return new FabricResponse<List<DatasetUserAccess>>
                {
                    Success = false,
                    Error = "Dataset ID not provided and no default dataset configured"
                };

            var url = $"{_apiBaseUrl}/groups/{_workspaceId}/datasets/{id}/users";
            var response = await GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new FabricResponse<List<DatasetUserAccess>>
                {
                    Success = false,
                    Error = $"Failed to get dataset users: {response.StatusCode} - {error}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var users = ParseDatasetUsers(responseBody);

            return new FabricResponse<List<DatasetUserAccess>>
            {
                Success = true,
                Data = users,
                FormattedText = FormatDatasetUsers(users)
            };
        }
        catch (Exception ex)
        {
            return new FabricResponse<List<DatasetUserAccess>>
            {
                Success = false,
                Error = $"Exception getting dataset users: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Performs an aggregation on a column.
    /// </summary>
    public async Task<FabricResponse<AggregationResult>> AggregateDataAsync(
        string tableName,
        string columnName,
        string aggregationFunction,
        string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var aggregationFunc = aggregationFunction.ToUpper();
            var daxQuery = aggregationFunc switch
            {
                "SUM" => $"EVALUATE ROW(\"Total\", SUM('{tableName}'[{columnName}]))",
                "COUNT" => $"EVALUATE ROW(\"Count\", COUNT('{tableName}'[{columnName}]))",
                "AVERAGE" or "AVG" => $"EVALUATE ROW(\"Average\", AVERAGE('{tableName}'[{columnName}]))",
                "MIN" => $"EVALUATE ROW(\"Min\", MIN('{tableName}'[{columnName}]))",
                "MAX" => $"EVALUATE ROW(\"Max\", MAX('{tableName}'[{columnName}]))",
                _ => throw new ArgumentException($"Unsupported aggregation function: {aggregationFunction}")
            };

            var queryResponse = await ExecuteDAXQueryAsync(daxQuery, datasetId, cancellationToken);

            if (!queryResponse.Success || queryResponse.Data?.Rows.Count == 0)
            {
                return new FabricResponse<AggregationResult>
                {
                    Success = false,
                    Error = queryResponse.Error ?? "No results from aggregation"
                };
            }

            // Get the first value from the first row (aggregation returns single value)
            var resultValue = queryResponse.Data?.Rows[0].Values.FirstOrDefault();

            return new FabricResponse<AggregationResult>
            {
                Success = true,
                Data = new AggregationResult
                {
                    TableName = tableName,
                    ColumnName = columnName,
                    AggregationFunction = aggregationFunc,
                    Result = resultValue,
                    FormattedText = $"{aggregationFunc}({tableName}.{columnName}) = {resultValue}"
                },
                FormattedText = $"{aggregationFunc}({tableName}.{columnName}) = {resultValue}"
            };
        }
        catch (Exception ex)
        {
            return new FabricResponse<AggregationResult>
            {
                Success = false,
                Error = $"Exception during aggregation: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets distinct values from a column.
    /// </summary>
    public async Task<FabricResponse<DistinctValuesResult>> GetDistinctValuesAsync(
        string tableName,
        string columnName,
        string? datasetId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var daxQuery = $"EVALUATE DISTINCT('{tableName}'[{columnName}])";
            var queryResponse = await ExecuteDAXQueryAsync(daxQuery, datasetId, cancellationToken);

            if (!queryResponse.Success || queryResponse.Data == null)
            {
                return new FabricResponse<DistinctValuesResult>
                {
                    Success = false,
                    Error = queryResponse.Error ?? "Failed to get distinct values"
                };
            }

            // Extract all values from the row dictionaries
            var values = queryResponse.Data.Rows
                .SelectMany(row => row.Values)
                .ToList();

            return new FabricResponse<DistinctValuesResult>
            {
                Success = true,
                Data = new DistinctValuesResult
                {
                    TableName = tableName,
                    ColumnName = columnName,
                    Values = values,
                    FormattedText = FormatDistinctValues(tableName, columnName, values)
                },
                FormattedText = FormatDistinctValues(tableName, columnName, values)
            };
        }
        catch (Exception ex)
        {
            return new FabricResponse<DistinctValuesResult>
            {
                Success = false,
                Error = $"Exception getting distinct values: {ex.Message}"
            };
        }
    }

    // Private helper methods

    private async Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("FabricAPI");
        await SetAuthorizationHeaderAsync(client, cancellationToken);

        var response = await client.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("GET request failed. Response body: {ErrorContent}", errorContent);
        }

        return response;
    }

    private async Task<HttpResponseMessage> PostAsync(string url, HttpContent content, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("FabricAPI");
        await SetAuthorizationHeaderAsync(client, cancellationToken);

        var response = await client.PostAsync(url, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("POST request failed. Response body: {ErrorContent}", errorContent);
        }

        return response;
    }

    private async Task SetAuthorizationHeaderAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private QueryResult ParseQueryResult(string json)
    {
        var result = new QueryResult();
        try
        {
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var resultItem in results.EnumerateArray())
                    {
                        if (resultItem.TryGetProperty("tables", out var tables) && tables.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var table in tables.EnumerateArray())
                            {
                                if (table.TryGetProperty("columns", out var columns))
                                {
                                    foreach (var col in columns.EnumerateArray())
                                    {
                                        if (col.TryGetProperty("name", out var name))
                                        {
                                            result.Columns.Add(name.GetString() ?? string.Empty);
                                        }
                                    }
                                }

                                if (table.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var row in rows.EnumerateArray())
                                    {
                                        var rowDict = new Dictionary<string, object?>();

                                        if (row.ValueKind == JsonValueKind.Object)
                                        {
                                            foreach (var prop in row.EnumerateObject())
                                            {
                                                rowDict[prop.Name] = ConvertElement(prop.Value);
                                            }

                                            if (result.Columns.Count == 0 && rowDict.Count > 0)
                                            {
                                                result.Columns.AddRange(rowDict.Keys);
                                            }
                                        }
                                        else if (row.ValueKind == JsonValueKind.Array)
                                        {
                                            var values = row
                                                .EnumerateArray()
                                                .Select(ConvertElement)
                                                .ToList();

                                            if (result.Columns.Count < values.Count)
                                            {
                                                for (var i = result.Columns.Count; i < values.Count; i++)
                                                {
                                                    result.Columns.Add($"Column{i + 1}");
                                                }
                                            }

                                            for (var i = 0; i < values.Count; i++)
                                            {
                                                rowDict[result.Columns[i]] = values[i];
                                            }
                                        }

                                        if (rowDict.Count > 0)
                                        {
                                            result.Rows.Add(rowDict);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Parse error - return empty result
        }
        return result;
    }

    private static object? ConvertElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out var longVal) ? longVal : element.GetDecimal(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private List<TableMetadata> ParseTableMetadata(string json)
    {
        var tables = new List<TableMetadata>();
        try
        {
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("value", out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var tableItem in value.EnumerateArray())
                    {
                        var table = new TableMetadata();
                        if (tableItem.TryGetProperty("name", out var name))
                            table.Name = name.GetString();
                        if (tableItem.TryGetProperty("displayName", out var displayName))
                            table.DisplayName = displayName.GetString();

                        if (tableItem.TryGetProperty("columns", out var columns) && columns.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var col in columns.EnumerateArray())
                            {
                                var column = new ColumnMetadata();
                                if (col.TryGetProperty("name", out var colName))
                                    column.Name = colName.GetString();
                                if (col.TryGetProperty("displayName", out var colDisplayName))
                                    column.DisplayName = colDisplayName.GetString();
                                if (col.TryGetProperty("dataType", out var dataType))
                                    column.DataType = dataType.GetString();

                                table.Columns.Add(column);
                            }
                        }
                        tables.Add(table);
                    }
                }
            }
        }
        catch
        {
            // Parse error
        }
        return tables;
    }

    private List<DatasetInfo> ParseDatasets(string json)
    {
        var datasets = new List<DatasetInfo>();
        try
        {
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("value", out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var dsItem in value.EnumerateArray())
                    {
                        var dataset = new DatasetInfo();
                        if (dsItem.TryGetProperty("id", out var id))
                            dataset.Id = id.GetString();
                        if (dsItem.TryGetProperty("name", out var name))
                            dataset.Name = name.GetString();
                        if (dsItem.TryGetProperty("webUrl", out var webUrl))
                            dataset.WebUrl = webUrl.GetString();
                        datasets.Add(dataset);
                    }
                }
            }
        }
        catch
        {
            // Parse error
        }
        return datasets;
    }

    private string FormatQueryResult(QueryResult result)
    {
        if (result.Rows.Count == 0)
            return "No results";

        var lines = new List<string> { string.Join(" | ", result.Columns) };
        foreach (var row in result.Rows)
        {
            var values = result.Columns.Select(col => row.TryGetValue(col, out var val) ? val?.ToString() ?? "" : "");
            lines.Add(string.Join(" | ", values));
        }
        return string.Join("\n", lines);
    }

    private string FormatTableMetadata(List<TableMetadata> tables)
    {
        var lines = new List<string>();
        foreach (var table in tables)
        {
            lines.Add($"Table: {table.DisplayName ?? table.Name}");
            foreach (var col in table.Columns)
            {
                lines.Add($"  - {col.DisplayName ?? col.Name} ({col.DataType})");
            }
        }
        return string.Join("\n", lines);
    }

    private string FormatDatasets(List<DatasetInfo> datasets)
    {
        return string.Join("\n", datasets.Select(ds => $"• {ds.Name} (ID: {ds.Id})"));
    }

    private DatasetInfo ParseDatasetDetails(string json)
    {
        var dataset = new DatasetInfo();
        try
        {
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;

                if (root.TryGetProperty("id", out var id))
                    dataset.Id = id.GetString();
                if (root.TryGetProperty("name", out var name))
                    dataset.Name = name.GetString();
                if (root.TryGetProperty("webUrl", out var webUrl))
                    dataset.WebUrl = webUrl.GetString();
                if (root.TryGetProperty("addRowsAPIEnabled", out var addRowsAPIEnabled))
                    dataset.AddRowsAPIEnabled = addRowsAPIEnabled.GetBoolean();
                if (root.TryGetProperty("configuredBy", out var configuredBy))
                    dataset.ConfiguredBy = configuredBy.GetString();
                if (root.TryGetProperty("isRefreshable", out var isRefreshable))
                    dataset.IsRefreshable = isRefreshable.GetBoolean();
                if (root.TryGetProperty("isEffectiveIdentityRequired", out var isEffectiveIdentityRequired))
                    dataset.IsEffectiveIdentityRequired = isEffectiveIdentityRequired.GetBoolean();
                if (root.TryGetProperty("isEffectiveIdentityRolesRequired", out var isEffectiveIdentityRolesRequired))
                    dataset.IsEffectiveIdentityRolesRequired = isEffectiveIdentityRolesRequired.GetBoolean();
                if (root.TryGetProperty("isOnPremGatewayRequired", out var isOnPremGatewayRequired))
                    dataset.IsOnPremGatewayRequired = isOnPremGatewayRequired.GetBoolean();
                if (root.TryGetProperty("targetStorageMode", out var targetStorageMode))
                    dataset.TargetStorageMode = targetStorageMode.GetString();
                if (root.TryGetProperty("createdDate", out var createdDate))
                    dataset.CreatedDate = createdDate.GetDateTime();
                if (root.TryGetProperty("createReportEmbedURL", out var createReportEmbedURL))
                    dataset.CreateReportEmbedURL = createReportEmbedURL.GetString();
                if (root.TryGetProperty("qnaEmbedURL", out var qnaEmbedURL))
                    dataset.QnaEmbedURL = qnaEmbedURL.GetString();

                if (root.TryGetProperty("queryScaleOutSettings", out var qsoSettings) && qsoSettings.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    dataset.QueryScaleOutSettings = new QueryScaleOutSettings();
                    if (qsoSettings.TryGetProperty("autoSyncReadOnlyReplicas", out var autoSync))
                        dataset.QueryScaleOutSettings.AutoSyncReadOnlyReplicas = autoSync.GetBoolean();
                    if (qsoSettings.TryGetProperty("maxReadOnlyReplicas", out var maxReplicas))
                        dataset.QueryScaleOutSettings.MaxReadOnlyReplicas = maxReplicas.GetInt32();
                }
            }
        }
        catch
        {
            // Parse error
        }
        return dataset;
    }

    private string FormatDatasetDetails(DatasetInfo dataset)
    {
        var lines = new List<string>
        {
            $"Dataset: {dataset.Name}",
            $"ID: {dataset.Id}",
            $"Web URL: {dataset.WebUrl}"
        };

        if (dataset.ConfiguredBy != null)
            lines.Add($"Configured By: {dataset.ConfiguredBy}");
        if (dataset.CreatedDate.HasValue)
            lines.Add($"Created Date: {dataset.CreatedDate.Value:yyyy-MM-dd HH:mm:ss}");
        if (dataset.TargetStorageMode != null)
            lines.Add($"Storage Mode: {dataset.TargetStorageMode}");
        if (dataset.IsRefreshable.HasValue)
            lines.Add($"Is Refreshable: {dataset.IsRefreshable.Value}");
        if (dataset.AddRowsAPIEnabled.HasValue)
            lines.Add($"Push API Enabled: {dataset.AddRowsAPIEnabled.Value}");
        if (dataset.IsEffectiveIdentityRequired.HasValue)
            lines.Add($"Effective Identity Required: {dataset.IsEffectiveIdentityRequired.Value}");
        if (dataset.IsEffectiveIdentityRolesRequired.HasValue)
            lines.Add($"Effective Identity Roles Required: {dataset.IsEffectiveIdentityRolesRequired.Value}");
        if (dataset.IsOnPremGatewayRequired.HasValue)
            lines.Add($"On-Prem Gateway Required: {dataset.IsOnPremGatewayRequired.Value}");

        if (dataset.QueryScaleOutSettings != null)
        {
            lines.Add($"Query Scale-Out Settings:");
            lines.Add($"  Auto Sync Read-Only Replicas: {dataset.QueryScaleOutSettings.AutoSyncReadOnlyReplicas}");
            lines.Add($"  Max Read-Only Replicas: {dataset.QueryScaleOutSettings.MaxReadOnlyReplicas}");
        }

        return string.Join("\n", lines);
    }

    private string FormatDistinctValues(string tableName, string columnName, List<object?> values)
    {
        var valueStrings = values.Select(v => v?.ToString() ?? "NULL");
        return $"{tableName}.{columnName}: {string.Join(", ", valueStrings)}";
    }

    private List<DataSourceInfo> ParseDatasources(string json)
    {
        var datasources = new List<DataSourceInfo>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
            {
                foreach (var dsItem in value.EnumerateArray())
                {
                    var datasource = new DataSourceInfo();
                    if (dsItem.TryGetProperty("datasourceType", out var type))
                        datasource.DatasourceType = type.GetString();
                    if (dsItem.TryGetProperty("datasourceId", out var dsId))
                        datasource.DatasourceId = dsId.GetString();
                    if (dsItem.TryGetProperty("gatewayId", out var gatewayId))
                        datasource.GatewayId = gatewayId.GetString();
                    if (dsItem.TryGetProperty("connectionString", out var connectionString))
                        datasource.ConnectionString = connectionString.GetString();
                    if (dsItem.TryGetProperty("name", out var name))
                        datasource.Name = name.GetString();

                    if (dsItem.TryGetProperty("connectionDetails", out var details) && details.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in details.EnumerateObject())
                        {
                            datasource.ConnectionDetails[property.Name] = property.Value.ValueKind switch
                            {
                                JsonValueKind.String => property.Value.GetString(),
                                JsonValueKind.Number => property.Value.ToString(),
                                JsonValueKind.True => "true",
                                JsonValueKind.False => "false",
                                _ => property.Value.GetRawText()
                            };
                        }
                    }

                    datasources.Add(datasource);
                }
            }
        }
        catch
        {
            // Parse error
        }

        return datasources;
    }

    private List<DatasetParameter> ParseDatasetParameters(string json)
    {
        var parameters = new List<DatasetParameter>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
            {
                foreach (var parameterItem in value.EnumerateArray())
                {
                    var parameter = new DatasetParameter();
                    if (parameterItem.TryGetProperty("name", out var name))
                        parameter.Name = name.GetString();
                    if (parameterItem.TryGetProperty("type", out var type))
                        parameter.Type = type.GetString();
                    if (parameterItem.TryGetProperty("isRequired", out var isRequired))
                        parameter.IsRequired = isRequired.GetBoolean();
                    if (parameterItem.TryGetProperty("currentValue", out var currentValue))
                        parameter.CurrentValue = currentValue.GetString();
                    if (parameterItem.TryGetProperty("suggestedValues", out var suggestedValues) && suggestedValues.ValueKind == JsonValueKind.Array)
                    {
                        parameter.SuggestedValues.AddRange(suggestedValues.EnumerateArray().Select(v => v.GetString() ?? string.Empty));
                    }

                    parameters.Add(parameter);
                }
            }
        }
        catch
        {
            // Parse error
        }

        return parameters;
    }

    private List<DatasetRefresh> ParseRefreshHistory(string json)
    {
        var refreshes = new List<DatasetRefresh>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
            {
                foreach (var refreshItem in value.EnumerateArray())
                {
                    var refresh = new DatasetRefresh();
                    if (refreshItem.TryGetProperty("refreshType", out var refreshType))
                        refresh.RefreshType = refreshType.GetString();
                    if (refreshItem.TryGetProperty("startTime", out var startTime) && startTime.ValueKind == JsonValueKind.String && DateTime.TryParse(startTime.GetString(), out var start))
                        refresh.StartTime = start;
                    if (refreshItem.TryGetProperty("endTime", out var endTime) && endTime.ValueKind == JsonValueKind.String && DateTime.TryParse(endTime.GetString(), out var end))
                        refresh.EndTime = end;
                    if (refreshItem.TryGetProperty("status", out var status))
                        refresh.Status = status.GetString();
                    if (refreshItem.TryGetProperty("requestId", out var requestId))
                        refresh.RequestId = requestId.GetString();
                    if (refreshItem.TryGetProperty("serviceExceptionJson", out var serviceExceptionJson))
                        refresh.ServiceExceptionJson = serviceExceptionJson.GetString();

                    if (refreshItem.TryGetProperty("refreshAttempts", out var attempts) && attempts.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var attemptItem in attempts.EnumerateArray())
                        {
                            var attempt = new RefreshAttempt();
                            if (attemptItem.TryGetProperty("attemptId", out var attemptId) && attemptId.ValueKind == JsonValueKind.Number)
                                attempt.AttemptId = attemptId.GetInt32();
                            if (attemptItem.TryGetProperty("startTime", out var attemptStart) && attemptStart.ValueKind == JsonValueKind.String && DateTime.TryParse(attemptStart.GetString(), out var attemptStartTime))
                                attempt.StartTime = attemptStartTime;
                            if (attemptItem.TryGetProperty("endTime", out var attemptEnd) && attemptEnd.ValueKind == JsonValueKind.String && DateTime.TryParse(attemptEnd.GetString(), out var attemptEndTime))
                                attempt.EndTime = attemptEndTime;
                            if (attemptItem.TryGetProperty("type", out var attemptType))
                                attempt.Type = attemptType.GetString();
                            if (attemptItem.TryGetProperty("serviceExceptionJson", out var attemptException))
                                attempt.ServiceExceptionJson = attemptException.GetString();

                            refresh.Attempts.Add(attempt);
                        }
                    }

                    refreshes.Add(refresh);
                }
            }
        }
        catch
        {
            // Parse error
        }

        return refreshes;
    }

    private List<DatasetUserAccess> ParseDatasetUsers(string json)
    {
        var users = new List<DatasetUserAccess>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
            {
                foreach (var userItem in value.EnumerateArray())
                {
                    var user = new DatasetUserAccess();
                    if (userItem.TryGetProperty("identifier", out var identifier))
                        user.Identifier = identifier.GetString();
                    if (userItem.TryGetProperty("principalType", out var principalType))
                        user.PrincipalType = principalType.GetString();
                    if (userItem.TryGetProperty("datasetUserAccessRight", out var accessRight))
                        user.AccessRight = accessRight.GetString();

                    users.Add(user);
                }
            }
        }
        catch
        {
            // Parse error
        }

        return users;
    }

    private string FormatDatasources(List<DataSourceInfo> datasources)
    {
        if (datasources.Count == 0)
            return "No datasources found";

        return string.Join(
            "\n",
            datasources.Select(ds =>
            {
                var details = ds.ConnectionDetails.Count > 0
                    ? string.Join(", ", ds.ConnectionDetails.Select(kv => $"{kv.Key}:{kv.Value}"))
                    : "no connection details";
                return $"{ds.DatasourceType ?? "Unknown"} (ID: {ds.DatasourceId ?? "n/a"}, Gateway: {ds.GatewayId ?? "n/a"}) | {details}";
            }));
    }

    private string FormatDatasetParameters(List<DatasetParameter> parameters)
    {
        if (parameters.Count == 0)
            return "No parameters found";

        return string.Join(
            "\n",
            parameters.Select(p =>
            {
                var suggestions = p.SuggestedValues.Count > 0
                    ? string.Join(",", p.SuggestedValues)
                    : "none";
                return $"{p.Name} ({p.Type}) Required: {p.IsRequired} Current: {p.CurrentValue ?? "n/a"} Suggested: {suggestions}";
            }));
    }

    private string FormatRefreshHistory(List<DatasetRefresh> refreshes)
    {
        if (refreshes.Count == 0)
            return "No refresh history found";

        return string.Join(
            "\n",
            refreshes.Select(r =>
            {
                var attempts = r.Attempts.Count > 0
                    ? string.Join("; ", r.Attempts.Select(a => $"Attempt {a.AttemptId}: {a.Type} {a.StartTime:u}->{a.EndTime:u}"))
                    : "No attempts";
                return $"{r.Status ?? "Unknown"} | Type: {r.RefreshType ?? "n/a"} | Start: {r.StartTime:u} | End: {r.EndTime:u} | RequestId: {r.RequestId ?? "n/a"} | Attempts: {attempts}";
            }));
    }

    private string FormatDatasetUsers(List<DatasetUserAccess> users)
    {
        if (users.Count == 0)
            return "No users found";

        return string.Join(
            "\n",
            users.Select(u => $"{u.Identifier} | {u.PrincipalType} | {u.AccessRight}"));
    }
}
