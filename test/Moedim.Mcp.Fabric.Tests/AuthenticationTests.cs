using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moedim.Mcp.Fabric.Configuration;
using Moedim.Mcp.Fabric.Extensions;
using Xunit;

namespace Moedim.Mcp.Fabric.Tests;

public class AuthenticationTests
{
    [Fact]
    public void AddJwtBearerAuthentication_WithValidationDisabled_DoesNotRegisterAuthentication()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fabric:WorkspaceId"] = "workspace-1",
                ["Authentication:EnableValidation"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddFabricSemanticModel(configuration);
        services.AddJwtBearerAuthentication(configuration);

        var provider = services.BuildServiceProvider();

        // Should not throw when getting options
        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        options.EnableValidation.Should().BeFalse();

        // Authentication scheme should not be registered when disabled
        var authSchemeProvider = provider.GetService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();
        authSchemeProvider.Should().BeNull();
    }

    [Fact]
    public void AddJwtBearerAuthentication_WithValidationEnabled_RegistersJwtBearer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fabric:WorkspaceId"] = "workspace-1",
                ["Authentication:EnableValidation"] = "true",
                ["Authentication:ClientId"] = "test-client-id",
                ["Authentication:AllowedTenants:0"] = "tenant-1",
                ["Authentication:AllowedTenants:1"] = "tenant-2"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFabricSemanticModel(configuration);
        services.AddJwtBearerAuthentication(configuration);

        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        options.EnableValidation.Should().BeTrue();
        options.ClientId.Should().Be("test-client-id");
        options.AllowedTenants.Should().BeEquivalentTo(new[] { "tenant-1", "tenant-2" });

        // JWT Bearer options should be configured
        var jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        jwtOptions.TokenValidationParameters.ValidAudience.Should().Be("test-client-id");
        jwtOptions.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
    }

    [Fact]
    public void AddJwtBearerAuthentication_WithMissingClientId_ThrowsValidationException()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fabric:WorkspaceId"] = "workspace-1",
                ["Authentication:EnableValidation"] = "true",
                ["Authentication:AllowedTenants:0"] = "tenant-1"
                // ClientId is missing
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFabricSemanticModel(configuration);
        services.AddJwtBearerAuthentication(configuration);

        var provider = services.BuildServiceProvider();

        Action action = () => provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value.ClientId.Should().NotBeNull();

        action.Should().Throw<OptionsValidationException>()
            .WithMessage("*ClientId is required*");
    }

    [Fact]
    public void AddJwtBearerAuthentication_WithEmptyAllowedTenants_ThrowsValidationException()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fabric:WorkspaceId"] = "workspace-1",
                ["Authentication:EnableValidation"] = "true",
                ["Authentication:ClientId"] = "test-client-id"
                // AllowedTenants is empty
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFabricSemanticModel(configuration);
        services.AddJwtBearerAuthentication(configuration);

        var provider = services.BuildServiceProvider();

        Action action = () => provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value.AllowedTenants.Should().NotBeNull();

        action.Should().Throw<OptionsValidationException>()
            .WithMessage("*AllowedTenants must contain at least one tenant*");
    }

    [Fact]
    public void AddJwtBearerAuthentication_WithCustomMetadataAddress_UsesProvidedAddress()
    {
        var customMetadataAddress = "https://custom.identity.provider/.well-known/openid-configuration";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fabric:WorkspaceId"] = "workspace-1",
                ["Authentication:EnableValidation"] = "true",
                ["Authentication:ClientId"] = "test-client-id",
                ["Authentication:AllowedTenants:0"] = "tenant-1",
                ["Authentication:MetadataAddress"] = customMetadataAddress
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFabricSemanticModel(configuration);
        services.AddJwtBearerAuthentication(configuration);

        var provider = services.BuildServiceProvider();

        var jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        jwtOptions.MetadataAddress.Should().Be(customMetadataAddress);
    }

    [Fact]
    public void AuthenticationOptions_DefaultValues_AreCorrect()
    {
        var options = new AuthenticationOptions();

        options.EnableValidation.Should().BeFalse();
        options.ClientId.Should().BeNull();
        options.AllowedTenants.Should().BeEmpty();
        options.MetadataAddress.Should().Be("https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration");
    }

    [Fact]
    public void AddJwtBearerAuthentication_ConfiguresClockSkew_ToFiveMinutes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fabric:WorkspaceId"] = "workspace-1",
                ["Authentication:EnableValidation"] = "true",
                ["Authentication:ClientId"] = "test-client-id",
                ["Authentication:AllowedTenants:0"] = "tenant-1"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFabricSemanticModel(configuration);
        services.AddJwtBearerAuthentication(configuration);

        var provider = services.BuildServiceProvider();

        var jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        jwtOptions.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.FromMinutes(5));
    }
}
