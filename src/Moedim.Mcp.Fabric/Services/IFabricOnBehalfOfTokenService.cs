namespace Moedim.Mcp.Fabric.Services;

/// <summary>
/// Provides a service for acquiring access tokens on behalf of a user.
/// </summary>
public interface IOnBehalfOfTokenService
{
    /// <summary>
    /// Acquires an access token on behalf of the user represented by the incoming access token.
    /// </summary>
    /// <param name="incomingAccessToken">The access token representing the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the access token string.</returns>
    Task<string> GetTokenOnBehalfOfAsync(string incomingAccessToken, CancellationToken cancellationToken = default);
}