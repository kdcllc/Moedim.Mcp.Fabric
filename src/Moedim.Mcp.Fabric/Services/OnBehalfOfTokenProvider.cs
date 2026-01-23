namespace Moedim.Mcp.Fabric.Services;

/// <summary>
/// Provides an access token on behalf of a user by exchanging an original token using the specified <see cref="IOnBehalfOfTokenService"/>.
/// </summary>
public class OnBehalfOfTokenProvider(ITokenProvider OriginalTokenProvider, IOnBehalfOfTokenService OnBehalfOfTokenService) : ITokenProvider
{
    /// <summary>
    /// Asynchronously obtains an access token on behalf of a user.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the access token string.</returns>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var originalToken = await OriginalTokenProvider.GetAccessTokenAsync(cancellationToken);
        return await OnBehalfOfTokenService.GetTokenOnBehalfOfAsync(originalToken, cancellationToken);
    }
}
