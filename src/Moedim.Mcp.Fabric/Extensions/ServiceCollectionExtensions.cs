using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moedim.Mcp.Fabric.Configuration;
using Moedim.Mcp.Fabric.Services;
using System.ComponentModel.DataAnnotations;

namespace Moedim.Mcp.Fabric.Extensions;

/// <summary>
/// Extension methods for registering Fabric services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Fabric Semantic Model services to the DI container with full configuration validation.
    /// Uses DefaultCredentialTokenProvider for stdio transport mode.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFabricSemanticModel(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return AddFabricSemanticModel(services, configuration, useHttpTransport: false);
    }

    /// <summary>
    /// Adds Fabric Semantic Model services to the DI container with full configuration validation.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="useHttpTransport">
    /// When true, uses HttpContextTokenProvider which extracts bearer tokens from HTTP requests
    /// and falls back to DefaultAzureCredential. When false, uses DefaultCredentialTokenProvider only.
    /// </param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFabricSemanticModel(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useHttpTransport)
    {
        services
            .AddOptions<FabricOptions>()
            .Bind(configuration.GetSection("Fabric"))
            .Validate(options => ValidateDataAnnotations(options), "Fabric options are invalid")
            .Validate(options => !string.IsNullOrWhiteSpace(options.WorkspaceId), "Fabric:WorkspaceId must be configured")
            .Validate(options => Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _), "Fabric:ApiBaseUrl must be a valid absolute URI")
            .Validate(options => options.HttpTimeoutSeconds > 0, "Fabric:HttpTimeoutSeconds must be greater than zero")
            .ValidateOnStart();

        services.AddHttpClient("FabricAPI", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<FabricOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Register DefaultCredentialTokenProvider as singleton for token caching across requests
        // Uses IOptions<FabricOptions> to configure credential exclusions
        services.AddSingleton<DefaultCredentialTokenProvider>(sp =>
            new DefaultCredentialTokenProvider(sp.GetRequiredService<IOptions<FabricOptions>>()));

        if (useHttpTransport)
        {
            // HTTP mode: Add HttpContextAccessor and use HttpContextTokenProvider
            // which extracts bearer tokens from requests and falls back to DefaultAzureCredential
            services
                .AddOptions<AzureAdOptions>()
                .Bind(configuration.GetSection("AzureAd"))
                .Validate(options => ValidateDataAnnotations(options), "AzureAd options are invalid")
                .ValidateOnStart();

            services.AddHttpContextAccessor();
            services.AddScoped<HttpContextTokenProvider>();
            services.AddScoped<IOnBehalfOfTokenService, FabricOnBehalfOfTokenService>();
            services.AddSingleton<ITokenProvider>(sp =>
            {
                var originalTokenProvider = sp.GetRequiredService<HttpContextTokenProvider>();
                var onBehalfOfTokenService = sp.GetRequiredService<IOnBehalfOfTokenService>();
                return new OnBehalfOfTokenProvider(originalTokenProvider, onBehalfOfTokenService);
            });

        }
        else
        {
            // Stdio mode: Use DefaultCredentialTokenProvider directly
            services.AddSingleton<ITokenProvider>(sp => sp.GetRequiredService<DefaultCredentialTokenProvider>());
        }

        services.AddScoped<IFabricSemanticModelService, FabricSemanticModelService>();

        return services;
    }

    /// <summary>
    /// Adds JWT Bearer authentication with multi-tenant support and automatic key rotation.
    /// Only registers authentication when Authentication:EnableValidation is true.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddJwtBearerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind authentication options
        services
            .AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection("Authentication"))
            .Validate(options => !options.EnableValidation || !string.IsNullOrWhiteSpace(options.ClientId),
                "Authentication:ClientId is required when EnableValidation is true")
            .Validate(options => !options.EnableValidation || options.AllowedTenants.Length > 0,
                "Authentication:AllowedTenants must contain at least one tenant ID when EnableValidation is true")
            .ValidateOnStart();

        var authOptions = configuration.GetSection("Authentication").Get<AuthenticationOptions>() ?? new AuthenticationOptions();

        if (!authOptions.EnableValidation)
        {
            return services;
        }

        // Create a HashSet for O(1) tenant lookup
        var allowedIssuers = new HashSet<string>(
            authOptions.AllowedTenants
                .Select(x => $"https://sts.windows.net/{x}/")
                .Union(authOptions.AllowedTenants
                    .Select(x => $"https://login.microsoftonline.com/{x}/v2.0")), 
            StringComparer.OrdinalIgnoreCase);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Use Azure AD common endpoint for automatic key rotation
                options.MetadataAddress = authOptions.MetadataAddress;

                // Disable automatic issuer validation - we validate tenant ID instead
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudience = authOptions.ClientId,
                    ValidateIssuer = true,
                    // Custom issuer validation to support multi-tenant with whitelist
                    IssuerValidator = (issuer, securityToken, validationParameters) =>
                    {
                        if (!allowedIssuers.Contains(issuer))
                        {
                            throw new SecurityTokenInvalidIssuerException(
                                $"Issuer '{issuer}' does not match expected Azure AD issuer.");
                        }

                        return issuer;
                    },
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                // Log authentication failures at Warning level
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogWarning(
                            context.Exception,
                            "JWT authentication failed for request to {Path}. Error: {Error}",
                            context.Request.Path,
                            context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogInformation(
                            "JWT authentication challenge for request to {Path}. Error: {Error}, Description: {Description}",
                            context.Request.Path,
                            context.Error ?? "(none)",
                            context.ErrorDescription ?? "(none)");
                        if (!string.IsNullOrEmpty(context.Error) || !string.IsNullOrEmpty(context.ErrorDescription))
                        {
                            logger.LogWarning(
                                "JWT authentication challenge for request to {Path}. Error: {Error}, Description: {Description}",
                                context.Request.Path,
                                context.Error ?? "(none)",
                                context.ErrorDescription ?? "(none)");
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Validates data annotations on the configuration objects.
    /// </summary>
    private static bool ValidateDataAnnotations(object options)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        return Validator.TryValidateObject(options, context, validationResults, true);
    }
}
