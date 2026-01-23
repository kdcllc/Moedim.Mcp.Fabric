using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Moedim.Mcp.Fabric.Extensions;

/// <summary>
/// Provides extension methods for configuring infrastructure services such as logging and telemetry.
/// </summary>
public static class InfrastructureExtensions
{
    /// <summary>
    /// Configures Azure Monitor logging and console logging for the specified <see cref="WebApplicationBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to configure.</param>
    /// <returns>The configured <see cref="WebApplicationBuilder"/>.</returns>
    public static WebApplicationBuilder ConfigureAzureMonitorLogging(this WebApplicationBuilder builder)
    {
        // Configure logging with OpenTelemetry
        // Check for Azure Monitor connection string
        var azureMonitorConnectionString = builder.Configuration["AzureMonitor:ConnectionString"];

        if (!string.IsNullOrEmpty(azureMonitorConnectionString))
        {
            // Configure Azure Monitor with OpenTelemetry for HTTP mode
            builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
            {
                options.ConnectionString = azureMonitorConnectionString;
            });
            Console.WriteLine("Azure Monitor telemetry enabled");
        }

        // Configure console logging at Debug level
        builder.Logging
            .SetMinimumLevel(LogLevel.Debug)
            .AddFilter("Microsoft.AspNetCore.Server.Kestrel.Connections", LogLevel.Warning)
            .AddFilter("Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets", LogLevel.Warning);

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;
        });
        builder.Logging.AddConsole();

        return builder;
    }
}
