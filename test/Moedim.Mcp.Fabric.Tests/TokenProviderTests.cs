using Azure.Core;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moedim.Mcp.Fabric.Configuration;
using Moedim.Mcp.Fabric.Services;
using Moq;
using Xunit;

namespace Moedim.Mcp.Fabric.Tests;

/// <summary>
/// Unit tests for token providers.
/// </summary>
public class TokenProviderTests
{
    #region DefaultCredentialTokenProvider Tests

    [Fact]
    public void Constructor_WithExcludeIdeCredentialsFalse_CreatesProvider()
    {
        // Arrange
        var options = Options.Create(new FabricOptions
        {
            WorkspaceId = "test-workspace",
            ExcludeIdeCredentials = false
        });

        // Act
        var provider = new DefaultCredentialTokenProvider(options);

        // Assert
        provider.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithExcludeIdeCredentialsTrue_CreatesProvider()
    {
        // Arrange
        var options = Options.Create(new FabricOptions
        {
            WorkspaceId = "test-workspace",
            ExcludeIdeCredentials = true
        });

        // Act
        var provider = new DefaultCredentialTokenProvider(options);

        // Assert
        provider.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DefaultCredentialTokenProvider((IOptions<FabricOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithValidCredential_ReturnsToken()
    {
        // Arrange
        var expectedToken = "test-access-token";
        var stubCredential = new StubTokenCredential(expectedToken, DateTimeOffset.UtcNow.AddHours(1));
        var provider = new DefaultCredentialTokenProvider(stubCredential);

        // Act
        var token = await provider.GetAccessTokenAsync();

        // Assert
        token.Should().Be(expectedToken);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithCachedToken_ReturnsCachedValue()
    {
        // Arrange
        var stubCredential = new StubTokenCredential("first-token", DateTimeOffset.UtcNow.AddHours(1));
        var provider = new DefaultCredentialTokenProvider(stubCredential);

        // Act - call twice
        var firstToken = await provider.GetAccessTokenAsync();
        stubCredential.Token = "second-token"; // Change the token
        var secondToken = await provider.GetAccessTokenAsync();

        // Assert - should still return cached token
        firstToken.Should().Be("first-token");
        secondToken.Should().Be("first-token");
        stubCredential.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithExpiredCache_RefreshesToken()
    {
        // Arrange - token expires in 2 minutes (within 5-minute buffer)
        var stubCredential = new StubTokenCredential("first-token", DateTimeOffset.UtcNow.AddMinutes(2));
        var provider = new DefaultCredentialTokenProvider(stubCredential);

        // Act - first call to cache
        await provider.GetAccessTokenAsync();

        // Change the token for next call
        stubCredential.Token = "refreshed-token";
        stubCredential.ExpiresOn = DateTimeOffset.UtcNow.AddHours(1);

        // Second call should refresh because cached token expires within 5 minutes
        var secondToken = await provider.GetAccessTokenAsync();

        // Assert
        secondToken.Should().Be("refreshed-token");
        stubCredential.CallCount.Should().Be(2);
    }

    #endregion

    #region HttpContextTokenProvider Tests

    [Fact]
    public async Task GetAccessTokenAsync_WithBearerToken_ReturnsPassedToken()
    {
        // Arrange
        var expectedToken = "user-bearer-token";
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = $"Bearer {expectedToken}";

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var fallbackProvider = new DefaultCredentialTokenProvider(
            new StubTokenCredential("fallback-token", DateTimeOffset.UtcNow.AddHours(1)));

        var logger = NullLogger<HttpContextTokenProvider>.Instance;
        var provider = new HttpContextTokenProvider(httpContextAccessor.Object, fallbackProvider, logger);

        // Act
        var token = await provider.GetAccessTokenAsync();

        // Assert
        token.Should().Be(expectedToken);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithoutToken_FallsBackToDefaultCredential()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        // No Authorization header set

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var fallbackProvider = new DefaultCredentialTokenProvider(
            new StubTokenCredential("fallback-token", DateTimeOffset.UtcNow.AddHours(1)));

        var logger = NullLogger<HttpContextTokenProvider>.Instance;
        var provider = new HttpContextTokenProvider(httpContextAccessor.Object, fallbackProvider, logger);

        // Act
        var token = await provider.GetAccessTokenAsync();

        // Assert
        token.Should().Be("fallback-token");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithEmptyBearerToken_FallsBackToDefaultCredential()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer ";

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var fallbackProvider = new DefaultCredentialTokenProvider(
            new StubTokenCredential("fallback-token", DateTimeOffset.UtcNow.AddHours(1)));

        var logger = NullLogger<HttpContextTokenProvider>.Instance;
        var provider = new HttpContextTokenProvider(httpContextAccessor.Object, fallbackProvider, logger);

        // Act
        var token = await provider.GetAccessTokenAsync();

        // Assert
        token.Should().Be("fallback-token");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithNonBearerAuth_FallsBackToDefaultCredential()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Basic dXNlcjpwYXNz";

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var fallbackProvider = new DefaultCredentialTokenProvider(
            new StubTokenCredential("fallback-token", DateTimeOffset.UtcNow.AddHours(1)));

        var logger = NullLogger<HttpContextTokenProvider>.Instance;
        var provider = new HttpContextTokenProvider(httpContextAccessor.Object, fallbackProvider, logger);

        // Act
        var token = await provider.GetAccessTokenAsync();

        // Assert
        token.Should().Be("fallback-token");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithNullHttpContext_FallsBackToDefaultCredential()
    {
        // Arrange
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var fallbackProvider = new DefaultCredentialTokenProvider(
            new StubTokenCredential("fallback-token", DateTimeOffset.UtcNow.AddHours(1)));

        var logger = NullLogger<HttpContextTokenProvider>.Instance;
        var provider = new HttpContextTokenProvider(httpContextAccessor.Object, fallbackProvider, logger);

        // Act
        var token = await provider.GetAccessTokenAsync();

        // Assert
        token.Should().Be("fallback-token");
    }

    #endregion

    #region Test Helpers

    private sealed class StubTokenCredential : TokenCredential
    {
        public string Token { get; set; }
        public DateTimeOffset ExpiresOn { get; set; }
        public int CallCount { get; private set; }

        public StubTokenCredential(string token, DateTimeOffset expiresOn)
        {
            Token = token;
            ExpiresOn = expiresOn;
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            CallCount++;
            return new AccessToken(Token, ExpiresOn);
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(new AccessToken(Token, ExpiresOn));
        }
    }

    #endregion
}
