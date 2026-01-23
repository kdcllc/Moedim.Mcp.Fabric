namespace Moedim.Mcp.Fabric.Services;

/// <summary>
/// Provides access tokens for authenticating with Microsoft Fabric APIs.
/// </summary>
public interface ITokenProvider
{
    /// <summary>
    /// Gets an access token for the Microsoft Fabric API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A valid access token string.</returns>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}