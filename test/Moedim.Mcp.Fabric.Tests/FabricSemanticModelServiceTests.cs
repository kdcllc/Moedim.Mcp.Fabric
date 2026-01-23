using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moedim.Mcp.Fabric.Configuration;
using Moedim.Mcp.Fabric.Services;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace Moedim.Mcp.Fabric.Tests;

public class FabricSemanticModelServiceTests
{
    [Fact]
    public async Task ExecuteDAXQueryAsync_WithValidResponse_ReturnsParsedResult()
    {
        var handler = new StubHttpMessageHandler(CreateJsonResponse(SampleQueryResultJson()));
        var service = CreateService(handler);

        var response = await service.ExecuteDAXQueryAsync("EVALUATE ROW('Total')");

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Rows.Should().HaveCount(2);
        response.FormattedText.Should().Contain("Total");
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteDAXQueryAsync_WithoutDatasetId_ReturnsError()
    {
        var handler = new StubHttpMessageHandler();
        var service = CreateService(handler, defaultDatasetId: null);

        var response = await service.ExecuteDAXQueryAsync("EVALUATE ROW('Total')");

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Dataset ID not provided");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ListDatasetsAsync_WithDatasets_ReturnsFormattedList()
    {
        var handler = new StubHttpMessageHandler(CreateJsonResponse(SampleDatasetsJson()));
        var service = CreateService(handler);

        var response = await service.ListDatasetsAsync();

        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(2);
        response.FormattedText.Should().Contain("Sales");
    }

      [Fact]
      public async Task ListDatasetsAsync_WhenRequestFails_ReturnsError()
      {
        var handler = new StubHttpMessageHandler(CreateJsonResponse("{}", HttpStatusCode.BadRequest));
        var service = CreateService(handler);

        var response = await service.ListDatasetsAsync();

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Failed to list datasets");
      }

    [Fact]
    public async Task GetSemanticModelMetadataAsync_WithValidResponse_ReturnsTables()
    {
        var handler = new StubHttpMessageHandler(CreateJsonResponse(SampleTableMetadataJson()));
        var service = CreateService(handler);

        var response = await service.GetSemanticModelMetadataAsync();

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data![0].Columns[0].DataType.Should().Be("Double");
        response.FormattedText.Should().Contain("Sales Table");
    }

      [Fact]
      public async Task GetSemanticModelMetadataAsync_WhenRequestFails_ReturnsError()
      {
        var handler = new StubHttpMessageHandler(CreateJsonResponse("{}", HttpStatusCode.BadRequest));
        var service = CreateService(handler);

        var response = await service.GetSemanticModelMetadataAsync();

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Failed to get metadata");
      }

    [Fact]
    public async Task GetDatasetDetailsAsync_WithValidResponse_MapsProperties()
    {
        var handler = new StubHttpMessageHandler(CreateJsonResponse(SampleDatasetDetailsJson()));
        var service = CreateService(handler);

        var response = await service.GetDatasetDetailsAsync();

        response.Success.Should().BeTrue();
        response.Data!.ConfiguredBy.Should().Be("owner@contoso.com");
        response.Data.QueryScaleOutSettings!.MaxReadOnlyReplicas.Should().Be(3);
        response.FormattedText.Should().Contain("owner@contoso.com");
    }

      [Fact]
      public async Task GetDatasetDetailsAsync_WhenDatasetMissing_ReturnsError()
      {
        var handler = new StubHttpMessageHandler();
        var service = CreateService(handler, defaultDatasetId: null);

        var response = await service.GetDatasetDetailsAsync();

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Dataset ID not provided");
      }

    [Fact]
    public async Task GetDatasourcesAsync_WithConnectionDetails_ParsesDictionary()
    {
        var handler = new StubHttpMessageHandler(CreateJsonResponse(SampleDatasourcesJson()));
        var service = CreateService(handler);

        var response = await service.GetDatasourcesAsync();

        response.Success.Should().BeTrue();
        response.Data!.Single().ConnectionDetails["server"].Should().Be("localhost");
        response.FormattedText.Should().Contain("Sql");
    }

      [Fact]
      public async Task GetDatasourcesAsync_WhenRequestFails_ReturnsError()
      {
        var handler = new StubHttpMessageHandler(CreateJsonResponse("{}", HttpStatusCode.BadRequest));
        var service = CreateService(handler);

        var response = await service.GetDatasourcesAsync();

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Failed to get datasources");
      }

    [Fact]
    public async Task GetDatasetParametersAsync_WithSuggestedValues_ReturnsParameters()
    {
        var handler = new StubHttpMessageHandler(CreateJsonResponse(SampleParametersJson()));
        var service = CreateService(handler);

        var response = await service.GetDatasetParametersAsync();

        response.Success.Should().BeTrue();
        response.Data!.Single().SuggestedValues.Should().Contain("2024");
        response.FormattedText.Should().Contain("Year");
    }

      [Fact]
      public async Task GetDatasetParametersAsync_WhenRequestFails_ReturnsError()
      {
        var handler = new StubHttpMessageHandler(CreateJsonResponse("{}", HttpStatusCode.BadRequest));
        var service = CreateService(handler);

        var response = await service.GetDatasetParametersAsync();

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Failed to get parameters");
      }

    [Fact]
    public async Task GetRefreshHistoryAsync_WithAttempts_ParsesAttempts()
    {
        var handler = new StubHttpMessageHandler(CreateJsonResponse(SampleRefreshHistoryJson()));
        var service = CreateService(handler);

        var response = await service.GetRefreshHistoryAsync(top: 1);

        response.Success.Should().BeTrue();
        response.Data!.Single().Attempts.Should().ContainSingle();
        response.FormattedText.Should().Contain("Attempt 1");
    }

      [Fact]
      public async Task GetRefreshHistoryAsync_WhenDatasetMissing_ReturnsError()
      {
        var handler = new StubHttpMessageHandler();
        var service = CreateService(handler, defaultDatasetId: null);

        var response = await service.GetRefreshHistoryAsync();

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Dataset ID not provided");
      }

    [Fact]
    public async Task GetDatasetUsersAsync_WithUsers_ReturnsList()
    {
        var handler = new StubHttpMessageHandler(CreateJsonResponse(SampleDatasetUsersJson()));
        var service = CreateService(handler);

        var response = await service.GetDatasetUsersAsync();

        response.Success.Should().BeTrue();
        response.Data!.Single().AccessRight.Should().Be("Read");
        response.FormattedText.Should().Contain("user@contoso.com");
    }

      [Fact]
      public async Task GetDatasetUsersAsync_WhenRequestFails_ReturnsError()
      {
        var handler = new StubHttpMessageHandler(CreateJsonResponse("{}", HttpStatusCode.BadRequest));
        var service = CreateService(handler);

        var response = await service.GetDatasetUsersAsync();

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Failed to get dataset users");
      }

    [Fact]
    public async Task AggregateDataAsync_WithInvalidFunction_ReturnsFailureResponse()
    {
        var handler = new StubHttpMessageHandler();
        var service = CreateService(handler);

        var response = await service.AggregateDataAsync("Sales", "Amount", "Median");

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Unsupported aggregation function");
    }

    [Fact]
    public async Task AggregateDataAsync_WithValidFunction_ReturnsFormattedResult()
    {
        var handler = new StubHttpMessageHandler(CreateJsonResponse(SampleAggregationResultJson()));
        var service = CreateService(handler);

        var response = await service.AggregateDataAsync("Sales", "Amount", "SUM");

        response.Success.Should().BeTrue();
        response.Data!.Result.Should().Be(123);
        response.FormattedText.Should().Contain("SUM(Sales.Amount)");
    }

      [Fact]
      public async Task ExecuteDAXQueryAsync_WhenRequestFails_ReturnsError()
      {
        var handler = new StubHttpMessageHandler(CreateJsonResponse("{\"error\":\"bad\"}", HttpStatusCode.BadRequest));
        var service = CreateService(handler);

        var response = await service.ExecuteDAXQueryAsync("EVALUATE ROW('Total')");

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Query execution failed");
      }

    [Fact]
    public async Task GetDistinctValuesAsync_WithValidResponse_ReturnsValues()
    {
        var handler = new StubHttpMessageHandler(CreateJsonResponse(SampleDistinctValuesJson()));
        var service = CreateService(handler);

        var response = await service.GetDistinctValuesAsync("Sales", "Category");

        response.Success.Should().BeTrue();
        response.Data!.Values.Should().BeEquivalentTo(new object?[] { "A", "B" });
        response.FormattedText.Should().Contain("Sales.Category");
    }

    private static FabricSemanticModelService CreateService(
        StubHttpMessageHandler handler,
        string? defaultDatasetId = "dataset-123")
    {
        var factory = new StubHttpClientFactory(handler);
        var options = Options.Create(new FabricOptions
        {
            WorkspaceId = "workspace-abc",
            DefaultDatasetId = defaultDatasetId,
            ApiBaseUrl = "https://api.powerbi.com/v1.0/myorg",
            HttpTimeoutSeconds = 30
        });

        var tokenProvider = new StubTokenProvider();
        var logger = NullLogger<FabricSemanticModelService>.Instance;
        return new FabricSemanticModelService(factory, options, tokenProvider, logger);
    }

    private static HttpResponseMessage CreateJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string SampleQueryResultJson() =>
        """
        {
          "results": [
            {
              "tables": [
                {
                  "columns": [
                    { "name": "Total" },
                    { "name": "Category" }
                  ],
                  "rows": [
                    { "Total": 100, "Category": "A" },
                    { "Total": 200, "Category": "B" }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private static string SampleDatasetsJson() =>
        """
        {
          "value": [
            { "id": "ds1", "name": "Sales", "webUrl": "https://contoso/sales" },
            { "id": "ds2", "name": "Finance", "webUrl": "https://contoso/finance" }
          ]
        }
        """;

    private static string SampleTableMetadataJson() =>
        """
        {
          "value": [
            {
              "name": "Sales",
              "displayName": "Sales Table",
              "columns": [
                { "name": "Amount", "displayName": "Amount", "dataType": "Double" }
              ]
            }
          ]
        }
        """;

    private static string SampleDatasetDetailsJson() =>
        """
        {
          "id": "ds1",
          "name": "Sales",
          "webUrl": "https://contoso/sales",
          "configuredBy": "owner@contoso.com",
          "isRefreshable": true,
          "targetStorageMode": "PremiumFiles",
          "createdDate": "2024-01-01T00:00:00Z",
          "queryScaleOutSettings": {
            "autoSyncReadOnlyReplicas": true,
            "maxReadOnlyReplicas": 3
          }
        }
        """;

    private static string SampleDatasourcesJson() =>
        """
        {
          "value": [
            {
              "datasourceType": "Sql",
              "datasourceId": "1",
              "gatewayId": "gw1",
              "connectionString": "Server=.;Database=Sales;",
              "name": "SalesDb",
              "connectionDetails": {
                "server": "localhost",
                "database": "Sales"
              }
            }
          ]
        }
        """;

    private static string SampleParametersJson() =>
        """
        {
          "value": [
            {
              "name": "Year",
              "type": "Text",
              "isRequired": true,
              "currentValue": "2024",
              "suggestedValues": ["2023", "2024"]
            }
          ]
        }
        """;

    private static string SampleRefreshHistoryJson() =>
        """
        {
          "value": [
            {
              "refreshType": "Scheduled",
              "startTime": "2024-01-01T00:00:00Z",
              "endTime": "2024-01-01T00:05:00Z",
              "status": "Completed",
              "requestId": "abc",
              "refreshAttempts": [
                {
                  "attemptId": 1,
                  "startTime": "2024-01-01T00:00:00Z",
                  "endTime": "2024-01-01T00:02:00Z",
                  "type": "Data",
                  "serviceExceptionJson": null
                }
              ]
            }
          ]
        }
        """;

    private static string SampleDatasetUsersJson() =>
        """
        {
          "value": [
            {
              "identifier": "user@contoso.com",
              "principalType": "User",
              "datasetUserAccessRight": "Read"
            }
          ]
        }
        """;

    private static string SampleAggregationResultJson() =>
        """
        {
          "results": [
            {
              "tables": [
                {
                  "columns": [ { "name": "Total" } ],
                  "rows": [ { "Total": 123 } ]
                }
              ]
            }
          ]
        }
        """;

    private static string SampleDistinctValuesJson() =>
        """
        {
          "results": [
            {
              "tables": [
                {
                  "columns": [ { "name": "Category" } ],
                  "rows": [
                    { "Category": "A" },
                    { "Category": "B" }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler)
            {
                DefaultRequestHeaders = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
            };
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public List<HttpRequestMessage> Requests { get; } = new();

        public StubHttpMessageHandler(params HttpResponseMessage[] responses)
        {
            foreach (var response in responses)
            {
                _responses.Enqueue(response);
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No responses configured for handler.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class StubTokenProvider : ITokenProvider
    {
        public string Token { get; set; } = "stub-token";

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Token);
        }
    }
}