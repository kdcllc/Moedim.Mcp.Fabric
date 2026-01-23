using Azure.Monitor.OpenTelemetry.AspNetCore;
using DotNetEnv.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moedim.Mcp.Fabric.Extensions;
using Moedim.Mcp.Fabric.Tools;

// Parse command-line arguments
var config = ParseArguments(args);

if (config.ShowHelp)
{
    ShowHelp();
    return 0;
}

// Create appropriate builder based on transport mode
if (config.UseHttp)
{
    var builder = WebApplication.CreateBuilder(args);

    // Load environment variables from .env file
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddDotNetEnv();

    if(builder.Environment.IsDevelopment())
    {
        builder.Configuration.AddUserSecrets<Program>();
    }

    builder.ConfigureAzureMonitorLogging();

    // Register Fabric services for dependency injection with HTTP transport
    builder.Services.AddFabricSemanticModel(builder.Configuration, useHttpTransport: true);

    // Register JWT Bearer authentication with multi-tenant support (only if enabled)
    builder.Services.AddJwtBearerAuthentication(builder.Configuration);

    // Configure MCP server with HTTP transport
    builder.Services
        .AddMcpServer()
        .WithHttpTransport(options => options.Stateless = true)
        .WithTools<FabricSemanticModelTools>();

    // Configure HTTP port via environment or directly on webhost settings
    // Use ListenAnyIP to bind to 0.0.0.0 for container accessibility
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(config.Port);
    });

    // Add health checks for container liveness/readiness probes
    builder.Services.AddHealthChecks();

    var webApp = builder.Build();

    var logging = webApp.Services.GetRequiredService<ILogger<Program>>();
    logging.LogInformation("Starting Fabric Semantic Model MCP Server on port {Port} with HTTP transport", config.Port);

    // Configure HTTPS redirection for production
    if (!config.DisableHttpsRedirect && !webApp.Environment.IsDevelopment())
    {
        webApp.UseHttpsRedirection();
    }

    // Enable routing
    webApp.UseRouting();

    // Enable authentication and authorization middleware (only active if JWT validation is enabled)
    webApp.UseAuthentication();
    webApp.UseAuthorization();

    // Map health check endpoint for container probes (ACA/ACI) - always allow anonymous
    webApp.MapHealthChecks("/health").AllowAnonymous();

    // Map MCP endpoint with authorization (requires valid JWT when authentication is enabled)
    webApp.MapMcp("/mcp").RequireAuthorization();

    logging.LogInformation($"Fabric Semantic Model MCP Server initialized [HTTP Mode]");
    logging.LogInformation($"Endpoint: http://localhost:{config.Port}/mcp");
    logging.LogInformation($"HTTPS Redirection: {(!config.DisableHttpsRedirect && !webApp.Environment.IsDevelopment() ? "Enabled" : "Disabled")}");
    logging.LogInformation("Available tools: query_semantic_model, list_semantic_models, get_semantic_model_metadata, aggregate_data, get_distinct_values");

    await webApp.RunAsync();
}
else
{
    var builder = Host.CreateApplicationBuilder(args);

    // Load environment variables from .env file
    builder.Configuration.AddDotNetEnv();

    // Configure logging for stdio MCP protocol compliance
    // Route logs to stderr at Debug level
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Debug);

    // Register Fabric services for dependency injection with stdio transport (DefaultAzureCredential)
    builder.Services.AddFabricSemanticModel(builder.Configuration);

    // Configure MCP server with stdio transport
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<FabricSemanticModelTools>();

    var host = builder.Build();

    Console.Error.WriteLine("Fabric Semantic Model MCP Server initialized [Stdio Mode]");
    Console.Error.WriteLine("Available tools: query_semantic_model, list_semantic_models, get_semantic_model_metadata, aggregate_data, get_distinct_values");

    await host.RunAsync();
}

return 0;

static AppConfig ParseArguments(string[] args)
{
    var useHttp = args.Contains("--http", StringComparer.OrdinalIgnoreCase) ||
                  Environment.GetEnvironmentVariable("UseHttp")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    var disableHttpsRedirect = args.Contains("--disable-https-redirect", StringComparer.OrdinalIgnoreCase);
    var showHelp = args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase);

    // Parse port from command-line or environment variable
    var port = 5000; // Default
    var portIndex = Array.FindIndex(args, a => a.Equals("--port", StringComparison.OrdinalIgnoreCase) || a.Equals("-p", StringComparison.OrdinalIgnoreCase));

    if (portIndex >= 0 && portIndex + 1 < args.Length)
    {
        if (int.TryParse(args[portIndex + 1], out var parsedPort) && parsedPort >= 1 && parsedPort <= 65535)
        {
            port = parsedPort;
        }
        else
        {
            Console.Error.WriteLine($"Invalid port number: {args[portIndex + 1]}. Using default port {port}.");
        }
    }
    else
    {
        // Check environment variable
        var portEnv = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out var parsedPort) && parsedPort >= 1 && parsedPort <= 65535)
        {
            port = parsedPort;
        }
    }

    return new AppConfig(useHttp, port, disableHttpsRedirect, showHelp);
}

static void ShowHelp()
{
    Console.WriteLine("Moedim.Mcp.Fabric - Microsoft Fabric Semantic Model MCP Server");
    Console.WriteLine();
    Console.WriteLine("Usage: Moedim.Mcp.Fabric [options]");
    Console.WriteLine();
    Console.WriteLine("Transport Mode Options:");
    Console.WriteLine("  --stdio                    Use stdio transport (default)");
    Console.WriteLine("  --http                     Use HTTP/SSE transport (stateless mode)");
    Console.WriteLine();
    Console.WriteLine("HTTP Mode Options:");
    Console.WriteLine("  --port, -p <number>        Port number for HTTP server (default: 5000, range: 1-65535)");
    Console.WriteLine("  --disable-https-redirect   Disable HTTPS redirection in production");
    Console.WriteLine();
    Console.WriteLine("Other Options:");
    Console.WriteLine("  --help, -h                 Show this help message");
    Console.WriteLine();
    Console.WriteLine("Environment Variables:");
    Console.WriteLine("  UseHttp=true              Enable HTTP transport (command-line --http takes precedence)");
    Console.WriteLine("  PORT=<number>             Set HTTP port (command-line --port takes precedence)");
    Console.WriteLine("  Fabric:WorkspaceId        Microsoft Fabric workspace ID (required)");
    Console.WriteLine("  Fabric:DefaultDatasetId   Default dataset ID for queries (optional)");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  # Run in stdio mode (default)");
    Console.WriteLine("  Moedim.Mcp.Fabric");
    Console.WriteLine();
    Console.WriteLine("  # Run in HTTP mode on default port 5000");
    Console.WriteLine("  Moedim.Mcp.Fabric --http");
    Console.WriteLine();
    Console.WriteLine("  # Run in HTTP mode on custom port");
    Console.WriteLine("  Moedim.Mcp.Fabric --http --port 8080");
    Console.WriteLine();
    Console.WriteLine("  # Run in HTTP mode with HTTPS redirection disabled");
    Console.WriteLine("  Moedim.Mcp.Fabric --http --disable-https-redirect");
    Console.WriteLine();
    Console.WriteLine("  # Using environment variables");
    Console.WriteLine("  UseHttp=true PORT=8080 Moedim.Mcp.Fabric");
    Console.WriteLine();
    Console.WriteLine("For more information, see QUICKSTART.md");
}

record AppConfig(bool UseHttp, int Port, bool DisableHttpsRedirect, bool ShowHelp);
